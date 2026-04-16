using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
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
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<int, string>> PresenceLabelCache =
        new(StringComparer.OrdinalIgnoreCase);

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

    private static string GetResponsesFolder()
    {
        return Path.Combine(AppConstants.ResourcesDir, "responses", GetCurrentGameTitle());
    }

    private static Task<IResult> HandleGetRelationships(HttpContext ctx, ILogger<Program> logger)
    {
        if (!LoginEndpoints.TryGetSession(ctx, out var session))
        {
            return Task.FromResult<IResult>(Results.Ok(new object[]
            {
                0,
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<object>(),
                Array.Empty<object>()
            }));
        }

        return Task.FromResult<IResult>(Results.Ok(BuildRelationshipsPayload(session)));
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
        if (!bound || !LoginEndpoints.TryGetSession(ctx, out var session))
        {
            return Results.Ok(new object[] { 2 });
        }

        session.Presence = req.PresenceId;
        await NotifyPresenceChangedAsync(session);

        return Results.Ok(new object[] { 0 });
    }

    private static async Task<IResult> HandleSetPresenceProperty(HttpContext ctx, ILogger<Program> logger)
    {
        var req = new SetPresencePropertyRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || !LoginEndpoints.TryGetSession(ctx, out var session))
        {
            return Results.Ok(new object[] { 2 });
        }

        if (req.Properties.Count > 0)
        {
            foreach (var kv in req.Properties)
            {
                if (!int.TryParse(kv.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var propertyId))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(kv.Value))
                {
                    session.PresenceProperties.Remove(propertyId);
                }
                else
                {
                    session.PresenceProperties[propertyId] = kv.Value;
                }
            }
        }
        else if (req.PresencePropertyId != 0)
        {
            if (string.IsNullOrEmpty(req.Value))
            {
                session.PresenceProperties.Remove(req.PresencePropertyId);
            }
            else
            {
                session.PresenceProperties[req.PresencePropertyId] = req.Value;
            }
        }

        await NotifyPresenceChangedAsync(session);

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

        var requesterVersion = LoginEndpoints.TryGetSession(ctx, out var requesterSession)
            ? requesterSession.ClientLibVersion
            : (ushort)190;

        if (TryGetSessionByProfileId(req.TargetProfileId, out var targetSession))
        {
            return Results.Ok(new object[] { 2, LoginEndpoints.EncodeProfileInfo(targetSession, requesterVersion) });
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

        var requesterVersion = LoginEndpoints.TryGetSession(ctx, out var requesterSession)
            ? requesterSession.ClientLibVersion
            : (ushort)190;

        if (TryGetSessionByProfileId(req.TargetProfileId, out var targetSession))
        {
            return Results.Ok(new object[]
            {
                2,
                LoginEndpoints.EncodeProfileInfo(targetSession, requesterVersion),
                Array.Empty<object>()
            });
        }

        return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>() });
    }

    private static Task<IResult> HandleClearRelationship(ILogger<Program> logger)
    {
        return Task.FromResult<IResult>(Results.Ok(new object[] { 2 }));
    }

    internal static object[] BuildRelationshipsPayload(SessionData currentSession)
    {
        var gameTitle = GetCurrentGameTitle();

        var profileInfo = LoginEndpoints.Sessions.Values
            .Where(s => s.Presence > 0 && s.UserId != currentSession.UserId)
            .Select(s => LoginEndpoints.EncodeProfileInfoWithPresence(s, currentSession.ClientLibVersion))
            .Cast<object>()
            .ToArray();

        var useFriends = gameTitle is GameIds.AgeOfEmpires3 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology;

        return new object[]
        {
            0,
            useFriends ? profileInfo : Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            useFriends ? Array.Empty<object>() : profileInfo,
            Array.Empty<object>(),
            Array.Empty<object>()
        };
    }

    internal static async Task NotifyPresenceChangedAsync(SessionData session)
    {
        var payload = new object[]
        {
            LoginEndpoints.EncodeProfileInfoWithPresence(session, session.ClientLibVersion)
        };

        foreach (var targetSession in LoginEndpoints.Sessions.Values)
        {
            await WsMessageSender.SendOrStoreMessageAsync(targetSession.SessionId, "PresenceMessage", payload);
        }
    }

    internal static string GetPresenceLabel(int presenceId)
    {
        var gameTitle = GetCurrentGameTitle();
        var labels = PresenceLabelCache.GetOrAdd(gameTitle, LoadPresenceLabels);

        if (labels.TryGetValue(presenceId, out var label))
        {
            return label;
        }

        return string.Empty;
    }

    private static IReadOnlyDictionary<int, string> LoadPresenceLabels(string gameTitle)
    {
        var result = new Dictionary<int, string>();
        var path = Path.Combine(AppConstants.ResourcesDir, "responses", gameTitle, "presenceData.json");
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() < 2)
            {
                return result;
            }

            var definitions = doc.RootElement[1];
            if (definitions.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var def in definitions.EnumerateArray())
            {
                if (def.ValueKind != JsonValueKind.Array || def.GetArrayLength() < 3)
                {
                    continue;
                }

                if (def[0].TryGetInt32(out var id))
                {
                    result[id] = def[2].GetString() ?? string.Empty;
                }
            }
        }
        catch
        {
            return new Dictionary<int, string>();
        }

        return result;
    }

    private static bool TryGetSessionByProfileId(int profileId, out SessionData session)
    {
        foreach (var candidate in LoginEndpoints.Sessions.Values)
        {
            if (candidate.UserId == profileId || candidate.ProfileId == profileId)
            {
                session = candidate;
                return true;
            }
        }

        session = null!;
        return false;
    }

    private static string GetCurrentGameTitle()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId)
            ? GameIds.AgeOfEmpires4
            : ServerRuntime.CurrentGameId;
    }
}
