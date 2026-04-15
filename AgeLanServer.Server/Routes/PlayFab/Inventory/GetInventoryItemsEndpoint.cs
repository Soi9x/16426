// Port từ server/internal/routes/playfab/Inventory/GetInventoryItems.go
// Endpoint /PlayFab/Client/GetInventoryItems (hoặc /Inventory/GetInventoryItems) - trả về vật phẩm trong kho.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Inventory;

/// <summary>
/// DTO yêu cầu cho GetInventoryItems.
/// </summary>
public sealed class GetInventoryItemsRequest
{
    [JsonPropertyName("CollectionId")]
    public string? CollectionId { get; set; }

    [JsonPropertyName("ContinuationToken")]
    public string? ContinuationToken { get; set; }

    [JsonPropertyName("Count")]
    public byte Count { get; set; }

    [JsonPropertyName("CustomTags")]
    public object? CustomTags { get; set; }

    [JsonPropertyName("Entity")]
    public object? Entity { get; set; }

    [JsonPropertyName("Filter")]
    public string? Filter { get; set; }
}

/// <summary>
/// DTO inventory item - đại diện cho một vật phẩm trong kho của người chơi.
/// </summary>
public sealed class InventoryItem
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("ItemClass")]
    public string ItemClass { get; set; } = string.Empty;

    [JsonPropertyName("CatalogVersion")]
    public string CatalogVersion { get; set; } = string.Empty;

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("UnitPrice")]
    public uint UnitPrice { get; set; }

    [JsonPropertyName("CustomData")]
    public object? CustomData { get; set; }
}

/// <summary>
/// DTO phản hồi cho GetInventoryItems.
/// </summary>
public sealed class GetInventoryItemsResponse
{
    [JsonPropertyName("Items")]
    public List<InventoryItem> Items { get; set; } = new();

    [JsonPropertyName("ContinuationToken")]
    public string? ContinuationToken { get; set; }

    [JsonPropertyName("ETag")]
    public string ETag { get; set; } = string.Empty;
}

/// <summary>
/// Endpoint GetInventoryItems - trả về danh sách vật phẩm trong kho của người chơi.
/// Hỗ trợ phân trang thông qua ContinuationToken và Count.
/// </summary>
public static class GetInventoryItemsEndpoint
{
    // Kho inventory items mặc định theo user
    private static readonly Dictionary<string, List<InventoryItem>> UserInventories = new()
    {
        // Có thể thêm inventory items mặc định tại đây
    };

    /// <summary>
    /// Xử lý yêu cầu GetInventoryItems.
    /// Lấy danh sách inventory items, hỗ trợ phân trang với ContinuationToken.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        var req = new GetInventoryItemsRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);

        if (!bound)
        {
            await PlayFabResponder.RespondBadRequestAsync(ctx);
            return;
        }

        // Lấy inventory items từ game state hoặc user data
        var inventoryItems = new List<InventoryItem>();

        // Xác định user từ session hoặc entity
        var userId = GetUserIdFromContext(ctx);
        if (!string.IsNullOrEmpty(userId) && UserInventories.TryGetValue(userId, out var userItems))
        {
            inventoryItems = userItems;
        }

        // Xử lý phân trang
        var offset = 0;
        if (!string.IsNullOrEmpty(req.ContinuationToken))
        {
            if (!int.TryParse(req.ContinuationToken, out offset))
            {
                await PlayFabResponder.RespondBadRequestAsync(ctx);
                return;
            }
            inventoryItems = inventoryItems.Skip(offset).ToList();
        }

        // Giới hạn số lượng trả về
        var count = req.Count > 0 ? req.Count : (byte)Math.Min(50, inventoryItems.Count);
        var returnItems = inventoryItems.Take(count).ToList();

        // Tạo continuation token nếu còn dữ liệu
        string? continuationToken = null;
        if (returnItems.Count < inventoryItems.Count)
        {
            continuationToken = (offset + returnItems.Count).ToString();
        }

        var response = new GetInventoryItemsResponse
        {
            Items = returnItems,
            ETag = "1/MQ==",
            ContinuationToken = continuationToken
        };

        await PlayFabResponder.RespondOkAsync(ctx, response);
    }

    /// <summary>
    /// Helper: Lấy userId từ context.
    /// </summary>
    private static string? GetUserIdFromContext(HttpContext ctx)
    {
        // Lấy từ session
        if (ctx.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
        {
            return userId.ToString();
        }

        // Lấy từ Entity token
        if (ctx.Request.Headers.TryGetValue("X-EntityToken", out var token))
        {
            return token.ToString();
        }

        return null;
    }

    /// <summary>
    /// Thêm inventory item cho user.
    /// </summary>
    public static void AddInventoryItem(string userId, InventoryItem item)
    {
        if (!UserInventories.ContainsKey(userId))
        {
            UserInventories[userId] = new List<InventoryItem>();
        }
        UserInventories[userId].Add(item);
    }

    /// <summary>
    /// Đăng ký endpoint GetInventoryItems.
    /// Route: POST /PlayFab/Client/GetInventoryItems
    /// hoặc POST /Inventory/GetInventoryItems
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/GetInventoryItems", Handle);
        app.MapPost("/Inventory/GetInventoryItems", Handle);
    }
}
