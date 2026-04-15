using System.CommandLine;
using System.IO.Pipes;
using System.Text.Json;
using AgeLanServer.Common;
using AgeLanServer.LauncherCommon;

namespace AgeLanServer.LauncherConfig;

/// <summary>
/// Trình cấu hình launcher: thiết lập và đảo ngược cấu hình hệ thống.
/// Tương đương launcher-config/ trong bản Go gốc.
/// </summary>
public static class LauncherConfig
{
    /// <summary>
    /// Thiết lập cấu hình: chứng chỉ user, backup metadata/profile,
    /// ánh xạ hosts, cài cert local, cấu hình CA cert game.
    /// </summary>
    public static async Task<int> SetUpAsync(
        string gameId,
        string serverIp,
        string? certDataBase64,
        CancellationToken ct = default)
    {
        if (!GameIds.IsValid(gameId))
        {
            AppLogger.Error($"Game ID không hợp lệ: {gameId}");
            return LauncherErrorCodes.InvalidGame;
        }

        var revertArgs = new ConfigRevertManager.RevertArgs
        {
            GameId = gameId,
            ServerIp = serverIp,
            CertData = certDataBase64 ?? string.Empty
        };

        var hosts = GameDomains.GetAllHosts(gameId);

        // Kiểm tra xem IP đã khớp với domain game chưa
        bool ipMatches = false;
        foreach (var host in hosts)
        {
            if (await DnsResolver.MatchesAsync(serverIp, host, ct))
            {
                ipMatches = true;
                break;
            }
        }

        // 1. Thêm chứng chỉ vào kho tin cậy user (nếu có)
        if (!string.IsNullOrEmpty(certDataBase64))
        {
            try
            {
                await CertificateUtilities.TrustLocalCertificateAsync(certDataBase64, ct);
                revertArgs = revertArgs with { Flags = revertArgs.Flags | ConfigRevertManager.RevertFlags.AddLocalCert };
                AppLogger.Info("Đã thêm chứng chỉ vào kho tin cậy user");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Lỗi thêm chứng chỉ: {ex.Message}");
                return ErrorCodes.General;
            }
        }

        // 2. Backup metadata và profiles
        try
        {
            UserDataManager.BackupAllUserData(gameId);
            revertArgs = revertArgs with
            {
                Flags = revertArgs.Flags
                    | ConfigRevertManager.RevertFlags.MetadataBackup
                    | ConfigRevertManager.RevertFlags.ProfileBackup
            };
            AppLogger.Info("Đã backup dữ liệu user");
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Lỗi backup dữ liệu user: {ex.Message}");
        }

        // 3. Thêm CA cert vào game (nếu hỗ trợ)
        if (GameCertificateManager.SupportsCaCertModification(gameId))
        {
            try
            {
                GameCertificateManager.BackupCaCertificate(gameId);

                if (!string.IsNullOrEmpty(certDataBase64))
                {
                    var certData = Convert.FromBase64String(certDataBase64);
                    var certPem = System.Text.Encoding.UTF8.GetString(certData);
                    await GameCertificateManager.AppendCaCertificateAsync(gameId, certPem, ct);
                    revertArgs = revertArgs with
                    {
                        Flags = revertArgs.Flags | ConfigRevertManager.RevertFlags.GameCaCert
                    };
                }
                AppLogger.Info("Đã cấu hình CA cert cho game");
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Lỗi cấu hình CA cert game: {ex.Message}");
            }
        }

        // 4. Ánh xạ IP vào hosts file (nếu IP không khớp với domain game)
        if (!ipMatches)
        {
            try
            {
                HostsManager.AddHostMappings(serverIp, hosts);
                HostsManager.FlushDnsCache();
                revertArgs = revertArgs with
                {
                    Flags = revertArgs.Flags | ConfigRevertManager.RevertFlags.MapIP
                };
                AppLogger.Info($"Đã ánh xạ {serverIp} tới {hosts.Length} domain");
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Lỗi ánh xạ hosts: {ex.Message}");
            }
        }

        // 5. Giao tiếp với admin agent (nếu cần)
        await TryNotifyAdminAgentAsync(IpcConstants.ActionSetup, gameId, serverIp, certDataBase64, hosts, ct);

        // Lưu tham số revert
        ConfigRevertManager.StoreRevertArgs(revertArgs);

        AppLogger.Info("Hoàn tất thiết lập cấu hình");
        return ErrorCodes.Success;
    }

