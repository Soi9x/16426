// Port từ server/internal/routes/playfab/Client/LoginWithCustomID.go
// Endpoint /PlayFab/Client/LoginWithCustomID - đăng nhập bằng Custom ID.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Models;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Client;

/// <summary>
/// DTO yêu cầu cho LoginWithCustomID.
/// </summary>
public sealed class LoginWithCustomIdRequest
{
    [JsonPropertyName("CustomId")]
    public string CustomId { get; set; } = string.Empty;
}

/// <summary>
/// DTO entity trong phản hồi login.
/// </summary>
public sealed class EntityResponse
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("TypeString")]
    public string TypeString { get; set; } = string.Empty;
}

/// <summary>
/// DTO entity token trong phản hồi login.
/// </summary>
public sealed class EntityTokenResponse
{
    [JsonPropertyName("EntityToken")]
    public string EntityToken { get; set; } = string.Empty;

    [JsonPropertyName("TokenExpiration")]
    public string TokenExpiration { get; set; } = string.Empty;

    [JsonPropertyName("Entity")]
    public EntityResponse Entity { get; set; } = new();
}

/// <summary>
/// DTO cấu hình người dùng trong phản hồi login.
/// </summary>
public sealed class SettingsForUserResponse
{
    [JsonPropertyName("NeedsAttribution")]
    public bool NeedsAttribution { get; set; }

    [JsonPropertyName("GatherDeviceInfo")]
    public bool GatherDeviceInfo { get; set; }

    [JsonPropertyName("GatherFocusInfo")]
    public bool GatherFocusInfo { get; set; }
}

/// <summary>
/// DTO thông tin treatment assignment trong phản hồi login.
/// </summary>
public sealed class TreatmentAssignmentResponse
{
    [JsonPropertyName("Variants")]
    public List<object> Variants { get; set; } = new();

    [JsonPropertyName("Variables")]
    public List<object> Variables { get; set; } = new();
}

/// <summary>
/// DTO phản hồi cơ bản cho login endpoints.
/// </summary>
public sealed class LoginWithCustomIdResponse
{
    [JsonPropertyName("SessionTicket")]
    public string SessionTicket { get; set; } = string.Empty;

    [JsonPropertyName("PlayFabId")]
    public string PlayFabId { get; set; } = string.Empty;

    [JsonPropertyName("NewlyCreated")]
    public bool NewlyCreated { get; set; }

    [JsonPropertyName("SettingsForUser")]
    public SettingsForUserResponse SettingsForUser { get; set; } = new();

    [JsonPropertyName("LastLoginTime")]
    public string LastLoginTime { get; set; } = string.Empty;

    [JsonPropertyName("EntityToken")]
    public EntityTokenResponse EntityToken { get; set; } = new();

    [JsonPropertyName("TreatmentAssignment")]
    public TreatmentAssignmentResponse TreatmentAssignment { get; set; } = new();

    // infoResultPayload (gộp trong LoginWithCustomID)
    [JsonPropertyName("UserInventory")]
    public List<object> UserInventory { get; set; } = new();

    [JsonPropertyName("CharacterInventories")]
    public List<object> CharacterInventories { get; set; } = new();
}

/// <summary>
/// Endpoint LoginWithCustomID - đăng nhập bằng Custom ID (user ID số nguyên).
/// Tạo session mới cho người dùng và trả về session ticket, entity token, v.v.
/// </summary>
public static class LoginWithCustomIdEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu LoginWithCustomID.
    /// Parse CustomId thành userId (int32), tạo session, và trả về phản hồi login đầy đủ.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        var req = new LoginWithCustomIdRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);

        if (!bound || string.IsNullOrEmpty(req.CustomId))
        {
            await PlayFabResponder.RespondBadRequestAsync(ctx);
            return;
        }

        // Parse CustomId thành userId
        if (!int.TryParse(req.CustomId, out var userId))
        {
            await PlayFabResponder.RespondBadRequestAsync(ctx);
            return;
        }

        // Tạo phản hồi login
        var response = BuildLoginResponse(userId, ctx.RequestServices);

        await PlayFabResponder.RespondOkAsync(ctx, response);
    }

    /// <summary>
    /// Xây dựng đối tượng phản hồi login.
    /// </summary>
    private static LoginWithCustomIdResponse BuildLoginResponse(int userId, IServiceProvider serviceProvider)
    {
        var now = DateTime.UtcNow;
        var sessionTicket = userId.ToString();

        return new LoginWithCustomIdResponse
        {
            SessionTicket = sessionTicket,
            PlayFabId = sessionTicket,
            NewlyCreated = true,
            SettingsForUser = new SettingsForUserResponse
            {
                NeedsAttribution = false,
                GatherDeviceInfo = true,
                GatherFocusInfo = true
            },
            // Ngày cố định như trong bản Go
            LastLoginTime = PlayFabDateFormats.FormatDate(new DateTime(2025, 11, 12, 3, 34, 0, DateTimeKind.Utc)),
            EntityToken = new EntityTokenResponse
            {
                EntityToken = sessionTicket,
                TokenExpiration = PlayFabDateFormats.FormatDate(now.AddDays(1)),
                Entity = new EntityResponse
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "title_player_account",
                    TypeString = "title_player_account"
                }
            },
            TreatmentAssignment = new TreatmentAssignmentResponse
            {
                Variants = new List<object>(),
                Variables = new List<object>()
            },
            UserInventory = new List<object>(),
            CharacterInventories = new List<object>()
        };
    }

    /// <summary>
    /// Đăng ký endpoint LoginWithCustomID.
    /// Route: POST /PlayFab/Client/LoginWithCustomID
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/LoginWithCustomID", Handle);
    }
}
