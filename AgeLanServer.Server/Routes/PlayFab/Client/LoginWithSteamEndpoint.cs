// Port từ server/internal/routes/playfab/Client/LoginWithSteam.go
// Endpoint /PlayFab/Client/LoginWithSteam - đăng nhập bằng Steam ticket.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Models;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Client;

/// <summary>
/// DTO yêu cầu cho LoginWithSteam.
/// </summary>
public sealed class LoginWithSteamRequest
{
    [JsonPropertyName("SteamTicket")]
    public string SteamTicket { get; set; } = string.Empty;
}

/// <summary>
/// Endpoint LoginWithSteam - đăng nhập bằng Steam ticket.
/// Giải mã Steam ticket để lấy Steam ID, tạo session và trả về phản hồi login.
/// </summary>
public static class LoginWithSteamEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu LoginWithSteam.
    /// Giải mã Steam ticket để lấy Steam ID, tạo session, và trả về phản hồi login đầy đủ.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        var req = new LoginWithSteamRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);

        if (!bound || string.IsNullOrEmpty(req.SteamTicket))
        {
            await PlayFabResponder.RespondBadRequestAsync(ctx);
            return;
        }

        // Giải mã Steam ticket để lấy Steam ID
        var steamId = ParseSteamId(req.SteamTicket);
        if (string.IsNullOrEmpty(steamId))
        {
            await PlayFabResponder.RespondBadRequestAsync(ctx);
            return;
        }

        // Tạo phản hồi login
        var response = BuildLoginResponse(steamId);

        await PlayFabResponder.RespondOkAsync(ctx, response);
    }

    /// <summary>
    /// Giải mã Steam ticket để lấy Steam ID.
    /// Steam ticket thường là hex string. Trong LAN server, đơn giản dùng trực tiếp giá trị.
    /// </summary>
    private static string? ParseSteamId(string steamTicket)
    {
        // Cố gắng parse như hex string
        try
        {
            // Nếu là hex hợp lệ, dùng luôn làm steamId
            if (steamTicket.Length >= 8)
            {
                return steamTicket;
            }
        }
        catch
        {
            // Không parse được
        }
        return null;
    }

    /// <summary>
    /// Xây dựng đối tượng phản hồi login.
    /// </summary>
    private static LoginWithCustomIdResponse BuildLoginResponse(string steamId)
    {
        var now = DateTime.UtcNow;
        var sessionTicket = steamId;

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
    /// Đăng ký endpoint LoginWithSteam.
    /// Route: POST /PlayFab/Client/LoginWithSteam
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/LoginWithSteam", Handle);
    }
}
