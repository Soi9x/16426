using System.CommandLine;
using System.CommandLine.Invocation;
using System.Security.Cryptography.X509Certificates;
using AgeLanServer.Common;
using AgeLanServer.LauncherCommon;

namespace AgeLanServer.LauncherConfigAdmin;

/// <summary>
/// Điểm vào chính của chương trình config-admin.
/// Kiểm tra quyền admin, đăng ký các lệnh setup/revert,
/// và xử lý cleanup khi thoát.
/// </summary>
public static class Program
{
    /// <summary>
    /// Cờ theo dõi các thao tác đã thực hiện để revert khi thoát.
    /// </summary>
    private static ConfigRevertManager.RevertFlags _executedFlags = ConfigRevertManager.RevertFlags.None;
    private static string _currentCertDataBase64 = string.Empty;
    private static string _currentIp = string.Empty;
    private static string _currentGameId = string.Empty;

    public static async Task<int> Main(string[] args)
    {
        // 1. Cấu hình Unicode cho console (khắc phục lỗi ký tự ?)
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        // 2. Kiểm tra quyền admin
        if (OperatingSystem.IsWindows() && !CommandExecutor.IsRunningAsAdmin())
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Console.WriteLine("Lỗi: Không tìm thấy file thực thi để nâng quyền.");
                return ErrorCodes.General;
            }

