// Port từ server/internal/routes/playfab/MultiplayerServer/GetCognitiveServicesToken.go
// Endpoint /PlayFab/MultiplayerServer/GetCognitiveServicesToken - lấy token dịch vụ nhận thức.

using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.MultiplayerServer;

/// <summary>
/// Endpoint GetCognitiveServicesToken - trả về token cho Azure Cognitive Services.
/// Trong LAN server, endpoint này trả về "Service Unavailable" vì không hỗ trợ Cognitive Services.
/// </summary>
public static class GetCognitiveServicesTokenEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu GetCognitiveServicesToken.
    /// Trả về lỗi ServiceUnavailable vì không có Cognitive Services trong LAN.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        await PlayFabResponder.RespondNotAvailableAsync(ctx);
    }

    /// <summary>
    /// Đăng ký endpoint GetCognitiveServicesToken.
    /// Route: POST /PlayFab/MultiplayerServer/GetCognitiveServicesToken
    /// hoặc POST /MultiplayerServer/GetCognitiveServicesToken
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/MultiplayerServer/GetCognitiveServicesToken", Handle);
        app.MapPost("/MultiplayerServer/GetCognitiveServicesToken", Handle);
    }
}
