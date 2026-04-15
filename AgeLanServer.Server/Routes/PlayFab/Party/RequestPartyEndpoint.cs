// Port từ server/internal/routes/playfab/Party/RequestParty.go
// Endpoint /PlayFab/Party/RequestParty - yêu cầu thông tin party.

using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Party;

/// <summary>
/// Endpoint RequestParty - trả về thông tin party từ PlayFab Party service.
/// Trong LAN server, endpoint này trả về "Service Unavailable" vì không tích hợp PlayFab Party.
/// </summary>
public static class RequestPartyEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu RequestParty.
    /// Trả về lỗi ServiceUnavailable vì không có PlayFab Party service trong LAN.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        await PlayFabResponder.RespondNotAvailableAsync(ctx);
    }

    /// <summary>
    /// Đăng ký endpoint RequestParty.
    /// Route: POST /PlayFab/Party/RequestParty
    /// hoặc POST /Party/RequestParty
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Party/RequestParty", Handle);
        app.MapPost("/Party/RequestParty", Handle);
    }
}
