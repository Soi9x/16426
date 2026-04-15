using System.CommandLine;
using System.Security.Cryptography.X509Certificates;
using AgeLanServer.LauncherCommon;
using AgeLanServer.LauncherCommon.Cert;

namespace AgeLanServer.LauncherConfigAdmin;

/// <summary>
/// Xử lý lệnh "revert": gỡ chứng chỉ khỏi kho tin cậy,
/// xóa ánh xạ IP khỏi tệp hosts hệ thống, và thiết lập signal handler
/// để tự động thêm lại chứng chỉ khi nhận tín hiệu thoát.
/// Chuyển thể từ launcher-config-admin/internal/cmd/revert.go.
/// </summary>
public static class CmdRevert
{
    /// <summary>
    /// Tạo đối tượng Command "revert" với các tùy chọn cần thiết.
    /// </summary>
    public static Command CreateCommand()
    {
        var unmapIpOption = new Option<bool>(
            "--ip",
            "Xoa anh xa IP khoi tep hosts.");

        var removeCertOption = new Option<bool>(
            "--localCert",
            "Xoa chung chi khoi kho tin cay cua may cuc bo.");

        var removeAllOption = new Option<bool>(
            "--all",
            "Xoa toan bo cau hinh. Tuong duong voi --ip --localCert nhung khong dung khi loi.");

        var logRootOption = new Option<string?>(
            "--logRoot",
            "Duong dan thu muc ghi log. Neu bo qua thi khong ghi log ra file.");

        var revertCommand = new Command("revert", "Dao nguoc cau hinh: go chung chi va anh xa IP.")
        {
            unmapIpOption,
            removeCertOption,
            removeAllOption,
            logRootOption
        };

        revertCommand.SetHandler(
            async (unmapIp, removeCert, removeAll, logRoot) =>
            {
                var exitCode = await RunRevertAsync(unmapIp, removeCert, removeAll, logRoot);
                if (exitCode != 0)
                {
                    Environment.Exit(exitCode);
                }
            },
            unmapIpOption,
            removeCertOption,
            removeAllOption,
            logRootOption);

        return revertCommand;
    }

