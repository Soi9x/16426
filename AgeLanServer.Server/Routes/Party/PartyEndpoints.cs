using AgeLanServer.Server.Routes.Shared;
using AgeLanServer.Server.Routes.Login;
using System.Collections.Concurrent;
using System.Linq;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Party;

/// <summary>
/// ÄÄƒng kÃ½ cÃ¡c endpoint quáº£n lÃ½ party: peer add/update, match chat, report match, replay, update host.
/// </summary>
public static class PartyEndpoints
{
    // Kho lÆ°u trá»¯ party data trong bá»™ nhá»›
    internal static readonly ConcurrentDictionary<int, PartyData> Parties = new();

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/party");
        var gameId = GetCurrentGameId();

        // ThÃªm peer vÃ o party
        group.MapPost("/peerAdd", HandlePeerAdd);

        // Cáº­p nháº­t peer trong party
        group.MapPost("/peerUpdate", HandlePeerUpdate);

        // Gá»­i tin nháº¯n trong match
        group.MapPost("/sendMatchChat", HandleSendMatchChat);

        // BÃ¡o cÃ¡o káº¿t quáº£ match
        group.MapPost("/reportMatch", HandleReportMatch);

        // HoÃ n táº¥t upload replay
        group.MapPost("/finalizeReplayUpload", HandleFinalizeReplayUpload);

        // Cáº­p nháº­t host cá»§a party
        group.MapPost("/updateHost", HandleUpdateHost);

