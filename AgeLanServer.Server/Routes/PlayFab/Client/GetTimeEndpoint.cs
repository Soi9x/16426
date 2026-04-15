// Port từ server/internal/routes/playfab/Client/GetTime.go
// Endpoint /PlayFab/Client/GetTime - trả về thời gian hiện tại của server.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Client;

/// <summary>
/// DTO phản hồi cho GetTime endpoint.
/// </summary>
public sealed class GetTimeResponse
{
    [JsonPropertyName("Time")]
    public string Time { get; set; } = string.Empty;
}

/// <summary>
/// Endpoint GetTime - trả về thời gian hiện tại của server theo UTC.
/// Client dùng endpoint này để đồng bộ thời gian với server.
/// </summary>
public static class GetTimeEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu GetTime.
    /// Trả về thời gian UTC hiện tại theo định dạng PlayFab.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        var response = new GetTimeResponse
        {
            Time = PlayFabDateFormats.FormatDate(DateTime.UtcNow)
        };
        await PlayFabResponder.RespondOkAsync(ctx, response);
    }

    /// <summary>
    /// Đăng ký endpoint GetTime.
    /// Route: POST /PlayFab/Client/GetTime
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/GetTime", Handle);
    }
}
