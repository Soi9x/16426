// Port từ server/internal/routes/playfab/Client/UpdateUserTitleDisplayName.go
// Endpoint /PlayFab/Client/UpdateUserTitleDisplayName - cập nhật hiển thị tên người dùng.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Client;

/// <summary>
/// DTO yêu cầu cho UpdateUserTitleDisplayName.
/// </summary>
public sealed class UpdateUserTitleDisplayNameRequest
{
    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// DTO phản hồi cho UpdateUserTitleDisplayName.
/// </summary>
public sealed class UpdateUserTitleDisplayNameResponse
{
    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Endpoint UpdateUserTitleDisplayName - cập nhật tên hiển thị của người dùng.
/// Trong LAN server, endpoint này chỉ đơn giản echo lại tên đã gửi.
/// </summary>
public static class UpdateUserTitleDisplayNameEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu UpdateUserTitleDisplayName.
    /// Bind request, nếu thành công thì trả về tên hiển thị đã cập nhật.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        var req = new UpdateUserTitleDisplayNameRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);

        if (!bound || string.IsNullOrEmpty(req.DisplayName))
        {
            await PlayFabResponder.RespondBadRequestAsync(ctx);
            return;
        }

        var response = new UpdateUserTitleDisplayNameResponse
        {
            DisplayName = req.DisplayName
        };
        await PlayFabResponder.RespondOkAsync(ctx, response);
    }

    /// <summary>
    /// Đăng ký endpoint UpdateUserTitleDisplayName.
    /// Route: POST /PlayFab/Client/UpdateUserTitleDisplayName
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/UpdateUserTitleDisplayName", Handle);
    }
}