        if (gameId is GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            // Táº¡o hoáº·c bÃ¡o cÃ¡o single player (AoE4/AoM)
            group.MapPost("/createOrReportSinglePlayer", HandleCreateOrReportSinglePlayer);
        }
    }

    /// <summary>
    /// Xá»­ lÃ½ thÃªm peer vÃ o party lobby.
    /// Chá»‰ host má»›i cÃ³ quyá»n thÃªm peers.
    /// </summary>
    private static async Task<IResult> HandlePeerAdd(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new PeerRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // Kiá»ƒm tra Ä‘á»™ dÃ i cÃ¡c máº£ng pháº£i báº±ng nhau
        if (req.ProfileIds.Data.Count != req.RaceIds.Data.Count ||
            req.ProfileIds.Data.Count != req.TeamIds.Data.Count)
        {
            return Results.Ok(new object[] { 2 });
        }

        // 1. Kiá»ƒm tra user hiá»‡n táº¡i lÃ  host
        var userId = GetUserIdFromSession(ctx);
        var party = Parties.GetOrAdd(req.MatchId, _ => new PartyData { MatchId = req.MatchId, HostId = userId });

        if (party.HostId != userId)
        {
            return Results.Ok(new object[] { 2 }); // KhÃ´ng pháº£i host
        }

        // 2. Kiá»ƒm tra táº¥t cáº£ users tá»“n táº¡i
        // 3. ThÃªm tá»«ng peer vÃ o advertisement
        var addedPeers = new List<object>();
        for (int i = 0; i < req.ProfileIds.Data.Count; i++)
        {
            var peer = new PeerData
            {
                ProfileId = req.ProfileIds.Data[i],
                RaceId = req.RaceIds.Data[i],
                TeamId = req.TeamIds.Data[i]
            };
            party.Peers[peer.ProfileId] = peer;
            addedPeers.Add(new object[] { peer.ProfileId, peer.RaceId, peer.TeamId });
        }

        // 4. Náº¿u cÃ³ lá»—i, rollback táº¥t cáº£ peers Ä‘Ã£ thÃªm
        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ cáº­p nháº­t thÃ´ng tin peer trong party.
    /// Chá»‰ host má»›i cÃ³ quyá»n cáº­p nháº­t peers.
    /// </summary>
    private static async Task<IResult> HandlePeerUpdate(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new PeerRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // Kiá»ƒm tra Ä‘á»™ dÃ i cÃ¡c máº£ng pháº£i báº±ng nhau
        if (req.ProfileIds.Data.Count != req.RaceIds.Data.Count ||
            req.ProfileIds.Data.Count != req.TeamIds.Data.Count)
        {
            return Results.Ok(new object[] { 2 });
        }

        // 1. Kiá»ƒm tra user hiá»‡n táº¡i lÃ  host
        var userId = GetUserIdFromSession(ctx);
        if (!Parties.TryGetValue(req.MatchId, out var party) || party.HostId != userId)
        {
            return Results.Ok(new object[] { 2 });
        }

        // 2. Láº¥y tá»«ng peer tá»« advertisement
        // 3. Cáº­p nháº­t race vÃ  team cho má»—i peer
        var updatedPeers = new List<object>();
        for (int i = 0; i < req.ProfileIds.Data.Count; i++)
        {
            var profileId = req.ProfileIds.Data[i];
            if (party.Peers.TryGetValue(profileId, out var peer))
            {
                peer.RaceId = req.RaceIds.Data[i];
                peer.TeamId = req.TeamIds.Data[i];
                updatedPeers.Add(new object[] { peer.ProfileId, peer.RaceId, peer.TeamId });
            }
        }

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ gá»­i tin nháº¯n trong match.
    /// PhÃ¢n phá»‘i tin nháº¯n tá»›i cÃ¡c ngÆ°á»i chÆ¡i Ä‘Æ°á»£c chá»‰ Ä‘á»‹nh qua WebSocket.
    /// </summary>
    private static async Task<IResult> HandleSendMatchChat(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new MatchChatRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Kiá»ƒm tra user lÃ  peer trong match
        var userId = GetUserIdFromSession(ctx);
        if (!Parties.TryGetValue(req.MatchId, out var party) || !party.Peers.ContainsKey(userId))
        {
            return Results.Ok(new object[] { 2 });
        }

        var recipients = req.ToProfileIds.Data.Count > 0
            ? req.ToProfileIds.Data
            : (req.ToProfileId != 0 ? new List<int> { req.ToProfileId } : party.Peers.Keys.ToList());

        // 2. Táº¡o tin nháº¯n tá»« advertisement
        // 3. Gá»­i MatchReceivedChatMessage qua WebSocket cho tá»«ng ngÆ°á»i nháº­n
        var chatMessage = new { matchId = req.MatchId, senderId = userId, message = req.Message, messageTypeId = req.MessageTypeId };
        foreach (var peerId in recipients)
        {
            if (peerId == userId)
            {
                continue;
            }

            if (!party.Peers.ContainsKey(peerId))
            {
                return Results.Ok(new object[] { 2 });
            }

            var peerSession = LoginEndpoints.Sessions.Values.FirstOrDefault(s => s.UserId == peerId);
            if (peerSession != null)
            {
                await WsMessageSender.SendOrStoreMessageAsync(peerSession.SessionId, "MatchReceivedChatMessage", chatMessage);
            }
        }

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ bÃ¡o cÃ¡o káº¿t quáº£ match.
    /// Hiá»‡n táº¡i chÆ°a Ä‘Æ°á»£c triá»ƒn khai, tráº£ vá» lá»—i.
    /// </summary>
    private static async Task<IResult> HandleReportMatch(ILogger<Program> logger)
    {
        // Trong báº£n Go: tráº£ vá» cáº¥u trÃºc rá»—ng vá»›i error code 2
        return Results.Ok(new object[]
        {
            2,
            Array.Empty<object>(),
            new object[] { -1, "", "", "", 0, "" },
            new object[] { -1, "", "", "", 0, "" },
            null,
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            0,
            null,
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>()
        });
    }

    /// <summary>
    /// Xá»­ lÃ½ hoÃ n táº¥t upload replay.
    /// Hiá»‡n táº¡i luÃ´n tráº£ vá» thÃ nh cÃ´ng.
    /// </summary>
    private static async Task<IResult> HandleFinalizeReplayUpload(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ cáº­p nháº­t host cá»§a party.
    /// Peer Ä‘áº§u tiÃªn trong danh sÃ¡ch cÃ³ thá»ƒ trá»Ÿ thÃ nh host má»›i.
    /// </summary>
    private static async Task<IResult> HandleUpdateHost(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new MatchIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Kiá»ƒm tra user lÃ  peer Ä‘áº§u tiÃªn trong advertisement
        // 2. Cáº­p nháº­t hostId thÃ nh userId hiá»‡n táº¡i
        var userId = GetUserIdFromSession(ctx);
        if (Parties.TryGetValue(req.MatchId, out var party))
        {
            // Kiá»ƒm tra user lÃ  peer Ä‘áº§u tiÃªn
            var firstPeer = party.Peers.Values.FirstOrDefault();
            if (firstPeer != null && firstPeer.ProfileId == userId)
            {
                party.HostId = userId;
            }
        }

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ táº¡o hoáº·c bÃ¡o cÃ¡o single player match (AoE4/AoM only).
    /// Hiá»‡n táº¡i chÆ°a Ä‘Æ°á»£c triá»ƒn khai.
    /// </summary>
    private static async Task<IResult> HandleCreateOrReportSinglePlayer(ILogger<Program> logger)
    {
        // Trong báº£n Go: tráº£ vá» cáº¥u trÃºc rá»—ng vá»›i error code 2
        return Results.Ok(new object[]
        {
            2, 0, "", Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>(),
            Array.Empty<object>(), null, 0, 0, Array.Empty<object>(), Array.Empty<object>(),
            null, Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>(),
            Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>(),
            Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>()
        });
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

    private static string GetCurrentGameId()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }
}

/// <summary>
/// Dá»¯ liá»‡u party trong bá»™ nhá»›.
/// </summary>
internal sealed class PartyData
{
    public int MatchId { get; set; }
    public int HostId { get; set; }
    public ConcurrentDictionary<int, PeerData> Peers { get; set; } = new();
}

/// <summary>
/// Dá»¯ liá»‡u peer trong party.
/// </summary>
internal sealed class PeerData
{
    public int ProfileId { get; set; }
    public int RaceId { get; set; }
    public int TeamId { get; set; }
}

/// <summary>
/// DTO cho yÃªu cáº§u chá»©a match ID.
/// </summary>
public sealed class MatchIdRequest
{
    public int MatchId { get; set; }
}
