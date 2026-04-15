// Port từ server/internal/routes/test/test.go
/// Endpoint test - trả về thông tin server ID và phiên bản.

using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using Microsoft.AspNetCore.Http;

namespace AgeLanServer.Server.Routes.Test;

/// <summary>
/// Endpoint test đơn giản, trả về thông tin server.
/// Tương đương hàm Test trong Go.
/// Thiết lập header X-Id và X-Version, sau đó trả về dữ liệu thông báo UDP.
/// </summary>
public static class TestEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu test.
    /// - Đặt header X-Id từ ServerRuntime.Id
    /// - Đặt header X-Version từ AnnounceVersions.Latest
    /// - Trả về dữ liệu thông báo UDP tương ứng với phiên bản mới nhất
    /// </summary>
    public static async Task HandleTest(HttpContext ctx)
    {
        // Đặt header X-Id
        ctx.Response.Headers[AppConstants.IdHeader] = ServerRuntime.Id.ToString();

        // Đặt header X-Version
        ctx.Response.Headers[AppConstants.VersionHeader] = AnnounceVersions.Latest.ToString();

        // Lấy dữ liệu thông báo theo phiên bản mới nhất
        // Lưu ý: Trong Go gốc, dữ liệu được tra theo game title (string).
        // Trong C#, ServerRuntime.AnnounceMessageData dùng key là int (phiên bản).
        if (ServerRuntime.AnnounceMessageData.TryGetValue(AnnounceVersions.Latest, out var announceData))
        {
            await HttpHelpers.JsonAsync(ctx.Response, announceData);
        }
        else
        {
            // Trả về object rỗng nếu không có dữ liệu
            await HttpHelpers.JsonAsync(ctx.Response, new { });
        }
    }
}
