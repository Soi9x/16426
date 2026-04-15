// Port từ server/internal/ip/announce.go
/// Xử lý thông báo UDP multicast cho khám phá server.

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using AgeLanServer.Common;

namespace AgeLanServer.Server.Internal.Ip;

/// <summary>
/// Kết quả từ QueryConnections.
/// </summary>
public record QueryConnectionsResult
{
    /// <summary>Danh sách UDP connections đã tạo.</summary>
    public List<UdpClient> Connections { get; init; } = new();

    /// <summary>Lỗi (nếu có).</summary>
    public Exception? Error { get; init; }
}

/// <summary>
/// Xử lý thông báo UDP multicast.
/// Tương đương các hàm QueryConnections và ListenQueryConnections trong Go.
/// </summary>
public static class AnnounceHandler
{
    /// <summary>
    /// Tạo UDP connections cho thông báo multicast.
    /// </summary>
    /// <param name="ipAddr">Địa chỉ IP lắng nghe (IPAddress.Any cho tất cả).</param>
    /// <param name="multicastGroups">Tập các multicast group cần join.</param>
    /// <param name="port">Cổng lắng nghe.</param>
    /// <returns>Kết quả chứa các UDP connections.</returns>
    public static QueryConnectionsResult QueryConnections(
        IPAddress ipAddr,
        HashSet<IPAddress> multicastGroups,
        int port)
    {
        var result = new QueryConnectionsResult();
        var interfaces = GetRunningInterfaces();

        var hasUnspecified = ipAddr.Equals(IPAddress.Any);

        foreach (var (iface, nets) in interfaces)
        {
            // Bỏ qua interface không hỗ trợ multicast
            if ((iface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) ||
                !iface.Supports(System.Net.NetworkInformation.NetworkInterfaceComponent.IPv4))
            {
                continue;
            }

            // Kiểm tra xem interface có thuộc về IP cần lắng nghe không
            if (!hasUnspecified && !nets.Contains(ipAddr))
            {
                continue;
            }

            // Interface này sẽ được dùng
        }

        try
        {
            var endpoint = new IPEndPoint(ipAddr, port);
            var udpClient = new UdpClient(endpoint);

            // Join các multicast group
            foreach (var multicastGroup in multicastGroups)
            {
                try
                {
                    udpClient.JoinMulticastGroup(multicastGroup);
                }
                catch
                {
                    // Bỏ qua nếu không join được multicast group
                }
            }

            result.Connections.Add(udpClient);
        }
        catch (Exception ex)
        {
            return result with { Error = ex };
        }

        return result;
    }

    /// <summary>
    /// Lắng nghe và phản hồi các yêu cầu thông báo trên các connections.
    /// Chạy mỗi connection trong một task riêng.
    /// </summary>
    /// <param name="connections">Danh sách UDP connections.</param>
    /// <param name="serverId">ID của server.</param>
    /// <param name="cancellationToken">Token để dừng.</param>
    public static async Task ListenQueryConnectionsAsync(
        List<UdpClient> connections,
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var announceHeader = Encoding.UTF8.GetBytes(AppConstants.AnnounceHeader);
        var idBytes = serverId.ToByteArray();
        var responseData = new byte[announceHeader.Length + idBytes.Length];
        Buffer.BlockCopy(announceHeader, 0, responseData, 0, announceHeader.Length);
        Buffer.BlockCopy(idBytes, 0, responseData, announceHeader.Length, idBytes.Length);

        var tasks = connections.Select(conn =>
            ListenSingleConnectionAsync(conn, responseData, cancellationToken));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Lắng nghe trên một connection duy nhất.
    /// </summary>
    private static async Task ListenSingleConnectionAsync(
        UdpClient udpClient,
        byte[] responseData,
        CancellationToken cancellationToken)
    {
        try
        {
            var announceHeaderBytes = Encoding.UTF8.GetBytes(AppConstants.AnnounceHeader);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await udpClient.ReceiveAsync(cancellationToken);
                    var receivedBytes = result.Buffer;

                    // Kiểm tra độ dài và nội dung
                    if (receivedBytes.Length < announceHeaderBytes.Length)
                        continue;

                    var headerMatch = true;
                    for (var i = 0; i < announceHeaderBytes.Length; i++)
                    {
                        if (receivedBytes[i] != announceHeaderBytes[i])
                        {
                            headerMatch = false;
                            break;
                        }
                    }

                    if (!headerMatch)
                        continue;

                    // Gửi phản hồi
                    await udpClient.SendAsync(responseData, result.RemoteEndPoint);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Tiếp tục lắng nghe nếu có lỗi
                }
            }
        }
        finally
        {
            udpClient.Close();
        }
    }

    /// <summary>
    /// Lấy danh sách các interface mạng đang hoạt động.
    /// </summary>
    private static List<(System.Net.NetworkInformation.NetworkInterface Interface, HashSet<IPAddress> Addresses)> GetRunningInterfaces()
    {
        var result = new List<(System.Net.NetworkInformation.NetworkInterface, HashSet<IPAddress>)>();

        var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
        foreach (var iface in interfaces)
        {
            if (iface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                continue;

            var addresses = new HashSet<IPAddress>();
            var ipProps = iface.GetIPProperties();
            foreach (var addr in ipProps.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    addresses.Add(addr.Address);
                }
            }

            result.Add((iface, addresses));
        }

        return result;
    }
}
