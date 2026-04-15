using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.PlayerReport;

/// <summary>
/// Đăng ký endpoint quản lý player report: report user.
/// </summary>
public static class PlayerReportEndpoints
{
    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/playerreport");

        // Báo cáo người chơi
        group.MapPost("/reportUser", HandleReportUser);
    }

    /// <summary>
    /// Xử lý báo cáo người chơi.
    /// Hiện tại chưa được triển khai, trả về lỗi.
    /// Áp dụng cho AoE2/AoE4/AoM.
    /// </summary>
    private static async Task<IResult> HandleReportUser(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 2, 0 });
    }
}
