using System.Text.Json;
using System.Web;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Cloud;

/// <summary>
/// Đăng ký các endpoint quản lý cloud: get file URL, get temp credentials.
/// </summary>
public static class CloudEndpoints
{
    // Danh sách game cần bỏ qua headers x-ms-meta-filename và x-ms-meta-ContentLength
    // Tương đương logic trong Go: nếu game là age3 hoặc athens thì không set 2 header này
    private static readonly HashSet<string> GamesSkippingMetaHeaders = new()
    {
        AppConstants.GameAoE3,  // "age3"
        AppConstants.GameAoM    // "athens"
    };

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/cloud");

        // Lấy URL file (POST cho AoE3, GET cho AoE2/AoE4)
        group.MapGet("/getFileURL", HandleGetFileUrl);
        group.MapPost("/getFileURL", HandleGetFileUrl);

        // Lấy thông tin xác thực tạm thời
        group.MapGet("/getTempCredentials", HandleGetTempCredentials);
    }

    /// <summary>
    /// Xử lý lấy URL file cloud.
    /// Trả về danh sách file URLs dựa trên tên file được yêu cầu.
    /// </summary>
    private static async Task<IResult> HandleGetFileUrl(HttpContext ctx, ILogger<Program> logger)
    {
        // Parse request body để lấy danh sách file names
        var descriptions = new List<object>();

        try
        {
            // Đọc body request
            if (ctx.Request.HasJsonContentType())
            {
                var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
                var jsonDoc = JsonDocument.Parse(body);
                var root = jsonDoc.RootElement;

                // Thử lấy field "names" hoặc duyệt các field
                if (root.TryGetProperty("names", out var namesProp) && namesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var nameElement in namesProp.EnumerateArray())
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            var gameTitle = GetCurrentGameTitle(ctx);
                            var url = $"https://{ctx.Request.Host}/cloudfiles/{gameTitle}/{name}";
                            descriptions.Add(new object[] { name, url });
                        }
                    }
                }
            }
        }
        catch
        {
            // Nếu không parse được, trả về empty
        }

        int errorCode = 0;
        return Results.Ok(new object[] { errorCode, descriptions.ToArray() });
    }

    /// <summary>
    /// Xử lý lấy thông tin xác thực tạm thời.
    /// Tạo credential với expiry time và signature cho cloud file access.
    /// </summary>
    private static async Task<IResult> HandleGetTempCredentials(HttpContext ctx, ILogger<Program> logger)
    {
        var key = ctx.Request.Query["key"].ToString();
        var cleanKey = key.StartsWith("/cloudfiles/") ? key.Substring("/cloudfiles/".Length) : key;

        // 1. Lấy cloud files từ resources
        // 2. Tìm file theo key
        // 3. Tạo credential với expiry time
        // 4. Trả về: unix timestamp, signature query string, full key

        var expiryUnix = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var sig = "temp_sig";
        var se = HttpUtility.UrlEncode(DateTimeOffset.UtcNow.AddHours(1).ToString("o"));
        var sv = HttpUtility.UrlEncode("1.0");
        var gameTitle = GetCurrentGameTitle(ctx);
        var sasToken = $"title={gameTitle}&sig={HttpUtility.UrlEncode(sig)}&se={se}&sv={sv}&sp=r&sr=b";

        return Results.Ok(new object[] { 0, expiryUnix, sasToken, key });
    }

    /// <summary>
    /// Helper: Lấy game title từ context.
    /// Ưu tiên lấy từ header X-Game-Title, fallback về "age4".
    /// </summary>
    private static string GetCurrentGameTitle(HttpContext ctx)
    {
        if (ctx.Request.Headers.TryGetValue("X-Game-Title", out var headerTitle) &&
            !string.IsNullOrEmpty(headerTitle))
        {
            return headerTitle.ToString();
        }

        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }

    /// <summary>
    /// Kiểm tra xem game hiện tại có cần bỏ qua các meta headers không.
    /// Với game age3 và athens, không đặt header x-ms-meta-filename và x-ms-meta-ContentLength
    /// khi trả về response cloud files (tương đương logic trong cloudfiles.go của Go).
    /// </summary>
    public static bool ShouldSkipMetaHeaders(string gameTitle)
    {
        return GamesSkippingMetaHeaders.Contains(gameTitle);
    }
}
