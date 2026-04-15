// Port từ server/internal/routes/playfab/Catalog/GetItems.go
// Endpoint /PlayFab/Client/GetCatalogItems (hoặc /Catalog/GetItems) - trả về thông tin catalog items.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Catalog;

/// <summary>
/// DTO yêu cầu cho GetItems.
/// </summary>
public sealed class GetItemsRequest
{
    [JsonPropertyName("AlternateIds")]
    public List<object> AlternateIds { get; set; } = new();

    [JsonPropertyName("CustomTags")]
    public object? CustomTags { get; set; }

    [JsonPropertyName("Entity")]
    public object? Entity { get; set; }

    [JsonPropertyName("Ids")]
    public List<string> Ids { get; set; } = new();
}

/// <summary>
/// DTO catalog item - đại diện cho một vật phẩm trong catalog.
/// </summary>
public sealed class CatalogItem
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("ItemClass")]
    public string ItemClass { get; set; } = string.Empty;

    [JsonPropertyName("CatalogVersion")]
    public string CatalogVersion { get; set; } = string.Empty;

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("VirtualCurrencyPrices")]
    public Dictionary<string, uint> VirtualCurrencyPrices { get; set; } = new();
}

/// <summary>
/// DTO phản hồi cho GetItems.
/// </summary>
public sealed class GetItemsResponse
{
    [JsonPropertyName("Items")]
    public List<CatalogItem> Items { get; set; } = new();
}

/// <summary>
/// Endpoint GetItems - trả về thông tin các vật phẩm từ catalog.
/// Client gửi danh sách Ids, server trả về các catalog item tương ứng.
/// </summary>
public static class GetItemsEndpoint
{
    // Kho catalog items mặc định
    private static readonly Dictionary<string, CatalogItem> DefaultCatalog = new()
    {
        // Có thể thêm các items mặc định tại đây
    };

    /// <summary>
    /// Xử lý yêu cầu GetItems.
    /// Lấy các catalog items theo Ids từ game configuration.
    /// Trong LAN server, trả về các items rỗng hoặc cấu hình tĩnh.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        var req = new GetItemsRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);

        if (!bound || req.Ids.Count == 0)
        {
            await PlayFabResponder.RespondBadRequestAsync(ctx);
            return;
        }

        // Lấy catalog items từ game state hoặc configuration
        // Lọc theo req.Ids và trả về
        var catalogItems = new List<CatalogItem>();
        foreach (var id in req.Ids)
        {
            if (DefaultCatalog.TryGetValue(id, out var item))
            {
                catalogItems.Add(item);
            }
        }

        var response = new GetItemsResponse
        {
            Items = catalogItems
        };

        await PlayFabResponder.RespondOkAsync(ctx, response);
    }

    /// <summary>
    /// Đăng ký endpoint GetItems.
    /// Route: POST /PlayFab/Client/GetCatalogItems
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/GetCatalogItems", Handle);
    }
}
