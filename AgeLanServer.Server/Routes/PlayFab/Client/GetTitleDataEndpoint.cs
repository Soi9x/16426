// Port từ server/internal/routes/playfab/Client/GetTitleData.go
// Endpoint /PlayFab/Client/GetTitleData - trả về dữ liệu cấu hình CDN.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Client;

/// <summary>
/// DTO dữ liệu cho GetTitleData response.
/// </summary>
public sealed class GetTitleDataResponseData
{
    [JsonPropertyName("CdnUrl")]
    public string CdnUrl { get; set; } = string.Empty;

    [JsonPropertyName("CdnPathConfig")]
    public string CdnPathConfig { get; set; } = string.Empty;
}

/// <summary>
/// DTO phản hồi cho GetTitleData.
/// Lưu ý: PlayFab API wrapper Data bên trong như một object với các key là tên field.
/// </summary>
public sealed class GetTitleDataResponse
{
    [JsonPropertyName("Data")]
    public GetTitleDataResponseData Data { get; set; } = new();
}

/// <summary>
/// Endpoint GetTitleData - trả về cấu hình CDN URL và path.
/// Client dùng thông tin này để tải các tài nguyên game từ CDN nội bộ.
/// </summary>
public static class GetTitleDataEndpoint
{
    /// <summary>
    /// Hậu tố URL cho CDN static files.
    /// Tương đương playfab.StaticSuffix trong Go.
    /// </summary>
    private const string StaticSuffix = "/PlayFab/Static/";

    /// <summary>
    /// Tên cấu hình CDN path.
    /// Tương đương playfab.StaticConfig trong Go.
    /// </summary>
    private const string StaticConfig = "static";

    /// <summary>
    /// Xử lý yêu cầu GetTitleData.
    /// Xây dựng CDN URL từ request.Host và trả về cấu hình CDN path.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        var host = ctx.Request.Host.HasValue ? ctx.Request.Host.Value : "localhost";
        // Đảm bảo không có schema trong URL xây dựng
        var cleanHost = host.Contains("://") ? host.Split("://")[1] : host;

        var responseData = new GetTitleDataResponseData
        {
            CdnUrl = $"https://{cleanHost}{StaticSuffix}",
            CdnPathConfig = StaticConfig
        };

        var response = new GetTitleDataResponse
        {
            Data = responseData
        };
        await PlayFabResponder.RespondOkAsync(ctx, response);
    }

    /// <summary>
    /// Đăng ký endpoint GetTitleData.
    /// Route: POST /PlayFab/Client/GetTitleData
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/GetTitleData", Handle);
    }
}
