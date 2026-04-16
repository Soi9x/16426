// Port từ server/internal/routes/cdnAgeOfEmpires/aoe/serverStatus/serverStatus.go
// Endpoint /cdn/ageofempires/aoe/serverStatus - trạng thái server CDN.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.CdnAgeOfEmpires;

/// <summary>
/// Endpoint ServerStatus - trả về trạng thái server cho CDN AoE.
/// Trong LAN server, endpoint này luôn trả về 404 Not Found.
/// </summary>
public static class ServerStatusEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu ServerStatus.
    /// Luôn trả về HTTP 404 Not Found.
    /// </summary>
    public static Task Handle(HttpContext ctx)
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Đăng ký endpoint ServerStatus.
    /// Route gốc theo Go:
    /// - GET /aoe/rl-server-status.json
    /// - GET /aoe/athens-server-status.json
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/aoe/rl-server-status.json", Handle);
        app.MapGet("/aoe/athens-server-status.json", Handle);
        app.MapGet("/cdn/ageofempires/aoe/serverStatus", Handle);
    }
}
