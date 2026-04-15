using System.CommandLine;
using System.Security.Cryptography.X509Certificates;
using AgeLanServer.LauncherCommon;
using AgeLanServer.LauncherCommon.Cert;

namespace AgeLanServer.LauncherConfigAdmin;

/// <summary>
/// Xử lý lệnh "setup": thêm chứng chỉ cục bộ vào kho tin cậy,
/// ánh xạ IP vào tệp hosts hệ thống, và thiết lập signal handler
/// để tự động gỡ chứng chỉ khi nhận tín hiệu thoát (Ctrl+C / SIGTERM).
/// Chuyển thể từ launcher-config-admin/internal/cmd/setUp.go.
/// </summary>
public static class CmdSetup
{
    /// <summary>
    /// Tạo đối tượng Command "setup" với các tùy chọn cần thiết.
    /// </summary>
    public static Command CreateCommand()
    {
        var certOption = new Option<string?>(
            "--cert",
            "Dữ liệu chứng chỉ PEM dưới dạng base64. Nếu bỏ qua thì không thêm chứng chỉ.");

        var ipOption = new Option<string?>(
            "--ip",
            "Địa chỉ IP cần ánh xạ trong tệp hosts.");

        var hostsOption = new Option<string?>(
            "--hosts",
            "Danh sách tên host cần ánh xạ, phân cách bằng dấu phẩy.");

        var gameIdOption = new Option<string>(
            "--gameId",
            "Game ID (ví dụ: age1, age2, age3, age4, athens).")
        { IsRequired = true };

        var logRootOption = new Option<string?>(
            "--logRoot",
            "Đường dẫn thư mục ghi log. Nếu bỏ qua thì không ghi log ra file.");

        var setupCommand = new Command("setup", "Thiết lập cấu hình: thêm chứng chỉ và ánh xạ IP.")
        {
            certOption,
            ipOption,
            hostsOption,
            gameIdOption,
            logRootOption
        };

        setupCommand.SetHandler(
            async (certData, ip, hosts, gameId, logRoot) =>
            {
                var exitCode = await RunSetupAsync(certData, ip, hosts, gameId, logRoot);
                if (exitCode != 0)
                {
                    Environment.Exit(exitCode);
                }
            },
            certOption,
            ipOption,
            hostsOption,
            gameIdOption,
            logRootOption);

        return setupCommand;
    }

    /// <summary>
    /// Thực thi toàn bộ quy trình setup: thêm chứng chỉ, ánh xạ hosts,
    /// đăng ký signal handler để gỡ chứng chỉ khi thoát.
    /// Trả về mã thoát: 0 = thành công, khác 0 = lỗi.
    /// Tương đương Go: func runSetUp(args []string) error.
    /// </summary>
    private static async Task<int> RunSetupAsync(
        string? certDataBase64,
        string? ip,
        string? hosts,
        string gameId,
        string? logRoot)
    {
        // Validate: gameId là bắt buộc (tương đương Go: if launcherCommonCmd.GameId == "" { return error })
        if (string.IsNullOrWhiteSpace(gameId))
        {
            Console.Error.WriteLine("[SETUP] Loi: Thieu tham so bat buoc 'gameId'.");
            return AdminErrorCodes.ErrLocalCertRemove;
        }

        // Đặt trạng thái global là đang setup (tương đương Go: internal.SetUp = true)
        AdminState.SetUp = true;

        // Khởi tạo logger nếu có logRoot (tương đương Go: if logRoot != "" { internal.Initialize(logRoot) })
        if (!string.IsNullOrWhiteSpace(logRoot))
        {
            Console.WriteLine($"[SETUP] Log se duoc ghi vao: {logRoot}");
        }

        bool trustedCertificate = false;

        // --- Bước 1: Thêm chứng chỉ cục bộ vào kho tin cậy ---
        // (tương đương Go: if len(launcherCommonCmd.AddLocalCertData) > 0 { ... })
        if (!string.IsNullOrWhiteSpace(certDataBase64))
        {
            Console.WriteLine("[SETUP] Dang them chung chi cuc bo...");

            X509Certificate2? cert = ParseCertificateFromBase64(certDataBase64);
            if (cert == null)
            {
                Console.Error.WriteLine("[SETUP] Loi: Khong phan tich duoc chung chi (dinh dang khong hop le).");
                return AdminErrorCodes.ErrLocalCertAddParse;
            }

            try
            {
                // Thêm chứng chỉ vào kho LocalMachine\ROOT (userStore = false)
                // (tương đương Go: cert.TrustCertificates(false, []*x509.Certificate{crt}))
                CertificateStore.TrustCertificates(userStore: false, cert);

                Console.WriteLine("[SETUP] Them chung chi thanh cong.");
                trustedCertificate = true;

                // Đăng ký signal handler: khi nhận SIGINT/SIGTERM thì gỡ chứng chỉ rồi thoát
                // (tương đương Go: signal.Notify(sigs, syscall.SIGINT, syscall.SIGTERM) + goroutine)
                RegisterSetupSignalHandler();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SETUP] Loi: Khong the them chung chi: {ex.Message}");
                return AdminErrorCodes.ErrLocalCertAdd;
            }
        }

