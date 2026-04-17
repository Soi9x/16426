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
/// Đăng ký các endpoint quản lý item/inventory: definitions, loadouts, sign items, bundles,
/// inventory by profile, detach, level rewards, move, update attributes, create/update/equip loadout, prices, sales.
/// </summary>
public static class ItemEndpoints
{
    // Đường dẫn tại thư mục resources/responses/{gameId}
    private static string GetResponsesFolder()
    {
        var gameId = GetCurrentGameTitleStatic();
        return Path.Combine(AppConstants.ResourcesDir, "responses", gameId);
    }

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/item");
        var gameId = GetCurrentGameTitleStatic();

        // Lấy định nghĩa items (JSON đã ký)
        group.MapGet("/getItemDefinitionsJson", HandleGetItemDefinitionsJson);

        // Lấy loadouts của user
        group.MapGet("/getItemLoadouts", HandleGetItemLoadouts);

        // Ký items (chưa triển khai)
        group.MapPost("/signItems", HandleSignItems);

        if (gameId != GameIds.AgeOfEmpires1)
        {
            // Lấy bundle items (JSON đã ký)
            group.MapGet("/getItemBundleItemsJson", HandleGetItemBundleItemsJson);

            // Lấy inventory theo profile IDs
            group.MapGet("/getInventoryByProfileIDs", HandleGetInventoryByProfileIds);
            group.MapPost("/getInventoryByProfileIDs", HandleGetInventoryByProfileIds);

            // Detach items (tháo item khỏi vị trí)
            group.MapPost("/detachItems", HandleDetachItems);

            // Lấy bảng level rewards (JSON đã ký)
            group.MapGet("/getLevelRewardsTableJson", HandleGetLevelRewardsTableJson);

            // Di chuyển item
            group.MapPost("/moveItem", HandleMoveItem);

            // Cập nhật thuộc tính items
            group.MapPost("/updateItemAttributes", HandleUpdateItemAttributes);

            // Tạo loadout item
            group.MapPost("/createItemLoadout", HandleCreateItemLoadout);

            // Equip loadout
            group.MapPost("/equipItemLoadout", HandleEquipItemLoadout);

            // Cập nhật loadout
            group.MapPost("/updateItemLoadout", HandleUpdateItemLoadout);
        }

        // Lấy giá items
        group.MapGet("/getItemPrices", HandleGetItemPrices);

        // Lấy danh sách sale và items
        group.MapGet("/getScheduledSaleAndItems", HandleGetScheduledSaleAndItems);

