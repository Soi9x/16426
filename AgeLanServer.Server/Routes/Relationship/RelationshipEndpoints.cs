using AgeLanServer.Server.Routes.Shared;
using AgeLanServer.Server.Routes.Login;
using AgeLanServer.Common;
using System.Text.Json;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Relationship;

/// <summary>
/// ÄÄƒng kÃ½ cÃ¡c endpoint quáº£n lÃ½ quan há»‡ báº¡n bÃ¨: relationships, presence, add friend, ignore, clear.
/// </summary>
public static class RelationshipEndpoints
{
    // ÄÆ°á»ng dáº«n tá»›i thÆ° má»¥c resources/responses/{gameId}
    private static string GetResponsesFolder()
    {
        var gameId = GetCurrentGameTitleStatic();
        return Path.Combine(AppConstants.ResourcesDir, "responses", gameId);
    }

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/relationship");

        // Láº¥y danh sÃ¡ch relationships (báº¡n bÃ¨)
        group.MapGet("/getRelationships", HandleGetRelationships);
        group.MapPost("/getRelationships", HandleGetRelationships);

        // Láº¥y dá»¯ liá»‡u presence definitions
        group.MapGet("/getPresenceData", HandleGetPresenceData);

        // Cáº­p nháº­t presence
        group.MapPost("/setPresence", HandleSetPresence);

        // Cáº­p nháº­t thuá»™c tÃ­nh presence
        group.MapPost("/setPresenceProperty", HandleSetPresenceProperty);

        // ThÃªm báº¡n bÃ¨
        group.MapPost("/addfriend", HandleAddFriend);

        // Ignore user
        group.MapPost("/ignore", HandleIgnore);

