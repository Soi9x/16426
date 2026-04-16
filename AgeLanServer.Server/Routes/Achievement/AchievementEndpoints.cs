using System.Text.Json;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Achievement;

/// <summary>
/// Đăng ký các endpoint quản lý achievement: get, available, apply offline, grant, sync.
/// </summary>
public static class AchievementEndpoints
{
    // Đường dẫn tới thư mục resources/responses/{gameId}
    private static string GetResponsesFolder()
    {
        var gameId = GetCurrentGameTitleStatic();
        return Path.Combine(AppConstants.ResourcesDir, "responses", gameId);
    }

    public static void RegisterEndpoints(WebApplication app)
    {
        // Route với chữ A hoa (legacy)
        var legacyGroup = app.MapGroup("/game/Achievement");

        // Lấy achievements của user
        legacyGroup.MapGet("/getAchievements", HandleGetAchievements);

        // Lấy danh sách achievements có sẵn
        legacyGroup.MapGet("/getAvailableAchievements", HandleGetAvailableAchievements);

        // Route với chữ a thường
        var group = app.MapGroup("/game/achievement");

        // Cập nhật offline updates
        group.MapPost("/applyOfflineUpdates", HandleApplyOfflineUpdates);

        // Grant achievement (không cho phép)
        group.MapPost("/grantAchievement", HandleGrantAchievement);

        // Sync stats
        group.MapPost("/syncStats", HandleSyncStats);
    }

    /// <summary>
    /// Xử lý lấy achievements của user.
    /// KHÔNG trả về achievements vì sẽ thực sự grant chúng trên Xbox.
    /// </summary>
    private static async Task<IResult> HandleGetAchievements(HttpContext ctx, ILogger<Program> logger)
    {
        // Trong bản Go: trả về userId nhưng mảng achievements rỗng
        var userId = GetUserIdFromSession(ctx);
        return Results.Ok(new object[]
        {
            0,
            new object[]
            {
                new object[]
                {
                    userId,
                    Array.Empty<object>() // KHÔNG trả về achievements
                }
            }
        });
    }

    /// <summary>
    /// Xử lý lấy danh sách achievements có sẵn.
    /// Trả về file achievements.json đã ký từ resources.
    /// </summary>
    private static async Task<IResult> HandleGetAvailableAchievements(HttpContext ctx, ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "achievements.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }

    /// <summary>
    /// Xử lý cập nhật offline updates.
    /// Hiện tại chưa rõ loại updates nào, trả về cấu trúc rỗng.
    /// </summary>
    private static async Task<IResult> HandleApplyOfflineUpdates(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>() });
    }

    /// <summary>
    /// Xử lý grant achievement.
    /// KHÔNG cho phép client claim achievements.
    /// </summary>
    private static async Task<IResult> HandleGrantAchievement(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 2, DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
    }

    /// <summary>
    /// Xử lý sync stats.
    /// Hiện tại chưa rõ chức năng, trả về lỗi.
    /// </summary>
    private static async Task<IResult> HandleSyncStats(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 2 });
    }

    /// <summary>
    /// Helper: Lấy userId từ session hiện tại.
    /// </summary>
    private static int GetUserIdFromSession(HttpContext ctx)
    {
        // Lấy session từ context - ưu tiên từ Items (được set bởi middleware)
        if (ctx.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
        {
            return userId;
        }
        return 0;
    }

    /// <summary>
    /// Helper: Lấy game title tĩnh.
    /// </summary>
    private static string GetCurrentGameTitleStatic()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }
}