            Console.WriteLine("config-admin cần chạy với quyền Administrator để sửa hosts/cert. Đang nâng quyền...");
            
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
        else if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && !CommandExecutor.IsRunningAsAdmin())
        {
            Console.Error.WriteLine("Lỗi: Chương trình yêu cầu quyền root (sudo) trên Linux/macOS.");
            return ErrorCodes.General;
        }

        // --- Đăng ký lệnh setup ---
        var setupIpOption = new Option<string>(
            "--ip",
            "IP can anh xa trong hosts file.")
        { IsRequired = false };

        var setupCertOption = new Option<string>(
            "--cert",
            "Chung chi PEM duoi dang base64.")
        { IsRequired = false };

        var setupHostsOption = new Option<string>(
            "--hosts",
            "Danh sach host can anh xa, phan cach bang dau phay.")
        { IsRequired = false };

        var setupGameIdOption = new Option<string>(
            "--gameId",
            "Game ID (age1, age2, age3, age4, athens).")
        { IsRequired = false };

        var setupCommand = new Command("setup", "Thiet lap cau hinh he thong.")
        {
            setupIpOption,
            setupCertOption,
            setupHostsOption,
            setupGameIdOption
        };

        setupCommand.SetHandler(
            async (ip, cert, hosts, gameId) =>
            {
                try
                {
                    await HandleSetupAsync(ip, cert, hosts, gameId);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[SETUP] Loi: {ex.Message}");
                    Environment.ExitCode = 1;
                }
            },
            setupIpOption,
            setupCertOption,
            setupHostsOption,
            setupGameIdOption);

        // --- Đăng ký lệnh revert ---
        var revertIpOption = new Option<string>(
            "--ip",
            "IP can xoa anh xa.")
        { IsRequired = false };

        var revertCertOption = new Option<string>(
            "--cert",
            "Chung chi PEM duoi dang base64 can xoa.")
        { IsRequired = false };

        var removeAllOption = new Option<bool>(
            "--removeAll",
            "Xoa toan bo cau hinh.")
        { IsRequired = false };

        var revertCommand = new Command("revert", "Dao nguoc cau hinh he thong.")
        {
            revertIpOption,
            revertCertOption,
            removeAllOption
        };

        revertCommand.SetHandler(
            (ip, cert, removeAll) =>
            {
                try
                {
                    HandleRevert(ip, cert, removeAll);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[REVERT] Loi: {ex.Message}");
                    Environment.ExitCode = 1;
                }
            },
            revertIpOption,
            revertCertOption,
            removeAllOption);

        // --- Root command ---
        var rootCommand = new RootCommand(
            "config-admin - Quan tri cau hinh he thong cho AgeLanServer.")
        {
            setupCommand,
            revertCommand
        };

        // Đăng ký signal handler để cleanup khi thoát
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[SIGNAL] Nhan duoc Ctrl+C, dang thuc hien cleanup...");
            PerformCleanup();
            Environment.Exit(0);
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            PerformCleanup();
        };

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Xử lý lệnh setup: thêm cert, ánh xạ hosts, lưu revert args.
    /// </summary>
    private static async Task HandleSetupAsync(
        string? ip,
        string? cert,
        string? hosts,
        string? gameId)
    {
        _currentIp = ip ?? string.Empty;
        _currentCertDataBase64 = cert ?? string.Empty;
        _currentGameId = gameId ?? string.Empty;

        // 1. Thêm chứng chỉ vào kho tin cậy
        if (!string.IsNullOrEmpty(cert))
        {
            Console.WriteLine("[SETUP] Dang them chung chi vao kho tin cay...");
            CertificateManager.TrustCertificate(cert);
            _executedFlags |= ConfigRevertManager.RevertFlags.AddLocalCert;
        }

        // 2. Ánh xạ hosts
        if (!string.IsNullOrEmpty(ip) && !string.IsNullOrEmpty(hosts))
        {
            var hostList = hosts
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            Console.WriteLine($"[SETUP] Dang anh xa {ip} toi {hostList.Length} host(s)...");
            HostsManager.AddHostMappings(ip, hostList);
            HostsManager.FlushDnsCache();
            _executedFlags |= ConfigRevertManager.RevertFlags.MapIP;
        }

        // 3. Lưu revert args để khôi phục khi cần
        var revertArgs = new ConfigRevertManager.RevertArgs
        {
            Flags = _executedFlags,
            GameId = _currentGameId,
            ServerIp = _currentIp,
            CertData = _currentCertDataBase64,
            BattleServerExe = string.Empty,
            BattleServerRegion = string.Empty
        };

        ConfigRevertManager.StoreRevertArgs(revertArgs);
        Console.WriteLine("[SETUP] Da luu tham so de revert.");
    }

    /// <summary>
    /// Xử lý lệnh revert: xóa cert, xóa hosts mapping, cleanup.
    /// </summary>
    private static void HandleRevert(
        string? ip,
        string? cert,
        bool removeAll)
    {
        if (removeAll)
        {
            Console.WriteLine("[REVERT] Dang xoa toan bo cau hinh...");

            // Thử đọc revert args đã lưu
            var savedArgs = ConfigRevertManager.LoadRevertArgs();
            if (savedArgs != null)
            {
                Console.WriteLine("[REVERT] Su dung tham so da luu de dao nguoc...");

                if (savedArgs.Flags.HasFlag(ConfigRevertManager.RevertFlags.AddLocalCert)
                    && !string.IsNullOrEmpty(savedArgs.CertData))
                {
                    CertificateManager.UntrustCertificate(savedArgs.CertData);
                }

                if (savedArgs.Flags.HasFlag(ConfigRevertManager.RevertFlags.MapIP)
                    && !string.IsNullOrEmpty(savedArgs.ServerIp))
                {
                    HostsManager.RemoveOwnMappings();
                    HostsManager.FlushDnsCache();
                }

                ConfigRevertManager.ClearRevertArgs();
            }
            else
            {
                Console.WriteLine("[REVERT] Khong tim thay tham so da luu. Thuc hien revert mac dinh...");

                // Revert mặc định: xóa tất cả ánh xạ có đánh dấu
                HostsManager.RemoveOwnMappings();
                HostsManager.FlushDnsCache();
            }

            Console.WriteLine("[REVERT] Da hoan tat xoa toan bo cau hinh.");
        }
        else
        {
            // Revert từng phần
            if (!string.IsNullOrEmpty(cert))
            {
                Console.WriteLine("[REVERT] Dang xoa chung chi...");
                CertificateManager.UntrustCertificate(cert);
            }

            if (!string.IsNullOrEmpty(ip))
            {
                Console.WriteLine($"[REVERT] Dang xoa anh xa hosts cho IP: {ip}");
                HostsManager.RemoveOwnMappings();
                HostsManager.FlushDnsCache();
            }
        }
    }

    /// <summary>
    /// Thực hiện cleanup khi chương trình thoát bất ngờ.
    /// </summary>
    private static void PerformCleanup()
    {
        if (_executedFlags == ConfigRevertManager.RevertFlags.None)
            return;

        Console.WriteLine("[CLEANUP] Dang thuc hien cleanup...");

        try
        {
            var revertArgs = new ConfigRevertManager.RevertArgs
            {
                Flags = _executedFlags,
                GameId = _currentGameId,
                ServerIp = _currentIp,
                CertData = _currentCertDataBase64,
                BattleServerExe = string.Empty,
                BattleServerRegion = string.Empty
            };

            ConfigRevertManager.StoreRevertArgs(revertArgs);
            Console.WriteLine("[CLEANUP] Da luu trang thai de co the revert sau.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLEANUP] Loi khi cleanup: {ex.Message}");
        }
    }
}