    /// <summary>
    /// Thực thi toàn bộ quy trình revert: gỡ chứng chỉ, xóa ánh xạ hosts,
    /// đăng ký signal handler để thêm lại chứng chỉ khi thoát.
    /// Trả về mã thoát: 0 = thành công, khác 0 = lỗi.
    /// Tương đương Go: func runRevert(args []string) error.
    /// </summary>
    private static async Task<int> RunRevertAsync(
        bool unmapIp,
        bool removeCert,
        bool removeAll,
        string? logRoot)
    {
        // Đặt trạng thái global là đang revert (tương đương Go: internal.SetUp = false)
        AdminState.SetUp = false;

        // Khởi tạo logger nếu có logRoot
        if (!string.IsNullOrWhiteSpace(logRoot))
        {
            Console.WriteLine($"[REVERT] Log se duoc ghi vao: {logRoot}");
        }

        // Nếu --all được đặt, bật cả hai tùy chọn con
        // (tương đương Go: if launcherCommonCmd.RemoveAll { UnmapIPs = true; RemoveLocalCert = true })
        if (removeAll)
        {
            unmapIp = true;
            removeCert = true;
        }

        List<X509Certificate2>? removedCertificates = null;

        // --- Bước 1: Gỡ chứng chỉ cục bộ ---
        // (tương đương Go: if launcherCommonCmd.RemoveLocalCert { ... })
        if (removeCert)
        {
            Console.WriteLine("[REVERT] Dang go chung chi cuc bo...");

            try
            {
                // Gỡ chứng chỉ khỏi kho LocalMachine\ROOT (userStore = false)
                // (tương đương Go: removedCertificates, err = cert.UntrustCertificates(false))
                removedCertificates = CertificateStore.UntrustCertificates(userStore: false);

                if (removedCertificates.Count > 0)
                {
                    Console.WriteLine($"[REVERT] Go chung chi thanh cong. Da xoa {removedCertificates.Count} chung chi.");

                    // Đăng ký signal handler: khi nhận SIGINT/SIGTERM thì thêm lại chứng chỉ
                    // (tương đương Go: signal.Notify(sigs, syscall.SIGINT, syscall.SIGTERM) + goroutine
                    //  go func() { _, ok := <-sigs; if ok { trustCertificates(removedCertificates); os.Exit(common.ErrSignal) } }())
                    RegisterRevertSignalHandler(removedCertificates);
                }
                else
                {
                    Console.WriteLine("[REVERT] Khong tim thay chung chi nao de xoa.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[REVERT] Loi: Khong the go chung chi: {ex.Message}");
                if (!removeAll)
                {
                    return AdminErrorCodes.ErrLocalCertRemove;
                }
            }
        }

        // --- Bước 2: Xóa ánh xạ IP khỏi hosts file ---
        // (tương đương Go: if launcherCommonCmd.UnmapIPs { ... })
        if (unmapIp)
        {
            Console.WriteLine("[REVERT] Dang xoa anh xa IP...");

            try
            {
                // Xóa các dòng có đánh dấu "own" và khôi phục từ .bak nếu có
                // (tương đương Go: hosts.RemoveHosts())
                bool hostsOk = HostsRemover.RemoveHosts();

                if (!hostsOk)
                {
                    throw new InvalidOperationException("Khong the xoa anh xa IP.");
                }

                Console.WriteLine("[REVERT] Xoa anh xa IP thanh cong.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[REVERT] Loi: Khong the xoa anh xa IP: {ex.Message}");

                int errorCode = AdminErrorCodes.ErrIpMapRemove;
                if (!removeAll)
                {
                    // Nếu đã gỡ chứng chỉ trước đó, cố gắng thêm lại (fail-safe revert)
                    // (tương đương Go: if removedCertificates != nil { if !trustCertificates(removedCertificates) { errorCode = ErrIpMapRemoveRevert } })
                    if (removedCertificates != null && removedCertificates.Count > 0)
                    {
                        if (!TrustCertificatesSafe(removedCertificates))
                        {
                            errorCode = AdminErrorCodes.ErrIpMapRemoveRevert;
                        }
                    }
                }

                if (!removeAll)
                {
                    return errorCode;
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Đăng ký handler cho Ctrl+C (SIGINT) để tự động thêm lại chứng chỉ đã gỡ khi thoát.
    /// Tương đương Go: signal.Notify(sigs, syscall.SIGINT, syscall.SIGTERM) + goroutine
    /// go func() { _, ok := <-sigs; if ok { trustCertificates(removedCertificates); os.Exit(common.ErrSignal) } }().
    /// </summary>
    /// <param name="removedCertificates">Danh sách chứng chỉ đã gỡ, cần thêm lại khi thoát.</param>
    private static void RegisterRevertSignalHandler(List<X509Certificate2> removedCertificates)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // Ngăn thoát ngay lập tức
            Console.WriteLine("\n[REVERT] Nhan Ctrl+C, dang them lai chung chi truoc khi thoat...");
            TrustCertificatesSafe(removedCertificates);
            Environment.Exit(Common.ErrorCodes.Signal);
        };
    }

    /// <summary>
    /// Thêm lại danh sách chứng chỉ đã gỡ trước đó.
    /// Trả về true nếu thành công, false nếu thất bại.
    /// Tương đương Go: trustCertificates([]*x509.Certificate) -> bool.
    /// </summary>
    private static bool TrustCertificatesSafe(List<X509Certificate2> certificates)
    {
        Console.WriteLine("[REVERT] Dang them lai chung chi...");
        try
        {
            // Thêm lại chứng chỉ vào kho LocalMachine\ROOT (userStore = false)
            // (tương đương Go: cert.TrustCertificates(false, certificates))
            CertificateStore.TrustCertificates(userStore: false, certificates.ToArray());
            Console.WriteLine("[REVERT] Them lai chung chi thanh cong.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[REVERT] Loi khi them lai chung chi: {ex.Message}");
            return false;
        }
    }
}
