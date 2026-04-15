// Port từ server/internal/routes/router/hostMiddleware.go
// Middleware định tuyến dựa trên Host header.

using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace AgeLanServer.Server.Middleware;

/// <summary>
/// Interface cho các handler điều kiện dựa trên Host header.
/// Tương đương ConditionalHandler trong Go.
/// </summary>
public interface IConditionalHandler
{
    /// <summary>
    /// Tên của handler (dùng cho logging).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Kiểm tra xem request có thuộc handler này không dựa trên Host header.
    /// </summary>
    bool Check(string host);

    /// <summary>
    /// Khởi tạo handler với gameId. Trả về false nếu handler không áp dụng cho game này.
    /// </summary>
    bool Initialize(string gameId);

    /// <summary>
    /// Xử lý request và ghi response. Trả về true nếu đã xử lý xong.
    /// </summary>
    Task<bool> HandleAsync(HttpContext context, string gameId);
}

/// <summary>
/// Middleware định tuyến request dựa trên Host header.
/// Kiểm tra từng handler theo thứ tự, chuyển request đến handler phù hợp.
/// Tương đương HostMiddleware trong Go.
/// </summary>
public class HostMiddleware
{
    private readonly RequestDelegate _fallback;
    private readonly HandlerEntry[] _handlers;
    private readonly string _gameId;

    /// <summary>
    /// Entry chứa handler và writer tương ứng.
    /// </summary>
    public record HandlerEntry(IConditionalHandler Handler, PrefixedWriter Writer);

    /// <summary>
    /// Tạo HostMiddleware với danh sách handler điều kiện.
    /// </summary>
    /// <param name="gameId">ID game hiện tại.</param>
    /// <param name="logWriter">TextWriter để ghi log.</param>
    /// <param name="fallback">Delegate mặc định nếu không tìm thấy handler phù hợp.</param>
    public HostMiddleware(string gameId, TextWriter logWriter, RequestDelegate? fallback = null)
    {
        _gameId = gameId;
        _fallback = fallback ?? (context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return Task.CompletedTask;
        });

        // Danh sách handler theo thứ tự ưu tiên (tương đương Go)
        var condHandlers = new IConditionalHandler[]
        {
            new PlayfabApiHandler(),
            new ApiAgeOfEmpiresHandler(),
            new Aoe4ApiAgeOfEmpiresHandler(),
            new CdnAgeOfEmpiresHandler(),
            new GameRouteHandler()
        };

        var entries = new List<HandlerEntry>();
        foreach (var handler in condHandlers)
        {
            if (handler.Initialize(gameId))
            {
                var writer = new PrefixedWriter(logWriter, gameId, handler.Name, useFileLogger: false);
                entries.Add(new HandlerEntry(handler, writer));
            }
        }

        _handlers = entries.ToArray();
    }

    /// <summary>
    /// Lấy danh sách handler đã khởi tạo.
    /// </summary>
    public IReadOnlyList<HandlerEntry> GetHandlers() => _handlers;

    /// <summary>
    /// Invoke middleware - kiểm tra Host header và chuyển đến handler phù hợp.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var host = NormalizeHost(context.Request.Host);

        foreach (var entry in _handlers)
        {
            if (entry.Handler.Check(host))
            {
                // Ghi log request
                await LogRequest(context, entry.Writer);

                // Xử lý qua handler
                var handled = await entry.Handler.HandleAsync(context, _gameId);
                if (!handled)
                {
                    await _fallback(context);
                }
                return;
            }
        }

        // Không tìm thấy handler phù hợp - chuyển đến fallback
        await _fallback(context);
    }

    /// <summary>
    /// Chuẩn hóa Host header (loại bỏ port nếu có).
    /// </summary>
    private static string NormalizeHost(HostString hostString)
    {
        var host = hostString.Value ?? string.Empty;
        var colonIndex = host.LastIndexOf(':');
        if (colonIndex > 0)
        {
            host = host.Substring(0, colonIndex);
        }
        return host.ToLowerInvariant();
    }

    /// <summary>
    /// Ghi log request (đơn giản - tương đương CustomLoggingHandler trong Go).
    /// </summary>
    private static async Task LogRequest(HttpContext context, TextWriter writer)
    {
        var logLine = $"{DateTime.UtcNow:O} [{context.Request.Method}] {context.Request.Path}";
        await writer.WriteLineAsync(logLine);
    }
}

