using System.Text.Json;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Automatch;

/// <summary>
/// Đăng ký endpoint quản lý automatch maps.
/// </summary>
public static class AutomatchEndpoints
{
    // Đường dẫn tới thư mục resources/responses/{gameId}
    private static string GetResponsesFolder()
    {
        var gameId = GetCurrentGameTitleStatic();
        return Path.Combine(AppConstants.ResourcesDir, "responses", gameId);
    }

    public static void RegisterEndpoints(WebApplication app)
    {
        var gameId = GetCurrentGameTitleStatic();

        if (gameId == GameIds.AgeOfEmpires4)
        {
            var automatchGroup = app.MapGroup("/game/automatch");
            automatchGroup.MapGet("/getAutomatchMap", HandleGetAutomatchMap);
            return;
        }

        var automatch2Group = app.MapGroup("/game/automatch2");
        automatch2Group.MapGet("/getAutomatchMap", HandleGetAutomatchMap);
    }

    /// <summary>
    /// Xử lý lấy danh sách automatch maps.
    /// Trả về file automatchMaps.json từ resources.
    /// </summary>
    private static async Task<IResult> HandleGetAutomatchMap(HttpContext ctx, ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "automatchMaps.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }

    /// <summary>
    /// Helper: Lấy game title tĩnh.
    /// </summary>
    private static string GetCurrentGameTitleStatic()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }
}
