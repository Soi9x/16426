using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using AgeLanServer.Common;

namespace AgeLanServer.BattleServerBroadcast;

/// <summary>
/// Phát sóng thông báo UDP để khám phá battle server trên mạng LAN.
/// Tương đương battle-server-broadcast/ trong bản Go gốc.
/// </summary>
public static class BattleServerBroadcaster
{
    // Header bytes: 0x21, 0x24, 0x00
    private static readonly byte[] HeaderBytes = { 0x21, 0x24, 0x00 };

    /// <summary>
    /// Lấy địa chỉ IP interface phù hợp để làm battle server broadcast.
    /// Trả về (địa chỉ ưu tiên, danh sách các địa chỉ còn lại).
    /// Tương đương RetrieveBsInterfaceAddresses trong Go.
    /// </summary>
    public static async Task<(IPAddress? MostPriority, List<IPAddress> RestInterfaces)> RetrieveBsInterfaceAddressesAsync(CancellationToken ct = default)
    {
        IPAddress? mostPriority = null;
        var restInterfaces = new List<IPAddress>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var iface in interfaces)
            {
                // Chỉ chọn interface đang hoạt động, hỗ trợ IPv4, không phải loopback
                if (iface.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                if (!iface.Supports(NetworkInterfaceComponent.IPv4))
                    continue;

                var props = iface.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    // Interface ưu tiên đầu tiên gặp được
                    if (mostPriority == null)
                    {
                        mostPriority = addr.Address;
                    }
                    else
                    {
                        restInterfaces.Add(addr.Address);
                    }
                }
            }
        }
        catch
        {
            // Bỏ qua lỗi, trả về kết quả hiện có
        }

        return (mostPriority, restInterfaces);
    }

    /// <summary>
    /// Tính địa chỉ broadcast từ IP và subnet mask.
    /// </summary>
    private static IPAddress CalculateBroadcastIp(IPAddress ip, IPAddress mask)
    {
        var ipBytes = ip.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var broadcast = new byte[ipBytes.Length];

        for (var i = 0; i < ipBytes.Length; i++)
        {
            broadcast[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
        }

        return new IPAddress(broadcast);
    }

    /// <summary>
    /// Nhận thông báo announce từ interface chính và phát lại tới các interface còn lại.
    /// Tương đương CloneAnnouncements trong Go.
    /// Chạy liên tục cho đến khi cancellation.
    /// </summary>
    public static async Task CloneAnnouncementsAsync(
        IPAddress mostPriority,
        List<IPAddress> restInterfaces,
        int port,
        CancellationToken ct = default)
    {
        if (restInterfaces.Count == 0)
            return;

        // Bind UDP listener trên interface chính
        using var conn = new UdpClient();
        conn.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        conn.Client.Bind(new IPEndPoint(mostPriority, port));

        // Tạo các UDP connection tới broadcast addresses của các interface còn lại
        var targets = new List<UdpClient>();

        try
        {
            // Lấy subnet mask từ interface tương ứng
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            var restMaskMap = new Dictionary<string, IPAddress>();

            foreach (var iface in interfaces)
            {
                if (iface.OperationalStatus != OperationalStatus.Up)
                    continue;

                var props = iface.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        restInterfaces.Contains(addr.Address))
                    {
                        restMaskMap[addr.Address.ToString()] = addr.IPv4Mask;
                    }
                }
            }

            foreach (var restIp in restInterfaces)
            {
                try
                {
                    var mask = restMaskMap.GetValueOrDefault(restIp.ToString());
                    if (mask == null) continue;

                    var broadcastIp = CalculateBroadcastIp(restIp, mask);
                    var target = new UdpClient();
                    target.Connect(new IPEndPoint(broadcastIp, port));
                    targets.Add(target);
                }
                catch
                {
                    // Bỏ qua interface không kết nối được
                }
            }

            if (targets.Count == 0)
                return;

            // Vòng lặp nhận và phát lại thông báo
            var buffer = new byte[65535];
            const int minimumSize = 3 + 36 + 2 + 1 + 3 * 2; // header + guid + uint16 + 1 + 3*uint16

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await conn.ReceiveAsync(ct);
                    var data = result.Buffer;

                    // Kiểm tra kích thước tối thiểu và header
                    if (data.Length < minimumSize)
                        continue;
                    if (data.Length < 3 || data[0] != HeaderBytes[0] || data[1] != HeaderBytes[1] || data[2] != HeaderBytes[2])
                        continue;

                    // Phát lại tới tất cả targets
                    foreach (var target in targets)
                    {
                        try
                        {
                            await target.SendAsync(data, ct);
                        }
                        catch
                        {
                            // Bỏ qua lỗi gửi riêng lẻ
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Bỏ qua lỗi đọc
                }
            }
        }
        finally
        {
            foreach (var target in targets)
            {
                try { target.Close(); } catch { }
                target.Dispose();
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem có cần broadcast battle server không (Windows, không phải AoE4/AoM).
    /// </summary>
    public static bool IsRequired(string gameId)
    {
        // Chỉ cần trên Windows và không phải AoE4/AoM
        return OperatingSystem.IsWindows() &&
               gameId != GameIds.AgeOfEmpires4 &&
               gameId != GameIds.AgeOfMythology;
    }
}
