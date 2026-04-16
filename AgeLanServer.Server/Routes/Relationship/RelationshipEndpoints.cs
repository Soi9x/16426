using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Login;
using AgeLanServer.Server.Routes.Shared;
using AgeLanServer.Server.Routes.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Relationship;

public static class RelationshipEndpoints
{
    private static string GetResponsesFolder()
    {
        var gameId = GetCurrentGameTitle();
        return Path.Combine(AppConstants.ResourcesDir, "responses", gameId);
    }

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/relationship");

        group.MapGet("/getRelationships", HandleGetRelationships);
        group.MapPost("/getRelationships", HandleGetRelationships);

        group.MapGet("/getPresenceData", HandleGetPresenceData);
        group.MapPost("/setPresence", HandleSetPresence);
        group.MapPost("/setPresenceProperty", HandleSetPresenceProperty);

        group.MapPost("/addfriend", HandleAddFriend);
        group.MapPost("/ignore", HandleIgnore);
        group.MapPost("/clearRelationship", HandleClearRelationship);
    }

    private static Task<IResult> HandleGetRelationships(HttpContext ctx, ILogger<Program> logger)
    {
        var userId = GetUserIdFromSession(ctx);
        var gameTitle = GetCurrentGameTitle();

        var onlineUsers = LoginEndpoints.Sessions.Values
            .Where(s => s.Presence > 0 && s.ProfileId != userId)
            .Select(EncodeProfileInfo)
            .Cast<object>()
            .ToArray();

        var useFriends = gameTitle is GameIds.AgeOfEmpires3 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology;

        var friends = useFriends ? onlineUsers : Array.Empty<object>();
        var lastConnection = useFriends ? Array.Empty<object>() : onlineUsers;

        return Task.FromResult<IResult>(Results.Ok(new object[]
        {
            0,
            friends,
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            lastConnection,
            Array.Empty<object>(),
            Array.Empty<object>()
        }));
    }

    private static async Task<IResult> HandleGetPresenceData(HttpContext ctx, ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "presenceData.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path, ctx.RequestAborted);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }

    private static async Task<IResult> HandleSetPresence(HttpContext ctx, ILogger<Program> logger)
    {
        var req = new SetPresenceRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound)
        {
            return Results.Ok(new object[] { 2 });
        }

        var sessionId = ctx.Items["SessionId"] as string;
        if (!string.IsNullOrEmpty(sessionId) &&
            LoginEndpoints.Sessions.TryGetValue(sessionId, out var session))
        {
            session.Presence = req.PresenceId;

            var presenceMessage = new
            {
                profileId = session.ProfileId,
                presence = req.PresenceId,
                properties = session.PresenceProperties
            };

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

    private static async Task<IResult> HandleSetPresenceProperty(HttpContext ctx, ILogger<Program> logger)
    {
        var req = new SetPresencePropertyRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound)
        {
            return Results.Ok(new object[] { 2 });
        }

        var sessionId = ctx.Items["SessionId"] as string;
        SessionData? session = null;

        if (!string.IsNullOrEmpty(sessionId) && LoginEndpoints.Sessions.TryGetValue(sessionId, out var currentSession))
        {
            session = currentSession;

            if (req.Properties.Count > 0)
            {
                foreach (var kv in req.Properties)
                {
                    session.PresenceProperties[kv.Key] = kv.Value;
                }
            }
            else if (req.PresencePropertyId != 0)
            {
                var propertyKey = req.PresencePropertyId.ToString(CultureInfo.InvariantCulture);
                session.PresenceProperties[propertyKey] = req.Value;
            }
        }

        var presenceMessage = new
        {
            profileId = session?.ProfileId ?? 0,
            presence = session?.Presence ?? 0,
            properties = session?.PresenceProperties ?? new Dictionary<string, string>()
        };

        foreach (var otherSession in LoginEndpoints.Sessions.Values)
        {
            if (otherSession.SessionId != sessionId)
            {
                await WsMessageSender.SendOrStoreMessageAsync(otherSession.SessionId, "PresenceMessage", presenceMessage);
            }
        }

        return Results.Ok(new object[] { 0 });
    }

    private static async Task<IResult> HandleAddFriend(HttpContext ctx, ILogger<Program> logger)
    {
        var req = new FriendRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound)
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>() });
        }

        if (TryGetSessionByProfileId(req.TargetProfileId, out var targetSession))
        {
            return Results.Ok(new object[] { 2, EncodeProfileInfo(targetSession) });
        }

        return Results.Ok(new object[] { 2, Array.Empty<object>() });
    }

    private static async Task<IResult> HandleIgnore(HttpContext ctx, ILogger<Program> logger)
    {
        var req = new FriendRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound)
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>() });
        }

        if (TryGetSessionByProfileId(req.TargetProfileId, out var targetSession))
        {
            return Results.Ok(new object[] { 2, EncodeProfileInfo(targetSession), Array.Empty<object>() });
        }

        return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>() });
    }

    private static Task<IResult> HandleClearRelationship(ILogger<Program> logger)
    {
        return Task.FromResult<IResult>(Results.Ok(new object[] { 2 }));
    }

    private static int GetUserIdFromSession(HttpContext ctx)
    {
        if (ctx.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
        {
            return userId;
        }

        return 0;
    }

    private static string GetCurrentGameTitle()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }

    private static bool TryGetSessionByProfileId(int profileId, out SessionData session)
    {
        foreach (var candidate in LoginEndpoints.Sessions.Values)
        {
            if (candidate.ProfileId == profileId)
            {
                session = candidate;
                return true;
            }
        }

        session = null!;
        return false;
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

    internal static readonly ConcurrentDictionary<int, HashSet<int>> UserFriends = new();
    internal static readonly ConcurrentDictionary<int, HashSet<int>> UserIgnored = new();
}
