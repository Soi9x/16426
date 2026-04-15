// Port từ server/internal/routes/apiAgeOfEmpires/textmoderation/textmoderation.go
// Endpoint /api/ageofempires/textmoderation - kiểm duyệt nội dung văn bản.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.ApiAgeOfEmpires;

/// <summary>
/// DTO yêu cầu cho TextModeration.
/// </summary>
public sealed class TextModerationRequest
{
    [JsonPropertyName("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    [JsonPropertyName("textContent")]
    public string TextContent { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("textType")]
    public string TextType { get; set; } = string.Empty;
}

/// <summary>
/// DTO phản hồi cho TextModeration.
/// </summary>
public sealed class TextModerationResponse
{
    [JsonPropertyName("filterResult")]
    public string FilterResult { get; set; } = string.Empty;

    [JsonPropertyName("familyFriendlyResult")]
    public string FamilyFriendlyResult { get; set; } = string.Empty;

    [JsonPropertyName("mediumResult")]
    public string MediumResult { get; set; } = string.Empty;

    [JsonPropertyName("matureResult")]
    public string MatureResult { get; set; } = string.Empty;

    [JsonPropertyName("maturePlusResult")]
    public string MaturePlusResult { get; set; } = string.Empty;

    [JsonPropertyName("translationAvailable")]
    public bool TranslationAvailable { get; set; }
}

/// <summary>
/// Endpoint TextModeration - kiểm duyệt nội dung văn bản trong game.
/// Trong LAN server, luôn trả về "Allow" cho tất cả mức độ kiểm duyệt.
/// Chỉ xử lý khi TextType là "SanitisationUsername".
/// </summary>
public static class TextModerationEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu TextModeration.
    /// Nếu TextType là "SanitisationUsername", trả về "Allow" cho tất cả mức độ.
    /// Các TextType khác không được xử lý.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        var req = new TextModerationRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);

        if (!bound)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // Chỉ xử lý cho loại kiểm duyệt tên người dùng
        if (req.TextType == "SanitisationUsername")
        {
            var response = new TextModerationResponse
            {
                FilterResult = "Allow",
                FamilyFriendlyResult = "Allow",
                MediumResult = "Allow",
                MatureResult = "Allow",
                MaturePlusResult = "Allow",
                TranslationAvailable = false
            };
            await HttpHelpers.JsonAsync(ctx.Response, response);
        }
        // Các loại TextType khác: không phản hồi (tương đương bản Go)
    }

    /// <summary>
    /// Đăng ký endpoint TextModeration.
    /// Route: POST /api/ageofempires/textmoderation
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ageofempires/textmoderation", Handle);
    }
}
