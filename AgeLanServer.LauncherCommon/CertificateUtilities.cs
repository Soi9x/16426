using System.Security.Cryptography.X509Certificates;

namespace AgeLanServer.LauncherCommon;

/// <summary>
/// Tiện ích quản lý chứng chỉ: đọc, ghi PEM, thêm/xóa khỏi kho tin cậy hệ thống.
/// Tương đương launcher-common/cert/ trong bản Go gốc.
/// </summary>
public static class CertificateUtilities
{
    /// <summary>
    /// Đọc chứng chỉ từ file PEM.
    /// </summary>
    public static X509Certificate2? ReadCertificatePem(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var pemContent = File.ReadAllText(path);
            return X509Certificate2.CreateFromPem(pemContent);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Ghi chứng chỉ ra file PEM.
    /// </summary>
    public static void WriteCertificatePem(X509Certificate2 cert, string path)
    {
        var pemData = cert.ExportCertificatePem();
        File.WriteAllText(path, pemData);
    }

    /// <summary>
    /// Đọc và tính SHA256 fingerprint của chứng chỉ.
    /// </summary>
    public static string? GetCertificateFingerprint(string path)
    {
        var cert = ReadCertificatePem(path);
        return cert?.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256);
    }

    /// <summary>
    /// Thêm chứng chỉ vào kho tin cậy local machine (ROOT store) trên Windows.
    /// Trên Linux, thêm vào hệ thống CA trust.
    /// </summary>
    public static async Task TrustLocalCertificateAsync(string certDataBase64, CancellationToken ct = default)
    {
        if (OperatingSystem.IsWindows())
        {
            await Task.Run(() =>
            {
                var certData = Convert.FromBase64String(certDataBase64);
#pragma warning disable SYSLIB0057
                using var cert = new X509Certificate2(certData);
#pragma warning restore SYSLIB0057

                using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(cert);
                store.Close();
            }, ct);
        }
        else if (OperatingSystem.IsLinux())
        {
            await TrustCertificateLinuxAsync(certDataBase64, ct);
        }
    }

    /// <summary>
    /// Xóa chứng chỉ khỏi kho tin cậy local machine dựa trên subject organization.
    /// </summary>
    public static async Task UntrustLocalCertificateAsync(string certDataBase64, CancellationToken ct = default)
    {
        if (OperatingSystem.IsWindows())
        {
            await Task.Run(() =>
            {
                var certData = Convert.FromBase64String(certDataBase64);
#pragma warning disable SYSLIB0057
                using var cert = new X509Certificate2(certData);
#pragma warning restore SYSLIB0057

                using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);

                // Xóa theo subject organization
                var targetOrg = cert.Subject;
                var certsToRemove = store.Certificates
                    .Cast<X509Certificate2>()
                    .Where(c => c.Subject == targetOrg)
                    .ToList();

                foreach (var c in certsToRemove)
                    store.Remove(c);

                store.Close();
            }, ct);
        }
        else if (OperatingSystem.IsLinux())
        {
            await UntrustCertificateLinuxAsync(certDataBase64, ct);
        }
    }

    /// <summary>
    /// Liệt kê chứng chỉ trong kho tin cậy local machine.
    /// </summary>
    public static List<X509Certificate2> EnumLocalCertificates()
    {
        if (OperatingSystem.IsWindows())
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Cast<X509Certificate2>().ToList();
            store.Close();
            return certs;
        }

        // Linux: đọc từ ca-bundle
        var linuxCerts = new List<X509Certificate2>();
        var caBundlePath = "/etc/ssl/certs/ca-certificates.crt";
        if (File.Exists(caBundlePath))
        {
            try
            {
                var content = File.ReadAllText(caBundlePath);
                // Parse PEM certificates (đơn giản hóa)
                var parts = content.Split("-----END CERTIFICATE-----", StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var pem = part + "-----END CERTIFICATE-----";
                    try
                    {
                        linuxCerts.Add(X509Certificate2.CreateFromPem(pem));
                    }
                    catch { }
                }
            }
            catch { }
        }

        return linuxCerts;
    }

    private static async Task TrustCertificateLinuxAsync(string certDataBase64, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            var certData = Convert.FromBase64String(certDataBase64);
            var certPem = Convert.ToBase64String(certData); // Đơn giản hóa

            // Thử cập nhật CA trust
            var targetPaths = new[]
            {
                "/usr/local/share/ca-certificates/ageLANServer.crt",
                "/etc/pki/ca-trust/source/anchors/ageLANServer.crt"
            };

            foreach (var targetPath in targetPaths)
            {
                try
                {
                    var dir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllBytes(targetPath, certData);
                    break;
                }
                catch
                {
                    // Thử đường dẫn tiếp theo
                }
            }

            // Chạy cập nhật CA
            RunCommand("update-ca-certificates");
            RunCommand("update-ca-trust");
        }, ct);
    }

    private static async Task UntrustCertificateLinuxAsync(string certDataBase64, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            var targetPaths = new[]
            {
                "/usr/local/share/ca-certificates/ageLANServer.crt",
                "/etc/pki/ca-trust/source/anchors/ageLANServer.crt"
            };

            foreach (var targetPath in targetPaths)
            {
                if (File.Exists(targetPath))
                    File.Delete(targetPath);
            }

            RunCommand("update-ca-certificates");
            RunCommand("update-ca-trust");
        }, ct);
    }

    private static void RunCommand(string command)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(5000);
        }
        catch
        {
            // Bỏ qua lỗi
        }
    }
}
