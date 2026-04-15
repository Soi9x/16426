using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace AgeLanServer.Common;

/// <summary>
/// Tiện ích chung cho toàn bộ hệ thống.
/// </summary>
public static class CommonUtilities
{
    /// <summary>
    /// User-Agent cho các request HTTP.
    /// </summary>
    public static string UserAgent() => $"{AppConstants.Name}/1.0";

    /// <summary>
    /// Chuyển đổi danh sách string thành IPAddress.
    /// </summary>
    public static List<IPAddress> StringSliceToNetIPSlice(List<string> strings)
    {
        var result = new List<IPAddress>();
        foreach (var s in strings)
        {
            if (IPAddress.TryParse(s, out var ip))
                result.Add(ip);
        }
        return result;
    }

    /// <summary>
    /// Chuyển hostname hoặc IP thành danh sách IP.
    /// </summary>
    public static List<string> HostOrIpToIps(string hostOrIp)
    {
        if (IPAddress.TryParse(hostOrIp, out _))
            return new List<string> { hostOrIp };

        try
        {
            var entries = Dns.GetHostEntry(hostOrIp);
            var result = new List<string>();
            foreach (var addr in entries.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    result.Add(addr.ToString());
            }
            return result;
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Lấy tất cả địa chỉ IPv4 cục bộ.
    /// </summary>
    public static List<IPAddress> GetLocalIPv4Addresses()
    {
        var result = new List<IPAddress>();
        foreach (var iface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                iface.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
            {
                foreach (var ua in iface.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        result.Add(ua.Address);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Kiểm tra IP có khớp với host/domain không.
    /// </summary>
    public static bool Matches(string ip, string host)
    {
        try
        {
            var ips = HostOrIpToIps(host);
            return ips.Contains(ip);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Xóa cache (DNS, v.v.).
    /// </summary>
    public static void ClearCache()
    {
        // Trong .NET, DNS cache được quản lý bởi OS
        // Ta có thể gọi Dns.GetHostEntry để làm mới
    }

    /// <summary>
    /// Lưu ánh xạ vào cache DNS.
    /// </summary>
    public static void CacheMapping(string host, string ip)
    {
        // Ủy quyền cho DnsResolver quản lý DNS cache
        DnsResolver.CacheMapping(host, ip);
    }

    /// <summary>
    /// Phân tích đường dẫn file thực thi.
    /// </summary>
    public static (FileInfo? file, string resolvedPath) ParsePath(List<string> pathParts, string? workingDir)
    {
        var path = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts);
        if (Path.IsPathRooted(path))
        {
            var fi = new FileInfo(path);
            return (fi.Exists ? fi : null, path);
        }

        var baseDir = workingDir ?? Directory.GetCurrentDirectory();
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, path));
        var fullFi = new FileInfo(fullPath);
        return (fullFi.Exists ? fullFi : null, fullPath);
    }

    /// <summary>
    /// Chuyển đổi string sang List<string> (tương thích với EnhancedViper).
    /// </summary>
    public static List<string> EnhancedViperStringToStringSlice(string value)
    {
        return new List<string> { value };
    }

    /// <summary>
    /// Kiểm tra cặp certificate tồn tại không.
    /// </summary>
    public static (bool exists, string? certFolder, string? cert, string? key,
        string? caCert, string? selfSignedCert, string? selfSignedKey)
        CertificatePairs(string serverExecutablePath)
    {
        var exeDir = Path.GetDirectoryName(serverExecutablePath);
        if (string.IsNullOrEmpty(exeDir))
            return (false, null, null, null, null, null, null);

        var certFolder = Path.Combine(exeDir, "resources", "certificates");
        var cert = Path.Combine(certFolder, AppConstants.Cert);
        var key = Path.Combine(certFolder, AppConstants.Key);
        var caCert = Path.Combine(certFolder, AppConstants.CaCert);
        var selfSignedCert = Path.Combine(certFolder, AppConstants.SelfSignedCert);
        var selfSignedKey = Path.Combine(certFolder, AppConstants.SelfSignedKey);

        var exists = File.Exists(cert) && File.Exists(key);

        return (exists, certFolder,
            File.Exists(cert) ? cert : null,
            File.Exists(key) ? key : null,
            File.Exists(caCert) ? caCert : null,
            File.Exists(selfSignedCert) ? selfSignedCert : null,
            File.Exists(selfSignedKey) ? selfSignedKey : null);
    }
}
