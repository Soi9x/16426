// Port từ server/internal/routes/shutdown/shutdown.go
/// Endpoint dừng server, chỉ cho phép từ IP cục bộ.

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using AgeLanServer.Common;
using Microsoft.AspNetCore.Http;

namespace AgeLanServer.Server.Routes.Shutdown;

/// <summary>
/// Endpoint xử lý yêu cầu dừng server.
/// Chỉ cho phép các IP là interface cục bộ (tương đương ResolveUnspecifiedIps trong Go).
/// </summary>
public static class ShutdownEndpoint
{
    /// <summary>
    /// Lấy danh sách IP của các network interface đang hoạt động.
    /// Tương đương ResolveUnspecifiedIps() trong Go.
    /// </summary>
    private static List<string> ResolveLocalIps()
    {
        var ips = new List<string>();
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                if ((ni.OperationalStatus != OperationalStatus.Up &&
                     ni.OperationalStatus != OperationalStatus.Unknown) ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
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
        }
        catch { }

        // Thêm localhost
        ips.Add("127.0.0.1");
        return ips.Distinct().ToList();
    }

    /// <summary>
    /// Xử lý yêu cầu dừng server.
    /// Kiểm tra IP nguồn, nếu là IP cục bộ thì gửi tín hiệu dừng.
    /// </summary>
    public static async Task HandleShutdown(HttpContext ctx, IHostApplicationLifetime lifetime)
    {
        // Lấy IP từ kết nối remote
        var remoteIp = ctx.Connection.RemoteIpAddress;
        if (remoteIp == null)
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        // Lấy danh sách IP cục bộ
        var allowedIps = ResolveLocalIps();
        if (allowedIps.Count == 0)
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        // Kiểm tra IP có trong danh sách cho phép
        var remoteIpStr = remoteIp.ToString();
        bool isAllowed = allowedIps.Contains(remoteIpStr, StringComparer.OrdinalIgnoreCase);

        if (!isAllowed)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Gửi tín hiệu dừng server
        ctx.Response.StatusCode = StatusCodes.Status200OK;
        await ctx.Response.CompleteAsync();

        // Dừng server sau khi phản hồi
        _ = Task.Run(() =>
        {
            AppLogger.Info("Nhận tín hiệu dừng server từ shutdown endpoint");
            lifetime.StopApplication();
        });
    }
}
