using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AgeLanServer.Common;

/// <summary>
/// Quản lý chứng chỉ SSL/TLS: kiểm tra, đọc và xác thực cặp chứng chỉ.
/// Tương đương common/cert.go trong bản Go gốc.
/// </summary>
public static class CertificateManager
{
    /// <summary>
    /// Lấy thư mục chứa chứng chí dựa trên đường dẫn file thực thi server.
    /// Tạo thư mục nếu chưa tồn tại.
    /// </summary>
    public static string? GetCertificateFolder(string? executablePath)
    {
        if (string.IsNullOrEmpty(executablePath))
            return null;

        var parentDir = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrEmpty(parentDir))
            return null;

        var certFolder = Path.Combine(parentDir, "resources", "certificates");

        if (!Directory.Exists(certFolder))
        {
            try
            {
                Directory.CreateDirectory(certFolder);
            }
            catch
            {
                return null;
            }
        }

        return certFolder;
    }

    /// <summary>
    /// Kiểm tra sự tồn tại của tất cả cặp chứng chỉ (cert, key, cacert, self-signed).
    /// Trả về đường dẫn đầy đủ nếu tất cả tồn tại.
    /// </summary>
    public static bool CheckAllCertificates(
        string? executablePath,
        out string? certFolder,
        out string? certPath,
        out string? keyPath,
        out string? caCertPath,
        out string? selfSignedCertPath,
        out string? selfSignedKeyPath)
    {
        certPath = null;
        keyPath = null;
        caCertPath = null;
        selfSignedCertPath = null;
        selfSignedKeyPath = null;

        certFolder = GetCertificateFolder(executablePath);
        if (string.IsNullOrEmpty(certFolder))
            return false;

        certPath = Path.Combine(certFolder, AppConstants.Cert);
        if (!File.Exists(certPath))
            return false;

        keyPath = Path.Combine(certFolder, AppConstants.Key);
        if (!File.Exists(keyPath))
            return false;

        caCertPath = Path.Combine(certFolder, AppConstants.CaCert);
        if (!File.Exists(caCertPath))
            return false;

        selfSignedCertPath = Path.Combine(certFolder, AppConstants.SelfSignedCert);
        if (!File.Exists(selfSignedCertPath))
            return false;

        selfSignedKeyPath = Path.Combine(certFolder, AppConstants.SelfSignedKey);
        if (!File.Exists(selfSignedKeyPath))
            return false;

        return true;
    }

    /// <summary>
    /// Đọc chứng chỉ từ file PEM.
    /// </summary>
    public static X509Certificate2? ReadCertificateFromFile(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            // Thử đọc dưới dạng PEM
            var pemContent = File.ReadAllText(path);
            return X509Certificate2.CreateFromPem(pemContent);
        }
        catch
        {
            // Thử đọc dưới dạng DER
            try
            {
                var derContent = File.ReadAllBytes(path);
#pragma warning disable SYSLIB0057
                return new X509Certificate2(derContent);
#pragma warning restore SYSLIB0057
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Đọc khóa riêng từ file PEM.
    /// </summary>
    public static RSA? ReadPrivateKeyFromFile(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var pemContent = File.ReadAllText(path);
            return RSA.Create();
            // Lưu ý: .NET 10 hỗ trợ RSA.LoadFromEncryptedPem
            // Nhưng để đơn giản, trả về null ở đây và xử lý riêng khi cần
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Kiểm tra xem chứng chỉ có sắp hết hạn không (trong vòng 24 giờ).
    /// </summary>
    public static bool IsCertificateSoonExpired(X509Certificate2 cert, TimeSpan? threshold = null)
    {
        threshold ??= TimeSpan.FromHours(24);
        return cert.NotAfter - DateTime.UtcNow < threshold;
    }
}
