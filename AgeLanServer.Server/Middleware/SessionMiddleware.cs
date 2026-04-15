// Port từ server/internal/routes/router/sessionMiddleware.go
// Middleware xác thực session từ cookie hoặc query parameter.

using AgeLanServer.Server.Models;
using Microsoft.AspNetCore.Http;

namespace AgeLanServer.Server.Middleware;

/// <summary>
/// Middleware xác thực session cho game routes.
/// Kiểm tra sessionID từ query string hoặc cookie.
/// Tương đương SessionMiddleware trong Go.
/// </summary>
public class SessionMiddleware
{
    /// <summary>
    /// Danh sách đường dẫn không yêu cầu xác thực session.
    /// Tương đương sessAnonymousPaths trong Go.
    /// </summary>
    private static readonly HashSet<string> AnonymousPaths = new()
    {
        "/game/msstore/getStoreTokens",
        "/game/login/platformlogin",
        "/game/news/getNews",
        "/game/Challenge/getChallenges",
        "/game/item/getItemBundleItemsJson"
    };

    /// <summary>
    /// Tiền tố đường dẫn websocket không yêu cầu xác thực.
    /// </summary>
    private const string WssPrefix = "/wss/";

    /// <summary>
    /// Tiền tố đường dẫn cloud files không yêu cầu xác thực.
    /// </summary>
    private const string CloudFilesPrefix = "/cloudfiles/";

    private readonly RequestDelegate _next;
    private readonly Func<string, MainSessions>? _sessionsProvider;

    /// <summary>
    /// Tạo SessionMiddleware.
    /// </summary>
    /// <param name="next">Middleware tiếp theo trong pipeline.</param>
    /// <param name="sessionsProvider">Hàm lấy session store (có thể null để dùng DI).</param>
    public SessionMiddleware(RequestDelegate next, Func<string, MainSessions>? sessionsProvider = null)
    {
        _next = next;
        _sessionsProvider = sessionsProvider;
    }

    /// <summary>
    /// Xử lý xác thực session.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, string gameId)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Bỏ qua các đường dẫn anonymous, wss và cloudfiles
        if (IsAnonymousPath(path))
        {
            await _next(context);
            return;
        }

        // Lấy sessionID từ query string hoặc cookie
        var sessionId = context.Request.Query["sessionID"].ToString();

        if (string.IsNullOrEmpty(sessionId))
        {
            // Thử lấy từ cookie
            sessionId = context.Request.Cookies["sessionID"];
        }

        if (string.IsNullOrEmpty(sessionId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        // Lấy session store
        var sessions = GetSessions(context, gameId);
        if (sessions == null)
        {
            // Nếu không có session store, cho qua (fallback)
            await _next(context);
            return;
        }

        var session = sessions.GetById(sessionId);
        if (session == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        // Reset thời gian hết hạn
        sessions.ResetExpiry(sessionId);

        // Lưu session vào HttpContext.Items để các middleware sau dùng
        context.Items["Session"] = session;

        await _next(context);
    }

    /// <summary>
    /// Kiểm tra đường dẫn có thuộc danh sách anonymous không.
    /// </summary>
    private static bool IsAnonymousPath(string path)
    {
        if (AnonymousPaths.Contains(path))
            return true;

        if (path.StartsWith(WssPrefix, StringComparison.OrdinalIgnoreCase))
            return true;

        if (path.StartsWith(CloudFilesPrefix, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Lấy session store từ DI hoặc provider.
    /// </summary>
    private MainSessions? GetSessions(HttpContext context, string gameId)
    {
        // Thử lấy từ DI trước
        var sessions = context.RequestServices.GetService<MainSessions>();
        if (sessions != null)
            return sessions;

        // Thử lấy từ provider nếu có
        if (_sessionsProvider != null)
            return _sessionsProvider(gameId);

        return null;
    }
}
