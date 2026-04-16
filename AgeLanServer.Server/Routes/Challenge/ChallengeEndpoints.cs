using System.Text.Json;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Challenge;

/// <summary>
/// Đăng ký các endpoint quản lý challenge: progress, batched updates.
/// </summary>
public static class ChallengeEndpoints
{
    // Đường dẫn tới thư mục resources/responses/{gameId}
    private static string GetResponsesFolder()
    {
        var gameId = GetCurrentGameTitleStatic();
        return Path.Combine(AppConstants.ResourcesDir, "responses", gameId);
    }

    public static void RegisterEndpoints(WebApplication app)
    {
        // Route với chữ C hoa (legacy)
        var legacyGroup = app.MapGroup("/game/Challenge");

        // Lấy progress challenge
        legacyGroup.MapGet("/getChallengeProgress", HandleGetChallengeProgress);
        legacyGroup.MapPost("/getChallengeProgress", HandleGetChallengeProgress);

        // Lấy danh sách challenges
        legacyGroup.MapGet("/getChallenges", HandleGetChallenges);

        // Route với chữ c thường
        var group = app.MapGroup("/game/challenge");

        // Cập nhật progress
        group.MapPost("/updateProgress", HandleUpdateProgress);

        // Cập nhật progress batched
        group.MapPost("/updateProgressBatched", HandleUpdateProgressBatched);
    }


    /// <summary>
    /// Xử lý lấy danh sách challenges.
    /// Trả về file challenges.json đã ký từ resources.
    /// </summary>
    private static async Task<IResult> HandleGetChallenges(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "challenges.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }

    /// <summary>
    /// Xử lý lấy progress challenge.
    /// Hiện tại trả về mảng rỗng (nghĩa là tất cả đã hoàn thành).
    /// </summary>
    private static async Task<IResult> HandleGetChallengeProgress([FromServices] ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>() });
    }

    /// <summary>
    /// Xử lý cập nhật progress (AoE3 only).
    /// Hiện tại chưa được triển khai, trả về lỗi.
    /// </summary>
    private static async Task<IResult> HandleUpdateProgress([FromServices] ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>() });
    }

    /// <summary>
    /// Xử lý cập nhật progress batched (AoE4/AoM only).
    /// Hiện tại trả về thành công.
    /// </summary>
    private static async Task<IResult> HandleUpdateProgressBatched([FromServices] ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Helper: Lấy game title tĩnh.
    /// </summary>
    private static string GetCurrentGameTitleStatic()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }
}
