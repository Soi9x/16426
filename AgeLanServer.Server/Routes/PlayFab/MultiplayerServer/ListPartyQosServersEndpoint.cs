// Port từ server/internal/routes/playfab/MultiplayerServer/ListPartyQosServers.go
// Endpoint /PlayFab/MultiplayerServer/ListPartyQosServers - trả về QoS servers cho party.

using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.MultiplayerServer;

/// <summary>
/// Endpoint ListPartyQosServers - trả về danh sách QoS servers cho party matchmaking.
/// Trong LAN server, endpoint này trả về "Service Unavailable" vì không hỗ trợ QoS servers.
/// </summary>
public static class ListPartyQosServersEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu ListPartyQosServers.
    /// Trả về lỗi ServiceUnavailable vì không có QoS servers trong LAN.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        await PlayFabResponder.RespondNotAvailableAsync(ctx);
    }

    /// <summary>
    /// Đăng ký endpoint ListPartyQosServers.
    /// Route: POST /PlayFab/MultiplayerServer/ListPartyQosServers
    /// hoặc POST /MultiplayerServer/ListPartyQosServers
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/MultiplayerServer/ListPartyQosServers", Handle);
        app.MapPost("/MultiplayerServer/ListPartyQosServers", Handle);
    }
}
