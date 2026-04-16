using System.Text.Json;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.CommunityEvent;

/// <summary>
/// Đăng ký các endpoint quản lý community event: available events, leaderboard, stats.
/// </summary>
public static class CommunityEventEndpoints
{
    // Đường dẫn tới thư mục resources/responses/{gameId}
    private static string GetResponsesFolder(string gameTitle)
    {
        return Path.Combine(AppConstants.ResourcesDir, "responses", gameTitle);
    }

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/CommunityEvent");
        var gameId = GetCurrentGameTitle(null);

        // Lấy danh sách community events có sẵn
        group.MapGet("/getAvailableCommunityEvents", HandleGetAvailableCommunityEvents);

        if (gameId is GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            // Lấy leaderboard event (AoE4/AoM only)
            group.MapGet("/getEventLeaderboard", HandleGetEventLeaderboard);

            // Lấy stats event (AoE4/AoM only)
            group.MapGet("/getEventStats", HandleGetEventStats);
        }
    }

    /// <summary>
    /// Xử lý lấy danh sách community events có sẵn.
    /// AoM có cấu trúc response khác với các game khác.
    /// </summary>
    private static async Task<IResult> HandleGetAvailableCommunityEvents(HttpContext ctx, ILogger<Program> logger)
    {
        var gameTitle = GetCurrentGameTitle(ctx);

        // AoM có response đặc biệt
        if (gameTitle == "athens")
        {
            // Lấy community events từ file resources/responses/athens/
            var path = Path.Combine(GetResponsesFolder(gameTitle), "playfab", "public-production", "2", "feature_flags.json");
            if (File.Exists(path))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(path);
                    var jsonDoc = JsonDocument.Parse(content);
                    return Results.Ok(new object[] { 0, jsonDoc.RootElement, Array.Empty<object>() });
                }
                catch
                {
                    // Fallback nếu lỗi
                }
            }

            return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>() });
        }

        // AoE2/AoE4 có thêm các fields bổ sung
        var response = new object[]
        {
            0,
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>()
        };

        return Results.Ok(response);
    }

    /// <summary>
    /// Xử lý lấy leaderboard event (AoE4/AoM only).
    /// Hiện tại chưa được triển khai, trả về lỗi.
    /// </summary>
    private static async Task<IResult> HandleGetEventLeaderboard(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>() });
    }

    /// <summary>
    /// Xử lý lấy stats event (AoE4/AoM only).
    /// Hiện tại chưa được triển khai, trả về lỗi.
    /// </summary>
    private static async Task<IResult> HandleGetEventStats(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>() });
    }

    /// <summary>
    /// Helper: Lấy game title từ context.
    /// Ưu tiên lấy từ header X-Game-Title, fallback về "age4".
    /// </summary>
    private static string GetCurrentGameTitle(HttpContext? ctx)
    {
        if (ctx != null &&
            ctx.Request.Headers.TryGetValue("X-Game-Title", out var headerTitle) &&
            !string.IsNullOrEmpty(headerTitle))
        {
            return headerTitle.ToString();
        }

        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }
}
