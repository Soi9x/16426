using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
namespace AgeLanServer.Server.Routes.WebSocket;

public static class WebSocketEndpoints
{
    public static void RegisterEndpoints(WebApplication app)
    {
        app.MapGet("/wss", HandleWebSocket);
        app.MapGet("/wss/", HandleWebSocket);
    }

    private static async Task HandleWebSocket(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var webSocket = await ctx.WebSockets.AcceptWebSocketAsync();
        await WsMessageSender.HandleConnectionAsync(webSocket, ctx, ctx.RequestAborted);
    }
}
