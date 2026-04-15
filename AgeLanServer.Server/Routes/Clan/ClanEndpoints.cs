using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Clan;

/// <summary>
/// Đăng ký các endpoint quản lý clan: create, find.
/// </summary>
public static class ClanEndpoints
{
    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/clan");

        // Tạo clan mới
        group.MapPost("/create", HandleCreate);

        // Tìm clans
        group.MapGet("/find", HandleFind);
    }

    /// <summary>
    /// Xử lý tạo clan mới.
    /// Hiện tại chưa được triển khai, trả về lỗi.
    /// </summary>
    private static async Task<IResult> HandleCreate(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 2, null, null, Array.Empty<object>() });
    }

    /// <summary>
    /// Xử lý tìm clans.
    /// Hiện tại trả về danh sách rỗng.
    /// FIXME: Client có thể gọi liên tục như thể có phân trang vô hạn.
    /// </summary>
    private static async Task<IResult> HandleFind(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>() });
    }
}
