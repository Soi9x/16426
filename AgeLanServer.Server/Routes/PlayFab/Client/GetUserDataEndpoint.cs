// Port từ server/internal/routes/playfab/Client/GetUserData.go
// Endpoint /PlayFab/Client/GetUserData - trả về dữ liệu người dùng.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Client;

/// <summary>
/// DTO dữ liệu giá trị cơ bản.
/// </summary>
public sealed class BaseValueData
{
    [JsonPropertyName("Value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("Permission")]
    public string Permission { get; set; } = string.Empty;

    public BaseValueData(string permission, string value)
    {
        Permission = permission;
        Value = value;
    }
}

/// <summary>
/// DTO phản hồi cho GetUserData.
/// </summary>
public sealed class GetUserDataResponse
{
    [JsonPropertyName("Data")]
    public Dictionary<string, object> Data { get; set; } = new();

    [JsonPropertyName("DataVersion")]
    public uint DataVersion { get; set; }
}

/// <summary>
/// Endpoint GetUserData - trả về dữ liệu người dùng cơ bản.
/// Trong LAN server, chỉ trả về RLinkProfileID công khai.
/// </summary>
public static class GetUserDataEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu GetUserData.
    /// Trả về RLinkProfileID với permission "public".
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        // Lấy session từ context (nếu có middleware xử lý)
        var playfabId = ctx.User?.Identity?.Name ?? "0";

        var responseData = new GetUserDataResponse
        {
            Data = new Dictionary<string, object>
            {
                ["RLinkProfileID"] = new BaseValueData("public", playfabId)
            },
            DataVersion = 0
        };

        await PlayFabResponder.RespondOkAsync(ctx, responseData);
    }

    /// <summary>
    /// Đăng ký endpoint GetUserData.
    /// Route: POST /PlayFab/Client/GetUserData
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/GetUserData", Handle);
    }
}
