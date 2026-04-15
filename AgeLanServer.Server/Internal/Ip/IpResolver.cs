// Port từ server/internal/ip/ip.go
/// Tiện ích phân giải host thành địa chỉ IP.

using System.Net;
using System.Net.Sockets;
using AgeLanServer.Common;

namespace AgeLanServer.Server.Internal.Ip;

/// <summary>
/// Phân giải danh sách host thành tập địa chỉ IPv4.
/// Tương đương ResolveHosts trong Go.
/// </summary>
public static class HostResolver
{
    /// <summary>
    /// Phân giải danh sách host (tên hoặc IP) thành tập địa chỉ IPv4.
    /// Nếu host là IP hợp lệ thì thêm trực tiếp.
    /// Nếu không, phân giải DNS.
    /// </summary>
    /// <param name="hosts">Tập các host (tên miền hoặc IP).</param>
    /// <returns>Tập địa chỉ IPv4 đã phân giải.</returns>
    public static HashSet<IPAddress> ResolveHosts(IEnumerable<string> hosts)
    {
        var ipAddrs = new HashSet<IPAddress>();

        foreach (var host in hosts)
        {
            // Thử phân tích như IP trực tiếp
            if (IPAddress.TryParse(host, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
            {
                ipAddrs.Add(ip);
            }
            else
            {
                // Phân giải DNS
                var resolvedIps = Dns.GetHostAddressesAsync(host).Result.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                foreach (var resolvedIp in resolvedIps)
                {
                    ipAddrs.Add(resolvedIp);
                }
            }
        }

        return ipAddrs;
    }
}
