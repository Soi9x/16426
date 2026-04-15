// Port từ server/internal/routes/router/playfabapiMiddleware.go
// Middleware xác thực PlayFab session từ header X-Sessionticket hoặc X-Entitytoken.

using AgeLanServer.Common;
using AgeLanServer.Server.Models.Playfab;
using Microsoft.AspNetCore.Http;

namespace AgeLanServer.Server.Middleware;

/// <summary>
/// Middleware xác thực PlayFab session.
/// Kiểm tra header X-Sessionticket (AoE4) hoặc X-Entitytoken (game khác).
/// Tương đương PlayfabMiddleware trong Go.
/// </summary>
public class PlayfabMiddleware
{
    /// <summary>
    /// Danh sách đường dẫn không yêu cầu xác thực.
    /// Tương đương playAnonymousPaths trong Go.
    /// </summary>
    private static readonly HashSet<string> AnonymousPaths = new()
    {
        "/Client/LoginWithSteam",
        "/Client/LoginWithCustomID",
        "/MultiplayerServer/ListPartyQosServers",
        "/Event/WriteTelemetryEvents"
    };

    /// <summary>
    /// Tiền tố đường dẫn static không yêu cầu xác thực.
    /// </summary>
    public const string StaticSuffix = "/static";

    private readonly RequestDelegate _next;

    /// <summary>
    /// Tạo PlayfabMiddleware.
    /// </summary>
    /// <param name="next">Middleware tiếp theo trong pipeline.</param>
    public PlayfabMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Xử lý xác thực PlayFab session.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, string gameId)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Bỏ qua các đường dẫn anonymous và static
        if (!AnonymousPaths.Contains(path) && !path.StartsWith(StaticSuffix, StringComparison.OrdinalIgnoreCase))
        {
            // Chọn header xác thực dựa trên game
            var authHeader = gameId == GameIds.AgeOfEmpires4 ? "X-Sessionticket" : "X-Entitytoken";

            var token = context.Request.Headers[authHeader].ToString();

            if (string.IsNullOrEmpty(token))
            {
                await RespondError(context, authHeader);
                return;
            }

            // Lấy session từ store (lấy từ DI hoặc static)
            var sessionStore = context.RequestServices.GetService<MainPlayfabSessions>();
            if (sessionStore == null)
            {
                // Nếu không có session store, cho qua (fallback)
                await _next(context);
                return;
            }

            var session = sessionStore.GetById(token);
            if (session == null)
            {
                await RespondError(context, authHeader);
                return;
            }

            // Reset thời gian hết hạn
            sessionStore.ResetExpiry(token);

            // Lưu session vào HttpContext.Items để các middleware sau dùng
            context.Items["PlayfabSession"] = session;
        }

        await _next(context);
    }

    /// <summary>
    /// Trả về lỗi Unauthorized.
    /// </summary>
    private static async Task RespondError(HttpContext context, string authHeader)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            code = 401,
            status = "Unauthorized",
            error = "Unauthorized",
            errorCode = 401,
            errorMessage = $"Invalid {authHeader} header"
        };

        await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, errorResponse);
    }
}
