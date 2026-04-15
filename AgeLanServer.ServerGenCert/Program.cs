using System.CommandLine;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AgeLanServer.Common;

namespace AgeLanServer.ServerGenCert;

/// <summary>
/// Trình tạo chứng chỉ tự ký cho server.
/// Tương đương server-genCert/ trong bản Go gốc.
/// </summary>
public static class CertificateGenerator
{
    /// <summary>
    /// Tạo cặp chứng chỉ self-signed (cert + key).
    /// </summary>
    public static void GenerateSelfSignedCertificates(string outputFolder, bool replace = false)
    {
        Directory.CreateDirectory(outputFolder);

        var certPath = Path.Combine(outputFolder, AppConstants.SelfSignedCert);
        var keyPath = Path.Combine(outputFolder, AppConstants.SelfSignedKey);

        if (!replace && File.Exists(certPath) && File.Exists(keyPath))
        {
            Console.WriteLine($"Chứng chỉ self-signed đã tồn tại. Dùng -r để thay thế.");
            return;
        }

        Console.WriteLine("Đang tạo chứng chỉ self-signed...");

        var (certPem, keyPem) = CreateSelfSignedCertPair(outputFolder);

        File.WriteAllText(certPath, certPem);
        File.WriteAllText(keyPath, keyPem);

        Console.WriteLine($"Đã tạo: {certPath}");
        Console.WriteLine($"Đã tạo: {keyPath}");
    }

    /// <summary>
    /// Tạo cặp chứng chỉ CA + leaf cert (cert.pem, key.pem, cacert.pem).
    /// </summary>
    public static void GenerateFullCertificatePair(string outputFolder, bool replace = false)
    {
        Directory.CreateDirectory(outputFolder);

        var certPath = Path.Combine(outputFolder, AppConstants.Cert);
        var keyPath = Path.Combine(outputFolder, AppConstants.Key);
        var caCertPath = Path.Combine(outputFolder, AppConstants.CaCert);

        if (!replace && File.Exists(certPath) && File.Exists(keyPath) && File.Exists(caCertPath))
        {
            Console.WriteLine($"Chứng chỉ đã tồn tại. Dùng -r để thay thế.");
            return;
        }

        Console.WriteLine("Đang tạo cặp chứng chỉ CA + leaf...");

        // Tạo CA certificate + giữ lại private key để ký leaf cert
        var (caCertPem, caKeyPem) = CreateCaCertificate();

        // Tạo leaf cert ký bởi CA (dùng cả cert + key của CA)
        var (leafCertPem, leafKeyPem) = CreateLeafCertificateSignedByCa(caCertPem, caKeyPem, outputFolder);

        File.WriteAllText(caCertPath, caCertPem);
        File.WriteAllText(certPath, leafCertPem);
        File.WriteAllText(keyPath, leafKeyPem);

        // Cũng tạo self-signed cert
        var (selfSignedCertPem, selfSignedKeyPem) = CreateSelfSignedCertPair(outputFolder);
        var selfSignedCertPath = Path.Combine(outputFolder, AppConstants.SelfSignedCert);
        var selfSignedKeyPath = Path.Combine(outputFolder, AppConstants.SelfSignedKey);
        File.WriteAllText(selfSignedCertPath, selfSignedCertPem);
        File.WriteAllText(selfSignedKeyPath, selfSignedKeyPem);

        Console.WriteLine($"Đã tạo: {caCertPath}");
        Console.WriteLine($"Đã tạo: {certPath}");
        Console.WriteLine($"Đã tạo: {keyPath}");
    }

    /// <summary>
    /// Tạo chứng chỉ self-signed với các domain cần thiết.
    /// </summary>
    private static (string certPem, string keyPem) CreateSelfSignedCertPair(string outputFolder)
    {
        using var rsa = RSA.Create(2048);

        var subjectName = new X500DistinguishedName(
            $"CN={AppConstants.Name}, O={AppConstants.CertSubjectOrganization}");

        var sanBuilder = new SubjectAlternativeNameBuilder();

        var allDomains = new HashSet<string>(GameDomains.SelfSignedCertDomains);
        foreach (var gameId in GameIds.SupportedGames)
        {
            foreach (var host in GameDomains.GetAllHosts(gameId))
            {
                allDomains.Add(host);
                if (!host.Contains("*") && host.Contains("."))
                {
                    var parts = host.Split('.');
                    if (parts.Length >= 2)
                        allDomains.Add("*." + string.Join(".", parts.Skip(1)));
                }
            }
        }

        foreach (var domain in allDomains)
        {
            sanBuilder.AddDnsName(domain);
        }

        sanBuilder.AddDnsName("*." + GameDomains.PlayFabDomain);
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.Parse("192.168.1.4"));

        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Thêm các Extension chuẩn
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(sanBuilder.Build());
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication") }, true));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10));

