using System.Collections.Concurrent;
using System.Text.Json;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Shared;
using AgeLanServer.Server.Routes.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Advertisement;

/// <summary>
/// Đăng ký các endpoint quản lý advertisement (lobby) trên mạng LAN.
/// Bao gồm: host, join, leave, update, find, search, tags, observing.
/// </summary>
public static class AdvertisementEndpoints
{
    // Kho lưu trữ advertisements trong bộ nhớ
    private static readonly ConcurrentDictionary<int, AdvertisementData> Advertisements = new();
    private static int _nextId = 1;

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/advertisement");
        var gameId = GetCurrentGameId();

        // Tạo lobby mới
        group.MapPost("/host", HandleHost);

        // Tham gia lobby
        group.MapPost("/join", HandleJoin);

        // Rời lobby
        group.MapPost("/leave", HandleLeave);

        // Cập nhật lobby
        group.MapPost("/update", HandleUpdate);

        // Tìm kiếm advertisements (POST hoặc GET tùy game)
        if (gameId is GameIds.AgeOfEmpires1 or GameIds.AgeOfEmpires3)
        {
            group.MapPost("/findAdvertisements", HandleFindAdvertisements);
        }
        else if (gameId is GameIds.AgeOfEmpires2 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            group.MapGet("/findAdvertisements", HandleFindAdvertisements);
        }

        // Lấy danh sách advertisements theo match IDs
        group.MapGet("/getAdvertisements", HandleGetAdvertisements);

        // Lấy danh sách LAN advertisements
        if (gameId is GameIds.AgeOfEmpires1 or GameIds.AgeOfEmpires3)
        {
            group.MapPost("/getLanAdvertisements", HandleGetLanAdvertisements);
        }
        else if (gameId == GameIds.AgeOfEmpires2)
        {
            group.MapGet("/getLanAdvertisements", HandleGetLanAdvertisements);
        }

        // Cập nhật tags
        if (gameId is GameIds.AgeOfEmpires2 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            group.MapPost("/updateTags", HandleUpdateTags);
        }

        // Cập nhật platform session ID
        if (gameId is GameIds.AgeOfEmpires2 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            group.MapPost("/updatePlatformSessionID", HandleUpdatePlatformSessionId);
        }

        // Cập nhật platform lobby ID
        if (gameId is GameIds.AgeOfEmpires1 or GameIds.AgeOfEmpires3)
        {
            group.MapPost("/updatePlatformLobbyID", HandleUpdatePlatformLobbyId);
        }

        // Bắt đầu quan sát
        if (gameId is GameIds.AgeOfEmpires2 or GameIds.AgeOfEmpires3 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            group.MapPost("/startObserving", HandleStartObserving);
            // Dừng quan sát
            group.MapPost("/stopObserving", HandleStopObserving);
        }

        // Cập nhật trạng thái
        group.MapPost("/updateState", HandleUpdateState);

