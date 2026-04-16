using AgeLanServer.Common;
using AgeLanServer.Server;

// 1. Cấu hình Unicode cho console (khắc phục lỗi ký tự ?)
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

// 2. Kiểm tra quyền admin (khắc phục lỗi không mở được port 443)
if (OperatingSystem.IsWindows() && !CommandExecutor.IsRunningAsAdmin())
{
    var exePath = Environment.ProcessPath;
    if (string.IsNullOrEmpty(exePath))
    {
        Console.WriteLine("Lỗi: Không tìm thấy file thực thi để nâng quyền.");
        return;
    }

    Console.WriteLine("Server cần chạy với quyền Administrator để mở port 443. Đang yêu cầu nâng quyền...");
    
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exePath,
            Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1)),
            UseShellExecute = true,
            Verb = "runas" // Yêu cầu UAC
        };
        System.Diagnostics.Process.Start(psi);
        return; // Kết thúc tiến trình hiện tại
    }
    catch (System.ComponentModel.Win32Exception)
    {
        Console.WriteLine("Lỗi: Người dùng từ chối cấp quyền Administrator.");
        Environment.ExitCode = ErrorCodes.General;
        return;
    }
}

/// <summary>
/// Điểm vào chính của Age LAN Server.
/// </summary>
AppLogger.Initialize();
AppLogger.SetPrefix("SERVER");
CommandExecutor.ChangeWorkingDirectoryToExecutable();

AppLogger.Info("=== Age LAN Server - Core Server ===");

// Khóa file PID để đảm bảo chỉ một instance server chạy tại một thời điểm
// Tương đương fileLock.PidLock trong Go (root.go)
using var pidLock = new PidFileLock();
if (!pidLock.TryAcquire(out var existingPidPath))
{
    AppLogger.Error($"Không thể khóa file PID. Có thể đã có instance khác đang chạy (PID file: {existingPidPath}).");
    AppLogger.Error("Hãy kiểm tra task manager và dừng process 'server' nếu cần.");
    Environment.ExitCode = ErrorCodes.PidLock;
    return;
}

var cts = new CancellationTokenSource();

// Xử lý tín hiệu dừng
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    AppLogger.Info("Nhận tín hiệu dừng...");
    cts.Cancel();
};

// Đọc cấu hình
var configPath = Path.Combine("resources", "config", "config.toml");
Tomlyn.Model.TomlTable? tomlConfig = null;
if (File.Exists(configPath))
{
    try
    {
        var content = File.ReadAllText(configPath);
        tomlConfig = Tomlyn.Toml.ToModel(content);
    }
    catch (Exception ex)
    {
        AppLogger.Warn($"Không thể đọc file cấu hình: {ex.Message}");
    }
}

// Giải quyết chế độ xác thực (adaptive authentication)
// Tương đương logic trong root.go:
// Nếu auth mode là "adaptive", kiểm tra kết nối DNS.
// Nếu có kết nối -> resolve thành "cached", ngược lại -> "disabled".
var authMode = ConfigLoader.ResolveValue<string?>(null, "Server.Authentication", tomlConfig, "disabled") ?? "disabled";

if (authMode == "adaptive")
{
    AppLogger.Info("Đang kiểm tra kết nối DNS để giải quyết chế độ xác thực 'adaptive'...");
    var hasConnectivity = await DnsResolver.CheckDnsConnectivityAsync(cts.Token);
    authMode = hasConnectivity ? "cached" : "disabled";
    AppLogger.Info($"Chế độ xác thực 'adaptive' đã giải quyết thành '{authMode}' dựa trên kết nối DNS.");
}

if (authMode == "disabled")
{
    AppLogger.Warn("Chế độ xác thực đang là 'disabled'. Bạn chịu trách nhiệm đảm bảo người dùng truy cập hợp pháp.");
}
else if (authMode == "required")
{
    // Kiểm tra kết nối DNS nếu auth mode là "required"
    var hasConnectivity = await DnsResolver.CheckDnsConnectivityAsync(cts.Token);
    if (!hasConnectivity)
    {
        AppLogger.Error("Chế độ xác thực là 'required' nhưng không có kết nối Internet. Vui lòng thay đổi chế độ xác thực hoặc khắc phục kết nối.");
        Environment.ExitCode = ErrorCodes.General;
        return;
    }
}

AppLogger.Info($"Chế độ xác thực: {authMode}");

var rawGameId = ConfigLoader.ResolveValue<string?>(null, "Server.GameId", tomlConfig, GameIds.AgeOfEmpires4)
                ?? GameIds.AgeOfEmpires4;
var normalizedGameId = GameIds.Normalize(rawGameId) ?? GameIds.AgeOfEmpires4;

var serverConfig = new LanServer.ServerConfig
{
    Port = ConfigLoader.ResolveValue<int?>(null, "Server.Port", tomlConfig, 443) ?? 443,
    Host = ConfigLoader.ResolveValue<string?>(null, "Server.Host", tomlConfig, "0.0.0.0"),
    GameId = normalizedGameId,
    LogRequests = ConfigLoader.ResolveValue<bool?>(null, "Server.LogRequests", tomlConfig, true) ?? true,
    AuthenticationMode = authMode
};

// Tìm chứng chỉ
if (CertificateManager.CheckAllCertificates(
        Environment.ProcessPath,
        out var certFolder,
        out var certPath,
        out var keyPath,
        out _, out _, out _))
{
    serverConfig = serverConfig with { CertPath = certPath, KeyPath = keyPath };
    AppLogger.Info("Đã tìm thấy chứng chỉ SSL");
}
else
{
    AppLogger.Warn("Không tìm thấy chứng chỉ SSL - server sẽ chạy không HTTPS");
}

try
{
    await LanServer.RunAsync(serverConfig, cts.Token);
}
catch (OperationCanceledException)
{
    AppLogger.Info("Server đã dừng");
}
catch (Exception ex)
{
    AppLogger.Error($"Lỗi server: {ex.Message}");
    Environment.ExitCode = ErrorCodes.General;
}
