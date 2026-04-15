using System.Security.Cryptography.X509Certificates;

namespace AgeLanServer.LauncherCommon.Cert;

/// <summary>
/// Trình quản lý chứng chỉ trong kho tin cậy của hệ thống.
/// Chuyển đổi từ Go package: launcher-common/cert/cert.go, cert_windows.go, cert_linux.go, ca.go
/// </summary>
public static class CertificateStore
{
    // Tên organization dùng để nhận diện chứng chỉ của chương trình
    private const string CertSubjectOrganization = "github.com/luskaner/ageLANServer";

    /// <summary>
    /// Tin cậy các chứng chỉ bằng cách thêm vào kho Root của hệ thống.
    /// Trên Windows: thêm vào chứng chỉ Root (CurrentUser hoặc LocalMachine).
    /// Trên Linux: thêm vào cert store của hệ thống.
    /// </summary>
    /// <param name="userStore">True = CurrentUser, False = LocalMachine (Windows only)</param>
    /// <param name="certificates">Danh sách chứng chỉ cần tin cậy</param>
    public static void TrustCertificates(bool userStore, params X509Certificate2[] certificates)
    {
        if (OperatingSystem.IsWindows())
        {
            TrustCertificatesWindows(userStore, certificates);
        }
        else
        {
            TrustCertificatesLinux(certificates);
        }
    }

    /// <summary>
    /// Tin cậy các chứng chỉ trên Windows.
    /// Thêm vào kho chứng chỉ Root.
    /// </summary>
    private static void TrustCertificatesWindows(bool userStore, params X509Certificate2[] certificates)
    {
        StoreName storeName = StoreName.Root;
        StoreLocation storeLocation = userStore ? StoreLocation.CurrentUser : StoreLocation.LocalMachine;

        using var store = new X509Store(storeName, storeLocation);
        store.Open(OpenFlags.ReadWrite);

        foreach (var cert in certificates)
        {
            // Tạo chứng chỉ mới từ raw data
            using var newCert = new X509Certificate2(cert.RawData);

            // Chỉ thêm nếu chưa tồn tại (trùng subject)
            bool exists = false;
            foreach (var existing in store.Certificates)
            {
                if (existing.Subject == newCert.Subject)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                store.Add(newCert);
            }
        }

        store.Close();
    }