/// <summary>
/// Extension methods để đăng ký HostMiddleware.
/// </summary>
public static class HostMiddlewareExtensions
{
    /// <summary>
    /// Sử dụng Host-based routing middleware.
    /// </summary>
    public static IApplicationBuilder UseHostRouting(
        this IApplicationBuilder app,
        string gameId,
        TextWriter? logWriter = null)
    {
        var writer = logWriter ?? Console.Out;
        var middleware = new HostMiddleware(gameId, writer);

        return app.Use(async (context, next) =>
        {
            var host = NormalizeHost(context.Request.Host);
            bool handled = false;

            foreach (var entry in middleware.GetHandlers())
            {
                if (entry.Handler.Check(host))
                {
                    await LogRequest(context, entry.Writer);
                    handled = await entry.Handler.HandleAsync(context, gameId);
                    break;
                }
            }

            if (!handled)
            {
                await next();
            }
        });
    }

    private static string NormalizeHost(HostString hostString)
    {
        var host = hostString.Value ?? string.Empty;
        var colonIndex = host.LastIndexOf(':');
        if (colonIndex > 0)
        {
            host = host.Substring(0, colonIndex);
        }
        return host.ToLowerInvariant();
    }

    private static async Task LogRequest(HttpContext context, TextWriter writer)
    {
        var logLine = $"{DateTime.UtcNow:O} [{context.Request.Method}] {context.Request.Path}";
        await writer.WriteLineAsync(logLine);
    }
}

// =============================================
// Các handler cụ thể
// =============================================

/// <summary>
/// Handler cho PlayFab API (*.playfabapi.com).
/// </summary>
public class PlayfabApiHandler : IConditionalHandler
{
    public string Name => "playfabapi";

    public bool Check(string host)
    {
        return host.EndsWith("." + GameDomains.PlayFabDomain, StringComparison.OrdinalIgnoreCase);
    }

    public bool Initialize(string gameId)
    {
        return gameId == GameIds.AgeOfEmpires4 || gameId == GameIds.AgeOfMythology;
    }

    public async Task<bool> HandleAsync(HttpContext context, string gameId)
    {
        var middleware = new PlayfabMiddleware(
            ctx => { ctx.Response.StatusCode = 404; return Task.CompletedTask; }
        );
        await middleware.InvokeAsync(context, gameId);
        return true;
    }
}

/// <summary>
/// Handler cho api.ageofempires.com.
/// </summary>
public class ApiAgeOfEmpiresHandler : IConditionalHandler
{
    private ReverseProxy? _proxy;

    public string Name => GameDomains.ApiAgeOfEmpires;

    public bool Check(string host)
    {
        return string.Equals(host, GameDomains.ApiAgeOfEmpires, StringComparison.OrdinalIgnoreCase);
    }

    public bool Initialize(string gameId)
    {
        _proxy = new ReverseProxy(GameDomains.ApiAgeOfEmpires);
        return true;
    }

    public async Task<bool> HandleAsync(HttpContext context, string gameId)
    {
        return _proxy != null && await _proxy.HandleAsync(context, gameId);
    }
}

/// <summary>
/// Handler cho api-dr.ageofempires.com (chỉ AoE4).
/// </summary>
public class Aoe4ApiAgeOfEmpiresHandler : IConditionalHandler
{
    private ReverseProxy? _proxy;

    public string Name => GameDomains.Aoe4ApiAgeOfEmpires;

    public bool Check(string host)
    {
        return string.Equals(host, GameDomains.Aoe4ApiAgeOfEmpires, StringComparison.OrdinalIgnoreCase);
    }

    public bool Initialize(string gameId)
    {
        if (gameId == GameIds.AgeOfEmpires4)
        {
            _proxy = new ReverseProxy(GameDomains.Aoe4ApiAgeOfEmpires);
            return true;
        }
        return false;
    }

    public async Task<bool> HandleAsync(HttpContext context, string gameId)
    {
        return _proxy != null && await _proxy.HandleAsync(context, gameId);
    }
}

/// <summary>
/// Handler cho cdn.ageofempires.com.
/// </summary>
public class CdnAgeOfEmpiresHandler : IConditionalHandler
{
    private ReverseProxy? _proxy;

    public string Name => GameDomains.CdnAgeOfEmpires;

    public bool Check(string host)
    {
        return string.Equals(host, GameDomains.CdnAgeOfEmpires, StringComparison.OrdinalIgnoreCase);
    }

    public bool Initialize(string gameId)
    {
        if (gameId != GameIds.AgeOfEmpires4)
        {
            _proxy = new ReverseProxy(GameDomains.CdnAgeOfEmpires);
            return true;
        }
        return false;
    }

    public async Task<bool> HandleAsync(HttpContext context, string gameId)
    {
        return _proxy != null && await _proxy.HandleAsync(context, gameId);
    }
}

/// <summary>
/// Handler cho game routes (fallback cuối cùng).
/// </summary>
public class GameRouteHandler : IConditionalHandler
{
    public string Name => "game";

    public bool Check(string host) => true;

    public bool Initialize(string gameId) => true;

    public Task<bool> HandleAsync(HttpContext context, string gameId)
    {
        // Ủy quyền cho các route đã đăng ký xử lý
        return Task.FromResult(false);
    }
}
