// Registrar cho các endpoints bổ sung không thuộc game routes.
// Bao gồm: ApiAgeOfEmpires, CdnAgeOfEmpires, CloudFiles.
// Đây là các endpoints chung tương đương các thư mục routes/ ngoài game/ trong Go.

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
        // Kiểm duyệt văn bản (text moderation) cho AoE
        TextModerationEndpoint.RegisterEndpoint(app);

        // Trạng thái server CDN AoE
        ServerStatusEndpoint.RegisterEndpoint(app);

        // Cloud files (Azure Blob Storage mô phỏng)
        CloudFilesEndpoint.RegisterEndpoint(app);
    }
}
