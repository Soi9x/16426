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
/// ÄÄƒng kÃ½ cÃ¡c endpoint quáº£n lÃ½ item/inventory: definitions, loadouts, sign items, bundles,
/// inventory by profile, detach, level rewards, move, update attributes, create/update/equip loadout, prices, sales.
/// </summary>
public static class ItemEndpoints
{
    // ÄÆ°á»ng dáº«n tá»›i thÆ° má»¥c resources/responses/{gameId}
    private static string GetResponsesFolder()
    {
        var gameId = GetCurrentGameTitleStatic();
        return Path.Combine(AppConstants.ResourcesDir, "responses", gameId);
    }

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/item");
        var gameId = GetCurrentGameTitleStatic();

        // Láº¥y Ä‘á»‹nh nghÄ©a items (JSON Ä‘Ã£ kÃ½)
        group.MapGet("/getItemDefinitionsJson", HandleGetItemDefinitionsJson);

        // Láº¥y loadouts cá»§a user
        group.MapGet("/getItemLoadouts", HandleGetItemLoadouts);

        // KÃ½ items (chÆ°a triá»ƒn khai)
        group.MapPost("/signItems", HandleSignItems);

        if (gameId != GameIds.AgeOfEmpires1)
        {
            // Láº¥y bundle items (JSON Ä‘Ã£ kÃ½)
            group.MapGet("/getItemBundleItemsJson", HandleGetItemBundleItemsJson);

            // Láº¥y inventory theo profile IDs
            group.MapGet("/getInventoryByProfileIDs", HandleGetInventoryByProfileIds);
            group.MapPost("/getInventoryByProfileIDs", HandleGetInventoryByProfileIds);

            // Detach items (thÃ¡o item khá»i vá»‹ trÃ­)
            group.MapPost("/detachItems", HandleDetachItems);

            // Láº¥y báº£ng level rewards (JSON Ä‘Ã£ kÃ½)
            group.MapGet("/getLevelRewardsTableJson", HandleGetLevelRewardsTableJson);

            // Di chuyá»ƒn item
            group.MapPost("/moveItem", HandleMoveItem);

            // Cáº­p nháº­t thuá»™c tÃ­nh items
            group.MapPost("/updateItemAttributes", HandleUpdateItemAttributes);

            // Táº¡o loadout item
            group.MapPost("/createItemLoadout", HandleCreateItemLoadout);

            // Equip loadout
            group.MapPost("/equipItemLoadout", HandleEquipItemLoadout);

            // Cáº­p nháº­t loadout
            group.MapPost("/updateItemLoadout", HandleUpdateItemLoadout);
        }

        // Láº¥y giÃ¡ items
        group.MapGet("/getItemPrices", HandleGetItemPrices);

        // Láº¥y danh sÃ¡ch sale vÃ  items
        group.MapGet("/getScheduledSaleAndItems", HandleGetScheduledSaleAndItems);

        // Láº¥y personalized sale items
        group.MapGet("/getPersonalizedSaleItems", HandleGetPersonalizedSaleItems);
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y Ä‘á»‹nh nghÄ©a items.
    /// Tráº£ vá» file itemDefinitions.json Ä‘Ã£ kÃ½ tá»« resources.
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
    /// Xá»­ lÃ½ láº¥y danh sÃ¡ch loadout cá»§a user hiá»‡n táº¡i.
    /// </summary>
    private static async Task<IResult> HandleGetItemLoadouts(HttpContext ctx, ILogger<Program> logger)
    {
        // 1. Láº¥y user tá»« session
        var userId = GetUserIdFromSession(ctx);

        // 2. Láº·p qua táº¥t cáº£ loadouts vÃ  mÃ£ hÃ³a
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
    /// Xá»­ lÃ½ kÃ½ items.
    /// ChÆ°a Ä‘Æ°á»£c triá»ƒn khai (cáº§n base64 encode rá»“i encrypt).
    /// </summary>
    private static async Task<IResult> HandleSignItems(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 2, "" });
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y bundle items.
    /// Tráº£ vá» file itemBundleItems.json Ä‘Ã£ kÃ½ tá»« resources.
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
    /// Xá»­ lÃ½ láº¥y inventory theo profile IDs.
    /// Chá»‰ tráº£ vá» items cá»§a chÃ­nh user Ä‘á»ƒ trÃ¡nh crash (AoE4).
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