        var certPem = cert.ExportCertificatePem();
        var keyPem = rsa.ExportRSAPrivateKeyPem();

        return (certPem, keyPem);
    }

    /// <summary>
    /// Tạo CA certificate. Trả về cả cert PEM và private key PEM.
    /// </summary>
    private static (string certPem, string keyPem) CreateCaCertificate()
    {
        using var rsa = RSA.Create(4096);
        var subjectName = new X500DistinguishedName(
            $"CN={AppConstants.Name} Root CA, O={AppConstants.CertSubjectOrganization}");

        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, true));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(20));

        var certPem = cert.ExportCertificatePem();
        var keyPem = rsa.ExportRSAPrivateKeyPem();
        return (certPem, keyPem);
    }

    /// <summary>
    /// Tạo leaf certificate được ký bởi CA.
    /// </summary>
    private static (string certPem, string keyPem) CreateLeafCertificateSignedByCa(string caCertPem, string caKeyPem, string outputFolder)
    {
        using var rsa = RSA.Create(2048);
        using var caCert = X509Certificate2.CreateFromPem(caCertPem, caKeyPem);

        var subjectName = new X500DistinguishedName(
            $"CN={AppConstants.Name} Server, O={AppConstants.CertSubjectOrganization}");

        var sanBuilder = new SubjectAlternativeNameBuilder();
        var allDomains = new HashSet<string>(GameDomains.SelfSignedCertDomains);
        foreach (var gameId in GameIds.SupportedGames)
        {
            foreach (var host in GameDomains.GetAllHosts(gameId))
            {
                allDomains.Add(host);
                if (!host.Contains("*") && host.Contains("."))
                {
                    var parts = host.Split('.');
                    if (parts.Length >= 2)
                        allDomains.Add("*." + string.Join(".", parts.Skip(1)));
                }
            }
        }

        foreach (var domain in allDomains)
            sanBuilder.AddDnsName(domain);

        sanBuilder.AddDnsName("*." + GameDomains.PlayFabDomain);
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.Parse("192.168.1.4"));

        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        // Thêm Authority Key Identifier (quan trọng để build chain)
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(caCert, false, false));
        request.CertificateExtensions.Add(sanBuilder.Build());
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication") }, true));

        byte[] serialNumber = new byte[8];
        RandomNumberGenerator.Fill(serialNumber);

        using var leafCert = request.Create(
            caCert,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10),
            serialNumber);

        using var certWithKey = leafCert.CopyWithPrivateKey(rsa);

        // XUẤT PFX CHUẨN (Bao gồm cả CA để Kestrel gửi cho trình duyệt)
        var pfxPath = Path.Combine(outputFolder, "server.pfx");
        var collection = new X509Certificate2Collection { certWithKey, caCert };
        var pfxData = collection.Export(X509ContentType.Pfx, "");
        if (pfxData != null)
        {
            File.WriteAllBytes(pfxPath, pfxData);
            Console.WriteLine($"Da tao: {pfxPath} (PFX bao gom chuoi tin cay)");
        }

        var certPem = certWithKey.ExportCertificatePem();
        var keyPem = rsa.ExportRSAPrivateKeyPem();

        return (certPem, keyPem);
    }
}

/// <summary>
/// Điểm vào chương trình.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // 1. Cấu hình Unicode cho console (khắc phục lỗi ký tự ?)
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        // 2. Kiểm tra quyền admin (khắc phục lỗi không lưu được file)
        if (OperatingSystem.IsWindows() && !CommandExecutor.IsRunningAsAdmin())
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Console.WriteLine("Lỗi: Không tìm thấy file thực thi để nâng quyền.");
                return ErrorCodes.General;
            }

            Console.WriteLine("Cần chạy với quyền Administrator để lưu chứng chỉ hệ thống. Đang nâng quyền...");
            
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

        CommandExecutor.ChangeWorkingDirectoryToExecutable();

        var replaceOption = new Option<bool>(
            aliases: new[] { "--replace", "-r" },
            description: "Thay thế chứng chỉ hiện có");

        var rootCommand = new RootCommand("Trình tạo chứng chỉ SSL cho Age LAN Server")
        {
            replaceOption
        };

        rootCommand.SetHandler((replace) =>
        {
            var exePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
            var certFolder = CertificateManager.GetCertificateFolder(exePath);

            if (string.IsNullOrEmpty(certFolder))
            {
                Console.Error.WriteLine("Không thể xác định thư mục chứng chỉ.");
                Environment.ExitCode = ErrorCodes.General;
                return;
            }

            CertificateGenerator.GenerateFullCertificatePair(certFolder, replace);
        }, replaceOption);

        return rootCommand.Invoke(args);
    }
}
