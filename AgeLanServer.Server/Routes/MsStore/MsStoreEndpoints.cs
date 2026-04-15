using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.MsStore;

/// <summary>
/// Đăng ký endpoint quản lý MS Store tokens.
/// </summary>
public static class MsStoreEndpoints
{
    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/msstore");

        // Lấy store tokens
        group.MapGet("/getStoreTokens", HandleGetStoreTokens);
    }

    /// <summary>
    /// Xử lý lấy store tokens.
    /// Có thể được dùng để gửi qua platformlogin cho DLCs.
    /// </summary>
    private static async Task<IResult> HandleGetStoreTokens(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, null, "" });
    }
}
