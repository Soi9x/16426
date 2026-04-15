using AgeLanServer.Server.Models.Athens.User;
using AgeLanServer.Server.Models.Playfab;

namespace AgeLanServer.Server.Models.Athens.Routes.Playfab;

/// <summary>
/// Cấu hình cột trong mê cung Gauntlet.
/// </summary>
public class ColumnConfig
{
    public string MissionPool { get; set; } = null!;
}

/// <summary>
/// Cấu hình mê cung Gauntlet.
/// </summary>
public class LabyrinthConfig
{
    public List<string> ForGauntletDifficulties { get; set; } = new();
    public List<ColumnConfig> ColumnConfigs { get; set; } = new();
    public string BossMissionPool { get; set; } = null!;
}

/// <summary>
/// Phần thưởng Gauntlet.
/// </summary>
public class GauntletRewards
{
    public List<Blessing> ExcludeFromRegularRewards { get; set; } = new();
    public List<object> PreferredFinalRewards { get; set; } = new();
}

/// <summary>
/// Dữ liệu Gauntlet (thử thách) trong Age of Mythology: Retold.
/// </summary>
public class Gauntlet
{
    public List<LabyrinthConfig> LabyrinthConfigs { get; set; } = new();
    public GauntletRewards Rewards { get; set; } = new();
}

/// <summary>
/// Đọc dữ liệu Gauntlet từ tệp JSON.
/// </summary>
public static class GauntletReader
{
    public static Gauntlet ReadGauntlet()
    {
        var path = Path.Combine(PlayfabConstants.BaseDir, "public-production", "2", "gauntlet.json");
        // JSON deserialization sẽ được thực hiện ở đây
        return new Gauntlet();
    }
}

/// <summary>
/// Nhóm nhiệm vụ Gauntlet.
/// </summary>
public class GauntletMissionPool
{
    public string Name { get; set; } = null!;
    public List<ChallengeMission> Missions { get; set; } = new();
}

/// <summary>
/// Tập hợp các nhóm nhiệm vụ Gauntlet.
/// </summary>
public class GauntletMissionPools : List<GauntletMissionPool>
{
}

/// <summary>
/// Đọc dữ liệu nhóm nhiệm vụ Gauntlet từ tệp JSON.
/// </summary>
public static class GauntletMissionPoolsReader
{
    public static GauntletMissionPools ReadGauntletMissionPools()
    {
        var path = Path.Combine(PlayfabConstants.BaseDir, "public-production", "2", "gauntlet_mission_pools.json");
        // JSON deserialization sẽ được thực hiện ở đây
        return new GauntletMissionPools();
    }
}

/// <summary>
/// Phước lành (Blessing) trong game.
/// </summary>
public class Blessing
{
    public string EffectName { get; set; } = null!;
    public List<int> KnownRarities { get; set; } = new();
}

/// <summary>
/// JSON chứa danh sách phước lành.
/// </summary>
public class BlessingsJson
{
    public List<Blessing> KnownBlessings { get; set; } = new();
}

/// <summary>
/// Đọc danh sách phước lành từ tệp JSON.
/// </summary>
public static class PlayfabItems
{
    public static List<Blessing> ReadBlessings()
    {
        var path = Path.Combine(PlayfabConstants.BaseDir, "public-production", "2", "known_blessings.json");
        // JSON deserialization sẽ được thực hiện ở đây
        return new List<Blessing>();
    }

    public static string ItemName(string category, string effectName, int rarity)
    {
        return $"Item_{category}_{effectName}_{rarity}";
    }

    public static Dictionary<string, CatalogItem> CreateCatalogItems(List<Blessing> blessings)
    {
        var catalogItems = new Dictionary<string, CatalogItem>();
        var inventoryItems = new List<InventoryItem>();
        var dateFormatted = new DateTime(2024, 5, 2, 3, 34, 0, DateTimeKind.Utc).ToString(PlayfabConstants.Iso8601Layout);

        foreach (var b in blessings)
        {
            foreach (var r in b.KnownRarities)
            {
                if (r > -1)
                    AddItem(ItemName("Season0", b.EffectName, r), catalogItems, inventoryItems, dateFormatted);

                if (b.EffectName.StartsWith("GrantLegend"))
                    AddItem(ItemName("", b.EffectName, r), catalogItems, inventoryItems, dateFormatted);
            }
        }

        return catalogItems;
    }

    public static List<InventoryItem> CreateInventoryItems(List<Blessing> blessings)
    {
        var inventoryItems = new List<InventoryItem>();
        var dateFormatted = new DateTime(2024, 5, 2, 3, 34, 0, DateTimeKind.Utc).ToString(PlayfabConstants.Iso8601Layout);

        foreach (var b in blessings)
        {
            foreach (var r in b.KnownRarities)
            {
                if (r > -1)
                    AddItem(ItemName("Season0", b.EffectName, r), new Dictionary<string, CatalogItem>(), inventoryItems, dateFormatted);
            }
        }

        return inventoryItems;
    }

    private static void AddItem(string name, Dictionary<string, CatalogItem> catalogItems, List<InventoryItem> inventoryItems, string dateFormatted)
    {
        inventoryItems.Add(new InventoryItem
        {
            Id = name,
            StackId = "default",
            Amount = 1,
            Type = "catalogItem"
        });

        catalogItems[name] = new CatalogItem
        {
            Id = name,
            Type = "catalogItem",
            AlternateIds = new List<CatalogItemAlternativeId> { new() { Type = "FriendlyId", Value = name } },
            FriendlyId = name,
            Title = new CatalogItemTitle { NEUTRAL = name },
            CreatorEntity = new CatalogItemCreatorEntity { Id = "C15F9", Type = "title", TypeString = "title" },
            CreationDate = dateFormatted,
            LastModifiedDate = dateFormatted,
            StartDate = dateFormatted
        };
    }
}