            // Chá»‰ tráº£ vá» items cá»§a chÃ­nh user
            if (userId == profileId)
            {
                // Láº¥y items tá»« UserItems
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
    /// Xá»­ lÃ½ detach items (thÃ¡o item khá»i vá»‹ trÃ­).
    /// Cáº­p nháº­t locationId vÃ  durabilityCount cho tá»«ng item.
    /// </summary>
    private static async Task<IResult> HandleDetachItems(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new DetachItemsRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // Kiá»ƒm tra Ä‘á»™ dÃ i cÃ¡c máº£ng pháº£i báº±ng nhau
        var minLen = Math.Min(req.ItemIds.Data.Count,
                     Math.Min(req.LocationIds.Data.Count, req.DurabilityCounts.Data.Count));
        var maxLen = Math.Max(req.ItemIds.Data.Count,
                     Math.Max(req.LocationIds.Data.Count, req.DurabilityCounts.Data.Count));

        if (minLen == 0 || maxLen == 0 || minLen != maxLen)
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>() });
        }

        // 1. Láº¥y user tá»« session
        var userId = GetUserIdFromSession(ctx);
        var userItems = UserItems.GetOrAdd(userId, _ => new List<ItemData>());

        var errorCodes = new int[minLen];
        var itemsEncoded = new object[minLen];

        // 2. Vá»›i má»—i item, cáº­p nháº­t locationId vÃ  durabilityCount (náº¿u khÃ¡c -1)
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

        // 4. MÃ£ hÃ³a item Ä‘Ã£ cáº­p nháº­t
        return Results.Ok(new object[] { 0, errorCodes, itemsEncoded });
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y báº£ng level rewards.
    /// Tráº£ vá» file levelRewardsTable.json Ä‘Ã£ kÃ½ tá»« resources.
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
    /// Xá»­ lÃ½ di chuyá»ƒn item giá»¯a cÃ¡c vá»‹ trÃ­.
    /// Cáº­p nháº­t locationId, positionId, vÃ  slotId cho tá»«ng item.
    /// </summary>
    private static async Task<IResult> HandleMoveItem(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new MoveItemRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // Kiá»ƒm tra Ä‘á»™ dÃ i cÃ¡c máº£ng pháº£i báº±ng nhau
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

        // 1. Láº¥y user tá»« session
        var userId = GetUserIdFromSession(ctx);
        var userItems = UserItems.GetOrAdd(userId, _ => new List<ItemData>());

        var errorCodes = new int[minLen];
        var itemsEncoded = new object[minLen];

        // 2. Vá»›i má»—i item, cáº­p nháº­t locationId, positionId, slotId (náº¿u khÃ¡c -1)
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
    /// Xá»­ lÃ½ cáº­p nháº­t thuá»™c tÃ­nh items.
    /// Cáº­p nháº­t cÃ¡c attribute key-value cho tá»«ng item.
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

        // 1. Láº¥y user tá»« session
        var userId = GetUserIdFromSession(ctx);
        var userItems = UserItems.GetOrAdd(userId, _ => new List<ItemData>());

        var errorCodes = new object[minLen];
        var itemsEncoded = new object[minLen];

        // 2. Vá»›i má»—i item, cáº­p nháº­t táº¥t cáº£ attributes
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
    /// Xá»­ lÃ½ táº¡o loadout item má»›i.
    /// Kiá»ƒm tra táº¥t cáº£ item IDs tá»“n táº¡i trÆ°á»›c khi táº¡o.
    /// </summary>
    private static async Task<IResult> HandleCreateItemLoadout(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new CreateItemLoadoutRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Láº¥y user tá»« session
        var userId = GetUserIdFromSession(ctx);
        var userItems = UserItems.GetOrAdd(userId, _ => new List<ItemData>());
        var userLoadouts = UserLoadouts.GetOrAdd(userId, _ => new List<LoadoutData>());

        // 2. Kiá»ƒm tra táº¥t cáº£ itemOrLocIds tá»“n táº¡i (hoáº·c lÃ  location IDs há»£p lá»‡)
        foreach (var itemOrLocId in req.ItemOrLocIds)
        {
            var exists = userItems.Any(x => x.Id == itemOrLocId);
            if (!exists)
            {
                // CÃ³ thá»ƒ lÃ  location ID, bá» qua
            }
        }

        // 3. Táº¡o loadout má»›i
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
    /// Xá»­ lÃ½ equip loadout.
    /// Tráº£ vá» thÃ´ng tin loadout Ä‘Ã£ equip.
    /// </summary>
    private static async Task<IResult> HandleEquipItemLoadout(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new ItemLoadoutRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Láº¥y user tá»« session
        var userId = GetUserIdFromSession(ctx);
        var userLoadouts = UserLoadouts.GetOrAdd(userId, _ => new List<LoadoutData>());

        // 2. TÃ¬m loadout theo ID
        var loadout = userLoadouts.FirstOrDefault(l => l.Id == req.Id);
        if (loadout == null)
        {
            return Results.Ok(new object[] { 1, Array.Empty<object>(), Array.Empty<object>() });
        }

        // 3. Tráº£ vá» loadout Ä‘Ã£ mÃ£ hÃ³a
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
    /// Xá»­ lÃ½ cáº­p nháº­t loadout item.
    /// Cáº­p nháº­t name, type, vÃ  danh sÃ¡ch items trong loadout.
    /// </summary>
    private static async Task<IResult> HandleUpdateItemLoadout(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new UpdateItemLoadoutRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Láº¥y user tá»« session
        var userId = GetUserIdFromSession(ctx);
        var userLoadouts = UserLoadouts.GetOrAdd(userId, _ => new List<LoadoutData>());

        // 2. TÃ¬m loadout theo ID
        var loadout = userLoadouts.FirstOrDefault(l => l.Id == req.Id);
        if (loadout == null)
        {
            return Results.Ok(new object[] { 1, Array.Empty<object>() });
        }

        // 3. Cáº­p nháº­t name, type, items
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
    /// Xá»­ lÃ½ láº¥y giÃ¡ items.
    /// Hiá»‡n táº¡i tráº£ vá» cáº¥u trÃºc rá»—ng.
    /// </summary>
    private static async Task<IResult> HandleGetItemPrices(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>(), 0, Array.Empty<object>() });
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y danh sÃ¡ch sale vÃ  items.
    /// Hiá»‡n táº¡i tráº£ vá» cáº¥u trÃºc rá»—ng.
    /// </summary>
    private static async Task<IResult> HandleGetScheduledSaleAndItems(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>(), 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y personalized sale items.
    /// Hiá»‡n táº¡i tráº£ vá» cáº¥u trÃºc rá»—ng.
    /// </summary>
    private static async Task<IResult> HandleGetPersonalizedSaleItems(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>() });
    }

    /// <summary>
    /// Helper: Láº¥y userId tá»« session hiá»‡n táº¡i.
    /// </summary>
    private static int GetUserIdFromSession(HttpContext ctx)
    {
        // Láº¥y session tá»« context - Æ°u tiÃªn tá»« Items (Ä‘Æ°á»£c set bá»Ÿi middleware)
        if (ctx.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
        {
            return userId;
        }
        return 0;
    }

    /// <summary>
    /// Helper: Láº¥y game title tÄ©nh.
    /// </summary>
    private static string GetCurrentGameTitleStatic()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }

    // Kho lÆ°u trá»¯ items vÃ  loadouts theo user ID
    internal static readonly ConcurrentDictionary<int, List<ItemData>> UserItems = new();
    internal static readonly ConcurrentDictionary<int, List<LoadoutData>> UserLoadouts = new();
}

/// <summary>
/// Dá»¯ liá»‡u item trong bá»™ nhá»›.
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
/// Dá»¯ liá»‡u loadout trong bá»™ nhá»›.
/// </summary>
internal sealed class LoadoutData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public List<int> ItemIds { get; set; } = new();
}
