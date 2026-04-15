using System.CommandLine;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using AgeLanServer.BattleServerBroadcast;
using AgeLanServer.Common;
using AgeLanServer.Launcher.Internal.Server;
using AgeLanServer.LauncherAgent;
using AgeLanServer.LauncherCommon;
using ConfigModule = AgeLanServer.LauncherConfig.LauncherConfig;

namespace AgeLanServer.Launcher;

/// <summary>
/// Launcher chính - điều phối toàn bộ quy trình:
/// khám phá server, tạo chứng chỉ, cấu hình hệ thống, khởi động game.
/// Tương đương launcher/ trong bản Go gốc.
/// </summary>
public static class LauncherApp
{
    /// <summary>
    /// Cấu hình launcher.
    /// </summary>
    public record LauncherConfig
    {
        public string GameId { get; init; } = string.Empty;
        public ServerConfig Server { get; init; } = new();
        public ClientConfig Client { get; init; } = new();
        public bool TrustCertificate { get; init; } = true;
        public bool MapHosts { get; init; } = true;
        public bool IsolateMetadata { get; init; } = true;
        public bool IsolateProfiles { get; init; } = true;
        public bool LogToFile { get; init; } = true;
    }

    public record ServerConfig
    {
        public string ExecutablePath { get; init; } = string.Empty;
        public bool AutoStart { get; init; } = true;
        public bool AutoStop { get; init; } = true;
        public int AnnouncePort { get; init; } = AppConstants.AnnouncePort;
    }

    public record ClientConfig
    {
        public string Executable { get; init; } = "steam"; // steam, msstore, hoặc đường dẫn tùy chỉnh
        public string GamePath { get; init; } = string.Empty;
        public string ExtraArgs { get; init; } = string.Empty;
    }

    private static int _exitCode = ErrorCodes.Success;

