using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AgeLanServer.Common;

/// <summary>
/// Cung cấp các phương thức phân giải DNS, ánh xạ IP-Host và bộ nhớ đệm.
/// Tương đương package common/resolve.go trong bản Go gốc.
/// </summary>
public static class DnsResolver
{
    // Danh sách DNS servers: Google, Cloudflare, OpenDNS (primary + secondary)
    private static readonly string[] DnsServers =
    {
        "8.8.8.8", "1.1.1.1", "208.67.222.222",
        "8.8.4.4", "1.0.0.1", "208.67.220.220"
    };

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    private static readonly ConcurrentDictionary<string, DateTimeOffset> FailedIpToHosts = new();
    private static readonly ConcurrentDictionary<string, DateTimeOffset> FailedHostToIps = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> IpToHosts = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> HostToIps = new();

    /// <summary>
    /// Phân giải tên miền sang địa chỉ IPv4 bằng cách thử nhiều DNS server.
    /// Trả về null nếu không tìm thấy.
    /// </summary>
    public static async Task<string?> ResolveHostToIpAsync(string host, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        try
        {
            var results = await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, cts.Token);
            if (results.Length > 0)
                return results[0].ToString();
        }
        catch
        {
            // Fallback: thử từng DNS server riêng
        }

        // Thử từng DNS server nếu fallback không thành
        foreach (var dnsServer in DnsServers)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(dnsServer, 53, cts.Token).AsTask();
                if (await Task.WhenAny(connectTask, Task.Delay(1000, cts.Token)) == connectTask)
                {
                    client.Close();
                    // Nếu kết nối được, thử dùng DNS mặc định của hệ thống
                    var ips = await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, cts.Token);
                    if (ips.Length > 0)
                        return ips[0].ToString();
                }
                client.Close();
            }
            catch
            {
                // Thử server tiếp theo
            }
        }

        return null;
    }

    /// <summary>
    /// Phân giải ngược từ IP sang tên miền (reverse DNS).
    /// </summary>
    public static async Task<string[]?> ReverseDnsAsync(string ip, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(1));
            var hostEntry = await Dns.GetHostEntryAsync(ip, AddressFamily.InterNetwork, cts.Token);
            return new[] { hostEntry.HostName };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Kiểm tra kết nối DNS bằng cách thử kết nối TCP tới port 53.
    /// </summary>
    public static async Task<bool> CheckDnsConnectivityAsync(CancellationToken ct = default)
    {
        foreach (var dnsServer in DnsServers)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(1));
                using var client = new TcpClient();
                var task = client.ConnectAsync(dnsServer, 53, cts.Token).AsTask();
                if (await Task.WhenAny(task, Task.Delay(1000, cts.Token)) == task)
                {
                    client.Close();
                    return true;
                }
                client.Close();
            }
            catch
            {
                // Thử server tiếp theo
            }
        }
        return false;
    }

    /// <summary>
    /// Lưu ánh xạ host-IP vào bộ nhớ đệm.
    /// </summary>
    public static void CacheMapping(string host, string ip)
    {
        var hostLower = host.ToLowerInvariant();

        HostToIps.AddOrUpdate(
            hostLower,
            _ => new HashSet<string> { ip },
            (_, set) => { set.Add(ip); return set; });

        IpToHosts.AddOrUpdate(
            ip,
            _ => new HashSet<string> { hostLower },
            (_, set) => { set.Add(hostLower); return set; });

        FailedIpToHosts.TryRemove(ip, out _);
        FailedHostToIps.TryRemove(hostLower, out _);
    }

    /// <summary>
    /// Xóa toàn bộ bộ nhớ đệm DNS.
    /// </summary>
    public static void ClearCache()
    {
        FailedIpToHosts.Clear();
        FailedHostToIps.Clear();
        IpToHosts.Clear();
        HostToIps.Clear();
    }

    /// <summary>
    /// Phân giải host hoặc IP thành danh sách địa chỉ IP.
    /// Nếu là IP thì trả về chính nó; nếu là hostname thì tra DNS + cache.
    /// </summary>
    public static async Task<string[]> ResolveHostOrIpToIpsAsync(string host, CancellationToken ct = default)
    {
        // Nếu đã là địa chỉ IP
        if (IPAddress.TryParse(host, out var ip))
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                if (IPAddress.Any.Equals(ip))
                    return await GetActiveInterfaceIpsAsync(ct);
                return new[] { ip.ToString() };
            }
            return Array.Empty<string>();
        }

        // Kiểm tra cache
        var hostLower = host.ToLowerInvariant();
        if (HostToIps.TryGetValue(hostLower, out var cachedIps))
            return cachedIps.ToArray();

        if (FailedHostToIps.TryGetValue(hostLower, out var failedTime) &&
            DateTimeOffset.UtcNow - failedTime < CacheTtl)
            return Array.Empty<string>();

        // Tra DNS
        var resolvedIp = await ResolveHostToIpAsync(host, ct);
        if (resolvedIp != null)
        {
            CacheMapping(host, resolvedIp);
            return new[] { resolvedIp };
        }

        // Lưu vào cache failed
        FailedHostToIps[hostLower] = DateTimeOffset.UtcNow;
        return Array.Empty<string>();
    }

    /// <summary>
    /// Kiểm tra xem hai địa chỉ (host hoặc IP) có trùng nhau không.
    /// </summary>
    public static async Task<bool> MatchesAsync(string addr1, string addr2, CancellationToken ct = default)
    {
        var ips1 = await ResolveHostOrIpToIpsAsync(addr1, ct);
        var ips2 = await ResolveHostOrIpToIpsAsync(addr2, ct);
        return ips1.Intersect(ips2).Any();
    }

    /// <summary>
    /// Lấy danh sách IP từ reverse DNS lookup.
    /// </summary>
    public static async Task<HashSet<string>> GetHostsFromIpAsync(string ip, CancellationToken ct = default)
    {
        if (IpToHosts.TryGetValue(ip, out var cached))
            return new HashSet<string>(cached);

        var hosts = new HashSet<string>();
        var names = await ReverseDnsAsync(ip, ct);
        if (names != null)
        {
            foreach (var name in names)
            {
                hosts.Add(name);
                CacheMapping(name, ip);
            }
        }
        return hosts;
    }

    /// <summary>
    /// Lấy tất cả IPv4 của các network interface đang hoạt động.
    /// </summary>
    public static async Task<string[]> GetActiveInterfaceIpsAsync(CancellationToken ct = default)
    {
        var ips = new List<string>();
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (var ni in interfaces)
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            var props = ni.GetIPProperties();
            foreach (var ua in props.UnicastAddresses)
            {
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    ips.Add(ua.Address.ToString());
                }
            }
        }

        return ips.ToArray();
    }
}
