using System.Collections.Concurrent;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Login;
using AgeLanServer.Server.Routes.Shared;
using AgeLanServer.Server.Routes.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Advertisement;

public static class AdvertisementEndpoints
{
    private static readonly ConcurrentDictionary<int, AdvertisementData> Advertisements = new();
    private static int _nextId;

    private const string BattleServerIp = "127.0.0.1";
    private const int BattleServerPort = 27012;
    private const int BattleServerWebSocketPort = 27112;
    private const int BattleServerOutOfBandPort = 27212;

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/advertisement");
        var gameId = GetCurrentGameId();

        group.MapPost("/host", HandleHost);
        group.MapPost("/join", HandleJoin);
        group.MapPost("/leave", HandleLeave);
        group.MapPost("/update", HandleUpdate);

        if (gameId is GameIds.AgeOfEmpires1 or GameIds.AgeOfEmpires3)
        {
            group.MapPost("/findAdvertisements", HandleFindAdvertisements);
        }
        else if (gameId is GameIds.AgeOfEmpires2 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            group.MapGet("/findAdvertisements", HandleFindAdvertisements);
        }

        group.MapGet("/getAdvertisements", HandleGetAdvertisements);

        if (gameId is GameIds.AgeOfEmpires1 or GameIds.AgeOfEmpires3)
        {
            group.MapPost("/getLanAdvertisements", HandleGetLanAdvertisements);
        }
        else if (gameId == GameIds.AgeOfEmpires2)
        {
            group.MapGet("/getLanAdvertisements", HandleGetLanAdvertisements);
        }

        if (gameId is GameIds.AgeOfEmpires2 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            group.MapPost("/updateTags", HandleUpdateTags);
            group.MapPost("/updatePlatformSessionID", HandleUpdatePlatformSessionId);
        }

        if (gameId is GameIds.AgeOfEmpires1 or GameIds.AgeOfEmpires3)
        {
            group.MapPost("/updatePlatformLobbyID", HandleUpdatePlatformLobbyId);
        }

        if (gameId is GameIds.AgeOfEmpires2 or GameIds.AgeOfEmpires3 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            group.MapPost("/startObserving", HandleStartObserving);
            group.MapPost("/stopObserving", HandleStopObserving);
        }

        group.MapPost("/updateState", HandleUpdateState);

