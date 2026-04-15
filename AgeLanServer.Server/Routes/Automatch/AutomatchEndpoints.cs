using System.Text.Json;
using AgeLanServer.Common;
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
        // AoE4 dùng /automatch, các game khác dùng /automatch2
        var automatchGroup = app.MapGroup("/game/automatch");
        var automatch2Group = app.MapGroup("/game/automatch2");

        // Lấy danh sách automatch maps
        automatchGroup.MapGet("/getAutomatchMap", HandleGetAutomatchMap);
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
        return "age4";
    }
}