    /// <summary>
    /// Đảo ngược cấu hình: xóa cert, khôi phục metadata/profile,
    /// xóa hosts, khôi phục CA cert game.
    /// </summary>
    public static async Task<int> RevertAsync(
        string? gameId,
        bool removeAll,
        CancellationToken ct = default)
    {
        var storedArgs = ConfigRevertManager.LoadRevertArgs();
        if (storedArgs == null && string.IsNullOrEmpty(gameId))
        {
            AppLogger.Info("Không có cấu hình nào để đảo ngược");
            return ErrorCodes.Success;
        }

        var targetGameId = gameId ?? storedArgs?.GameId ?? string.Empty;

        if (removeAll)
        {
            AppLogger.Info("Đang đảo ngược toàn bộ cấu hình...");
        }

        // 1. Xóa chứng chỉ khỏi kho tin cậy
        if (!string.IsNullOrEmpty(storedArgs?.CertData) && storedArgs.Flags.HasFlag(ConfigRevertManager.RevertFlags.AddLocalCert))
        {
            try
            {
                await CertificateUtilities.UntrustLocalCertificateAsync(storedArgs.CertData, ct);
                AppLogger.Info("Đã xóa chứng chỉ khỏi kho tin cậy");
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Lỗi xóa chứng chỉ: {ex.Message}");
            }
        }

        // 2. Khôi phục dữ liệu user
        if (!string.IsNullOrEmpty(targetGameId))
        {
            try
            {
                UserDataManager.RestoreAllUserData(targetGameId);
                AppLogger.Info("Đã khôi phục dữ liệu user");
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Lỗi khôi phục dữ liệu user: {ex.Message}");
            }

            // 3. Khôi phục CA cert game
            if (GameCertificateManager.SupportsCaCertModification(targetGameId))
            {
                try
                {
                    await GameCertificateManager.RestoreCaCertificateAsync(targetGameId, ct);
                    AppLogger.Info("Đã khôi phục CA cert game");
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"Lỗi khôi phục CA cert game: {ex.Message}");
                }
            }
        }

        // 4. Xóa ánh xạ hosts
        try
        {
            HostsManager.RemoveOwnMappings();
            HostsManager.FlushDnsCache();
            AppLogger.Info("Đã xóa ánh xạ hosts");
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Lỗi xóa ánh xạ hosts: {ex.Message}");
        }

        // 5. Thông báo cho admin agent
        await TryNotifyAdminAgentAsync(IpcConstants.ActionRevert, targetGameId, storedArgs?.ServerIp ?? "", null, Array.Empty<string>(), ct);

        // Xóa tham số revert
        ConfigRevertManager.ClearRevertArgs();

        AppLogger.Info("Hoàn tất đảo ngược cấu hình");
        return ErrorCodes.Success;
    }

    /// <summary>
    /// Gửi thông báo tới admin agent qua IPC.
    /// </summary>
    private static async Task TryNotifyAdminAgentAsync(
        byte action,
        string gameId,
        string serverIp,
        string? certDataBase64,
        string[] hosts,
        CancellationToken ct)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", IpcConstants.IpcPath, PipeDirection.Out);
            await client.ConnectAsync(TimeSpan.FromSeconds(2), ct);

            var data = new
            {
                Action = action,
                GameId = gameId,
                ServerIp = serverIp,
                CertData = certDataBase64,
                Hosts = hosts
            };

            var json = JsonSerializer.Serialize(data);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            await client.WriteAsync(bytes, ct);
            await client.FlushAsync(ct);
        }
        catch
        {
            // Agent không chạy - bỏ qua
        }
    }
}

/// <summary>
/// Điểm vào chương trình Launcher Config.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // 1. Cấu hình Unicode cho console (khắc phục lỗi ký tự ?)
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        AppLogger.Initialize();
        AppLogger.SetPrefix("CONFIG");
        CommandExecutor.ChangeWorkingDirectoryToExecutable();

        // === Lệnh setup ===
        var gameOption = new Option<string>(new[] { "--game", "-g" }, "ID game") { IsRequired = true };
        var ipOption = new Option<string>(new[] { "--ip", "-i" }, "IP server") { IsRequired = true };
        var certOption = new Option<string?>(new[] { "--cert", "-c" }, "Chứng chỉ base64");

        var setupCmd = new Command("setup", "Thiết lập cấu hình LAN")
        {
            gameOption, ipOption, certOption
        };
        setupCmd.SetHandler(async (game, ip, cert) =>
        {
            Environment.ExitCode = await LauncherConfig.SetUpAsync(game, ip, cert);
        }, gameOption, ipOption, certOption);

        // === Lệnh revert ===
        var revertGameOption = new Option<string?>(new[] { "--game", "-g" }, "ID game");
        var removeAllFlag = new Option<bool>(new[] { "--all", "-a" }, "Đảo ngược toàn bộ");

        var revertCmd = new Command("revert", "Đảo ngược cấu hình")
        {
            revertGameOption, removeAllFlag
        };
        revertCmd.SetHandler(async (game, all) =>
        {
            Environment.ExitCode = await LauncherConfig.RevertAsync(game, all);
        }, revertGameOption, removeAllFlag);

        var rootCmd = new RootCommand("Launcher Config - Cấu hình hệ thống cho LAN Server")
        {
            setupCmd, revertCmd
        };

        return await rootCmd.InvokeAsync(args);
    }
}
