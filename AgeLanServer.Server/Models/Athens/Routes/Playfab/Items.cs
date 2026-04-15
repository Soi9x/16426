using AgeLanServer.Server.Models.Playfab;

namespace AgeLanServer.Server.Models.Athens.Routes.Playfab;

/// <summary>
/// Tiện ích tạo Catalog Items và Inventory Items từ danh sách Blessings.
/// Port từ server/internal/models/athens/routes/playfab/items.go
/// </summary>
public static class ItemsHelper
{
    // Ngày cố định dùng cho các field thời gian của catalog item (theo Go: 2024-05-02T03:34:00.000Z)
    private static readonly string FixedDate = new DateTime(2024, 5, 2, 3, 34, 0, DateTimeKind.Utc)
        .ToString(PlayfabConstants.Iso8601Layout);

    /// <summary>
    /// Tạo tên item theo định dạng: Item_{category}_{effectName}_{rarity}
    /// Nếu category rỗng thì: Item__{effectName}_{rarity}
    /// </summary>
    public static string ItemName(string category, string effectName, int rarity)
    {
        return $"Item_{category}_{effectName}_{rarity}";
    }

    /// <summary>
    /// Thêm một item vào catalogItems và inventoryItems.
    /// </summary>
    private static void AddItem(string name,
        Dictionary<string, CatalogItem> catalogItems,
        List<InventoryItem> inventoryItems)
    {
        // Thêm vào inventory
        inventoryItems.Add(new InventoryItem
        {
            Id = name,
            StackId = "default",
            Amount = 1,
            Type = "catalogItem"
        });

        // Thêm vào catalog
        catalogItems[name] = new CatalogItem
        {
            Id = name,
            Type = "catalogItem",
            AlternateIds = new List<CatalogItemAlternativeId>
            {
                new() { Type = "FriendlyId", Value = name }
            },
            FriendlyId = name,
            Title = new CatalogItemTitle { NEUTRAL = name },
            CreatorEntity = new CatalogItemCreatorEntity
            {
                Id = "C15F9",
                Type = "title",
                TypeString = "title"
            },
            Platforms = new List<object>(),
            Tags = new List<object>(),
            CreationDate = FixedDate,
            LastModifiedDate = FixedDate,
            StartDate = FixedDate,
            Contents = new List<object>(),
            Images = new List<object>(),
            ItemReferences = new List<object>(),
            DeepLinks = new List<object>()
        };
    }

    /// <summary>
    /// Tạo catalog items và inventory items từ danh sách blessings.
    /// Với mỗi blessing và mỗi knownRarity > -1, tạo một item.
    /// Nếu effectName bắt đầu bằng "GrantLegend", tạo thêm một item không có category (Season0).
    /// </summary>
    /// <param name="blessings">Danh sách blessings</param>
    /// <returns>Tuple chứa catalogItems (dictionary) và inventoryItems (list)</returns>
    public static (Dictionary<string, CatalogItem> catalogItems, List<InventoryItem> inventoryItems)
        Items(List<Blessing> blessings)
    {
        var catalogItems = new Dictionary<string, CatalogItem>();
        var inventoryItems = new List<InventoryItem>();

        foreach (var blessing in blessings)
        {
            if (blessing.KnownRarities == null)
                continue;

            foreach (var rarity in blessing.KnownRarities)
            {
                if (rarity > -1)
                {
                    // Tạo item với category "Season0"
                    var itemName = ItemName("Season0", blessing.EffectName, rarity);
                    AddItem(itemName, catalogItems, inventoryItems);

                    // Nếu là legend item, tạo thêm item không category
                    if (blessing.EffectName.StartsWith("GrantLegend"))
                    {
                        var legendItemName = ItemName("", blessing.EffectName, rarity);
                        AddItem(legendItemName, catalogItems, inventoryItems);
                    }
                }
            }
        }

        return (catalogItems, inventoryItems);
    }
}