        if (gameId == GameIds.AgeOfEmpires3)
        {
            group.MapPost("/findObservableAdvertisements", HandleFindObservableAdvertisements);
        }
        else if (gameId is GameIds.AgeOfEmpires2 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            group.MapGet("/findObservableAdvertisements", HandleFindObservableAdvertisements);
        }
    }

    private static async Task<IResult> HandleHost(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementHostRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || !LoginEndpoints.TryGetSession(ctx, out var session))
        {
            return Results.Ok(EncodeHostErrorResponse());
        }

        if (req.Id != 0 && req.Id != -1)
        {
            return Results.Ok(EncodeHostErrorResponse());
        }

        var id = Interlocked.Increment(ref _nextId);
        var hostPeer = new PeerData
        {
            UserId = req.HostId,
            StatId = session.StatId,
            Party = req.Party,
            Race = req.Race,
            Team = req.Team,
        };

        var advertisement = new AdvertisementData
        {
            Id = id,
            Description = req.Description,
            MapName = req.MapName,
            HostId = req.HostId,
            MaxPlayers = req.MaxPlayers > 0 ? req.MaxPlayers : (byte)8,
            MatchType = req.MatchType,
            Passworded = req.Passworded,
            Password = req.Password,
            Visible = req.Visible,
            Joinable = req.Joinable,
            Observable = req.Observable,
            ObserverDelay = req.ObserverDelay,
            ObserverPassword = req.ObserverPassword,
            State = req.State,
            RelayRegion = req.RelayRegion,
            AppBinaryChecksum = req.AppBinaryChecksum,
            DataChecksum = req.DataChecksum,
            ModDllFile = req.ModDllFile,
            ModDllChecksum = req.ModDllChecksum,
            ModName = req.ModName,
            ModVersion = req.ModVersion,
            VersionFlags = req.VersionFlags,
            PlatformSessionId = req.PsnSessionId,
            Options = req.Options,
            SlotInfo = req.SlotInfo,
            IsLan = req.ServiceType != 0,
            XboxSessionId = "0",
            CreatedAt = DateTime.UtcNow,
            AdvertisementIp = $"/10.0.11.{(id % 254) + 1}",
            Observers = new List<int>()
        };

        hostPeer.Ip = advertisement.AdvertisementIp;
        advertisement.Peers = new List<PeerData> { hostPeer };

        Advertisements[id] = advertisement;

        logger.LogInformation("Advertisement created: ID={Id}, Description={Desc}", id, req.Description);
        return Results.Ok(EncodeHostResponse(advertisement));
    }

    private static async Task<IResult> HandleJoin(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementBaseRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || !LoginEndpoints.TryGetSession(ctx, out var session))
        {
            return Results.Ok(EncodeJoinResponse(2, string.Empty, Array.Empty<object>()));
        }

        if (!Advertisements.TryGetValue(req.Id, out var adv))
        {
            return Results.Ok(EncodeJoinResponse(2, string.Empty, Array.Empty<object>()));
        }

        if (!adv.Joinable)
        {
            return Results.Ok(EncodeJoinResponse(2, string.Empty, Array.Empty<object>()));
        }

        if (adv.AppBinaryChecksum != 0 && adv.AppBinaryChecksum != req.AppBinaryChecksum)
        {
            return Results.Ok(EncodeJoinResponse(2, string.Empty, Array.Empty<object>()));
        }

        if (adv.DataChecksum != 0 && adv.DataChecksum != req.DataChecksum)
        {
            return Results.Ok(EncodeJoinResponse(2, string.Empty, Array.Empty<object>()));
        }

        if (!string.Equals(adv.ModDllFile, req.ModDllFile, StringComparison.OrdinalIgnoreCase) ||
            adv.ModDllChecksum != req.ModDllChecksum ||
            !string.Equals(adv.ModName, req.ModName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(adv.ModVersion, req.ModVersion, StringComparison.OrdinalIgnoreCase) ||
            adv.VersionFlags != req.VersionFlags)
        {
            return Results.Ok(EncodeJoinResponse(2, string.Empty, Array.Empty<object>()));
        }

        if (adv.Passworded)
        {
            var providedPassword = string.Empty;
            if (ctx.Request.HasFormContentType)
            {
                var form = await ctx.Request.ReadFormAsync();
                providedPassword = form["password"].ToString();
            }

            if (!string.Equals(providedPassword, adv.Password, StringComparison.Ordinal))
            {
                return Results.Ok(EncodeJoinResponse(2, string.Empty, Array.Empty<object>()));
            }
        }

        var existingPeer = adv.Peers.FirstOrDefault(p => p.UserId == session.UserId);
        if (existingPeer is null)
        {
            existingPeer = new PeerData
            {
                UserId = session.UserId,
                StatId = session.StatId,
                Party = req.Party,
                Race = req.Race,
                Team = req.Team,
                Ip = adv.AdvertisementIp
            };
            adv.Peers.Add(existingPeer);
        }
        else
        {
            existingPeer.Party = req.Party;
            existingPeer.Race = req.Race;
            existingPeer.Team = req.Team;
            existingPeer.Ip = adv.AdvertisementIp;
        }

        return Results.Ok(EncodeJoinResponse(0, adv.AdvertisementIp, EncodePeer(adv.Id, existingPeer)));
    }

    private static async Task<IResult> HandleLeave(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementIdRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || !LoginEndpoints.TryGetSession(ctx, out var session))
        {
            return Results.Ok(new object[] { 2 });
        }

        if (!Advertisements.TryGetValue(req.AdvertisementId, out var adv))
        {
            return Results.Ok(new object[] { 2 });
        }

        adv.Peers.RemoveAll(p => p.UserId == session.UserId);
        if (adv.Peers.Count == 0)
        {
            Advertisements.TryRemove(req.AdvertisementId, out _);
        }

        return Results.Ok(new object[] { 0 });
    }

    private static async Task<IResult> HandleUpdate(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementUpdateRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound)
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>() });
        }

        if (!Advertisements.TryGetValue(req.Id, out var adv))
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>() });
        }

        adv.Description = req.Description;
        adv.MapName = req.MapName;
        adv.MaxPlayers = req.MaxPlayers;
        adv.Passworded = req.Passworded;
        adv.Password = req.Password;
        adv.Visible = req.Visible;
        adv.Joinable = req.Joinable;
        adv.Observable = req.Observable;
        adv.ObserverDelay = req.ObserverDelay;
        adv.ObserverPassword = req.ObserverPassword;
        adv.State = req.State;
        adv.MatchType = req.MatchType;
        adv.Options = req.Options;
        adv.SlotInfo = req.SlotInfo;
        adv.AppBinaryChecksum = req.AppBinaryChecksum;
        adv.DataChecksum = req.DataChecksum;
        adv.ModDllFile = req.ModDllFile;
        adv.ModDllChecksum = req.ModDllChecksum;
        adv.ModName = req.ModName;
        adv.ModVersion = req.ModVersion;
        adv.VersionFlags = req.VersionFlags;
        adv.PlatformSessionId = req.PsnSessionId;

        if (adv.State == 1 && adv.StartTime is null)
        {
            adv.StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            adv.Visible = false;
            adv.Joinable = false;
        }

        return Results.Ok(new object[] { 0, EncodeAdvertisement(adv) });
    }

    private static async Task<IResult> HandleFindAdvertisements(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var query = new WanQuery();
        var search = new SearchQuery();
        await HttpHelpers.BindAsync(ctx.Request, query);
        await HttpHelpers.BindAsync(ctx.Request, search);

        var filtered = Advertisements.Values
            .Where(a => a.Visible)
            .Where(a => search.AppBinaryChecksum == 0 || a.AppBinaryChecksum == search.AppBinaryChecksum)
            .Where(a => search.DataChecksum == 0 || a.DataChecksum == search.DataChecksum)
            .Where(a => search.MatchType is null || a.MatchType == search.MatchType)
            .Where(a => string.IsNullOrEmpty(search.ModDllFile) || a.ModDllFile == search.ModDllFile)
            .Where(a => search.ModDllChecksum == 0 || a.ModDllChecksum == search.ModDllChecksum)
            .Where(a => string.IsNullOrEmpty(search.ModName) || a.ModName == search.ModName)
            .Where(a => string.IsNullOrEmpty(search.ModVersion) || a.ModVersion == search.ModVersion)
            .Where(a => search.VersionFlags == 0 || a.VersionFlags == search.VersionFlags)
            .ToList();

        var offset = Math.Max(0, query.Offset);
        var length = query.Length > 0 ? query.Length : filtered.Count;
        var page = filtered.Skip(offset).Take(length).ToList();
        var encoded = page.Select(EncodeAdvertisement).ToArray();

        return Results.Ok(new object[] { 0, encoded, Array.Empty<object>() });
    }

    private static async Task<IResult> HandleGetAdvertisements(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new GetMatchIdsRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound)
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>() });
        }

        var result = req.MatchIds.Data
            .Select(matchId => Advertisements.TryGetValue(matchId, out var adv) ? EncodeAdvertisement(adv) : null)
            .Where(adv => adv is not null)
            .ToArray();

        return Results.Ok(new object[] { 0, result });
    }

    private static async Task<IResult> HandleGetLanAdvertisements(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var query = new LanQuery();
        await HttpHelpers.BindAsync(ctx.Request, query);

        if (query.LanServerGuids == "[]")
        {
            return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>() });
        }

        var ads = Advertisements.Values
            .Where(a => a.Visible)
            .Select(EncodeAdvertisement)
            .ToArray();

        return Results.Ok(new object[] { 0, ads, Array.Empty<object>() });
    }

    private static async Task<IResult> HandleUpdateTags(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var idReq = new AdvertisementIdRequest();
        var tagReq = new TagRequest();
        await HttpHelpers.BindAsync(ctx.Request, idReq);
        await HttpHelpers.BindAsync(ctx.Request, tagReq);

        if (tagReq.StringTagNames.Data.Count != tagReq.StringTagValues.Data.Count ||
            tagReq.NumericTagNames.Data.Count != tagReq.NumericTagValues.Data.Count)
        {
            return Results.Ok(new object[] { 2 });
        }

        if (!Advertisements.TryGetValue(idReq.AdvertisementId, out var adv))
        {
            return Results.Ok(new object[] { 1 });
        }

        for (int i = 0; i < tagReq.StringTagNames.Data.Count; i++)
        {
            adv.StringTags[tagReq.StringTagNames.Data[i]] = tagReq.StringTagValues.Data[i];
        }

        for (int i = 0; i < tagReq.NumericTagNames.Data.Count; i++)
        {
            adv.NumericTags[tagReq.NumericTagNames.Data[i]] = tagReq.NumericTagValues.Data[i];
        }

        return Results.Ok(new object[] { 0 });
    }

    private static async Task<IResult> HandleUpdatePlatformSessionId(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new PlatformIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (Advertisements.TryGetValue(req.MatchId, out var adv) && ctx.Request.HasFormContentType)
        {
            var form = await ctx.Request.ReadFormAsync();
            var platformSession = form["platformSessionID"].ToString();
            if (ulong.TryParse(platformSession, out var parsed))
            {
                adv.PlatformSessionId = parsed;
            }
        }

        var sessionId = ctx.Items["SessionId"] as string ?? string.Empty;
        await WsMessageSender.SendOrStoreMessageAsync(sessionId, "PlatformSessionUpdate", new { matchId = req.MatchId });

        return Results.Ok(new object[] { 0 });
    }

    private static async Task<IResult> HandleUpdatePlatformLobbyId(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new PlatformIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        return Results.Ok(new object[] { 0 });
    }

    private static async Task<IResult> HandleStartObserving(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (!Advertisements.TryGetValue(req.AdvertisementId, out var adv))
        {
            return Results.Ok(new object[] { 1 });
        }

        if (!adv.Observable)
        {
            return Results.Ok(new object[] { 4 });
        }

        var userId = ctx.Items["UserId"] as int? ?? 0;
        if (!adv.Observers.Contains(userId))
        {
            adv.Observers.Add(userId);
        }

        return Results.Ok(new object[] { 0, BattleServerIp });
    }

    private static async Task<IResult> HandleStopObserving(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (Advertisements.TryGetValue(req.AdvertisementId, out var adv))
        {
            var userId = ctx.Items["UserId"] as int? ?? 0;
            adv.Observers.Remove(userId);
        }

        return Results.Ok(new object[] { 0 });
    }

    private static async Task<IResult> HandleUpdateState(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (!Advertisements.TryGetValue(req.AdvertisementId, out var adv))
        {
            return Results.Ok(new object[] { 1 });
        }

        if (ctx.Request.HasFormContentType)
        {
            var form = await ctx.Request.ReadFormAsync();
            var state = form["state"].ToString();
            if (sbyte.TryParse(state, out var parsedState))
            {
                adv.State = parsedState;
            }
        }

        if (adv.State == 1 && adv.StartTime is null)
        {
            adv.StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            adv.Visible = false;
            adv.Joinable = false;
        }

        return Results.Ok(new object[] { 0 });
    }

    private static async Task<IResult> HandleFindObservableAdvertisements(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var query = new WanQuery();
        await HttpHelpers.BindAsync(ctx.Request, query);

        var observableAds = Advertisements.Values
            .Where(a => a.Visible && a.Observable)
            .ToList();

        var offset = Math.Max(0, query.Offset);
        var length = query.Length > 0 ? query.Length : observableAds.Count;
        var page = observableAds.Skip(offset).Take(length).ToList();

        var encodedAds = page.Select(EncodeAdvertisement).ToArray();

        return Results.Ok(new object[] { 0, encodedAds, Array.Empty<object>() });
    }

    private static object[] EncodeHostErrorResponse()
    {
        return new object[]
        {
            2,
            0,
            "authtoken",
            BattleServerIp,
            BattleServerPort,
            BattleServerWebSocketPort,
            BattleServerOutOfBandPort,
            string.Empty,
            Array.Empty<object>(),
            0,
            0,
            null!,
            null!,
            "0",
            string.Empty
        };
    }

    private static object[] EncodeHostResponse(AdvertisementData advertisement)
    {
        return new object[]
        {
            0,
            advertisement.Id,
            "authtoken",
            BattleServerIp,
            BattleServerPort,
            BattleServerWebSocketPort,
            BattleServerOutOfBandPort,
            advertisement.RelayRegion,
            advertisement.Peers.Select(p => EncodePeer(advertisement.Id, p)).ToArray(),
            0,
            0,
            null!,
            null!,
            advertisement.XboxSessionId,
            advertisement.Description
        };
    }

    private static object[] EncodeJoinResponse(int errorCode, string advertisementIp, object[] peerEncoded)
    {
        return new object[]
        {
            errorCode,
            advertisementIp,
            BattleServerIp,
            BattleServerPort,
            BattleServerWebSocketPort,
            BattleServerOutOfBandPort,
            new[] { peerEncoded }
        };
    }

    private static object[] EncodeAdvertisement(AdvertisementData advertisement)
    {
        return new object[]
        {
            advertisement.Id,
            advertisement.PlatformSessionId,
            0,
            string.Empty,
            string.Empty,
            advertisement.XboxSessionId,
            advertisement.HostId,
            advertisement.State,
            advertisement.Description,
            advertisement.Description,
            advertisement.Visible ? 1 : 0,
            advertisement.MapName,
            advertisement.Options,
            advertisement.Passworded ? 1 : 0,
            advertisement.MaxPlayers,
            advertisement.SlotInfo,
            advertisement.MatchType,
            advertisement.Peers.Select(p => EncodePeer(advertisement.Id, p)).ToArray(),
            advertisement.Observers.Count,
            0,
            advertisement.Observable ? 1 : 0,
            advertisement.ObserverDelay,
            string.IsNullOrEmpty(advertisement.ObserverPassword) ? 0 : 1,
            advertisement.IsLan ? 1 : 0,
            advertisement.StartTime,
            advertisement.RelayRegion,
            advertisement.IsLan ? null! : "localhost"
        };
    }

    private static object[] EncodePeer(int advertisementId, PeerData peer)
    {
        return new object[]
        {
            advertisementId,
            peer.UserId,
            -1,
            peer.StatId,
            peer.Race,
            peer.Team,
            peer.Ip
        };
    }

    private static string GetCurrentGameId()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId)
            ? GameIds.AgeOfEmpires4
            : ServerRuntime.CurrentGameId;
    }
}

