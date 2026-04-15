using System.CommandLine;
using System.IO.Pipes;
using System.Text.Json;
using System.Text;
using AgeLanServer.Common;
using AgeLanServer.LauncherCommon;

namespace AgeLanServer.LauncherConfigAdminAgent;

/// <summary>
/// IPC Agent chạy với quyền admin, nhận yêu cầu từ launcher-config
/// để thực hiện thao tác cần quyền quản trị.
/// Tương đương launcher-config-admin-agent/ trong bản Go gốc.
/// </summary>
public static class AdminAgent
{
    private static bool _mappedIps = false;
    private static bool _addedCert = false;

    /// <summary>
    /// Khởi động IPC server lắng nghe yêu cầu setup/revert.
    /// </summary>
    public static async Task RunServerAsync(CancellationToken ct = default)
    {
        AppLogger.Info("Đang khởi động Admin Agent IPC server...");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    IpcConstants.IpcPath,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);
                AppLogger.Info("Đã nhận kết nối IPC");

                // Đọc dữ liệu
                var buffer = new byte[4096];
                var bytesRead = await server.ReadAsync(buffer, ct);
                var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var action = root.GetProperty("Action").GetByte();
                var gameId = root.GetProperty("GameId").GetString() ?? "";
                var serverIp = root.GetProperty("ServerIp").GetString() ?? "";
                var certData = root.TryGetProperty("CertData", out var cd) ? cd.GetString() : null;
                var hosts = root.TryGetProperty("Hosts", out var h)
                    ? h.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
                    : Array.Empty<string>();

                // Xử lý theo action
                switch (action)
                {
                    case IpcConstants.ActionSetup:
                        await HandleSetupAsync(gameId, serverIp, certData, hosts, ct);
                        break;

                    case IpcConstants.ActionRevert:
                        await HandleRevertAsync(gameId, serverIp, certData, ct);
                        break;

                    case IpcConstants.ActionExit:
                        AppLogger.Info("Nhận yêu cầu thoát agent");
                        return;

                    default:
                        AppLogger.Warn($"Action không hợp lệ: {action}");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Lỗi xử lý IPC: {ex.Message}");
            }
        }

        AppLogger.Info("Admin Agent đã dừng");
    }

    /// <summary>
    /// Xử lý yêu cầu setup: thêm cert vào local machine, ánh xạ hosts.
    /// </summary>
    private static async Task HandleSetupAsync(
        string gameId,
        string serverIp,
        string? certDataBase64,
        string[] hosts,
        CancellationToken ct)
    {
        AppLogger.Info($"Xử lý setup cho game: {gameId}, IP: {serverIp}");

        // 1. Thêm chứng chỉ vào kho Local Machine
        if (!string.IsNullOrEmpty(certDataBase64) && !_addedCert)
        {
            try
            {
                await CertificateUtilities.TrustLocalCertificateAsync(certDataBase64, ct);
                _addedCert = true;
                AppLogger.Info("Đã thêm chứng chỉ vào kho Local Machine");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Lỗi thêm chứng chỉ: {ex.Message}");
            }
        }

        // 2. Ánh xạ IP vào hosts file hệ thống
        if (!string.IsNullOrEmpty(serverIp) && hosts.Length > 0 && !_mappedIps)
        {
            try
            {
                HostsManager.CreateBackup();
                HostsManager.AddHostMappings(serverIp, hosts);
                HostsManager.FlushDnsCache();
                _mappedIps = true;
                AppLogger.Info($"Đã ánh xạ {serverIp} vào hosts file hệ thống");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Lỗi ánh xạ hosts: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Xử lý yêu cầu revert: xóa cert, xóa hosts.
    /// </summary>
    private static async Task HandleRevertAsync(
        string gameId,
        string serverIp,
        string? certDataBase64,
        CancellationToken ct)
    {
        AppLogger.Info($"Xử lý revert cho game: {gameId}");

        // 1. Xóa chứng chỉ khỏi Local Machine
        if (!string.IsNullOrEmpty(certDataBase64) && _addedCert)
        {
            try
            {
                await CertificateUtilities.UntrustLocalCertificateAsync(certDataBase64, ct);
                _addedCert = false;
                AppLogger.Info("Đã xóa chứng chỉ khỏi kho Local Machine");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Lỗi xóa chứng chỉ: {ex.Message}");
            }
        }

        // 2. Xóa ánh xạ hosts
        if (_mappedIps)
        {
            try
            {
                HostsManager.RemoveOwnMappings();
                HostsManager.FlushDnsCache();
                _mappedIps = false;
                AppLogger.Info("Đã xóa ánh xạ hosts hệ thống");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Lỗi xóa ánh xạ hosts: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// Điểm vào Admin Agent.
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

            Console.WriteLine("Admin Agent cần chạy với quyền Administrator để sửa hosts/cert hệ thống. Đang nâng quyền...");
            
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
                return ErrorCodes.Success;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Console.WriteLine("Lỗi: Người dùng từ chối cấp quyền Administrator.");
                return ErrorCodes.General;
            }
        }

        AppLogger.Initialize();
        AppLogger.SetPrefix("ADMIN-AGENT");
        CommandExecutor.ChangeWorkingDirectoryToExecutable();

        // Kiểm tra PID lock
        var pidLock = new PidFileLock();
        if (!pidLock.TryAcquire(out var existingLock))
        {
            AppLogger.Warn($"Admin Agent đã chạy (lock: {existingLock})");
            return ErrorCodes.PidLock;
        }

        try
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                AppLogger.Info("Nhận tín hiệu dừng...");
                cts.Cancel();
            };

            await AdminAgent.RunServerAsync(cts.Token);
            return ErrorCodes.Success;
        }
        catch (OperationCanceledException)
        {
            return ErrorCodes.Success;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Lỗi agent: {ex.Message}");
            return ErrorCodes.General;
        }
        finally
        {
            pidLock.Release();
        }
    }
}
