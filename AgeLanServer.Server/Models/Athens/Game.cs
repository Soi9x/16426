using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Models.Age4;
using AgeLanServer.Server.Models.Athens.Routes.Game.CommunityEvent;
using AgeLanServer.Server.Models.Athens.Routes.Playfab;
using AgeLanServer.Server.Models.Athens.Routes.Playfab.CloudScriptFunction.BuildGauntletLabyrinth.Precomputed;
using AgeLanServer.Server.Models.Athens.User;
using AgeLanServer.Server.Models.Playfab;
using System.Collections.Immutable;

namespace AgeLanServer.Server.Models.Athens;

/// <summary>
/// Game Athens (Age of Mythology: Retold).
/// Mở rộng BaseGame với các tính năng PlayFab bổ sung:
/// - Gauntlet (thử thách)
/// - Blessings (lời chúc phúc)
/// - Community Events (sự kiện cộng đồng)
/// - Catalog Items và Inventory Items
/// </summary>
public class Game : BaseGame
{
    /// <summary>Ánh xạ mức phước lành -> danh sách tên phước lành được cho phép</summary>
    public Dictionary<int, List<string>> AllowedBlessings { get; set; } = new();

    /// <summary>Ánh xạ độ khó -> chỉ số nhóm thử thách</summary>
    public Dictionary<string, List<int>> GauntletPoolIndexByDifficulty { get; set; } = new();

    /// <summary>Dữ liệu Gauntlet (thử thách)</summary>
    public Gauntlet? Gauntlet { get; set; }

    /// <summary>Nhóm nhiệm vụ Gauntlet</summary>
    public GauntletMissionPools? GauntletMissionPools { get; set; }

    /// <summary>Danh mục vật phẩm</summary>
    public Dictionary<string, CatalogItem> CatalogItems { get; set; } = new();

    /// <summary>Vật phẩm tồn kho - tất cả người dùng có cùng vật phẩm cố định</summary>
    public List<InventoryItem> InventoryItems { get; set; } = new();

    /// <summary>
    /// Mã hóa sự kiện cộng đồng thành mảng đối tượng.
    /// </summary>
    public object[] CommunityEventsEncoded()
    {
        return CommunityEventsHelper.CommunityEventsEncoded();
    }
}

/// <summary>
/// Factory tạo game Athens (Age of Mythology: Retold).
/// </summary>
public static class GameFactory
{
    public static IGame CreateGame()
    {
        var mainGame = GameFactoryHelper.CreateMainGame(
            AppConstants.GameAoM,
            new CreateMainGameOpts
            {
                Instances = new InstanceOpts
                {
                    Users = new Users()
                },
                Resources = new ResourcesOpts
                {
                    KeyedFilenames = ImmutableHashSet.Create("itemBundleItems.json", "itemDefinitions.json")
                }
            });

        var game = new Game
        {
            Game = mainGame
        };

        game.PlayfabSessions.Initialize();
        CommunityEventsHelper.Initialize();

        // Đọc dữ liệu blessings, catalog items, gauntlet, v.v.
        var blessings = PlayfabItems.ReadBlessings();
        game.CatalogItems = PlayfabItems.CreateCatalogItems(blessings);
        game.InventoryItems = PlayfabItems.CreateInventoryItems(blessings);
        game.Gauntlet = GauntletReader.ReadGauntlet();
        game.GauntletMissionPools = GauntletMissionPoolsReader.ReadGauntletMissionPools();
        game.AllowedBlessings = PrecomputedHelper.AllowedGauntletBlessings(game.Gauntlet!, blessings);
        var gauntletPoolNamesToIndex = PrecomputedHelper.PoolNamesToIndex(game.GauntletMissionPools!);
        game.GauntletPoolIndexByDifficulty = PrecomputedHelper.PoolsIndexByDifficulty(game.Gauntlet!, gauntletPoolNamesToIndex);

        return game;
    }
}