internal sealed class AdvertisementData
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public int HostId { get; set; }
    public byte MaxPlayers { get; set; }
    public bool Passworded { get; set; }
    public string Password { get; set; } = string.Empty;
    public bool Visible { get; set; }
    public bool Joinable { get; set; }
    public bool Observable { get; set; }
    public uint ObserverDelay { get; set; }
    public string ObserverPassword { get; set; } = string.Empty;
    public sbyte State { get; set; }
    public byte MatchType { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RelayRegion { get; set; } = string.Empty;
    public int AppBinaryChecksum { get; set; }
    public int DataChecksum { get; set; }
    public string ModDllFile { get; set; } = string.Empty;
    public int ModDllChecksum { get; set; }
    public string ModName { get; set; } = string.Empty;
    public string ModVersion { get; set; } = string.Empty;
    public uint VersionFlags { get; set; }
    public ulong PlatformSessionId { get; set; }
    public string Options { get; set; } = string.Empty;
    public string SlotInfo { get; set; } = string.Empty;
    public bool IsLan { get; set; }
    public string XboxSessionId { get; set; } = "0";
    public string AdvertisementIp { get; set; } = "/10.0.11.1";
    public long? StartTime { get; set; }
    public List<PeerData> Peers { get; set; } = new();
    public List<int> Observers { get; set; } = new();
    public Dictionary<string, string> StringTags { get; set; } = new();
    public Dictionary<string, int> NumericTags { get; set; } = new();
}

internal sealed class PeerData
{
    public int UserId { get; set; }
    public int StatId { get; set; }
    public int Party { get; set; }
    public int Race { get; set; }
    public int Team { get; set; }
    public string Ip { get; set; } = "/10.0.11.1";
}

public sealed class GetMatchIdsRequest
{
    [BindAlias("match_ids")]
    public JsonArray<int> MatchIds { get; set; } = new();
}

public sealed class LanQuery
{
    [BindAlias("lanServerGuids")]
    public string LanServerGuids { get; set; } = string.Empty;
}

public sealed class PlatformIdRequest
{
    [BindAlias("matchid")]
    public int MatchId { get; set; }
}
