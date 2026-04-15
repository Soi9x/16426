using System.Security.Cryptography.X509Certificates;
using AgeLanServer.LauncherCommon;

namespace AgeLanServer.LauncherConfigAdmin;

/// <summary>
/// Quản lý chứng chỉ: tin cậy / bỏ tin cậy trong kho Local Machine ROOT (Windows)
/// hoặc hệ thống CA trust (Linux).
/// </summary>
public static class CertificateManager
{
    /// <summary>
    /// Thêm chứng chỉ vào kho Local Machine ROOT trên Windows,
    /// hoặc vào hệ thống CA trust trên Linux.
    /// </summary>
    /// <param name="certDataBase64">Chứng chỉ định dạng PEM encoded base64.</param>
    public static void TrustCertificate(string certDataBase64)
    {
        if (OperatingSystem.IsWindows())
        {
            TrustCertificateWindows(certDataBase64);
        }
        else if (OperatingSystem.IsLinux())
        {
            TrustCertificateLinux(certDataBase64);
        }
        else
        {
            throw new PlatformNotSupportedException(
                "Chỉ hỗ trợ Windows và Linux.");
        }
    }

    /// <summary>
    /// Xóa chứng chỉ khỏi kho Local Machine ROOT trên Windows
    /// hoặc hệ thống CA trust trên Linux, dựa trên subject organization.
    /// </summary>
    /// <param name="certDataBase64">
    /// Chứng chỉ gốc (dùng để xác định subject cần xóa).
    /// </param>
    public static void UntrustCertificate(string certDataBase64)
    {
        if (OperatingSystem.IsWindows())
        {
            UntrustCertificateWindows(certDataBase64);
        }
        else if (OperatingSystem.IsLinux())
        {
            UntrustCertificateLinux(certDataBase64);
        }
        else
        {
            throw new PlatformNotSupportedException(
                "Chỉ hỗ trợ Windows và Linux.");
        }
    }

    #region Windows

    /// <summary>
    /// Thêm chứng chỉ vào kho LocalMachine\ROOT trên Windows.
    /// </summary>
    private static void TrustCertificateWindows(string certDataBase64)
    {
        var certData = Convert.FromBase64String(certDataBase64);
#pragma warning disable SYSLIB0057
        using var cert = new X509Certificate2(certData);
#pragma warning restore SYSLIB0057

        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        store.Add(cert);
        store.Close();

        Console.WriteLine(
            $"[CERT] Da them chung chi vao LocalMachine\\ROOT: {cert.Subject}");
    }

    /// <summary>
    /// Xóa chứng chỉ khỏi kho LocalMachine\ROOT trên Windows theo subject.
    /// </summary>
    private static void UntrustCertificateWindows(string certDataBase64)
    {
        var certData = Convert.FromBase64String(certDataBase64);
#pragma warning disable SYSLIB0057
        using var cert = new X509Certificate2(certData);
#pragma warning restore SYSLIB0057

        var targetSubject = cert.Subject;

        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);

        var certsToRemove = store.Certificates
            .Cast<X509Certificate2>()
            .Where(c => c.Subject == targetSubject)
            .ToList();

        int removed = 0;
        foreach (var c in certsToRemove)
        {
            store.Remove(c);
            removed++;
        }

        store.Close();

        if (removed > 0)
        {
            Console.WriteLine(
                $"[CERT] Da xoa {removed} chung chi co subject: {targetSubject}");
        }
        else
        {
            Console.WriteLine(
                $"[CERT] Khong tim thay chung chi nao co subject: {targetSubject}");
        }
    }

    #endregion

    #region Linux

    /// <summary>
    /// Thêm chứng chỉ vào hệ thống CA trust trên Linux.
    /// Thử cả hai đường dẫn phổ biến.
    /// </summary>
    private static void TrustCertificateLinux(string certDataBase64)
    {
        var certData = Convert.FromBase64String(certDataBase64);

        // Các đường dẫn CA trust phổ biến trên Linux
        var targetPaths = new[]
        {
            "/usr/local/share/ca-certificates/ageLANServer.crt",
            "/etc/pki/ca-trust/source/anchors/ageLANServer.crt"
        };

        bool written = false;
        foreach (var targetPath in targetPaths)
        {
            try
            {
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(targetPath, certData);
                Console.WriteLine($"[CERT] Da ghi chung chi vao: {targetPath}");
                written = true;
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[CERT] Khong the ghi vao {targetPath}: {ex.Message}");
            }
        }

        if (!written)
        {
            throw new IOException(
                "Khong the ghi chung chỉ vao bat ky duong dan CA trust nao tren Linux.");
        }

        // Cập nhật hệ thống CA
        RunCommand("update-ca-certificates");
        RunCommand("update-ca-trust");
    }

    /// <summary>
    /// Xóa chứng chỉ khỏi hệ thống CA trust trên Linux.
    /// </summary>
    private static void UntrustCertificateLinux(string certDataBase64)
    {
        var targetPaths = new[]
        {
            "/usr/local/share/ca-certificates/ageLANServer.crt",
            "/etc/pki/ca-trust/source/anchors/ageLANServer.crt"
        };

        foreach (var targetPath in targetPaths)
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
                Console.WriteLine($"[CERT] Da xoa chung chi: {targetPath}");
            }
        }

        // Cập nhật hệ thống CA
        RunCommand("update-ca-certificates");
        RunCommand("update-ca-trust");
    }

    #endregion

    /// <summary>
    /// Chạy lệnh hệ thống (không shell).
    /// </summary>
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
        catch (Exception ex)
        {
            Console.WriteLine($"[CERT] Khong the chay lenh '{command}': {ex.Message}");
        }
    }
}