        // XÃ³a relationship
        group.MapPost("/clearRelationship", HandleClearRelationship);
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y danh sÃ¡ch relationships.
    /// Tráº£ vá» táº¥t cáº£ users online nhÆ° lÃ  báº¡n bÃ¨ (do khÃ´ng tÃ­ch há»£p Steam/Xbox friends).
    /// </summary>
    private static async Task<IResult> HandleGetRelationships(HttpContext ctx, ILogger<Program> logger)
    {
        // 1. Láº¥y user tá»« session
        var userId = GetUserIdFromSession(ctx);

        // 2. MÃ£ hÃ³a thÃ´ng tin profile cá»§a táº¥t cáº£ users cÃ³ presence > 0
        // Láº¥y táº¥t cáº£ sessions Ä‘ang online tá»« LoginEndpoints
        var onlineUsers = LoginEndpoints.Sessions.Values
            .Where(s => s.Presence > 0 && s.ProfileId != userId)
            .Select(s => new object[]
            {
                s.ProfileId,
                s.Alias,
                s.Presence,
                new DateTimeOffset(s.CreatedAt).ToUnixTimeSeconds()
            })
            .ToArray();

        // 3. Tráº£ vá» dÆ°á»›i dáº¡ng friends hoáº·c lastConnection tÃ¹y game
        return Results.Ok(new object[]
        {
            0,
            onlineUsers, // friends
            Array.Empty<object>(), // ignored
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(), // lastConnection
            Array.Empty<object>(),
            Array.Empty<object>()
        });
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y dá»¯ liá»‡u presence definitions.
    /// Tráº£ vá» file presenceData.json Ä‘Ã£ kÃ½.
    /// </summary>
    private static async Task<IResult> HandleGetPresenceData(HttpContext ctx, ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "presenceData.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }

    /// <summary>
    /// Xá»­ lÃ½ cáº­p nháº­t presence cá»§a user.
    /// ThÃ´ng bÃ¡o thay Ä‘á»•i presence cho táº¥t cáº£ users qua WebSocket.
    /// </summary>
    private static async Task<IResult> HandleSetPresence(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new SetPresenceRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Cáº­p nháº­t presence cho user
        var sessionId = ctx.Items["SessionId"] as string;
        if (!string.IsNullOrEmpty(sessionId) &&
            LoginEndpoints.Sessions.TryGetValue(sessionId, out var session))
        {
            session.Presence = req.PresenceId;

            // 2. Gá»­i PresenceMessage qua WebSocket cho táº¥t cáº£ users
            var presenceMessage = new { profileId = session.ProfileId, presence = req.PresenceId };
            foreach (var otherSession in LoginEndpoints.Sessions.Values)
            {
                if (otherSession.ProfileId != session.ProfileId)
                {
                    await WsMessageSender.SendOrStoreMessageAsync(otherSession.SessionId, "PresenceMessage", presenceMessage);
                }
            }
        }

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ cáº­p nháº­t thuá»™c tÃ­nh presence.
    /// ThÃ´ng bÃ¡o thay Ä‘á»•i cho táº¥t cáº£ users qua WebSocket.
    /// </summary>
    private static async Task<IResult> HandleSetPresenceProperty(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new SetPresencePropertyRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Cáº­p nháº­t presence property cho user
        var sessionId = ctx.Items["SessionId"] as string;
        SessionData? session = null;
        if (!string.IsNullOrEmpty(sessionId))
        {
            LoginEndpoints.Sessions.TryGetValue(sessionId, out session);
            if (session != null)
            {
                // Cáº­p nháº­t presence properties
                session.PresenceProperties = req.Properties;
            }
        }

        // 2. Gá»­i PresenceMessage qua WebSocket cho táº¥t cáº£ users
        var presenceMessage = new { profileId = session?.ProfileId ?? 0, properties = req.Properties };
        foreach (var otherSession in LoginEndpoints.Sessions.Values)
        {
            if (otherSession.SessionId != sessionId)
            {
                await WsMessageSender.SendOrStoreMessageAsync(otherSession.SessionId, "PresenceMessage", presenceMessage);
            }
        }

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ thÃªm báº¡n bÃ¨.
    /// Hiá»‡n táº¡i chÆ°a Ä‘Æ°á»£c triá»ƒn khai Ä‘áº§y Ä‘á»§, tráº£ vá» lá»—i.
    /// </summary>
    private static async Task<IResult> HandleAddFriend(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new FriendRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // Triá»ƒn khai thÃªm báº¡n bÃ¨ - lÆ°u vÃ o danh sÃ¡ch friends cá»§a user
        var userId = GetUserIdFromSession(ctx);
        var friends = UserFriends.GetOrAdd(userId, _ => new HashSet<int>());
        friends.Add(req.TargetProfileId);

        return Results.Ok(new object[] { 0, new object[] { req.TargetProfileId } });
    }

    /// <summary>
    /// Xá»­ lÃ½ ignore user.
    /// Hiá»‡n táº¡i chÆ°a Ä‘Æ°á»£c triá»ƒn khai Ä‘áº§y Ä‘á»§, tráº£ vá» lá»—i.
    /// </summary>
    private static async Task<IResult> HandleIgnore(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new FriendRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // Triá»ƒn khai ignore user - lÆ°u vÃ o danh sÃ¡ch ignored
        var userId = GetUserIdFromSession(ctx);
        var ignored = UserIgnored.GetOrAdd(userId, _ => new HashSet<int>());
        ignored.Add(req.TargetProfileId);

        return Results.Ok(new object[] { 0, Array.Empty<object>(), new object[] { req.TargetProfileId } });
    }

    /// <summary>
    /// Xá»­ lÃ½ xÃ³a relationship.
    /// Hiá»‡n táº¡i chÆ°a Ä‘Æ°á»£c triá»ƒn khai, tráº£ vá» lá»—i.
    /// </summary>
    private static async Task<IResult> HandleClearRelationship(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 2 });
    }

    /// <summary>
    /// Helper: Láº¥y userId tá»« session hiá»‡n táº¡i.
    /// </summary>
    private static int GetUserIdFromSession(HttpContext ctx)
    {
        if (ctx.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
        {
            return userId;
        }
        return 0;
    }

    /// <summary>
    /// Helper: Láº¥y game title tÄ©nh.
    /// </summary>
    private static string GetCurrentGameTitleStatic()
    {
        return "age4";
    }

    // Kho lÆ°u trá»¯ friends vÃ  ignored theo user ID
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<int, HashSet<int>> UserFriends = new();
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<int, HashSet<int>> UserIgnored = new();
}