    /// <summary>
    /// Tin cậy các chứng chỉ trên Linux.
    /// Sao chép cert vào thư mục anchors và chạy update-ca-certificates.
    /// </summary>
    private static void TrustCertificatesLinux(params X509Certificate2[] certificates)
    {
        // Tìm thư mục cert store
        string? certDir = GetLinuxCertDir();
        if (string.IsNullOrEmpty(certDir))
        {
            throw new InvalidOperationException("Cert store not found on Linux");
        }

        string certFileName = "ageLANServer.crt";
        string certPath = Path.Combine(certDir, certFileName);

        foreach (var cert in certificates)
        {
            // Ghi cert dưới dạng PEM
            WriteCertAsPem(cert, certPath);
        }

        // Cấp quyền đọc
        File.SetUnixFileMode(certPath, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        // Cập nhật cert store
        UpdateLinuxCertStore();
    }

    /// <summary>
    /// Gỡ tin cậy các chứng chỉ của chương trình.
    /// Trên Windows: xóa chứng chỉ có subject trùng với organization.
    /// Trên Linux: xóa file cert và cập nhật store.
    /// </summary>
    /// <param name="userStore">True = CurrentUser, False = LocalMachine (Windows only)</param>
    /// <returns>Danh sách chứng chỉ đã gỡ (trên Linux có thể trả về rỗng nếu update store thất bại)</returns>
    public static List<X509Certificate2> UntrustCertificates(bool userStore)
    {
        var removedCerts = new List<X509Certificate2>();

        if (OperatingSystem.IsWindows())
        {
            removedCerts = UntrustCertificatesWindows(userStore);
        }
        else
        {
            removedCerts = UntrustCertificatesLinux();
        }

        return removedCerts;
    }

    /// <summary>
    /// Gỡ tin cậy chứng chỉ trên Windows.
    /// Tìm và xóa chứng chí có Subject chứa organization của chương trình.
    /// </summary>
    private static List<X509Certificate2> UntrustCertificatesWindows(bool userStore)
    {
        var removedCerts = new List<X509Certificate2>();

        StoreName storeName = StoreName.Root;
        StoreLocation storeLocation = userStore ? StoreLocation.CurrentUser : StoreLocation.LocalMachine;

        using var store = new X509Store(storeName, storeLocation);
        store.Open(OpenFlags.ReadWrite);

        // Tìm chứng chỉ có subject chứa organization
        var certsToRemove = store.Certificates
            .Find(X509FindType.FindBySubjectName, CertSubjectOrganization, false)
            .Cast<X509Certificate2>()
            .ToList();

        foreach (var cert in certsToRemove)
        {
            removedCerts.Add(new X509Certificate2(cert.RawData));
            store.Remove(cert);
        }

        store.Close();

        return removedCerts;
    }

    /// <summary>
    /// Gỡ tin cậy chứng chỉ trên Linux.
    /// Xóa file cert và cập nhật store.
    /// </summary>
    private static List<X509Certificate2> UntrustCertificatesLinux()
    {
        var removedCerts = new List<X509Certificate2>();

        string? certDir = GetLinuxCertDir();
        if (string.IsNullOrEmpty(certDir))
        {
            return removedCerts;
        }

        string certFileName = "ageLANServer.crt";
        string certPath = Path.Combine(certDir, certFileName);

        if (!File.Exists(certPath))
        {
            return removedCerts;
        }

        try
        {
            // Đọc chứng chỉ trước khi xóa
            byte[] certBytes = File.ReadAllBytes(certPath);
            var cert = new X509Certificate2(certBytes);
            removedCerts.Add(cert);

            // Xóa file cert
            File.Delete(certPath);

            // Cập nhật cert store
            UpdateLinuxCertStore();
        }
        catch
        {
            // Nếu update store thất bại, vẫn trả về cert đã xóa
        }

        return removedCerts;
    }

    /// <summary>
    /// Liệt kê tất cả chứng chỉ trong kho tin cậy.
    /// Trên Windows: từ store Root.
    /// Trên Linux: từ cert bundle.
    /// </summary>
    /// <param name="userStore">True = CurrentUser, False = LocalMachine (Windows only)</param>
    /// <returns>Danh sách chứng chỉ</returns>
    public static List<X509Certificate2> EnumCertificates(bool userStore)
    {
        if (OperatingSystem.IsWindows())
        {
            return EnumCertificatesWindows(userStore);
        }
        else
        {
            return EnumCertificatesLinux();
        }
    }

    /// <summary>
    /// Liệt kê chứng chỉ trên Windows.
    /// </summary>
    private static List<X509Certificate2> EnumCertificatesWindows(bool userStore)
    {
        var certs = new List<X509Certificate2>();

        StoreName storeName = StoreName.Root;
        StoreLocation storeLocation = userStore ? StoreLocation.CurrentUser : StoreLocation.LocalMachine;

        using var store = new X509Store(storeName, storeLocation);
        store.Open(OpenFlags.ReadOnly);

        foreach (var cert in store.Certificates)
        {
            certs.Add(new X509Certificate2(cert.RawData));
        }

        store.Close();

        return certs;
    }

    /// <summary>
    /// Liệt kê chứng chỉ trên Linux từ cert bundle.
    /// </summary>
    private static List<X509Certificate2> EnumCertificatesLinux()
    {
        var certs = new List<X509Certificate2>();

        // Các đường dẫn cert bundle phổ biến trên Linux
        string[] bundlePaths = new[]
        {
            "/etc/ssl/certs/ca-certificates.crt",      // Debian, Arch
            "/etc/pki/tls/certs/ca-bundle.crt",        // Fedora
            "/etc/pki/tls/certs/ca-bundle.pem",        // OpenSUSE
            "/etc/ssl/ca-bundle.pem"                   // Other
        };

        foreach (var bundlePath in bundlePaths)
        {
            if (!File.Exists(bundlePath))
            {
                continue;
            }

            try
            {
                string pemContent = File.ReadAllText(bundlePath);
                certs = ParseCertificatesFromPem(pemContent);
                return certs;
            }
            catch
            {
                // Thử bundle tiếp theo
            }
        }

        throw new InvalidOperationException("No cert bundle found");
    }

    /// <summary>
    /// Lấy thư mục cert store trên Linux.
    /// </summary>
    private static string? GetLinuxCertDir()
    {
        // Các thư mục cert store phổ biến
        string[] certDirs = new[]
        {
            "/etc/ca-certificates/trust-source/anchors",  // Arch
            "/usr/local/share/ca-certificates",           // Debian
            "/etc/pki/ca-trust/source/anchors",           // Fedora
            "/etc/pki/trust/anchors"                      // OpenSUSE
        };

        foreach (var dir in certDirs)
        {
            if (Directory.Exists(dir))
            {
                return dir;
            }
        }

        return null;
    }

    /// <summary>
    /// Ghi chứng chỉ dưới dạng PEM.
    /// </summary>
    private static void WriteCertAsPem(X509Certificate2 cert, string filePath)
    {
        // Export cert dưới dạng PEM
        string pemContent = "-----BEGIN CERTIFICATE-----\n" +
            Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks) +
            "\n-----END CERTIFICATE-----\n";

        File.WriteAllText(filePath, pemContent);
    }

