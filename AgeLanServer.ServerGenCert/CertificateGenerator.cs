using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AgeLanServer.Common;

namespace AgeLanServer.ServerGenCert;

/// <summary>
/// Lớp sinh và quản lý chứng chỉ SSL/TLS.
/// Chuyển thể từ ssl.go trong server-genCert/internal.
/// Lưu ý: Lớp này được đặt tên SslCertificateGenerator để tránh xung đột với
/// lớp CertificateGenerator đã có trong Program.cs.
/// </summary>
public static class SslCertificateGenerator
{
    /// <summary>
    /// Sinh chứng chỉ tự ký (self-signed) và lưu ra thư mục chỉ định.
    /// Tạo RSA 2048-bit, ghi selfsigned_cert.pem và selfsigned_key.pem.
    /// Trả về true nếu thành công, false nếu thất bại.
    /// </summary>
    /// <param name="folder">Thư mục lưu chứng chỉ.</param>
    /// <returns>True nếu sinh và lưu thành công, ngược lại false.</returns>
    public static bool GenerateSelfSignedCertificate(string folder)
    {
        // Tạo khóa RSA 2048-bit
        using var privateKey = RSA.Create(2048);

        // Lấy template chứng chỉ tự ký
        var request = GetTemplate("selfsigned");

        // Gán khóa thật vào request (thay thế khóa tạm)
        // Tạo chứng chỉ tự ký (self-signed: issuer == subject)
        using var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddYears(1));

        // Mã hóa chứng chỉ dưới dạng PEM
        var certPem = cert.ExportCertificatePem();

        // Mã hóa khóa riêng dưới dạng PEM (PKCS#1)
        var keyPem = ExportPkcs1PrivateKeyPem(privateKey);

        // Đường dẫn file đầu ra
        var certPath = Path.Combine(folder, AppConstants.SelfSignedCert);
        var keyPath = Path.Combine(folder, AppConstants.SelfSignedKey);

        // Cờ theo dõi để xóa file nếu có lỗi xảy ra
        var deleteCertFile = false;
        var deleteKeyFile = false;