        // --- Bước 2: Ánh xạ IP vào hosts file ---
        // (tương đương Go: if len(launcherCommonCmd.MapIP) > 0 { ... })
        if (!string.IsNullOrWhiteSpace(ip) && !string.IsNullOrWhiteSpace(hosts))
        {
            Console.WriteLine("[SETUP] Dang them anh xa IP...");

            var hostList = hosts
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            try
            {
                // Tạo backup trước khi sửa đổi
                HostsManager.CreateBackup();

                // Thêm ánh xạ IP (tương đương Go: launcherCommonHosts.AddHosts(...))
                HostsManager.AddHostMappings(ip, hostList);

                // Flush DNS cache để áp dụng thay đổi
                HostsManager.FlushDnsCache();

                Console.WriteLine("[SETUP] Them anh xa IP thanh cong.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SETUP] Loi: Khong the them anh xa IP: {ex.Message}");

                // Nếu đã thêm chứng chỉ trước đó, cố gắng gỡ lại (fail-safe revert)
                // (tương đương Go: if trustedCertificate { if !untrustCertificate() { errorCode = ErrIpMapAddRevert } })
                int errorCode = AdminErrorCodes.ErrIpMapAdd;
                if (trustedCertificate)
                {
                    if (!UntrustCertificateSafe())
                    {
                        errorCode = AdminErrorCodes.ErrIpMapAddRevert;
                    }
                }

                return errorCode;
            }
        }

        return 0;
    }

    /// <summary>
    /// Giải mã chuỗi base64 thành đối tượng X509Certificate2.
    /// Trả về null nếu dữ liệu không hợp lệ.
    /// Tương đương Go: cert.BytesToCertificate().
    /// </summary>
    private static X509Certificate2? ParseCertificateFromBase64(string certDataBase64)
    {
        try
        {
            byte[] certBytes = Convert.FromBase64String(certDataBase64);
#pragma warning disable SYSLIB0057
            return new X509Certificate2(certBytes);
#pragma warning restore SYSLIB0057
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Đăng ký handler cho Ctrl+C (SIGINT) để tự động gỡ chứng chỉ khi thoát.
    /// Tương đương Go: signal.Notify(sigs, syscall.SIGINT, syscall.SIGTERM) + goroutine
    /// go func() { _, ok := <-sigs; if ok { untrustCertificate(); os.Exit(common.ErrSignal) } }().
    /// </summary>
    private static void RegisterSetupSignalHandler()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // Ngăn thoát ngay lập tức
            Console.WriteLine("\n[SETUP] Nhan Ctrl+C, dang go chung chi truoc khi thoat...");
            UntrustCertificateSafe();
            Environment.Exit(Common.ErrorCodes.Signal);
        };
    }

    /// <summary>
    /// Gỡ chứng chỉ cục bộ đã thêm trước đó.
    /// Trả về true nếu thành công, false nếu thất bại.
    /// Tương đương Go: untrustCertificate() -> bool.
    /// </summary>
    private static bool UntrustCertificateSafe()
    {
        Console.WriteLine("[SETUP] Dang go chung chi cu bo...");
        try
        {
            // Gỡ chứng chỉ khỏi kho LocalMachine\ROOT (userStore = false)
            // (tương đương Go: cert.UntrustCertificates(false))
            List<X509Certificate2> removed = CertificateStore.UntrustCertificates(userStore: false);
            Console.WriteLine($"[SETUP] Go chung chi thanh cong. Da xoa {removed.Count} chung chi.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SETUP] Loi khi go chung chi: {ex.Message}");
            return false;
        }
    }
}
