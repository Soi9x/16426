// Port từ server/internal/routes/playfab/Client/GetUserReadOnlyData.go
// Endpoint /PlayFab/Client/GetUserReadOnlyData - trả về dữ liệu chỉ-đọc của người dùng.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Client;

/// <summary>
/// DTO yêu cầu cho GetUserReadOnlyData.
/// </summary>
public sealed class GetUserReadOnlyDataRequest
{
    [JsonPropertyName("IfChangedFromDataVersion")]
    public uint? IfChangedFromDataVersion { get; set; }

    [JsonPropertyName("Keys")]
    public List<string> Keys { get; set; } = new();

    [JsonPropertyName("PlayFabId")]
    public string? PlayFabId { get; set; }
}

/// <summary>
/// DTO phản hồi cho GetUserReadOnlyData.
/// </summary>
public sealed class GetUserReadOnlyDataResponse
{
    [JsonPropertyName("DataVersion")]
    public uint DataVersion { get; set; }

    [JsonPropertyName("Data")]
    public Dictionary<string, object?> Data { get; set; } = new();
}

/// <summary>
/// Endpoint GetUserReadOnlyData - trả về dữ liệu chỉ-đọc của người dùng.
/// Hỗ trợ các key: PunchCardProgress, CurrentGauntletProgress, CurrentGauntletLabyrinth,
/// và các Mission_Season0_* (story missions).
/// </summary>
public static class GetUserReadOnlyDataEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu GetUserReadOnlyData.
    /// Trả về dữ liệu cho các key được yêu cầu nếu DataVersion thay đổi.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        var req = new GetUserReadOnlyDataRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);

        if (!bound || req.Keys.Count == 0)
        {
            await PlayFabResponder.RespondBadRequestAsync(ctx);
            return;
        }

        var responseData = new GetUserReadOnlyDataResponse
        {
            DataVersion = 0,
            Data = new Dictionary<string, object?>()
        };

        // Kiểm tra DataVersion thay đổi
        // Nếu IfChangedFromDataVersion null hoặc nhỏ hơn DataVersion hiện tại, trả về dữ liệu
        if (!req.IfChangedFromDataVersion.HasValue || req.IfChangedFromDataVersion.Value < responseData.DataVersion)
        {
            foreach (var key in req.Keys)
            {
                var value = GetValue(key);
                if (value != null)
                {
                    responseData.Data[key] = value;
                }
            }
        }

        await PlayFabResponder.RespondOkAsync(ctx, responseData);
    }

    /// <summary>
    /// Lấy giá trị cho key cụ thể.
    /// Hỗ trợ: PunchCardProgress, CurrentGauntletProgress, CurrentGauntletLabyrinth, Mission_Season0_*.
    /// </summary>
    private static object? GetValue(string key)
    {
        return key switch
        {
            "PunchCardProgress" => new { },
            "CurrentGauntletProgress" => null,
            "CurrentGauntletLabyrinth" => null,
            _ when key.StartsWith("Mission_Season0_") => new { },
            _ => null
        };
    }

    /// <summary>
    /// Đăng ký endpoint GetUserReadOnlyData.
    /// Route: POST /PlayFab/Client/GetUserReadOnlyData
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/GetUserReadOnlyData", Handle);
    }
}
