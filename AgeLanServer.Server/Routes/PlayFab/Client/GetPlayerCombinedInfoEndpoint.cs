// Port từ server/internal/routes/playfab/Client/GetPlayerCombinedInfo.go
// Endpoint /PlayFab/Client/GetPlayerCombinedInfo - trả về thông tin kết hợp của người chơi.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Client;

/// <summary>
/// DTO dữ liệu chỉ-đọc người dùng (trống trong LAN server).
/// </summary>
public sealed class UserReadonlyData
{
    // Không có thuộc tính trong LAN server
}

/// <summary>
/// DTO payload kết quả thông tin người chơi.
/// </summary>
public sealed class InfoResultPayload
{
    [JsonPropertyName("UserInventory")]
    public List<object> UserInventory { get; set; } = new();

    [JsonPropertyName("UserDataVersion")]
    public int UserDataVersion { get; set; }

    [JsonPropertyName("UserReadOnlyData")]
    public UserReadonlyData UserReadOnlyData { get; set; } = new();

    [JsonPropertyName("UserReadOnlyDataVersion")]
    public int UserReadOnlyDataVersion { get; set; }

    [JsonPropertyName("CharacterInventories")]
    public List<object> CharacterInventories { get; set; } = new();
}

/// <summary>
/// DTO yêu cầu cho GetPlayerCombinedInfo.
/// </summary>
public sealed class GetPlayerCombinedInfoRequest
{
    [JsonPropertyName("PlayFabId")]
    public string PlayFabId { get; set; } = string.Empty;

    [JsonPropertyName("InfoResultPayload")]
    public InfoResultPayload InfoResultPayload { get; set; } = new();
}

/// <summary>
/// DTO phản hồi cho GetPlayerCombinedInfo.
/// </summary>
public sealed class GetPlayerCombinedInfoResponse
{
    [JsonPropertyName("PlayFabId")]
    public string PlayFabId { get; set; } = string.Empty;

    [JsonPropertyName("InfoResultPayload")]
    public InfoResultPayload InfoResultPayload { get; set; } = new();
}

/// <summary>
/// Endpoint GetPlayerCombinedInfo - trả về thông tin kết hợp của người chơi.
/// Bao gồm inventory, userData, characterInventories, v.v.
/// Trong LAN server, các danh sách này trả về rỗng.
/// </summary>
public static class GetPlayerCombinedInfoEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu GetPlayerCombinedInfo.
    /// Lấy PlayFabId từ session và trả về thông tin cơ bản.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        // Trong Go: sess := playfab.SessionOrPanic(r)
        // PlayFabId lấy từ session middleware
        var playfabId = ctx.User?.Identity?.Name ?? "0";

        var response = new GetPlayerCombinedInfoResponse
        {
            PlayFabId = playfabId,
            InfoResultPayload = new InfoResultPayload
            {
                UserInventory = new List<object>(),
                CharacterInventories = new List<object>(),
                UserDataVersion = 0,
                UserReadOnlyDataVersion = 0
            }
        };

        await PlayFabResponder.RespondOkAsync(ctx, response);
    }

    /// <summary>
    /// Đăng ký endpoint GetPlayerCombinedInfo.
    /// Route: POST /PlayFab/Client/GetPlayerCombinedInfo
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/GetPlayerCombinedInfo", Handle);
    }
}