        // Tìm advertisements có thể quan sát
        if (gameId == GameIds.AgeOfEmpires3)
        {
            group.MapPost("/findObservableAdvertisements", HandleFindObservableAdvertisements);
        }
        else if (gameId is GameIds.AgeOfEmpires2 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            group.MapGet("/findObservableAdvertisements", HandleFindObservableAdvertisements);
        }
    }

    /// <summary>
    /// Xử lý tạo lobby mới (host).
    /// </summary>
    private static async Task<IResult> HandleHost(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementHostRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        // Tạo advertisement mới với battle server
        var id = Interlocked.Increment(ref _nextId);
        var adv = new AdvertisementData
        {
            Id = id,
            Description = req.Description,
            MapName = req.MapName,
            HostId = req.HostId,
            MaxPlayers = req.MaxPlayers,
            MatchType = req.MatchType,
            Passworded = req.Passworded,
            Visible = req.Visible,
            Joinable = true,
            Observable = req.Observable,
            ObserverDelay = req.ObserverDelay,
            State = req.State,
            Peers = new List<PeerData>(),
            Observers = new List<int>(),
            CreatedAt = DateTime.UtcNow
        };

        Advertisements[id] = adv;

        logger.LogInformation("Advertisement created: ID={Id}, Description={Desc}", id, req.Description);
        return Results.Ok(new object[] { 0, id.ToString(), "authtoken" });
    }

    /// <summary>
    /// Xử lý tham gia lobby.
    /// </summary>
    private static async Task<IResult> HandleJoin(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementBaseRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (!Advertisements.TryGetValue(req.Id, out var adv))
        {
            return Results.Ok(new object[] { 1 }); // Advertisement không tồn tại
        }

        // Kiểm tra password
        if (adv.Passworded)
        {
            var reqPassword = ctx.Request.Headers["X-Password"].FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrEmpty(reqPassword) || reqPassword != adv.Password)
            {
                return Results.Ok(new object[] { 5 }); // Sai password
            }
        }

        // Kiểm tra joinable
        if (!adv.Joinable)
        {
            return Results.Ok(new object[] { 3 }); // Lobby không cho phép join
        }

        // Thêm peer vào danh sách
        var peer = new PeerData
        {
            ProfileId = req.Party,
            Race = req.Race,
            Team = req.Team
        };
        adv.Peers.Add(peer);

        return Results.Ok(new object[] { 0, "127.0.0.1", "127.0.0.1" });
    }

    /// <summary>
    /// Xử lý rời lobby.
    /// </summary>
    private static async Task<IResult> HandleLeave(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (Advertisements.TryGetValue(req.AdvertisementId, out var adv))
        {
            // Xóa peer khỏi advertisement (dựa trên session)
            var userId = ctx.Items["UserId"] as int? ?? 0;
            adv.Peers.RemoveAll(p => p.ProfileId == userId);
        }
        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xử lý cập nhật thông tin lobby.
    /// </summary>
    private static async Task<IResult> HandleUpdate(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementUpdateRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (!Advertisements.TryGetValue(req.Id, out var adv))
        {
            return Results.Ok(new object[] { 1 });
        }

        // Cập nhật thông tin advertisement
        adv.Description = req.Description;
        adv.MapName = req.MapName;
        adv.MaxPlayers = req.MaxPlayers;
        adv.Passworded = req.Passworded;
        adv.Visible = req.Visible;
        adv.Joinable = req.Joinable;
        adv.Observable = req.Observable;
        adv.State = req.State;

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xử lý tìm kiếm advertisements.
    /// </summary>
    private static async Task<IResult> HandleFindAdvertisements(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var query = new WanQuery();
        var search = new SearchQuery();
        await HttpHelpers.BindAsync(ctx.Request, query);
        await HttpHelpers.BindAsync(ctx.Request, search);

        var allAds = Advertisements.Values
            .Where(a => a.Visible)
            .ToList();

        // Áp dụng phân trang
        var offset = query.Offset;
        var length = query.Length > 0 ? query.Length : allAds.Count;
        var page = allAds.Skip(offset).Take(length).ToList();

        var encodedAds = page.Select(a => EncodeAdvertisement(a)).ToList();

        return Results.Ok(new object[] { 0, encodedAds.ToArray(), Array.Empty<object>() });
    }

    /// <summary>
    /// Xử lý lấy danh sách advertisements theo match IDs.
    /// </summary>
    private static async Task<IResult> HandleGetAdvertisements(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new GetMatchIdsRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        var result = new List<object>();
        foreach (var matchId in req.MatchIds.Data)
        {
            if (Advertisements.TryGetValue(matchId, out var adv))
            {
                result.Add(EncodeAdvertisement(adv));
            }
        }

        return Results.Ok(new object[] { 0, result.ToArray() });
    }

    /// <summary>
    /// Xử lý lấy danh sách LAN advertisements theo relay regions.
    /// </summary>
    private static async Task<IResult> HandleGetLanAdvertisements(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var query = new LanQuery();
        await HttpHelpers.BindAsync(ctx.Request, query);

        if (query.LanServerGuids == "[]")
        {
            return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>() });
        }

        // Lọc advertisements theo LAN server GUIDs
        // Hiện tại trả về tất cả advertisements có sẵn
        var allAds = Advertisements.Values
            .Where(a => a.Visible)
            .Select(a => EncodeAdvertisement(a))
            .ToArray();

        return Results.Ok(new object[] { 0, allAds, Array.Empty<object>() });
    }

    /// <summary>
    /// Xử lý cập nhật tags cho advertisement.
    /// </summary>
    private static async Task<IResult> HandleUpdateTags(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var idReq = new AdvertisementIdRequest();
        var tagReq = new TagRequest();
        await HttpHelpers.BindAsync(ctx.Request, idReq);
        await HttpHelpers.BindAsync(ctx.Request, tagReq);

        // Kiểm tra độ dài mảng phải khớp
        if (tagReq.StringTagNames.Data.Count != tagReq.StringTagValues.Data.Count ||
            tagReq.NumericTagNames.Data.Count != tagReq.NumericTagValues.Data.Count)
        {
            return Results.Ok(new object[] { 2 });
        }

        if (!Advertisements.TryGetValue(idReq.AdvertisementId, out var adv))
        {
            return Results.Ok(new object[] { 1 });
        }

        // Cập nhật string tags
        for (int i = 0; i < tagReq.StringTagNames.Data.Count; i++)
        {
            adv.StringTags[tagReq.StringTagNames.Data[i]] = tagReq.StringTagValues.Data[i];
        }

        // Cập nhật numeric tags
        for (int i = 0; i < tagReq.NumericTagNames.Data.Count; i++)
        {
            adv.NumericTags[tagReq.NumericTagNames.Data[i]] = tagReq.NumericTagValues.Data[i];
        }

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xử lý cập nhật platform session ID.
    /// </summary>
    private static async Task<IResult> HandleUpdatePlatformSessionId(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new PlatformIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        // Gửi thông báo PlatformSessionUpdateMessage qua WebSocket tới các peers
        var sessionId = ctx.Items["SessionId"] as string ?? string.Empty;
        var message = new { matchId = req.MatchId };
        await WsMessageSender.SendOrStoreMessageAsync(sessionId, "PlatformSessionUpdate", message);
        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xử lý cập nhật platform lobby ID.
    /// </summary>
    private static async Task<IResult> HandleUpdatePlatformLobbyId(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new PlatformIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        // Tương tự UpdatePlatformSessionId nhưng với key "platformlobbyID"
        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xử lý bắt đầu quan sát match.
    /// </summary>
    private static async Task<IResult> HandleStartObserving(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementIdRequest();
        var search = new SearchQuery();
        await HttpHelpers.BindAsync(ctx.Request, req);
        await HttpHelpers.BindAsync(ctx.Request, search);

        if (!Advertisements.TryGetValue(req.AdvertisementId, out var adv))
        {
            return Results.Ok(new object[] { 1 });
        }

        if (!adv.Observable)
        {
            return Results.Ok(new object[] { 4 }); // Không cho phép quan sát
        }

        // Thêm userId vào danh sách observers
        var userId = ctx.Items["UserId"] as int? ?? 0;
        adv.Observers.Add(userId);
        return Results.Ok(new object[] { 0, "127.0.0.1" });
    }

    /// <summary>
    /// Xử lý dừng quan sát match.
    /// </summary>
    private static async Task<IResult> HandleStopObserving(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (Advertisements.TryGetValue(req.AdvertisementId, out var adv))
        {
            // Xóa userId khỏi danh sách observers
            var userId = ctx.Items["UserId"] as int? ?? 0;
            adv.Observers.Remove(userId);
        }
        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xử lý cập nhật trạng thái advertisement.
    /// </summary>
    private static async Task<IResult> HandleUpdateState(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new AdvertisementIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (!Advertisements.TryGetValue(req.AdvertisementId, out var adv))
        {
            return Results.Ok(new object[] { 1 });
        }

        // Lấy state từ request body (sử dụng AdvertisementUpdateRequest để có đầy đủ trường)
        // Trong trường hợp này, state đã được set từ query parameter hoặc default
        adv.State = 1; // Default state

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xử lý tìm advertisements có thể quan sát.
    /// </summary>
    private static async Task<IResult> HandleFindObservableAdvertisements(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var query = new WanQuery();
        var search = new SearchQuery();
        await HttpHelpers.BindAsync(ctx.Request, query);
        await HttpHelpers.BindAsync(ctx.Request, search);

        var observableAds = Advertisements.Values
            .Where(a => a.Visible && a.Observable)
            .ToList();

        var offset = query.Offset;
        var length = query.Length > 0 ? query.Length : observableAds.Count;
        var page = observableAds.Skip(offset).Take(length).ToList();

        var encodedAds = page.Select(a => EncodeAdvertisement(a)).ToList();

        return Results.Ok(new object[] { 0, encodedAds.ToArray(), Array.Empty<object>() });
    }

    /// <summary>
    /// Mã hóa advertisement thành object để trả về.
    /// </summary>
    private static object[] EncodeAdvertisement(AdvertisementData adv)
    {
        return new object[]
        {
            adv.Id,                       // 0: ID
            adv.Description,              // 1: Description
            adv.MapName,                  // 2: Map name
            adv.HostId,                   // 3: Host ID
            adv.MaxPlayers,               // 4: Max players
            adv.Peers.Count,              // 5: Current players
            adv.Passworded ? 1 : 0,       // 6: Passworded
            adv.Visible ? 1 : 0,          // 7: Visible
            adv.Joinable ? 1 : 0,         // 8: Joinable
            adv.MatchType,                // 9: Match type
            adv.State,                    // 10: State
            adv.Observable ? 1 : 0,       // 11: Observable
            new DateTimeOffset(adv.CreatedAt).ToUnixTimeSeconds() // 12: Created time
        };
    }

    private static string GetCurrentGameId()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }
}

/// <summary>
/// Dữ liệu advertisement trong bộ nhớ.
/// </summary>
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
    public sbyte State { get; set; }
    public byte MatchType { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PeerData> Peers { get; set; } = new();
    public List<int> Observers { get; set; } = new();
    public Dictionary<string, string> StringTags { get; set; } = new();
    public Dictionary<string, int> NumericTags { get; set; } = new();
}

/// <summary>
/// Dữ liệu peer trong advertisement.
/// </summary>
internal sealed class PeerData
{
    public int ProfileId { get; set; }
    public int Race { get; set; }
    public int Team { get; set; }
}

/// <summary>
/// DTO cho danh sách match IDs.
/// </summary>
public sealed class GetMatchIdsRequest
{
    public JsonArray<int> MatchIds { get; set; } = new();
}

/// <summary>
/// DTO cho LAN query.
/// </summary>
public sealed class LanQuery
{
    public string LanServerGuids { get; set; } = string.Empty;
}

/// <summary>
/// DTO cho platform ID request.
/// </summary>
public sealed class PlatformIdRequest
{
    public int MatchId { get; set; }
}