    /// <summary>
    /// Cập nhật cert store trên Linux.
    /// Chạy update-ca-certificates hoặc update-ca-trust.
    /// </summary>
    private static void UpdateLinuxCertStore()
    {
        string[] updateCommands = new[]
        {
            "update-ca-certificates",  // Debian, OpenSUSE
            "update-ca-trust"          // Fedora, Arch
        };

        foreach (var cmd in updateCommands)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = cmd,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit();

                if (proc?.ExitCode == 0)
                {
                    return;
                }
            }
            catch
            {
                // Thử lệnh tiếp theo
            }
        }

        throw new InvalidOperationException("Failed to update cert store");
    }

    /// <summary>
    /// Phân tích nhiều chứng chỉ từ nội dung PEM.
    /// </summary>
    private static List<X509Certificate2> ParseCertificatesFromPem(string pemContent)
    {
        var certs = new List<X509Certificate2>();

        string beginMarker = "-----BEGIN CERTIFICATE-----";
        string endMarker = "-----END CERTIFICATE-----";

        int startIndex = 0;
        while ((startIndex = pemContent.IndexOf(beginMarker, startIndex, StringComparison.Ordinal)) >= 0)
        {
            int endIndex = pemContent.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                break;
            }

            endIndex += endMarker.Length;
            string pemBlock = pemContent.Substring(startIndex, endIndex - startIndex).Trim();

            try
            {
                // Loại bỏ markers và decode base64
                string base64 = pemBlock
                    .Replace(beginMarker, "")
                    .Replace(endMarker, "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Trim();

                byte[] certBytes = Convert.FromBase64String(base64);
                var cert = new X509Certificate2(certBytes);
                certs.Add(cert);
            }
            catch
            {
                // Bỏ qua cert không hợp lệ
            }

            startIndex = endIndex;
        }

        return certs;
    }
}

/// <summary>
/// Lớp đại diện cho Certificate Authority (CA) của game.
/// Chuyển đổi từ Go package: launcher-common/cert/ca.go
/// </summary>
public class CA
{
    private readonly string _gamePath;

    /// <summary>
    /// Tên file chứng chỉ CA.
    /// </summary>
    private string Name => "cacert.pem";

    /// <summary>
    /// Tạo mới CA object.
    /// </summary>
    /// <param name="gameId">ID của game</param>
    /// <param name="gamePath">Đường dẫn đến thư mục game</param>
    public CA(string gameId, string gamePath)
    {
        // Với Age of Empires 2, thư mục certificates nằm trong gamePath
        if (gameId == "age2")
        {
            _gamePath = Path.Combine(gamePath, "certificates");
        }
        else
        {
            _gamePath = gamePath;
        }
    }

    /// <summary>
    /// Đường dẫn gốc của file CA cert.
    /// </summary>
    public string OriginalPath => Path.Combine(_gamePath, Name);

    /// <summary>
    /// Đường dẫn file tạm (LAN version).
    /// </summary>
    public string TmpPath => Path.Combine(_gamePath, Name + ".lan");

    /// <summary>
    /// Đường dẫn file backup.
    /// </summary>
    public string BackupPath => Path.Combine(_gamePath, Name + ".bak");
}