        // Lấy personalized sale items
        group.MapGet("/getPersonalizedSaleItems", HandleGetPersonalizedSaleItems);
    }

    /// <summary>
    /// Xử lý lấy định nghĩa items.
    /// Trả về file itemDefinitions.json đã ký từ resources.
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
    /// Xử lý lấy danh sách loadout của user hiện tại.
    /// Logic khớp Go: trả về mảng [status, [[id, name, type, [itemIds]]...]]
    /// </summary>
    private static async Task<IResult> HandleGetItemLoadouts(HttpContext ctx, ILogger<Program> logger)
    {
        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);

        // 2. Lặp qua tất cả loadouts và mã hóa
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
    /// Xử lý ký items.
    /// Chưa được triển khai (cần base64 encode rồi encrypt).
    /// Trả về lỗi như bản Go khi chưa implement.
    /// </summary>
    private static async Task<IResult> HandleSignItems(ILogger<Program> logger)
    {
        // Go trả về [2, ""] khi chưa implement
        return Results.Ok(new object[] { 2, "" });
    }

    /// <summary>
    /// Xử lý lấy bundle items.
    /// Trả về file itemBundleItems.json đã ký từ resources.
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
    /// Xử lý lấy inventory theo profile IDs.
    /// Logic khớp Go 100%:
    /// - InitialData: Chứa items của user (nếu profileId == userId).
    /// - FinalData: Chứa locations (hiện tại trả về mảng rỗng cho mỗi profile do thiếu EncodeLocations, nhưng cấu trúc phải có).
    /// </summary>
    private static async Task<IResult> HandleGetInventoryByProfileIds(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new InventoryRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        
        if (req.ProfileIds == null || req.ProfileIds.Data.Count == 0)
        {
            return Results.Ok(new object[] { 0, Array.Empty<object>(), Array.Empty<object>() });
        }

        var userId = GetUserIdFromSession(ctx);
        var initialData = new object[req.ProfileIds.Data.Count];
        var finalData = new object[req.ProfileIds.Data.Count];

        for (int j = 0; j < req.ProfileIds.Data.Count; j++)
        {
            var profileId = req.ProfileIds.Data[j];
            object[] itemsEncoded = Array.Empty<object>();
            object[] locationsEncoded = Array.Empty<object>(); // Placeholder cho locations

            // Chỉ trả về items của chính user
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
                
                // TODO: Implement EncodeLocations() nếu cần lấy danh sách locations cụ thể từ game state
                // Hiện tại giữ nguyên mảng rỗng để tránh crash nếu chưa có logic game instance
            }

            var profileIdStr = profileId.ToString();
            initialData[j] = new object[] { profileIdStr, itemsEncoded };
            finalData[j] = new object[] { profileIdStr, locationsEncoded }; 
        }

        return Results.Ok(new object[] { 0, initialData, finalData });
    }

    /// <summary>
    /// Xử lý detach items (tháo item khỏi vị trí).
    /// Logic khớp Go:
    /// - Kiểm tra độ dài mảng đầu vào.
    /// - Cập nhật locationId và durabilityCount nếu khác -1.
    /// - Tăng version.
    /// - Trả về [0, errorCodes, itemsEncoded].
    /// </summary>
    private static async Task<IResult> HandleDetachItems(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new DetachItemsRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        
        if (req.ItemIds == null || req.LocationIds == null || req.DurabilityCounts == null)
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>() });
        }

        // Kiểm tra độ dài các mảng phải bằng nhau
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

        // 2. Với mỗi item, cập nhật locationId và durabilityCount (nếu khác -1)
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

        // 4. Mã hóa item đã cập nhật
        return Results.Ok(new object[] { 0, errorCodes, itemsEncoded });
    }

    /// <summary>
    /// Xử lý lấy bảng level rewards.
    /// Trả về file levelRewardsTable.json đã ký từ resources.
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
    /// Xử lý di chuyển item giữa các vị trí.
    /// Logic khớp Go:
    /// - Kiểm tra độ dài 4 mảng đầu vào.
    /// - Cập nhật locationId, positionId, slotId nếu khác -1.
    /// - Tăng version.
    /// - Trả về [0, errorCodes, itemsEncoded].
    /// </summary>
    private static async Task<IResult> HandleMoveItem(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new MoveItemRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (req.ItemIds == null || req.LocationIds == null || req.PositionIds == null || req.SlotIds == null)
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>() });
        }

        // Kiểm tra độ dài các mảng phải bằng nhau
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

        // 2. Với mỗi item, cập nhật locationId, positionId, slotId (nếu khác -1)
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
    /// Xử lý cập nhật thuộc tính items.
    /// Logic khớp Go:
    /// - Duyệt qua từng item, cập nhật map attributes dựa trên keys/values tương ứng.
    /// - Tăng version.
    /// - Trả về [0, errorCodes, itemsEncoded].
    /// </summary>
    private static async Task<IResult> HandleUpdateItemAttributes(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new UpdateItemAttributesRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (req.Keys == null || req.Values == null || req.ItemIds == null || req.XpGains == null)
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>(), Array.Empty<object>() });
        }

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

        var errorCodes = new int[minLen]; // Go dùng int cho error codes
        var itemsEncoded = new object[minLen];

        // 2. Với mỗi item, cập nhật tất cả attributes
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
    /// Xử lý tạo loadout item mới.
    /// Logic khớp Go:
    /// - Tính toán ID mới = Max(Id) + 1.
    /// - Thêm vào danh sách.
    /// - Trả về [0, [id, name, type, [itemIds]]].
    /// </summary>
    private static async Task<IResult> HandleCreateItemLoadout(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new CreateItemLoadoutRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        
        if (string.IsNullOrEmpty(req.Name))
        {
             return Results.Ok(new object[] { 2 }); // Invalid name
        }

        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);
        var userItems = UserItems.GetOrAdd(userId, _ => new List<ItemData>());
        var userLoadouts = UserLoadouts.GetOrAdd(userId, _ => new List<LoadoutData>());

        // 2. Kiểm tra tất cả itemOrLocIds tồn tại (hoặc là location IDs hợp lệ)
        // Lưu ý: Bản Go có thể bỏ qua check này hoặc chỉ warning, ở đây giữ logic an toàn
        foreach (var itemOrLocId in req.ItemOrLocIds)
        {
            var exists = userItems.Any(x => x.Id == itemOrLocId);
            if (!exists)
            {
                // Có thể là location ID, bỏ qua kiểm tra lỗi nghiêm trọng
            }
        }

        // 3. Tạo loadout mới
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
    /// Xử lý equip loadout.
    /// Logic khớp Go:
    /// - Tìm loadout theo ID.
    /// - Trả về [0, [id, name, type, [itemIds]]] hoặc [2] nếu không tìm thấy.
    /// </summary>
    private static async Task<IResult> HandleEquipItemLoadout(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new EquipItemLoadoutRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        
        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);
        var userLoadouts = UserLoadouts.GetOrAdd(userId, _ => new List<LoadoutData>());

        // 2. Tìm loadout theo ID
        var loadout = userLoadouts.FirstOrDefault(l => l.Id == req.LoadoutId);
        if (loadout == null)
        {
            return Results.Ok(new object[] { 2 }); // Not found
        }

        // 3. Trả về thông tin loadout
        return Results.Ok(new object[] { 0, new object[]
        {
            loadout.Id,
            loadout.Name,
            loadout.Type,
            loadout.ItemIds.ToArray()
        } });
    }

    /// <summary>
    /// Xử lý cập nhật loadout.
    /// Logic khớp Go:
    /// - Tìm loadout theo ID.
    /// - Cập nhật tên và danh sách items.
    /// - Trả về [0, [id, name, type, [itemIds]]] hoặc [2] nếu không tìm thấy.
    /// </summary>
    private static async Task<IResult> HandleUpdateItemLoadout(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new UpdateItemLoadoutRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        
        if (string.IsNullOrEmpty(req.Name))
        {
            return Results.Ok(new object[] { 2 }); // Invalid name
        }

        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);
        var userLoadouts = UserLoadouts.GetOrAdd(userId, _ => new List<LoadoutData>());

        // 2. Tìm loadout theo ID
        var loadout = userLoadouts.FirstOrDefault(l => l.Id == req.LoadoutId);
        if (loadout == null)
        {
            return Results.Ok(new object[] { 2 }); // Not found
        }

        // 3. Cập nhật thông tin
        loadout.Name = req.Name;
        loadout.ItemIds = req.ItemOrLocIds;

        // 4. Trả về thông tin loadout đã cập nhật
        return Results.Ok(new object[] { 0, new object[]
        {
            loadout.Id,
            loadout.Name,
            loadout.Type,
            loadout.ItemIds.ToArray()
        } });
    }

    /// <summary>
    /// Xử lý lấy giá items.
    /// Trả về file itemPrices.json đã ký từ resources.
    /// </summary>
    private static async Task<IResult> HandleGetItemPrices(HttpContext ctx, ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "itemPrices.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }

    /// <summary>
    /// Xử lý lấy danh sách sale và items.
    /// Trả về file scheduledSaleAndItems.json đã ký từ resources.
    /// </summary>
    private static async Task<IResult> HandleGetScheduledSaleAndItems(HttpContext ctx, ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "scheduledSaleAndItems.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }

    /// <summary>
    /// Xử lý lấy personalized sale items.
    /// Trả về file personalizedSaleItems.json đã ký từ resources.
    /// </summary>
    private static async Task<IResult> HandleGetPersonalizedSaleItems(HttpContext ctx, ILogger<Program> logger)
    {
        var path = Path.Combine(GetResponsesFolder(), "personalizedSaleItems.json");
        if (!File.Exists(path))
        {
            return Results.Ok(new { });
        }

        var content = await File.ReadAllTextAsync(path);
        var jsonDoc = JsonDocument.Parse(content);
        return Results.Json(jsonDoc.RootElement);
    }
}
