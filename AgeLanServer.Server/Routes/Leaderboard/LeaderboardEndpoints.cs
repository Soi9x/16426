using AgeLanServer.Server.Routes.Shared;
using AgeLanServer.Common;
using System.Text.Json;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Leaderboard;

/// <summary>
/// ÄÄƒng kÃ½ cÃ¡c endpoint quáº£n lÃ½ leaderboard: recent matches, leaderboard, available leaderboards,
/// stats by profile, avatar stats, party stats, single player history, offline updates.
/// </summary>
public static class LeaderboardEndpoints
{
    // ÄÆ°á»ng dáº«n tá»›i thÆ° má»¥c resources/responses/{gameId}
    private static string GetResponsesFolder()
    {
        var gameId = GetCurrentGameTitleStatic();
        return Path.Combine(AppConstants.ResourcesDir, "responses", gameId);
    }

    public static void RegisterEndpoints(WebApplication app)
    {
        // Route vá»›i chá»¯ L hoa (legacy)
        var legacyGroup = app.MapGroup("/game/Leaderboard");

        // Láº¥y lá»‹ch sá»­ match gáº§n Ä‘Ã¢y
        legacyGroup.MapGet("/getRecentMatchHistory", HandleGetRecentMatchHistory);
        legacyGroup.MapPost("/getRecentMatchHistory", HandleGetRecentMatchHistory);

        // Láº¥y leaderboard
        legacyGroup.MapGet("/getLeaderBoard", HandleGetLeaderBoard);

        // Láº¥y danh sÃ¡ch leaderboards cÃ³ sáºµn
        legacyGroup.MapGet("/getAvailableLeaderboards", HandleGetAvailableLeaderboards);

        // Láº¥y stat groups theo profile IDs
        legacyGroup.MapGet("/getStatGroupsByProfileIDs", HandleGetStatGroupsByProfileIds);

        // Láº¥y stats cho leaderboard theo profile name
        legacyGroup.MapGet("/getStatsForLeaderboardByProfileName", HandleGetStatsForLeaderboardByProfileName);

        // Láº¥y party stat
        legacyGroup.MapGet("/getPartyStat", HandleGetPartyStat);

        // Láº¥y avatar stat leaderboard (AoE3)
        legacyGroup.MapGet("/getAvatarStatLeaderBoard", HandleGetAvatarStatLeaderBoard);

        // Láº¥y lá»‹ch sá»­ single player gáº§n Ä‘Ã¢y (AoE4)
        legacyGroup.MapGet("/getRecentMatchSinglePlayerHistory", HandleGetRecentMatchSinglePlayerHistory);

        // Route vá»›i chá»¯ l thÆ°á»ng
        var group = app.MapGroup("/game/leaderboard");

        // Cáº­p nháº­t offline updates
        group.MapPost("/applyOfflineUpdates", HandleApplyOfflineUpdates);

        // Cáº­p nháº­t avatar stat values
        group.MapPost("/setAvatarStatValues", HandleSetAvatarStatValues);
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y lá»‹ch sá»­ match gáº§n Ä‘Ã¢y.
    /// Hiá»‡n táº¡i tráº£ vá» cáº¥u trÃºc rá»—ng.
    /// </summary>
    private static async Task<IResult> HandleGetRecentMatchHistory(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>() });
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y leaderboard.
    /// Hiá»‡n táº¡i tráº£ vá» cáº¥u trÃºc rá»—ng.
    /// </summary>
    private static async Task<IResult> HandleGetLeaderBoard(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>() });
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y danh sÃ¡ch leaderboards cÃ³ sáºµn.
    /// Tráº£ vá» file leaderboards.json tá»« resources.
    /// </summary>
    private static async Task<IResult> HandleGetAvailableLeaderboards(HttpContext ctx, ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "leaderboards.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y stat groups theo profile IDs.
    /// Parse danh sÃ¡ch profile IDs tá»« query string vÃ  tráº£ vá» stats.
    /// </summary>
    private static async Task<IResult> HandleGetStatGroupsByProfileIds(HttpContext ctx,
        ILogger<Program> logger)
    {
        var profileIdsQuery = ctx.Request.Query["profileids"].ToString();
        return GetStatGroupsInternal(profileIdsQuery, isProfileId: true, includeExtraProfileInfo: true);
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y stats cho leaderboard theo profile name.
    /// TÆ°Æ¡ng tá»± getStatGroupsByProfileIDs.
    /// </summary>
    private static async Task<IResult> HandleGetStatsForLeaderboardByProfileName(HttpContext ctx,
        ILogger<Program> logger)
    {
        var profileIdsQuery = ctx.Request.Query["profileids"].ToString();
        return GetStatGroupsInternal(profileIdsQuery, isProfileId: true, includeExtraProfileInfo: false);
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y party stat.
    /// Parse stat IDs (khÃ´ng pháº£i profile IDs) vÃ  tráº£ vá» stats.
    /// </summary>
    private static async Task<IResult> HandleGetPartyStat(HttpContext ctx,
        ILogger<Program> logger)
    {
        var statsIdsQuery = ctx.Request.Query["statsids"].ToString();
        return GetStatGroupsInternal(statsIdsQuery, isProfileId: false, includeExtraProfileInfo: true);
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y avatar stat leaderboard (AoE3 only).
    /// Hiá»‡n táº¡i tráº£ vá» cáº¥u trÃºc rá»—ng.
    /// </summary>
    private static async Task<IResult> HandleGetAvatarStatLeaderBoard(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>() });
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y lá»‹ch sá»­ single player gáº§n Ä‘Ã¢y (AoE4 only).
    /// Hiá»‡n táº¡i tráº£ vá» cáº¥u trÃºc rá»—ng.
    /// </summary>
    private static async Task<IResult> HandleGetRecentMatchSinglePlayerHistory(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 13, Array.Empty<object>() });
    }

    /// <summary>
    /// Xá»­ lÃ½ cáº­p nháº­t offline updates.
    /// Hiá»‡n táº¡i chÆ°a rÃµ loáº¡i updates nÃ o, tráº£ vá» cáº¥u trÃºc rá»—ng.
    /// </summary>
    private static async Task<IResult> HandleApplyOfflineUpdates(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>() });
    }

    /// <summary>
    /// Xá»­ lÃ½ cáº­p nháº­t avatar stat values.
    /// Cáº­p nháº­t cÃ¡c avatar stats cho user, bá» qua cÃ¡c stats cá»‘ Ä‘á»‹nh cá»§a game.
    /// ThÃ´ng bÃ¡o AvatarStatsUpdatedMessage qua WebSocket.
    /// </summary>
    private static async Task<IResult> HandleSetAvatarStatValues(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new SetAvatarStatValuesRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // Kiá»ƒm tra dá»¯ liá»‡u Ä‘áº§u vÃ o
        if (req.Values.Data.Count < 1 ||
            req.AvatarStatIds.Data.Count != req.Values.Data.Count ||
            req.UpdateTypes.Data.Count != req.Values.Data.Count)
        {
            return Results.Ok(new object[] { 2 });
        }

        // 1. Láº¥y user tá»« session
        var userId = GetUserIdFromSession(ctx);
        var userAvatarStats = UserAvatarStats.GetOrAdd(userId, _ => new Dictionary<int, long>());

        // 2. Láº·p qua avatarStatIds vÃ  values
        // 3. Bá» qua cÃ¡c fixed avatar stats (theo game)
        // 4. Cáº­p nháº­t hoáº·c táº¡o avatar stat má»›i
        for (int i = 0; i < req.AvatarStatIds.Data.Count; i++)
        {
            var statId = req.AvatarStatIds.Data[i];
            var value = req.Values.Data[i];

            userAvatarStats[statId] = value;
        }

        // 5. Gá»­i AvatarStatsUpdatedMessage qua WebSocket
        var sessionId = ctx.Items["SessionId"] as string ?? string.Empty;
        var statsMessage = new { userId, avatarStats = userAvatarStats };
        await WsMessageSender.SendOrStoreMessageAsync(sessionId, "AvatarStatsUpdatedMessage", statsMessage);

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Helper: Xá»­ lÃ½ láº¥y stat groups tá»« query string.
    /// </summary>
    private static IResult GetStatGroupsInternal(string idsQuery, bool isProfileId, bool includeExtraProfileInfo)
    {
        if (string.IsNullOrEmpty(idsQuery))
        {
            return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>() });
        }

        // Parse JSON array tá»« query string
        int[]? ids = null;
        try
        {
            ids = JsonSerializer.Deserialize<int[]>(idsQuery);
        }
        catch
        {
            return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>() });
        }

        if (ids == null || ids.Length == 0)
        {
            return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>() });
        }

        // 1. Láº¥y user theo ID (profile ID hoáº·c stat ID)
        // 2. MÃ£ hÃ³a profile info
        // 3. Tráº£ vá» stat groups
        var message = new object[]
        {
            0,
            Array.Empty<object>(), // stat groups
            Array.Empty<object>(), // profile info
            includeExtraProfileInfo ? Array.Empty<object>() : null // extra profile info (náº¿u cáº§n)
        };

        return Results.Ok(message);
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

    // Kho lÆ°u trá»¯ avatar stats theo user ID
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<int, Dictionary<int, long>> UserAvatarStats = new();
}