    /// <summary>
    /// Luồng chính của launcher.
    /// </summary>
    public static async Task<int> RunAsync(LauncherConfig config, CancellationToken ct = default)
    {
        if (!GameIds.IsValid(config.GameId))
        {
            AppLogger.Error($"Game ID không hợp lệ: {config.GameId}");
            return LauncherErrorCodes.InvalidGame;
        }

        AppLogger.Info($"=== Age LAN Server Launcher ===");
        AppLogger.Info($"Game: {config.GameId}");

        // 1. Kiểm tra xem game có đang chạy không
        var gameProcesses = ProcessManager.GetGameProcesses(config.GameId,
            config.Client.Executable.Contains("steam", StringComparison.OrdinalIgnoreCase),
            config.Client.Executable.Contains("msstore", StringComparison.OrdinalIgnoreCase));

        foreach (var procName in gameProcesses)
        {
            var proc = ProcessManager.FindProcessByName(procName);
            if (proc != null)
            {
                AppLogger.Warn($"Game đang chạy (PID: {proc.Id}). Chờ tối đa 1 phút...");
                proc.Dispose();

                var waitStart = DateTime.UtcNow;
                while (DateTime.UtcNow - waitStart < TimeSpan.FromMinutes(1))
                {
                    proc = ProcessManager.FindProcessByName(procName);
                    if (proc == null)
                    {
                        AppLogger.Info("Game đã thoát");
                        break;
                    }
                    proc.Dispose();
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                }

                proc?.Dispose();
            }
        }

        // 2. Cleanup ban đầu (kill agent, revert cấu hình cũ)
        await InitialCleanupAsync(config, ct);

        // 3. Khám phá hoặc khởi động server
        string? serverIp = null;

        if (config.Server.AutoStart)
        {
            // Thử khám phá server hiện có
            serverIp = await DiscoverServerAsync(config.Server.AnnouncePort, ct);

            if (string.IsNullOrEmpty(serverIp))
            {
                AppLogger.Info("Không tìm thấy server LAN. Đang khởi động server mới...");
                // Khởi động server local bằng ServerModule.StartServerLocal()
                var serverExe = config.Server.ExecutablePath;
                if (string.IsNullOrEmpty(serverExe))
                    serverExe = ExecutablePaths.FindExecutablePath(ExecutablePaths.Server);

                if (!string.IsNullOrEmpty(serverExe) && File.Exists(serverExe))
                {
                    var serverId = Guid.NewGuid();
                    var serverArgs = new List<string>
                    {
                        "-e", config.GameId,
                        "--id", serverId.ToString()
                    };
                    var (ec, localIp) = ServerModule.StartServerLocal(
                        config.GameId, serverExe, serverArgs, stop: false, serverId);
                    if (ec == ErrorCodes.Success && !string.IsNullOrEmpty(localIp))
                    {
                        serverIp = localIp;
                        AppLogger.Info($"Đã khởi động server local tại {serverIp}");
                    }
                    else
                    {
                        AppLogger.Warn("Không thể khởi động server local, fallback về 127.0.0.1");
                        serverIp = "127.0.0.1";
                    }
                }
                else
                {
                    AppLogger.Warn("Không tìm thấy server executable, fallback về 127.0.0.1");
                    serverIp = "127.0.0.1";
                }
            }
        }
        else
        {
            // Chỉ khám phá
            serverIp = await DiscoverServerAsync(config.Server.AnnouncePort, ct);
            if (string.IsNullOrEmpty(serverIp))
            {
                AppLogger.Error("Không tìm thấy server LAN và auto-start bị tắt");
                return ErrorCodes.General;
            }
        }

        AppLogger.Info($"Server IP: {serverIp}");

        // 4. Tạo/xác thực chứng chỉ
        string? certDataBase64 = null;
        if (config.TrustCertificate)
        {
            certDataBase64 = await EnsureCertificatesAsync(serverIp, ct);
        }

        // 5. Cấu hình hệ thống
        if (config.MapHosts || config.IsolateMetadata || config.IsolateProfiles)
        {
            await SetupSystemAsync(config.GameId, serverIp, certDataBase64, config, ct);
        }

        // 6. Lưu tham số revert để cleanup khi thoát
        var revertArgs = new ConfigRevertManager.RevertArgs
        {
            GameId = config.GameId,
            ServerIp = serverIp,
            CertData = certDataBase64 ?? string.Empty,
            Flags = ConfigRevertManager.RevertFlags.None
        };

        if (config.MapHosts) revertArgs = revertArgs with { Flags = revertArgs.Flags | ConfigRevertManager.RevertFlags.MapIP };
        if (config.TrustCertificate) revertArgs = revertArgs with { Flags = revertArgs.Flags | ConfigRevertManager.RevertFlags.AddLocalCert };
        if (config.IsolateMetadata) revertArgs = revertArgs with { Flags = revertArgs.Flags | ConfigRevertManager.RevertFlags.MetadataBackup };
        if (config.IsolateProfiles) revertArgs = revertArgs with { Flags = revertArgs.Flags | ConfigRevertManager.RevertFlags.ProfileBackup };

        ConfigRevertManager.StoreRevertArgs(revertArgs);

        // 7. Khởi động game
        await LaunchGameAsync(config.GameId, config.Client, ct);

        // 8. Chờ game thoát
        AppLogger.Info("Đang chờ game... Nhấn Ctrl+C để thoát.");
        var isSteam = config.Client.Executable.Contains("steam", StringComparison.OrdinalIgnoreCase);
        var isXbox = config.Client.Executable.Contains("msstore", StringComparison.OrdinalIgnoreCase);

        var logDir = Path.Combine("logs", config.GameId, DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss"));
        await ProcessWatcher.WatchGameProcessAsync(config.GameId, isSteam, isXbox, logDestination: logDir);

        // 9. Cleanup khi thoát
        await CleanupAsync(config, ct);

        return _exitCode;
    }

    /// <summary>
    /// Cleanup ban đầu: kill agent cũ, revert cấu hình trước đó.
    /// </summary>
    private static async Task InitialCleanupAsync(LauncherConfig config, CancellationToken ct)
    {
        AppLogger.Info("Đang dọn dẹp ban đầu...");

        // Thử revert cấu hình cũ
        await ConfigModule.RevertAsync(config.GameId, false, ct);

        // Dừng server cũ nếu cần
        if (config.Server.AutoStop)
        {
            var serverExe = config.Server.ExecutablePath;
            if (string.IsNullOrEmpty(serverExe))
                serverExe = ExecutablePaths.FindExecutablePath(ExecutablePaths.Server);

            if (!string.IsNullOrEmpty(serverExe))
            {
                try
                {
                    await ProcessManager.KillProcessesByNameAsync(
                        new[] { Path.GetFileNameWithoutExtension(serverExe) }, ct);
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Khám phá server LAN qua UDP multicast/broadcast.
    /// </summary>
    private static async Task<string?> DiscoverServerAsync(int port, CancellationToken ct)
    {
        AppLogger.Info("Đang khám phá server LAN...");

        using var udp = new UdpClient(port);
        udp.EnableBroadcast = true;

        // Gửi announce request
        var message = $"{AppConstants.AnnounceHeader}\n{AppConstants.IdHeader}: discover\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(message);

        // Gửi tới multicast
        try
        {
            var multicastEp = new IPEndPoint(IPAddress.Parse(AppConstants.AnnounceMulticastGroup), port);
            await udp.SendAsync(bytes, multicastEp, ct);
        }
        catch { }

        // Gửi tới broadcast
        try
        {
            var broadcastEp = new IPEndPoint(IPAddress.Broadcast, port);
            await udp.SendAsync(bytes, broadcastEp, ct);
        }
        catch { }

        // Chờ phản hồi (timeout 5s)
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeout)
        {
            try
            {
                var result = await udp.ReceiveAsync().WaitAsync(TimeSpan.FromMilliseconds(500), ct);
                var response = System.Text.Encoding.UTF8.GetString(result.Buffer);

                if (response.StartsWith(AppConstants.AnnounceHeader))
                {
                    // Phân tích phản hồi để lấy server IP
                    var remoteIp = result.RemoteEndPoint.Address.ToString();
                    AppLogger.Info($"Đã tìm thấy server: {remoteIp}");
                    return remoteIp;
                }
            }
            catch
            {
                // Timeout hoặc lỗi - thử lại
            }
        }

        return null;
    }

    /// <summary>
    /// Đảm bảo chứng chỉ SSL tồn tại và hợp lệ.
    /// </summary>
    private static async Task<string?> EnsureCertificatesAsync(string serverIp, CancellationToken ct)
    {
        var exePath = Environment.ProcessPath;
        string? certDataBase64 = null;

        // Ưu tiên lấy CA Cert để cài vào hệ thống (Root Trust)
        if (CertificateManager.CheckAllCertificates(exePath, out var certFolder, out var certPath, out _, out var caCertPath, out var selfSignedCertPath, out _))
        {
            // Cố gắng đọc cacert.pem trước
            var pathToTrust = caCertPath;
            if (string.IsNullOrEmpty(pathToTrust) || !File.Exists(pathToTrust))
            {
                pathToTrust = certPath ?? selfSignedCertPath ?? string.Empty;
            }

            var cert = CertificateManager.ReadCertificateFromFile(pathToTrust);
            if (cert != null)
            {
                certDataBase64 = Convert.ToBase64String(cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));

                if (CertificateManager.IsCertificateSoonExpired(cert))
                {
                    AppLogger.Warn("Chứng chỉ sắp hết hạn. Cân nhắc tạo lại.");
                }
            }
        }
        else
        {
            AppLogger.Warn("Không tìm thấy chứng chỉ. Server có thể cần genCert.");
        }

        return certDataBase64;
    }

    /// <summary>
    /// Cấu hình hệ thống: hosts, metadata isolation, profile isolation, certificates.
    /// </summary>
    private static async Task SetupSystemAsync(
        string gameId,
        string serverIp,
        string? certDataBase64,
        LauncherConfig config,
        CancellationToken ct)
    {
        AppLogger.Info("Đang cấu hình hệ thống...");

        await ConfigModule.SetUpAsync(gameId, serverIp, certDataBase64, ct);
    }

    /// <summary>
    /// Khởi động game theo cấu hình client.
    /// </summary>
    private static async Task LaunchGameAsync(string gameId, ClientConfig client, CancellationToken ct)
    {
        AppLogger.Info($"Đang khởi động game: {client.Executable}");

        if (client.Executable.Equals("steam", StringComparison.OrdinalIgnoreCase))
        {
            // Mở qua Steam URI
            var steamUri = $"steam://run/1466860"; // AoE4 AppId
            AppLogger.Info($"Mở Steam URI: {steamUri}");

            if (OperatingSystem.IsWindows())
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = steamUri,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
        }
        else if (client.Executable.Equals("msstore", StringComparison.OrdinalIgnoreCase))
        {
            // Mở Xbox app qua shell:appsfolder với family name lookup
            var familyName = gameId switch
            {
                "aoe1" => "Microsoft.AgeofEmpiresDefinitiveEdition_8wekyb3d8bbwe",
                "aoe2" => "Microsoft.AgeofEmpiresIIDefinitiveEdition_8wekyb3d8bbwe",
                "age4" => "Microsoft.AgeofEmpiresIV_8wekyb3d8bbwe",
                "aom" => "Microsoft.AgeofMythologyRetold_8wekyb3d8bbwe",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(familyName))
            {
                AppLogger.Warn($"Game ID '{gameId}' không hỗ trợ launch qua Xbox.");
            }
            else if (OperatingSystem.IsWindows())
            {
                var shellPath = $@"shell:appsfolder\{familyName}!App";
                AppLogger.Info($"Mở game qua Microsoft Store: {shellPath}");

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = shellPath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            else
            {
                AppLogger.Warn("Xbox launch chỉ hỗ trợ trên Windows.");
            }
        }
        else if (!string.IsNullOrEmpty(client.Executable) && File.Exists(client.Executable))
        {
            // Chạy file thực thi tùy chỉnh
            AppLogger.Info($"Chạy launcher tùy chỉnh: {client.Executable}");
            CommandExecutor.StartDetached(client.Executable, client.ExtraArgs);
        }
        else
        {
            AppLogger.Warn($"Không thể khởi động game với '{client.Executable}'. Vui lòng tự mở game.");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Cleanup khi launcher thoát.
    /// </summary>
    private static async Task CleanupAsync(LauncherConfig config, CancellationToken ct)
    {
        AppLogger.Info("Đang dọn dẹp khi thoát...");

        // Revert cấu hình hệ thống
        await ConfigRevertManager.ExecuteRevertAsync(ct);

        // Dừng server nếu auto-stop
        if (config.Server.AutoStop)
        {
            var serverExe = config.Server.ExecutablePath;
            if (string.IsNullOrEmpty(serverExe))
                serverExe = ExecutablePaths.FindExecutablePath(ExecutablePaths.Server);

            if (!string.IsNullOrEmpty(serverExe))
            {
                try
                {
                    await ProcessManager.KillProcessesByNameAsync(
                        new[] { Path.GetFileNameWithoutExtension(serverExe) }, ct);
                    AppLogger.Info("Đã dừng server");
                }
                catch { }
            }
        }

        AppLogger.Info("Hoàn tất dọn dẹp");
    }
}

/// <summary>
/// Điểm vào Launcher chính.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // 1. Cấu hình Unicode cho console (khắc phục lỗi ký tự ?)
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        // 2. Kiểm tra quyền admin (khắc phục lỗi không sửa được hosts/cert)
        if (OperatingSystem.IsWindows() && !CommandExecutor.IsRunningAsAdmin())
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Console.WriteLine("Lỗi: Không tìm thấy file thực thi để nâng quyền.");
                return ErrorCodes.General;
            }

            Console.WriteLine("Ứng dụng cần chạy với quyền Administrator. Đang yêu cầu nâng quyền...");
            
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = string.Join(" ", args),
                    UseShellExecute = true,
                    Verb = "runas" // Yêu cầu UAC
                };
                System.Diagnostics.Process.Start(psi);
                return ErrorCodes.Success; // Kết thúc tiến trình hiện tại, nhường chỗ cho tiến trình mới
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Console.WriteLine("Lỗi: Người dùng từ chối cấp quyền Administrator.");
                return ErrorCodes.General;
            }
        }

        AppLogger.Initialize();
        AppLogger.SetPrefix("LAUNCHER");
        CommandExecutor.ChangeWorkingDirectoryToExecutable();

        var gameOption = new Option<string>(new[] { "--game", "-g" }, "ID game") { IsRequired = true };
        var serverExeOption = new Option<string?>(new[] { "--server-exe" }, "Đường dẫn server");
        var clientExeOption = new Option<string>(new[] { "--client-exe" }, () => "steam", "Launcher client (steam/msstore/đường dẫn)");
        var clientPathOption = new Option<string?>(new[] { "--client-path" }, "Đường dẫn game");
        var noCertFlag = new Option<bool>(new[] { "--no-cert" }, "Không cài chứng chỉ");
        var noHostsFlag = new Option<bool>(new[] { "--no-hosts" }, "Không sửa hosts file");
        var noIsolateFlag = new Option<bool>(new[] { "--no-isolate" }, "Không cô lập dữ liệu user");

        var rootCmd = new RootCommand("Age LAN Server Launcher - Chơi multiplayer offline")
        {
            gameOption, serverExeOption, clientExeOption, clientPathOption, noCertFlag, noHostsFlag, noIsolateFlag
        };

        rootCmd.SetHandler(async (game, serverExe, clientExe, clientPath, noCert, noHosts, noIsolate) =>
        {
            var config = new LauncherApp.LauncherConfig
            {
                GameId = game,
                Server = new LauncherApp.ServerConfig
                {
                    ExecutablePath = serverExe ?? ExecutablePaths.FindExecutablePath(ExecutablePaths.Server) ?? string.Empty
                },
                Client = new LauncherApp.ClientConfig
                {
                    Executable = clientExe,
                    GamePath = clientPath ?? string.Empty
                },
                TrustCertificate = !noCert,
                MapHosts = !noHosts,
                IsolateMetadata = !noIsolate,
                IsolateProfiles = !noIsolate
            };

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            Environment.ExitCode = await LauncherApp.RunAsync(config, cts.Token);
        }, gameOption, serverExeOption, clientExeOption, clientPathOption, noCertFlag, noHostsFlag, noIsolateFlag);

        return await rootCmd.InvokeAsync(args);
    }
}