        try
        {
            // Ghi file chứng chỉ
            File.WriteAllText(certPath, certPem);

            // Ghi file khóa riêng
            File.WriteAllText(keyPath, keyPem);

            return true;
        }
        catch
        {
            // Nếu ghi file khóa thất bại, xóa cả file chứng chỉ
            deleteCertFile = true;
            deleteKeyFile = true;
            return false;
        }
        finally
        {
            // Dọn dẹp file nếu có lỗi
            if (deleteCertFile && File.Exists(certPath))
            {
                File.Delete(certPath);
            }
            if (deleteKeyFile && File.Exists(keyPath))
            {
                File.Delete(keyPath);
            }
        }
    }

    /// <summary>
    /// Tạo template x509.Certificate dựa trên loại chứng chỉ.
    /// Các loại: "selfsigned", "ca", "normal".
    /// </summary>
    /// <param name="type">Loại chứng chỉ: "selfsigned", "ca", hoặc "normal".</param>
    /// <returns>CertificateRequest với các thuộc tính phù hợp.</returns>
    private static CertificateRequest GetTemplate(string type)
    {
        // Serial number dựa trên thời gian hiện tại (Unix timestamp)
        var serialNumber = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var serialBytes = BitConverter.GetBytes(serialNumber);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(serialBytes);
        }
        // Loại byte 0 ở đầu nếu có để đảm bảo số dương
        var serialList = serialBytes.ToList();
        while (serialList.Count > 0 && serialList[0] == 0)
        {
            serialList.RemoveAt(0);
        }
        if (serialList.Count == 0) serialList.Add(0);
        var serialBigInt = new BigInteger(serialList.ToArray());

        var subjectName = new X500DistinguishedName(
            $"CN={AppConstants.Name}, O={AppConstants.CertSubjectOrganization}");

        // Cài đặt cơ bản cho mọi loại
        var keyFlags = X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment;

        switch (type)
        {
            case "selfsigned":
            {
                // Chứng chỉ tự ký: là CA nhưng MaxPathLen = 0
                var request = new CertificateRequest(
                    new X500DistinguishedName(
                        $"CN={AppConstants.Name} Self-signed, O={AppConstants.CertSubjectOrganization}"),
                    RSA.Create(2048), // Khóa tạm, sẽ bị ghi đè
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                // Là CA nhưng không cho phép ký chứng chỉ con
                request.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(true, false, 0, true));

                // DNS names cho chứng chỉ tự ký
                request.CertificateExtensions.Add(
                    CreateSubjectAlternativeNameExtension(GameDomains.SelfSignedCertDomains));

                // Key usage
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(keyFlags, true));

                // Enhanced key usage: ServerAuth + ClientAuth
                var eku = new X509EnhancedKeyUsageExtension(
                    new OidCollection
                    {
                        new Oid("1.3.6.1.5.5.7.3.1"), // Server Authentication
                        new Oid("1.3.6.1.5.5.7.3.2"), // Client Authentication
                    },
                    true);
                request.CertificateExtensions.Add(eku);

                return request;
            }

            case "ca":
            {
                // Chứng chỉ CA: là CA thực sự
                var request = new CertificateRequest(
                    new X500DistinguishedName(
                        $"CN={AppConstants.Name} CA, O={AppConstants.CertSubjectOrganization}"),
                    RSA.Create(2048), // Khóa tạm, sẽ bị ghi đè
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                // Là CA, cho phép ký chứng chỉ con
                request.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(true, true, 0, true));

                // Không có DNS names cho CA
                // Key usage mặc định đã đủ cho CA

                return request;
            }

            default: // "normal"
            {
                // Chứng chỉ thường (leaf cert): không phải CA
                var request = new CertificateRequest(
                    subjectName,
                    RSA.Create(2048), // Khóa tạm, sẽ bị ghi đè
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                // Không phải CA
                request.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(false, false, 0, false));

                // DNS names từ cấu hình game
                request.CertificateExtensions.Add(
                    CreateSubjectAlternativeNameExtension(GameDomains.CertDomains()));

                // Key usage: KeyEncipherment + DigitalSignature
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(keyFlags, true));

                // Enhanced key usage: chỉ ServerAuth
                var eku = new X509EnhancedKeyUsageExtension(
                    new OidCollection
                    {
                        new Oid("1.3.6.1.5.5.7.3.1"), // Server Authentication
                    },
                    true);
                request.CertificateExtensions.Add(eku);

                return request;
            }
        }
    }

    /// <summary>
    /// Sinh cặp chứng chỉ: CA cert (không lưu file key, trả về buffer) hoặc
    /// leaf cert được ký bởi cha (parent cert + parent key).
    /// </summary>
    /// <param name="folder">Thư mục lưu chứng chỉ.</param>
    /// <param name="certName">Tên file chứng chỉ đầu ra.</param>
    /// <param name="keyName">Tên file khóa riêng đầu ra (rỗng nếu là CA).</param>
    /// <param name="parentCert">Chứng chỉ cha (null nếu tự sinh CA).</param>
    /// <param name="parentKey">Khóa riêng của cha (null nếu tự sinh CA).</param>
    /// <param name="caKeyBuffer">Buffer chứa khóa CA (chỉ có giá trị khi keyName rỗng).</param>
    /// <returns>True nếu thành công, ngược lại false.</returns>
    public static bool GenerateCertificatePair(
        string folder,
        string certName,
        string keyName,
        X509Certificate2? parentCert,
        RSA? parentKey,
        out byte[]? caKeyBuffer)
    {
        caKeyBuffer = null;

        // Tạo khóa RSA 2048-bit cho chứng chỉ mới
        using var key = RSA.Create(2048);

        // Xác định loại chứng chỉ
        var type = (parentCert == null && parentKey == null) ? "ca" : "normal";

        // Lấy template phù hợp
        var request = GetTemplate(type);

        CertificateRequest signingRequest;
        AsymmetricAlgorithm signingKey;

        if (type == "ca")
        {
            // Tự ký: dùng chính request làm issuer
            signingRequest = request;
            signingKey = key;
        }
        else
        {
            // Được ký bởi cha: dùng parent cert và parent key
            signingRequest = request;
            signingKey = parentKey!;
        }

        // Tạo chứng chỉ
        X509Certificate2 cert;

        if (type == "ca")
        {
            // Tự ký cho CA
            cert = signingRequest.CreateSelfSigned(
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddYears(1));
        }
        else
        {
            // Ký bởi cha
            cert = signingRequest.Create(
                parentCert!,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddYears(1),
                new byte[16]); // Serial number ngẫu nhiên
        }

        // Mã hóa chứng chỉ dưới dạng PEM
        var certPem = cert.ExportCertificatePem();

        // Mã hóa khóa riêng dưới dạng PEM (PKCS#1)
        var keyPem = ExportPkcs1PrivateKeyPem(key);

        var certPath = Path.Combine(folder, certName);
        var deleteCertFile = false;
        var deleteKeyFile = false;

        try
        {
            // Ghi file chứng chỉ
            File.WriteAllText(certPath, certPem);

            // Nếu có keyName, ghi file khóa riêng; ngược lại trả về buffer
            if (!string.IsNullOrEmpty(keyName))
            {
                var keyPath = Path.Combine(folder, keyName);
                File.WriteAllText(keyPath, keyPem);
            }
            else
            {
                // CA: trả về khóa dưới dạng buffer
                caKeyBuffer = key.ExportRSAPrivateKey();
            }

            return true;
        }
        catch
        {
            deleteCertFile = true;
            if (!string.IsNullOrEmpty(keyName))
            {
                deleteKeyFile = true;
            }
            return false;
        }
        finally
        {
            // Dọn dẹp file nếu có lỗi
            if (deleteCertFile && File.Exists(certPath))
            {
                File.Delete(certPath);
            }
            if (deleteKeyFile && !string.IsNullOrEmpty(keyName))
            {
                var keyPath = Path.Combine(folder, keyName);
                if (File.Exists(keyPath))
                {
                    File.Delete(keyPath);
                }
            }
        }
    }

    /// <summary>
    /// Điều phối toàn bộ quy trình sinh chứng chỉ:
    /// 1. Sinh CA cert (cacert.pem, không lưu key file).
    /// 2. Sinh leaf cert (cert.pem/key.pem) được ký bởi CA.
    /// 3. Sinh self-signed cert (selfsigned_cert.pem/selfsigned_key.pem).
    /// Nếu bất kỳ bước nào thất bại, sẽ dọn dẹp các file đã tạo.
    /// </summary>
    /// <param name="folder">Thư mục lưu chứng chỉ.</param>
    /// <returns>True nếu toàn bộ quy trình thành công, ngược lại false.</returns>
    public static bool GenerateCertificatePairs(string folder)
    {
        // Bước 1: Sinh CA certificate (không lưu file key)
        if (!GenerateCertificatePair(
                folder,
                AppConstants.CaCert,
                keyName: string.Empty,
                parentCert: null,
                parentKey: null,
                out var caKeyBytes))
        {
            return false;
        }

        var allSuccess = false;

        try
        {
            // Bước 2: Sinh leaf certificate được ký bởi CA
            // Đọc lại CA cert từ file để lấy đối tượng chứng chỉ
            var caCertPath = Path.Combine(folder, AppConstants.CaCert);
            var caCertPem = File.ReadAllText(caCertPath);
            using var caCert = X509Certificate2.CreateFromPem(caCertPem);

            // Tạo RSA từ buffer khóa CA
            using var caKey = RSA.Create();
            caKey.ImportRSAPrivateKey(caKeyBytes!, out _);

            if (!GenerateCertificatePair(
                    folder,
                    AppConstants.Cert,
                    AppConstants.Key,
                    caCert,
                    caKey,
                    out _))
            {
                // Nếu thất bại, xóa CA cert
                if (File.Exists(caCertPath))
                {
                    File.Delete(caCertPath);
                }
                return false;
            }

            // Bước 3: Sinh self-signed certificate
            if (!GenerateSelfSignedCertificate(folder))
            {
                // Nếu thất bại, xóa CA cert
                if (File.Exists(caCertPath))
                {
                    File.Delete(caCertPath);
                }
                return false;
            }

            allSuccess = true;
            return true;
        }
        finally
        {
            // Nếu không thành công, dọn dẹp các file đã tạo
            if (!allSuccess)
            {
                var certPath = Path.Combine(folder, AppConstants.Cert);
                var keyPath = Path.Combine(folder, AppConstants.Key);
                if (File.Exists(certPath)) File.Delete(certPath);
                if (File.Exists(keyPath)) File.Delete(keyPath);
            }
        }
    }

    /// <summary>
    /// Tạo extension Subject Alternative Name (SAN) với danh sách DNS names.
    /// </summary>
    /// <param name="dnsNames">Danh sách tên DNS cần thêm vào SAN.</param>
    /// <returns>X509SubjectAlternativeNameExtension.</returns>
    private static X509Extension CreateSubjectAlternativeNameExtension(string[] dnsNames)
    {
        var builder = new SubjectAlternativeNameBuilder();
        foreach (var dnsName in dnsNames)
        {
            builder.AddDnsName(dnsName);
        }
        return builder.Build();
    }

    /// <summary>
    /// Xuất khóa riêng RSA dưới dạng PEM encoding (PKCS#1 format).
    /// Tương đương x509.MarshalPKCS1PrivateKey trong Go.
    /// </summary>
    /// <param name="rsa">Đối tượng RSA cần xuất.</param>
    /// <returns>Chuỗi PEM chứa khóa riêng.</returns>
    private static string ExportPkcs1PrivateKeyPem(RSA rsa)
    {
        var pkcs1Bytes = rsa.ExportRSAPrivateKey();
        var base64 = Convert.ToBase64String(pkcs1Bytes);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("-----BEGIN RSA PRIVATE KEY-----");

        // Chia dòng mỗi 64 ký tự (chuẩn PEM)
        for (var i = 0; i < base64.Length; i += 64)
        {
            var lineLen = Math.Min(64, base64.Length - i);
            sb.AppendLine(base64.Substring(i, lineLen));
        }

        sb.AppendLine("-----END RSA PRIVATE KEY-----");
        return sb.ToString();
    }
}
