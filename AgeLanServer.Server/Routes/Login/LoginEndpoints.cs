using System.Collections.Concurrent;
using System.Text.Json;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Login;

/// <summary>
/// Đăng ký các endpoint quản lý login: platform login, logout, read session.
/// </summary>
public static class LoginEndpoints
{
    // Kho lưu trữ sessions trong bộ nhớ
    internal static readonly ConcurrentDictionary<string, SessionData> Sessions = new();

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/login");

        // Platform login
        group.MapPost("/platformlogin", HandlePlatformLogin);

        // Logout
        group.MapPost("/logout", HandleLogout);

        // Đọc session (lấy tin nhắn chờ)
        group.MapPost("/readSession", HandleReadSession);
    }

    /// <summary>
    /// Xử lý platform login.
    /// Tạo session mới, thiết lập presence, trả về thông tin profile và server.
    /// </summary>
    private static async Task<IResult> HandlePlatformLogin(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new PlatformLoginRequest();
        var now = DateTime.UtcNow;
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound)
        {
            return PlatformLoginError(now);
        }

        // 1. Lấy hoặc tạo user từ Game.Users()
        // 2. Xóa session cũ nếu có
        // 3. Tạo session mới
        var sessionId = Guid.NewGuid().ToString("N");
        var profileId = Interlocked.Increment(ref _nextProfileId);

        var session = new SessionData
        {
            SessionId = sessionId,
            ProfileId = profileId,
            Alias = string.IsNullOrEmpty(req.Alias) ? $"Player_{profileId}" : req.Alias,
            Language = "en",
            Region = "eur",
            Presence = 1, // online
            CreatedAt = now,
            Messages = new List<object[]>()
        };

        Sessions[sessionId] = session;

        // Đặt session vào context để các middleware khác sử dụng
        ctx.Items["SessionId"] = sessionId;
        ctx.Items["UserId"] = profileId;
        ctx.Items["UserName"] = session.Alias;

        logger.LogInformation("Platform login: SessionId={SessionId}, ProfileId={ProfileId}, Alias={Alias}",
            sessionId, profileId, session.Alias);

        var profileInfo = EncodeProfileInfo(session);
        var relationshipPayload = BuildInitialRelationshipPayload();
        var servers = BuildBattleServersResponse();

        var response = new object[]
        {
            0,
            sessionId,
            549_000_000,
            now.ToUnixTimeSeconds(),
            new object[]
            {
                profileId,
                "", // platform path
                "", // platform id
                -1,
                0,
                session.Language,
                session.Region,
                2,
                null
            },
            new object[] { profileInfo },
            0,
            0,
            null,
            Array.Empty<object>(),
            new object[]
            {
                0,
                profileInfo,
                relationshipPayload,
                Array.Empty<object>(),
                Array.Empty<object>(),
                null,
                Array.Empty<object>(),
                null,
                1
            },
            Array.Empty<object>(),
            0,
            servers
        };

        // 8. Đặt cookie reliclink
        ctx.Response.Headers.Append("Set-Cookie",
            $"reliclink={sessionId}; Path=/; HttpOnly; SameSite=Lax");

        return Results.Ok(response);
    }

    /// <summary>
    /// Xử lý logout.
    /// Xóa user khỏi advertisement, chat channels, thiết lập presence = 0, xóa session.
    /// </summary>
    private static async Task<IResult> HandleLogout(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        // 1. Lấy session từ context
        var sessionId = ctx.Items["SessionId"] as string
                        ?? ctx.Request.Cookies["reliclink"];

        if (string.IsNullOrEmpty(sessionId))
        {
            return Results.Ok(new object[] { 2 });
        }

        // 2. Lấy user từ session
        if (Sessions.TryRemove(sessionId, out var session))
        {
            // 3. Xóa user khỏi advertisement (nếu có)
            // 4. Xóa user khỏi tất cả chat channels
            // 5. Thiết lập presence = 0 (offline)
            session.Presence = 0;

            logger.LogInformation("Logout: SessionId={SessionId}, ProfileId={ProfileId}",
                sessionId, session.ProfileId);
        }

        // Xóa cookie
        ctx.Response.Headers.Append("Set-Cookie",
            "reliclink=; Path=/; HttpOnly; SameSite=Lax; Expires=Thu, 01 Jan 1970 00:00:00 GMT");

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xử lý đọc session (lấy tin nhắn chờ).
    /// Trả về các tin nhắn đã lưu cho session từ ack ID.
    /// </summary>
    private static async Task<IResult> HandleReadSession(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new ReadSessionRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound)
        {
            var emptyJson = JsonSerializer.Serialize(new object[] { Array.Empty<object>() });
            return Results.Content($"0,{emptyJson}", "application/json");
        }

        // 1. Lấy session từ context
        var sessionId = ctx.Items["SessionId"] as string
                        ?? ctx.Request.Cookies["reliclink"];

        if (string.IsNullOrEmpty(sessionId) || !Sessions.TryGetValue(sessionId, out var session))
        {
            var messageId = req.Ack + 1;
            var json = JsonSerializer.Serialize(new object[] { Array.Empty<object>() });
            var rawJson = $"{messageId},{json}";
            return Results.Content(rawJson, "application/json");
        }

        // 2. WaitForMessages(req.Ack) để lấy tin nhắn chờ
        object[] messages;
        lock (session.MessagesLock)
        {
            messages = session.Messages.ToArray();
            session.Messages.Clear();
        }

        // 3. Trả về dưới dạng JSON: messageId + messages
        var messageId2 = req.Ack + 1;
        var json2 = JsonSerializer.Serialize(messages);
        var rawJson2 = $"{messageId2},{json2}";

        return Results.Content(rawJson2, "application/json");
    }

    /// <summary>
    /// Thêm tin nhắn vào session.
    /// </summary>
    internal static void AddMessageToSession(string sessionId, object[] message)
    {
        if (Sessions.TryGetValue(sessionId, out var session))
        {
            lock (session.MessagesLock)
            {
                session.Messages.Add(message);
            }
        }
    }

    /// <summary>
    /// Lấy userId từ sessionId.
    /// </summary>
    internal static int? GetUserIdFromSession(string sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var session))
            return session.ProfileId;
        return null;
    }

    /// <summary>
    /// Tạo response lỗi cho platform login.
    /// </summary>
    public static IResult PlatformLoginError(DateTime now)
    {
        return Results.Ok(new object[]
        {
            2,
            "",
            0,
            now.ToUnixTimeSeconds(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            0,
            0,
            null,
            null,
            Array.Empty<object>(),
            Array.Empty<object>(),
            0,
            Array.Empty<object>()
        });
    }

    private static object[] EncodeProfileInfo(SessionData session)
    {
        return new object[]
        {
            session.ProfileId,
            session.Alias,
            session.Presence,
            new DateTimeOffset(session.CreatedAt).ToUnixTimeSeconds()
        };
    }

    private static object[] BuildInitialRelationshipPayload()
    {
        return new object[]
        {
            0,
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>()
        };
    }

    private static object[] BuildBattleServersResponse()
    {
        var gameId = ServerRuntime.CurrentGameId;
        var includeName = gameId != GameIds.AgeOfEmpires1;
        var includeOutOfBandPort = gameId != GameIds.AgeOfEmpires1;

        var serverData = new List<object>
        {
            "",
        };

        if (includeName)
        {
            serverData.Add("localhost");
        }

        serverData.Add("127.0.0.1");
        serverData.Add(27012);
        serverData.Add(27112);

        if (includeOutOfBandPort)
        {
            serverData.Add(27212);
        }

        return new object[] { serverData.ToArray() };
    }

    private static int _nextProfileId = 0;
}

/// <summary>
/// Dữ liệu session trong bộ nhớ.
/// </summary>
internal sealed class SessionData
{
    public string SessionId { get; set; } = string.Empty;
    public int ProfileId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string Region { get; set; } = "eur";
    public int Presence { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<object[]> Messages { get; set; } = new();
    public object MessagesLock { get; } = new();
    public Dictionary<string, string> PresenceProperties { get; set; } = new();
}

/// <summary>
/// Extension method cho DateTime.
/// </summary>
internal static class DateTimeExtensions
{
    public static long ToUnixTimeSeconds(this DateTime dt)
    {
        return new DateTimeOffset(dt).ToUnixTimeSeconds();
    }
}
