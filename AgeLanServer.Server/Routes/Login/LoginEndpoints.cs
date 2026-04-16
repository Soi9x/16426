using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Relationship;
using AgeLanServer.Server.Routes.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Login;

public static class LoginEndpoints
{
    internal static readonly ConcurrentDictionary<string, SessionData> Sessions = new();
    private static readonly ConcurrentDictionary<string, UserIdentity> UsersByPlatform = new();
    private static readonly ConcurrentDictionary<string, object[]> LoginDataCache = new(StringComparer.OrdinalIgnoreCase);

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/login");

        group.MapPost("/platformlogin", HandlePlatformLogin);
        group.MapPost("/logout", HandleLogout);
        group.MapPost("/readSession", HandleReadSession);
    }

    private static async Task<IResult> HandlePlatformLogin(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new PlatformLoginRequest();
        var now = DateTime.UtcNow;
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound)
        {
            return PlatformLoginError(now);
        }

        var isXbox = IsXboxAccount(req.AccountType);
        var platformUserId = req.PlatformUserId == 0 ? (ulong)Interlocked.Increment(ref _nextPlatformUserId) : req.PlatformUserId;
        var userKey = $"{(isXbox ? "xbox" : "steam")}:{platformUserId}";

        var user = UsersByPlatform.GetOrAdd(userKey, _ => new UserIdentity
        {
            UserId = Interlocked.Increment(ref _nextUserId),
            ProfileId = Interlocked.Increment(ref _nextProfileId),
            StatId = Interlocked.Increment(ref _nextStatId),
            Reliclink = Interlocked.Increment(ref _nextReliclink),
            PlatformUserId = platformUserId,
            IsXbox = isXbox,
            Alias = string.IsNullOrWhiteSpace(req.Alias) ? $"Player_{_nextUserId}" : req.Alias
        });

        if (!string.IsNullOrWhiteSpace(req.Alias))
        {
            user.Alias = req.Alias;
        }

        foreach (var existing in Sessions.Where(s => s.Value.UserId == user.UserId).Select(s => s.Key).ToArray())
        {
            Sessions.TryRemove(existing, out _);
        }

        var sessionId = Guid.NewGuid().ToString("N");
        var session = new SessionData
        {
            SessionId = sessionId,
            UserId = user.UserId,
            ProfileId = user.ProfileId,
            StatId = user.StatId,
            Reliclink = user.Reliclink,
            Alias = user.Alias,
            Language = "en",
            Region = "eur",
            Presence = 1,
            CreatedAt = now,
            PlatformUserId = user.PlatformUserId,
            IsXbox = user.IsXbox,
            PlatformPath = BuildPlatformPath(user.IsXbox, user.PlatformUserId),
            PlatformId = user.IsXbox ? 9 : 3,
            ClientLibVersion = req.ClientLibVersion,
            Messages = new List<object[]>()
        };

        Sessions[sessionId] = session;

        ctx.Items["SessionId"] = sessionId;
        ctx.Items["UserId"] = session.UserId;
        ctx.Items["ProfileId"] = session.ProfileId;
        ctx.Items["UserName"] = session.Alias;

        await RelationshipEndpoints.NotifyPresenceChangedAsync(session);

        logger.LogInformation("Platform login: SessionId={SessionId}, UserId={UserId}, Alias={Alias}",
            sessionId, session.UserId, session.Alias);

        var gameId = string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
        var profileInfo = EncodeProfileInfo(session, req.ClientLibVersion);
        var relationshipPayload = RelationshipEndpoints.BuildRelationshipsPayload(session);
        var servers = BuildBattleServersResponse();

        var response = new List<object>
        {
            0,
            sessionId,
            549_000_000,
            now.ToUnixTimeSeconds(),
            new object[]
            {
                session.ProfileId,
                session.PlatformPath,
                session.PlatformId,
                -1,
                0,
                session.Language,
                session.Region,
                2,
                null!
            },
            new object[] { profileInfo },
            0,
            0,
            null!
        };

        if (gameId == GameIds.AgeOfEmpires1)
        {
            response.Add(Array.Empty<object>());
        }
        else
        {
            response.Add(LoadLoginData(gameId));
        }

        var allProfileInfo = new List<object>
        {
            0,
            profileInfo,
            relationshipPayload,
            gameId == GameIds.AgeOfEmpires2 ? new object[] { EncodeExtraProfileInfo(session, req.ClientLibVersion) } : Array.Empty<object>(),
            gameId == GameIds.AgeOfEmpires1 ? Array.Empty<object>() : Array.Empty<object>(),
            null!,
            Array.Empty<object>(),
            null!,
            1
        };

        if (gameId != GameIds.AgeOfEmpires1)
        {
            allProfileInfo.Add(Array.Empty<object>());
        }

        if (req.ClientLibVersion >= 193)
        {
            allProfileInfo.Add(-1);
        }

        response.Add(allProfileInfo.ToArray());
        response.Add(Array.Empty<object>());
        response.Add(0);
        response.Add(servers);

        var expiration = DateTime.UtcNow.AddHours(1).ToString("R", CultureInfo.InvariantCulture);
        ctx.Response.Headers.Append("Set-Cookie", $"reliclink={session.Reliclink}; Expires={expiration}; Max-Age=3600");
        ctx.Response.Headers.Append("Request-Context", "appId=cid-v1:d21b644d-4116-48ea-a602-d6167fb46535");
        ctx.Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0");
        ctx.Response.Headers.Append("Expires", "Thu, 01 Jan 1970 00:00:00 GMT");

        return Results.Ok(response.ToArray());
    }

    private static async Task<IResult> HandleLogout(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        if (!TryGetSession(ctx, out var session))
        {
            return Results.Ok(new object[] { 2 });
        }

        if (Sessions.TryRemove(session.SessionId, out var removed))
        {
            removed.Presence = 0;
            await RelationshipEndpoints.NotifyPresenceChangedAsync(removed);

            logger.LogInformation("Logout: SessionId={SessionId}, UserId={UserId}",
                removed.SessionId, removed.UserId);
        }

        ctx.Response.Headers.Append("Set-Cookie",
            "reliclink=; Path=/; HttpOnly; SameSite=Lax; Expires=Thu, 01 Jan 1970 00:00:00 GMT");

        return Results.Ok(new object[] { 0 });
    }

    private static async Task<IResult> HandleReadSession(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new ReadSessionRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound)
        {
            return BuildReadSessionResponse(0, Array.Empty<object>());
        }

        if (!TryGetSession(ctx, out var session))
        {
            return BuildReadSessionResponse(req.Ack, Array.Empty<object>());
        }

        object[] messages;
        lock (session.MessagesLock)
        {
            messages = session.Messages.ToArray();
            session.Messages.Clear();
        }

        var messageId = messages.Length > 0 ? req.Ack + 1 : req.Ack;
        return BuildReadSessionResponse(messageId, messages);
    }

    private static IResult BuildReadSessionResponse(uint messageId, object[] messages)
    {
        var json = JsonSerializer.Serialize(new object[] { messages });
        return Results.Content($"{messageId},{json}", "application/json");
    }

    internal static bool TryGetSession(HttpContext ctx, out SessionData session)
    {
        if (ctx.Items.TryGetValue("SessionId", out var sessionIdObj) &&
            sessionIdObj is string sessionId &&
            Sessions.TryGetValue(sessionId, out session!))
        {
            return true;
        }

        var fallbackSessionId = ctx.Request.Query["sessionID"].ToString();
        if (string.IsNullOrWhiteSpace(fallbackSessionId) &&
            ctx.Request.Cookies.TryGetValue("sessionID", out var sessionCookie))
        {
            fallbackSessionId = sessionCookie;
        }

        if (!string.IsNullOrWhiteSpace(fallbackSessionId) && Sessions.TryGetValue(fallbackSessionId, out session!))
        {
            ctx.Items["SessionId"] = fallbackSessionId;
            ctx.Items["UserId"] = session.UserId;
            ctx.Items["ProfileId"] = session.ProfileId;
            ctx.Items["UserName"] = session.Alias;
            return true;
        }

        session = null!;
        return false;
    }

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

    internal static int? GetUserIdFromSession(string sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var session))
            return session.UserId;
        return null;
    }

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
            null!,
            null!,
            Array.Empty<object>(),
            Array.Empty<object>(),
            0,
            Array.Empty<object>()
        });
    }

    internal static object[] EncodeProfileInfo(SessionData session, ushort clientLibVersion)
    {
        var profileInfo = new List<object>
        {
            new DateTimeOffset(2024, 5, 2, 3, 34, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
            session.UserId,
            session.PlatformPath,
            session.AvatarMetadata,
            session.Alias,
        };

        if (clientLibVersion >= 190)
        {
            profileInfo.Add(session.Alias);
        }

        profileInfo.AddRange(new object[]
        {
            string.Empty,
            session.StatId,
            0,
            9999,
            session.IsXbox ? 3 : 0,
            null!,
            session.PlatformUserId.ToString(CultureInfo.InvariantCulture),
            session.PlatformId,
            Array.Empty<object>()
        });

        return profileInfo.ToArray();
    }

    internal static object[] EncodeProfileInfoWithPresence(SessionData session, ushort clientLibVersion)
    {
        var profileInfo = EncodeProfileInfo(session, clientLibVersion).ToList();

        if (ServerRuntime.CurrentGameId != GameIds.AgeOfEmpires1)
        {
            var properties = session.PresenceProperties
                .Select(kv => new object[] { kv.Key, kv.Value })
                .ToArray();

            profileInfo.Add(session.Presence);
            profileInfo.Add(RelationshipEndpoints.GetPresenceLabel(session.Presence));
            profileInfo.Add(properties);
        }

        return profileInfo.ToArray();
    }

    private static object[] EncodeExtraProfileInfo(SessionData session, ushort clientLibVersion)
    {
        var info = new List<object>
        {
            session.StatId,
            0,
            0,
            1,
            -1,
            0,
            0,
            -1,
            -1,
            -1,
            -1,
            -1,
            1000,
            1713372625,
            0,
            0,
            0,
        };

        if (clientLibVersion >= 190)
        {
            info.Add(0);
            info.Add(0);
        }

        return info.ToArray();
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

    private static object[] LoadLoginData(string gameId)
    {
        return LoginDataCache.GetOrAdd(gameId, key =>
        {
            var path = Path.Combine(AppConstants.ResourcesDir, "config", key, "login.json");
            if (!File.Exists(path))
            {
                return Array.Empty<object>();
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<object>();
            }

            var result = new List<object>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result.Add(new object[] { prop.Name, prop.Value.ToString() });
            }

            return result.ToArray();
        });
    }

    private static bool IsXboxAccount(string accountType)
    {
        if (string.IsNullOrWhiteSpace(accountType))
        {
            return false;
        }

        return accountType.Contains("xbox", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPlatformPath(bool isXbox, ulong platformUserId)
    {
        if (isXbox)
        {
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(BitConverter.GetBytes(platformUserId)));
            var fullId = string.Concat(Enumerable.Repeat(hash, 10)).Substring(0, 40);
            return $"/xboxlive/{fullId}";
        }

        return $"/steam/{platformUserId}";
    }

    private static long ToUnixTimeSeconds(this DateTime dt)
    {
        return new DateTimeOffset(dt).ToUnixTimeSeconds();
    }

    private static int _nextUserId = 10000;
    private static int _nextProfileId = 20000;
    private static int _nextStatId = 30000;
    private static int _nextReliclink = 40000;
    private static long _nextPlatformUserId = 50000;
}

internal sealed class UserIdentity
{
    public int UserId { get; set; }
    public int ProfileId { get; set; }
    public int StatId { get; set; }
    public int Reliclink { get; set; }
    public string Alias { get; set; } = string.Empty;
    public ulong PlatformUserId { get; set; }
    public bool IsXbox { get; set; }
}

internal sealed class SessionData
{
    public string SessionId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int ProfileId { get; set; }
    public int StatId { get; set; }
    public int Reliclink { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string Region { get; set; } = "eur";
    public int Presence { get; set; }
    public DateTime CreatedAt { get; set; }
    public ulong PlatformUserId { get; set; }
    public bool IsXbox { get; set; }
    public int PlatformId { get; set; }
    public string PlatformPath { get; set; } = string.Empty;
    public string AvatarMetadata { get; set; } = string.Empty;
    public ushort ClientLibVersion { get; set; }
    public List<object[]> Messages { get; set; } = new();
    public object MessagesLock { get; } = new();
    public Dictionary<int, string> PresenceProperties { get; set; } = new();
}
