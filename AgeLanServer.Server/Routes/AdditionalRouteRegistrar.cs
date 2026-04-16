// Registrar cho các endpoints bổ sung không thuộc game routes.
// Bao gồm: ApiAgeOfEmpires, CdnAgeOfEmpires, CloudFiles.
// Đây là các endpoints chung tương đương các thư mục routes/ ngoài game/ trong Go.

using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.ApiAgeOfEmpires;
using AgeLanServer.Server.Routes.CdnAgeOfEmpires;
using AgeLanServer.Server.Routes.CloudFiles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes;

/// <summary>
/// Đăng ký các endpoints chung (non-game routes).
/// Tương đương các routes ngoài thư mục game/ trong Go server:
/// - apiAgeOfEmpires/textmoderation
/// - cdnAgeOfEmpires/aoe/serverStatus
/// - cloudfiles
/// </summary>
public static class AdditionalRouteRegistrar
{
    /// <summary>
    /// Đăng ký tất cả endpoints chung.
    /// </summary>
    public static void RegisterEndpoints(IEndpointRouteBuilder app)
    {
        var gameId = string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId)
            ? GameIds.AgeOfEmpires4
            : ServerRuntime.CurrentGameId;

        // Kiểm duyệt văn bản (text moderation) cho AoE
        TextModerationEndpoint.RegisterEndpoint(app);

        // Trạng thái server CDN AoE (Go không bật cho age4)
        if (gameId != GameIds.AgeOfEmpires4)
        {
            ServerStatusEndpoint.RegisterEndpoint(app);
        }

        // Cloud files chỉ dùng cho age2/age3 như Go hiện tại
        if (gameId is GameIds.AgeOfEmpires2 or GameIds.AgeOfEmpires3)
        {
            CloudFilesEndpoint.RegisterEndpoint(app);
        }
    }
}
