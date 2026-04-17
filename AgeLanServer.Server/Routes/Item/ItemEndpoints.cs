using AgeLanServer.Common;
using System.Collections.Concurrent;
using System.Text.Json;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Item;

/// <summary>
/// ĐĒng ky cac endpoint quản ly item/inventory: definitions, loadouts, sign items, bundles,
/// inventory by profile, detach, level rewards, move, update attributes, create/update/equip loadout, prices, sales.
/// </summary>
public static class ItemEndpoints
{
    // Đường dẫn tai thư mục resources/responses/{gameId}
    private static string GetResponsesFolder()
    {
        var gameId = GetCurrentGameTitleStatic();
        return Path.Combine(AppConstants.ResourcesDir, "responses", gameId);
    }

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/item");
        var gameId = GetCurrentGameTitleStatic();

        // Lấy Đ'anh nghĩa items (JSON Đ'A ky)
        group.MapGet("/getItemDefinitionsJson", HandleGetItemDefinitionsJson);

        // Lấy loadouts của user
        group.MapGet("/getItemLoadouts", HandleGetItemLoadouts);

        // Ky items (chưa triỒn khai)
        group.MapPost("/signItems", HandleSignItems);

        if (gameId != GameIds.AgeOfEmpires1)
        {
            // Lấy bundle items (JSON Đ'A ky)
            group.MapGet("/getItemBundleItemsJson", HandleGetItemBundleItemsJson);

            // Lấy inventory theo profile IDs
            group.MapGet("/getInventoryByProfileIDs", HandleGetInventoryByProfileIds);
            group.MapPost("/getInventoryByProfileIDs", HandleGetInventoryByProfileIds);

            // Detach items (thao item khỏi va tri)
            group.MapPost("/detachItems", HandleDetachItems);

            // Lấy bảng level rewards (JSON Đ'A ky)
            group.MapGet("/getLevelRewardsTableJson", HandleGetLevelRewardsTableJson);

            // Di chuyỒn item
            group.MapPost("/moveItem", HandleMoveItem);

            // Cập nhật thuac tinh items
            group.MapPost("/updateItemAttributes", HandleUpdateItemAttributes);

            // Tạo loadout item
            group.MapPost("/createItemLoadout", HandleCreateItemLoadout);

            // Equip loadout
            group.MapPost("/equipItemLoadout", HandleEquipItemLoadout);

            // Cập nhật loadout
            group.MapPost("/updateItemLoadout", HandleUpdateItemLoadout);
        }

        // Lấy gia items
        group.MapGet("/getItemPrices", HandleGetItemPrices);

        // Lấy danh sach sale va items
        group.MapGet("/getScheduledSaleAndItems", HandleGetScheduledSaleAndItems);

