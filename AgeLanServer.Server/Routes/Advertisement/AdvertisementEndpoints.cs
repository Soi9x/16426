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
            return Results.Ok(EncodeHostErrorResponse(ctx));
        }

        if (req.Id != -1)
        {
            return Results.Ok(EncodeHostErrorResponse(ctx));
        }

        var gameId = GetCurrentGameId();
        if (gameId != GameIds.AgeOfEmpires4)
        {
            RemoveUserFromAdvertisements(session.UserId);
        }

        var isLan = req.ServiceType != 0 || BattleServerRuntime.IsLanRegion(req.RelayRegion);
        var battleServer = ResolveBattleServer(req.RelayRegion, isLan);

        var joinable = req.Joinable;
        if (gameId is GameIds.AgeOfEmpires1 or GameIds.AgeOfEmpires3 or GameIds.AgeOfMythology)
        {
            joinable = true;
        }

        var id = Interlocked.Increment(ref _nextId);
        var hostPeer = new PeerData
        {
            UserId = session.UserId,
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
            HostId = session.UserId,
            Party = req.Party,
            MaxPlayers = req.MaxPlayers > 0 ? req.MaxPlayers : (byte)8,
            MatchType = req.MatchType,
            Passworded = req.Passworded,
            Password = req.Password,
            Visible = req.Visible,
            Joinable = joinable,
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
            IsLan = isLan,
            XboxSessionId = isLan ? "0" : BuildXboxSessionId(gameId),
            BattleServerName = battleServer.Name,
            BattleServerIPv4 = battleServer.IPv4,
            BattleServerPort = battleServer.BsPort,
            BattleServerWebSocketPort = battleServer.WebSocketPort,
            BattleServerOutOfBandPort = battleServer.OutOfBandPort,
            CreatedAt = DateTime.UtcNow,
            AdvertisementIp = $"/10.0.11.{(id % 254) + 1}",
            Observers = new List<int>()
        };

        hostPeer.Ip = advertisement.AdvertisementIp;
        advertisement.Peers = new List<PeerData> { hostPeer };

        Advertisements[id] = advertisement;

        logger.LogInformation("Advertisement created: ID={Id}, Description={Desc}", id, req.Description);
        return Results.Ok(EncodeHostResponse(ctx, advertisement));
    }

    private static async Task<IResult> HandleJoin(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementBaseRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || !LoginEndpoints.TryGetSession(ctx, out var session))
        {
            return Results.Ok(EncodeJoinResponse(ctx, 2, string.Empty, CreateDefaultBattleServer(), Array.Empty<object>()));
        }

        var gameId = GetCurrentGameId();
        if (gameId != GameIds.AgeOfEmpires4)
        {
            RemoveUserFromAdvertisements(session.UserId, req.Id);
        }

        if (!Advertisements.TryGetValue(req.Id, out var adv))
        {
            return Results.Ok(EncodeJoinResponse(ctx, 2, string.Empty, CreateDefaultBattleServer(), Array.Empty<object>()));
        }

        if (req.Party != -1 && req.Party != adv.Party)
        {
            return Results.Ok(EncodeJoinResponse(ctx, 2, string.Empty, GetBattleServerForAdvertisement(adv), Array.Empty<object>()));
        }

        if (!adv.Joinable)
        {
            return Results.Ok(EncodeJoinResponse(ctx, 2, string.Empty, GetBattleServerForAdvertisement(adv), Array.Empty<object>()));
        }

        if (adv.AppBinaryChecksum != 0 && adv.AppBinaryChecksum != req.AppBinaryChecksum)
        {
            return Results.Ok(EncodeJoinResponse(ctx, 2, string.Empty, GetBattleServerForAdvertisement(adv), Array.Empty<object>()));
        }

        if (adv.DataChecksum != 0 && adv.DataChecksum != req.DataChecksum)
        {
            return Results.Ok(EncodeJoinResponse(ctx, 2, string.Empty, GetBattleServerForAdvertisement(adv), Array.Empty<object>()));
        }

        if (!string.Equals(adv.ModDllFile, req.ModDllFile, StringComparison.OrdinalIgnoreCase) ||
            adv.ModDllChecksum != req.ModDllChecksum ||
            !string.Equals(adv.ModName, req.ModName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(adv.ModVersion, req.ModVersion, StringComparison.OrdinalIgnoreCase) ||
            adv.VersionFlags != req.VersionFlags)
        {
            return Results.Ok(EncodeJoinResponse(ctx, 2, string.Empty, GetBattleServerForAdvertisement(adv), Array.Empty<object>()));
        }

        var providedPassword = string.Empty;
        if (ctx.Request.HasFormContentType)
        {
            var form = await ctx.Request.ReadFormAsync();
            providedPassword = form["password"].ToString();
        }

        if (adv.Passworded && !string.Equals(providedPassword, adv.Password, StringComparison.Ordinal))
        {
            return Results.Ok(EncodeJoinResponse(ctx, 2, string.Empty, GetBattleServerForAdvertisement(adv), Array.Empty<object>()));
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

        return Results.Ok(EncodeJoinResponse(ctx, 0, adv.AdvertisementIp, GetBattleServerForAdvertisement(adv), EncodePeer(adv.Id, existingPeer)));
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
        adv.Joinable = GetCurrentGameId() is GameIds.AgeOfEmpires1 or GameIds.AgeOfEmpires3
            ? true
            : req.Joinable;
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
        var tags = new TagRequest();
        await HttpHelpers.BindAsync(ctx.Request, query);
        await HttpHelpers.BindAsync(ctx.Request, search);
        await HttpHelpers.BindAsync(ctx.Request, tags);

        var currentUserId = ctx.Items["UserId"] as int? ?? 0;
        var gameId = GetCurrentGameId();

        var numericTags = tags.NumericTagNames.Data
            .Zip(tags.NumericTagValues.Data, (name, value) => new KeyValuePair<string, int>(name, value))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var stringTags = tags.StringTagNames.Data
            .Zip(tags.StringTagValues.Data, (name, value) => new KeyValuePair<string, string>(name, value))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        var filtered = Advertisements.Values
            .Where(a => a.Joinable || a.Visible)
            .Where(a => currentUserId == 0 || a.Peers.All(p => p.UserId != currentUserId))
            .Where(a => a.IsLan || string.IsNullOrWhiteSpace(a.RelayRegion) || BattleServerRuntime.IsLanRegion(a.RelayRegion) || BattleServerRuntime.TryGetConfiguredBattleServer(gameId, a.RelayRegion, out _))
            .Where(a => search.AppBinaryChecksum == 0 || a.AppBinaryChecksum == search.AppBinaryChecksum)
            .Where(a => search.DataChecksum == 0 || a.DataChecksum == search.DataChecksum)
            .Where(a => search.MatchType is null || a.MatchType == search.MatchType)
            .Where(a => string.IsNullOrEmpty(search.ModDllFile) || a.ModDllFile == search.ModDllFile)
            .Where(a => search.ModDllChecksum == 0 || a.ModDllChecksum == search.ModDllChecksum)
            .Where(a => string.IsNullOrEmpty(search.ModName) || a.ModName == search.ModName)
            .Where(a => string.IsNullOrEmpty(search.ModVersion) || a.ModVersion == search.ModVersion)
            .Where(a => search.VersionFlags == 0 || a.VersionFlags == search.VersionFlags)
            .Where(a => MatchesTags(a, numericTags, stringTags))
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

        if (!Advertisements.TryGetValue(req.AdvertisementId, out var adv) || !adv.Observable)
        {
            var fallback = CreateDefaultBattleServer();
            var fallbackIp = BattleServerRuntime.ResolveIPv4(ctx, fallback.IPv4);
            return Results.Ok(new object[] { 2, fallbackIp, fallback.BsPort, fallback.WebSocketPort, fallback.OutOfBandPort, Array.Empty<object>(), 0, Array.Empty<object>() });
        }

        var userId = ctx.Items["UserId"] as int? ?? 0;
        if (!adv.Observers.Contains(userId))
        {
            adv.Observers.Add(userId);
        }

        var battleServer = GetBattleServerForAdvertisement(adv);
        var ip = BattleServerRuntime.ResolveIPv4(ctx, battleServer.IPv4);
        var userIdsInt = adv.Peers.Select(p => new object[] { p.UserId, Array.Empty<object>() }).ToArray();
        var userIdsStr = adv.Peers.Select(p => new object[] { p.UserId.ToString(), Array.Empty<object>() }).ToArray();

        return Results.Ok(new object[]
        {
            0,
            ip,
            battleServer.BsPort,
            battleServer.WebSocketPort,
            battleServer.OutOfBandPort,
            userIdsInt,
            adv.StartTime ?? 0,
            userIdsStr
        });
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

    private static object[] EncodeHostErrorResponse(HttpContext ctx)
    {
        var battleServer = CreateDefaultBattleServer();
        var ip = BattleServerRuntime.ResolveIPv4(ctx, battleServer.IPv4);

        return new object[]
        {
            2,
            0,
            "authtoken",
            ip,
            battleServer.BsPort,
            battleServer.WebSocketPort,
            battleServer.OutOfBandPort,
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

    private static object[] EncodeHostResponse(HttpContext ctx, AdvertisementData advertisement)
    {
        var battleServer = GetBattleServerForAdvertisement(advertisement);
        var ip = BattleServerRuntime.ResolveIPv4(ctx, battleServer.IPv4);

        return new object[]
        {
            0,
            advertisement.Id,
            "authtoken",
            ip,
            battleServer.BsPort,
            battleServer.WebSocketPort,
            battleServer.OutOfBandPort,
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

    private static object[] EncodeJoinResponse(HttpContext ctx, int errorCode, string advertisementIp,
        BattleServerRuntimeInfo battleServer, object[] peerEncoded)
    {
        var ip = BattleServerRuntime.ResolveIPv4(ctx, battleServer.IPv4);

        return new object[]
        {
            errorCode,
            advertisementIp,
            ip,
            battleServer.BsPort,
            battleServer.WebSocketPort,
            battleServer.OutOfBandPort,
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
            advertisement.IsLan ? null! : advertisement.BattleServerName
        };
    }

    private static object[] EncodePeer(int advertisementId, PeerData peer)
    {
        return new object[]
        {
            advertisementId,
            peer.UserId,
            peer.Party,
            peer.StatId,
            peer.Race,
            peer.Team,
            peer.Ip
        };
    }

    private static bool MatchesTags(AdvertisementData advertisement, Dictionary<string, int> numericTags,
        Dictionary<string, string> stringTags)
    {
        foreach (var (name, value) in numericTags)
        {
            if (!advertisement.NumericTags.TryGetValue(name, out var actual) || actual != value)
            {
                return false;
            }
        }

        foreach (var (name, value) in stringTags)
        {
            if (!advertisement.StringTags.TryGetValue(name, out var actual) || !string.Equals(actual, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static BattleServerRuntimeInfo ResolveBattleServer(string relayRegion, bool isLan)
    {
        var gameId = GetCurrentGameId();

        if (isLan || BattleServerRuntime.IsLanRegion(relayRegion))
        {
            return BattleServerRuntime.CreateLanBattleServer(gameId, relayRegion);
        }

        if (BattleServerRuntime.TryGetConfiguredBattleServer(gameId, relayRegion, out var configured))
        {
            return configured;
        }

        return CreateDefaultBattleServer();
    }

    private static BattleServerRuntimeInfo GetBattleServerForAdvertisement(AdvertisementData advertisement)
    {
        if (advertisement.BattleServerPort <= 0 || advertisement.BattleServerWebSocketPort <= 0)
        {
            return CreateDefaultBattleServer();
        }

        return new BattleServerRuntimeInfo(
            advertisement.RelayRegion,
            advertisement.BattleServerName,
            advertisement.BattleServerIPv4,
            advertisement.BattleServerPort,
            advertisement.BattleServerWebSocketPort,
            advertisement.BattleServerOutOfBandPort <= 0
                ? 27212
                : advertisement.BattleServerOutOfBandPort);
    }

    private static BattleServerRuntimeInfo CreateDefaultBattleServer()
    {
        return new BattleServerRuntimeInfo(string.Empty, "localhost", "auto", 27012, 27112, 27212);
    }

    private static string BuildXboxSessionId(string gameId)
    {
        var scidEnd = gameId switch
        {
            GameIds.AgeOfMythology => "00006fe8b971",
            GameIds.AgeOfEmpires4 => "00007d18f66e",
            _ => "000068a451d4"
        };

        return $"{{\"templateName\":\"GameSession\",\"name\":\"{Guid.NewGuid()}\",\"scid\":\"00000000-0000-0000-0000-{scidEnd}\"}}";
    }

    private static void RemoveUserFromAdvertisements(int userId, int? exceptAdvertisementId = null)
    {
        foreach (var advertisement in Advertisements.Values)
        {
            if (exceptAdvertisementId.HasValue && advertisement.Id == exceptAdvertisementId.Value)
            {
                continue;
            }

            advertisement.Peers.RemoveAll(p => p.UserId == userId);
            if (advertisement.Peers.Count == 0)
            {
                Advertisements.TryRemove(advertisement.Id, out _);
            }
        }
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
    public int Party { get; set; }
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
    public string BattleServerName { get; set; } = "localhost";
    public string BattleServerIPv4 { get; set; } = "auto";
    public int BattleServerPort { get; set; }
    public int BattleServerWebSocketPort { get; set; }
    public int BattleServerOutOfBandPort { get; set; }
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
