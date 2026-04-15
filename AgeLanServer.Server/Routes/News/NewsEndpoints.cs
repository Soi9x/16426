using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.News;

/// <summary>
/// Đăng ký endpoint quản lý news.
/// </summary>
public static class NewsEndpoints
{
    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/news");

        // Lấy tin tức
        group.MapGet("/getNews", HandleGetNews);
    }

    /// <summary>
    /// Xử lý lấy tin tức.
    /// Hiện tại trả về cấu trúc rỗng.
    /// </summary>
    private static async Task<IResult> HandleGetNews(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>() });
    }
}
