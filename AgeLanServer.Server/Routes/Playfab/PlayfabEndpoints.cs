using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Login;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.Playfab;

public static class PlayfabEndpoints
{
    private static readonly ConcurrentDictionary<string, PlayfabSessionData> Sessions = new(StringComparer.Ordinal);

    public static void RegisterEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/Client/LoginWithCustomID", HandleLoginWithCustomId);
        app.MapPost("/Client/GetUserData", HandleGetUserData);
        app.MapPost("/Client/GetPlayerCombinedInfo", HandleGetPlayerCombinedInfo);
        app.MapPost("/Client/GetTime", HandleGetTime);
        app.MapPost("/Client/UpdateUserTitleDisplayName", HandleUpdateUserTitleDisplayName);

        app.MapPost("/Event/WriteTelemetryEvents", HandleWriteTelemetryEvents);

        app.MapPost("/MultiplayerServer/GetCognitiveServicesToken", HandleServiceUnavailable);
        app.MapPost("/MultiplayerServer/ListPartyQosServers", HandleServiceUnavailable);
        app.MapPost("/Party/RequestParty", HandleServiceUnavailable);
    }

    private static async Task<IResult> HandleLoginWithCustomId(HttpContext ctx)
    {
        var req = new LoginWithCustomIdRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || !int.TryParse(req.CustomId, out var userId))
        {
            return BuildBadRequest();
        }

        var loginSession = LoginEndpoints.Sessions.Values.FirstOrDefault(s => s.UserId == userId);
        if (loginSession is null)
        {
            return BuildBadRequest();
        }

        var sessionTicket = GenerateSessionTicket();
        var playfabSession = new PlayfabSessionData
        {
            SessionTicket = sessionTicket,
            UserId = loginSession.UserId,
            ProfileId = loginSession.ProfileId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        Sessions[sessionTicket] = playfabSession;

        var data = new Dictionary<string, object?>
        {
            ["SessionTicket"] = sessionTicket,
            ["PlayFabId"] = sessionTicket,
            ["NewlyCreated"] = true,
            ["SettingsForUser"] = new Dictionary<string, object>
            {
                ["NeedsAttribution"] = false,
                ["GatherDeviceInfo"] = true,
                ["GatherFocusInfo"] = true
            },
            ["LastLoginTime"] = FormatDate(new DateTime(2025, 11, 12, 3, 34, 0, DateTimeKind.Utc)),
            ["EntityToken"] = new Dictionary<string, object?>
            {
                ["EntityToken"] = sessionTicket,
                ["TokenExpiration"] = FormatDate(playfabSession.ExpiresAt),
                ["Entity"] = new Dictionary<string, object>
                {
                    ["Id"] = Guid.NewGuid().ToString(),
                    ["Type"] = "title_player_account",
                    ["TypeString"] = "title_player_account"
                }
            },
            ["TreatmentAssignment"] = new Dictionary<string, object>
            {
                ["Variants"] = Array.Empty<object>(),
                ["Variables"] = Array.Empty<object>()
            },
            ["UserInventory"] = Array.Empty<object>(),
            ["CharacterInventories"] = Array.Empty<object>(),
            ["UserDataVersion"] = 0,
            ["UserReadOnlyDataVersion"] = 0
        };

        return BuildOk(data);
    }

    private static IResult HandleGetUserData(HttpContext ctx)
    {
        if (!TryGetSession(ctx, out var session))
        {
            return BuildUnauthorized();
        }

        var payload = new Dictionary<string, object?>
        {
            ["Data"] = new Dictionary<string, object?>
            {
                ["RLinkProfileID"] = new Dictionary<string, object?>
                {
                    ["LastUpdated"] = FormatDate(DateTime.UtcNow),
                    ["Permission"] = "public",
                    ["Value"] = session.UserId.ToString(CultureInfo.InvariantCulture)
                }
            }
        };

        return BuildOk(payload);
    }

    private static IResult HandleGetPlayerCombinedInfo(HttpContext ctx)
    {
        if (!TryGetSession(ctx, out var session))
        {
            return BuildUnauthorized();
        }

        var payload = new Dictionary<string, object?>
        {
            ["PlayFabId"] = session.SessionTicket,
            ["InfoResultPayload"] = new Dictionary<string, object>
            {
                ["UserInventory"] = Array.Empty<object>(),
                ["CharacterInventories"] = Array.Empty<object>()
            }
        };

        return BuildOk(payload);
    }

    private static IResult HandleGetTime(HttpContext ctx)
    {
        if (!TryGetSession(ctx, out _))
        {
            return BuildUnauthorized();
        }

        return BuildOk(new Dictionary<string, object?>
        {
            ["Time"] = FormatDate(DateTime.UtcNow)
        });
    }

    private static async Task<IResult> HandleUpdateUserTitleDisplayName(HttpContext ctx)
    {
        if (!TryGetSession(ctx, out _))
        {
            return BuildUnauthorized();
        }

        var req = new UpdateUserTitleDisplayNameRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound)
        {
            return BuildBadRequest();
        }

        return BuildOk(new Dictionary<string, object?>
        {
            ["DisplayName"] = req.DisplayName
        });
    }

    private static IResult HandleWriteTelemetryEvents()
    {
        return BuildOk(new Dictionary<string, object>
        {
            ["AssignedEventIds"] = Array.Empty<object>()
        });
    }

    private static IResult HandleServiceUnavailable()
    {
        return BuildError(503, "ServiceUnavailable", -2, "Service is currently not available.");
    }

    private static bool TryGetSession(HttpContext ctx, out PlayfabSessionData session)
    {
        var sessionTicket = ctx.Request.Headers["X-Sessionticket"].ToString();
        if (string.IsNullOrWhiteSpace(sessionTicket) || !Sessions.TryGetValue(sessionTicket, out session!))
        {
            session = null!;
            return false;
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            Sessions.TryRemove(sessionTicket, out _);
            session = null!;
            return false;
        }

        return true;
    }

    private static IResult BuildOk(object payload)
    {
        return Results.Json(new Dictionary<string, object?>
        {
            ["code"] = 200,
            ["status"] = "OK",
            ["data"] = payload
        });
    }

    private static IResult BuildBadRequest()
    {
        return BuildError(400, "BadRequest", -1, "Could not parse request body.");
    }

    private static IResult BuildUnauthorized()
    {
        return BuildError(401, "Unauthorized", 401, "Invalid X-Sessionticket header");
    }

    private static IResult BuildError(int code, string status, int errorCode, string error)
    {
        return Results.Json(new Dictionary<string, object?>
        {
            ["code"] = code,
            ["status"] = status,
            ["errorCode"] = errorCode,
            ["error"] = error,
            ["errorMessage"] = string.Empty
        });
    }

    private static string GenerateSessionTicket()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string FormatDate(DateTime utcDateTime)
    {
        return utcDateTime.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }
}

internal sealed class PlayfabSessionData
{
    public string SessionTicket { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int ProfileId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

internal sealed class LoginWithCustomIdRequest
{
    public string CustomId { get; set; } = string.Empty;
}

internal sealed class UpdateUserTitleDisplayNameRequest
{
    public string DisplayName { get; set; } = string.Empty;
}