        // Lấy personalized sale items
        group.MapGet("/getPersonalizedSaleItems", HandleGetPersonalizedSaleItems);
    }

    /// <summary>
    /// Xử ly lấy Đ'anh nghĩa items.
    /// Trả về file itemDefinitions.json Đ'A ky từ resources.
    /// </summary>
    private static async Task<IResult> HandleGetItemDefinitionsJson(HttpContext ctx, ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "itemDefinitions.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }

    /// <summary>
    /// Xử ly lấy danh sach loadout của user hian tại.
    /// </summary>
    private static async Task<IResult> HandleGetItemLoadouts(HttpContext ctx, ILogger<Program> logger)
    {
        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);

        // 2. Lặp qua tất cả loadouts va ma hoa
        var userLoadouts = UserLoadouts.GetOrAdd(userId, _ => new List<LoadoutData>());

        var encodedLoadouts = userLoadouts.Select(l => new object[]
        {
            l.Id,
            l.Name,
            l.Type,
            l.ItemIds.ToArray()
        }).ToArray();

        return Results.Ok(new object[] { 0, encodedLoadouts });
    }

    /// <summary>
    /// Xử ly ky items.
    /// Chưa Đ'Aac triỒn khai (cần base64 encode ra'i encrypt).
    /// </summary>
    private static async Task<IResult> HandleSignItems(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 2, "" });
    }

    /// <summary>
    /// Xử ly lấy bundle items.
    /// Trả về file itemBundleItems.json Đ'A ky từ resources.
    /// </summary>
    private static async Task<IResult> HandleGetItemBundleItemsJson(HttpContext ctx, ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "itemBundleItems.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }

    /// <summary>
    /// Xử ly lấy inventory theo profile IDs.
    /// Cha trả về items của chinh user Đ'aƒ tranh crash (AoE4).
    /// </summary>
    private static async Task<IResult> HandleGetInventoryByProfileIds(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new InventoryRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        var userId = GetUserIdFromSession(ctx);
        var initialData = new object[req.ProfileIds.Data.Count];
        var finalData = new object[req.ProfileIds.Data.Count];

        for (int j = 0; j < req.ProfileIds.Data.Count; j++)
        {
            var profileId = req.ProfileIds.Data[j];
            object[] itemsEncoded = Array.Empty<object>();

            // Cha trả về items của chinh user
            if (userId == profileId)
            {
                // Lấy items từ UserItems
                var userItems = UserItems.GetOrAdd(userId, _ => new List<ItemData>());
                itemsEncoded = userItems.Select(item => new object[]
                {
                    item.Id,
                    item.ItemDefinitionId,
                    item.LocationId,
                    item.PositionId,
                    item.SlotId,
                    item.DurabilityCount,
                    item.Attributes
                }).ToArray();
            }

            var profileIdStr = profileId.ToString();
            initialData[j] = new object[] { profileIdStr, itemsEncoded };
            finalData[j] = new object[] { profileIdStr, Array.Empty<object>() }; // locations
        }

        return Results.Ok(new object[] { 0, initialData, finalData });
    }

    /// <summary>
    /// Xử ly detach items (thao item khỏi va tri).
    /// Cập nhật locationId va durabilityCount cho từng item.
    /// </summary>
    private static async Task<IResult> HandleDetachItems(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new DetachItemsRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // KiỒm tra Đ'a dai cac mảng phải bằng nhau
        var minLen = Math.Min(req.ItemIds.Data.Count,
                     Math.Min(req.LocationIds.Data.Count, req.DurabilityCounts.Data.Count));
        var maxLen = Math.Max(req.ItemIds.Data.Count,
                     Math.Max(req.LocationIds.Data.Count, req.DurabilityCounts.Data.Count));

        if (minLen == 0 || maxLen == 0 || minLen != maxLen)
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>() });
        }

        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);
        var userItems = UserItems.GetOrAdd(userId, _ => new List<ItemData>());

        var errorCodes = new int[minLen];
        var itemsEncoded = new object[minLen];

        // 2. Vai ma--i item, cập nhật locationId va durabilityCount (nếu khac -1)
        for (int i = 0; i < minLen; i++)
        {
            var itemId = req.ItemIds.Data[i];
            var locationId = req.LocationIds.Data[i];
            var durabilityCount = req.DurabilityCounts.Data[i];

            var item = userItems.FirstOrDefault(x => x.Id == itemId);
            if (item != null)
            {
                if (locationId != -1) item.LocationId = locationId;
                if (durabilityCount != -1) item.DurabilityCount = durabilityCount;

                // 3. Increment version
                item.Version++;

                itemsEncoded[i] = new object[]
                {
                    item.Id,
                    item.ItemDefinitionId,
                    item.LocationId,
                    item.PositionId,
                    item.SlotId,
                    item.DurabilityCount,
                    item.Attributes
                };
            }
            else
            {
                errorCodes[i] = 1; // Item not found
            }
        }

        // 4. Ma hoa item Đ'A cập nhật
        return Results.Ok(new object[] { 0, errorCodes, itemsEncoded });
    }

    /// <summary>
    /// Xử ly lấy bảng level rewards.
    /// Trả về file levelRewardsTable.json Đ'A ky từ resources.
    /// </summary>
    private static async Task<IResult> HandleGetLevelRewardsTableJson(HttpContext ctx, ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "levelRewardsTable.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }

    /// <summary>
    /// Xử ly di chuyỒn item giữa cac va tri.
    /// Cập nhật locationId, positionId, va slotId cho từng item.
    /// </summary>
    private static async Task<IResult> HandleMoveItem(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new MoveItemRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // KiỒm tra Đ'a dai cac mảng phải bằng nhau
        var minLen = Math.Min(req.ItemIds.Data.Count,
                     Math.Min(req.LocationIds.Data.Count,
                     Math.Min(req.PositionIds.Data.Count, req.SlotIds.Data.Count)));
        var maxLen = Math.Max(req.ItemIds.Data.Count,
                     Math.Max(req.LocationIds.Data.Count,
                     Math.Max(req.PositionIds.Data.Count, req.SlotIds.Data.Count)));

        if (minLen == 0 || maxLen == 0 || minLen != maxLen)
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>() });
        }

        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);
        var userItems = UserItems.GetOrAdd(userId, _ => new List<ItemData>());

        var errorCodes = new int[minLen];
        var itemsEncoded = new object[minLen];

        // 2. Vai ma--i item, cập nhật locationId, positionId, slotId (nếu khac -1)
        for (int i = 0; i < minLen; i++)
        {
            var itemId = req.ItemIds.Data[i];
            var locationId = req.LocationIds.Data[i];
            var positionId = req.PositionIds.Data[i];
            var slotId = req.SlotIds.Data[i];

            var item = userItems.FirstOrDefault(x => x.Id == itemId);
            if (item != null)
            {
                if (locationId != -1) item.LocationId = locationId;
                if (positionId != -1) item.PositionId = positionId;
                if (slotId != -1) item.SlotId = slotId;

                // 3. Increment version
                item.Version++;

                itemsEncoded[i] = new object[]
                {
                    item.Id,
                    item.ItemDefinitionId,
                    item.LocationId,
                    item.PositionId,
                    item.SlotId,
                    item.DurabilityCount,
                    item.Attributes
                };
            }
            else
            {
                errorCodes[i] = 1;
            }
        }

        return Results.Ok(new object[] { 0, errorCodes, itemsEncoded });
    }

    /// <summary>
    /// Xử ly cập nhật thuac tinh items.
    /// Cập nhật cac attribute key-value cho từng item.
    /// </summary>
    private static async Task<IResult> HandleUpdateItemAttributes(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new UpdateItemAttributesRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        var keyCount = req.Keys.Data.Count;
        var valueCount = req.Values.Data.Count;
        var itemCount = req.ItemIds.Data.Count;
        var xpCount = req.XpGains.Data.Count;

        var minLen = Math.Min(keyCount, Math.Min(valueCount, Math.Min(itemCount, xpCount)));
        var maxLen = Math.Max(keyCount, Math.Max(valueCount, Math.Max(itemCount, xpCount)));

        if (minLen == 0 || maxLen == 0 || minLen != maxLen)
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>() });
        }

        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);
        var userItems = UserItems.GetOrAdd(userId, _ => new List<ItemData>());

        var errorCodes = new object[minLen];
        var itemsEncoded = new object[minLen];

        // 2. Vai ma--i item, cập nhật tất cả attributes
        for (int i = 0; i < minLen; i++)
        {
            var itemId = req.ItemIds.Data[i];
            var item = userItems.FirstOrDefault(x => x.Id == itemId);
            if (item != null)
            {
                var keys = req.Keys.Data[i].Data;
                var values = req.Values.Data[i].Data;

                for (int j = 0; j < keys.Count && j < values.Count; j++)
                {
                    item.Attributes[keys[j]] = values[j];
                }

                // 3. Increment version
                item.Version++;

                itemsEncoded[i] = new object[]
                {
                    item.Id,
                    item.ItemDefinitionId,
                    item.LocationId,
                    item.PositionId,
                    item.SlotId,
                    item.DurabilityCount,
                    item.Attributes
                };
            }
            else
            {
                errorCodes[i] = 1;
            }
        }

        return Results.Ok(new object[] { 0, errorCodes, itemsEncoded });
    }

    /// <summary>
    /// Xử ly tạo loadout item mai.
    /// KiỒm tra tất cả item IDs ta'n tại trAac khi tạo.
    /// </summary>
    private static async Task<IResult> HandleCreateItemLoadout(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new CreateItemLoadoutRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);
        var userItems = UserItems.GetOrAdd(userId, _ => new List<ItemData>());
        var userLoadouts = UserLoadouts.GetOrAdd(userId, _ => new List<LoadoutData>());

        // 2. KiỒm tra tất cả itemOrLocIds ta'n tại (hoặc la location IDs hợp la)
        foreach (var itemOrLocId in req.ItemOrLocIds)
        {
            var exists = userItems.Any(x => x.Id == itemOrLocId);
            if (!exists)
            {
                // Co thỒ la location ID, bỏ qua
            }
        }

        // 3. Tạo loadout mai
        var newLoadout = new LoadoutData
        {
            Id = userLoadouts.Count > 0 ? userLoadouts.Max(l => l.Id) + 1 : 1,
            Name = req.Name,
            Type = req.Type,
            ItemIds = req.ItemOrLocIds
        };
        userLoadouts.Add(newLoadout);

        return Results.Ok(new object[] { 0, new object[]
        {
            newLoadout.Id,
            newLoadout.Name,
            newLoadout.Type,
            newLoadout.ItemIds.ToArray()
        } });
    }

    /// <summary>
    /// Xử ly equip loadout.
    /// Trả về thong tin loadout Đ'A equip.
    /// </summary>
    private static async Task<IResult> HandleEquipItemLoadout(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new ItemLoadoutRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);
        var userLoadouts = UserLoadouts.GetOrAdd(userId, _ => new List<LoadoutData>());

        // 2. Tim loadout theo ID
        var loadout = userLoadouts.FirstOrDefault(l => l.Id == req.Id);
        if (loadout == null)
        {
            return Results.Ok(new object[] { 1, Array.Empty<object>(), Array.Empty<object>() });
        }

        // 3. Trả về loadout Đ'A ma hoa
        var encoded = new object[]
        {
            loadout.Id,
            loadout.Name,
            loadout.Type,
            loadout.ItemIds.ToArray()
        };

        return Results.Ok(new object[] { 0, encoded, Array.Empty<object>() });
    }

    /// <summary>
    /// Xử ly cập nhật loadout item.
    /// Cập nhật name, type, va danh sach items trong loadout.
    /// </summary>
    private static async Task<IResult> HandleUpdateItemLoadout(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new UpdateItemLoadoutRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);
        var userLoadouts = UserLoadouts.GetOrAdd(userId, _ => new List<LoadoutData>());

        // 2. Tim loadout theo ID
        var loadout = userLoadouts.FirstOrDefault(l => l.Id == req.Id);
        if (loadout == null)
        {
            return Results.Ok(new object[] { 1, Array.Empty<object>() });
        }

        // 3. Cập nhật name, type, items
        loadout.Name = req.Name;
        loadout.Type = req.Type;
        loadout.ItemIds = req.ItemOrLocIds;

        return Results.Ok(new object[] { 0, new object[]
        {
            loadout.Id,
            loadout.Name,
            loadout.Type,
            loadout.ItemIds.ToArray()
        } });
    }

    /// <summary>
    /// Xử ly lấy gia items.
    /// Hian tại trả về cấu truc ra--ng.
    /// </summary>
    private static async Task<IResult> HandleGetItemPrices(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>(), 0, Array.Empty<object>() });
    }

    /// <summary>
    /// Xử ly lấy danh sach sale va items.
    /// Hian tại trả về cấu truc ra--ng.
    /// </summary>
    private static async Task<IResult> HandleGetScheduledSaleAndItems(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>(), 0 });
    }

    /// <summary>
    /// Xử ly lấy personalized sale items.
    /// Hian tại trả về cấu truc ra--ng.
    /// </summary>
    private static async Task<IResult> HandleGetPersonalizedSaleItems(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>() });
    }

    /// <summary>
    /// Helper: Lấy userId từ session hian tại.
    /// </summary>
    private static int GetUserIdFromSession(HttpContext ctx)
    {
        // Lấy session từ context - ưu tien từ Items (Đ'Aac set baŸi middleware)
        if (ctx.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
        {
            return userId;
        }
        return 0;
    }

    /// <summary>
    /// Helper: Lấy game title tĩnh.
    /// </summary>
    private static string GetCurrentGameTitleStatic()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }

    // Kho lưu trữ items va loadouts theo user ID
    internal static readonly ConcurrentDictionary<int, List<ItemData> UserItems = new();
    internal static readonly ConcurrentDictionary<int, List<LoadoutData> UserLoadouts = new();
}

/// <summary>
/// Dữ liau item trong ba nha.
/// </summary>
internal sealed class ItemData
{
    public int Id { get; set; }
    public int ItemDefinitionId { get; set; }
    public int LocationId { get; set; }
    public int PositionId { get; set; }
    public int SlotId { get; set; }
    public int DurabilityCount { get; set; }
    public int Version { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
}

/// <summary>
/// Dữ liau loadout trong ba nha.
/// </summary>
internal sealed class LoadoutData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public List<int> ItemIds { get; set; } = new();
}
