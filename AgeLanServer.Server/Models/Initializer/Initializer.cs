using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Models.Age1;
using AgeLanServer.Server.Models.Age2;
using AgeLanServer.Server.Models.Age3;
using AgeLanServer.Server.Models.Age4;
using AgeLanServer.Server.Models.Athens;
using AgeLanServer.Server.Models.Athens.Routes.Playfab.CloudScriptFunction;

namespace AgeLanServer.Server.Models.Initializer;

/// <summary>
/// Bộ khởi tạo game.
/// Quản lý bản đồ gameId -> IGame và khởi tạo máy chủ battle.
/// </summary>
public static class GameInitializer
{
    /// <summary>Bản đồ lưu trữ các game đã khởi tạo</summary>
    public static Dictionary<string, IGame> Games { get; } = new();

    /// <summary>
    /// Khởi tạo game theo gameId.
    /// Tạo máy chủ battle từ cấu hình và game tương ứng.
    /// </summary>
    /// <param name="gameId">ID game (aoe1, aoe2, aoe3, aoe4, aom)</param>
    /// <param name="configBattleServers">Danh sách máy chủ battle từ cấu hình</param>
    public static void InitializeGame(string gameId, IEnumerable<IBattleServer> configBattleServers)
    {
        // Khởi tạo máy chủ battle
        // models.InitializeBattleServers(gameId, configBattleServers);

        IGame game = gameId switch
        {
            AppConstants.GameAoE1 => Age1.GameFactory.CreateGame(),
            AppConstants.GameAoE2 => Age2.GameFactory.CreateGame(),
            AppConstants.GameAoE3 => Age3.GameFactory.CreateGame(),
            AppConstants.GameAoE4 => Age4.GameFactory.CreateGame(),
            AppConstants.GameAoM => Athens.GameFactory.CreateGame(),
            _ => throw new ArgumentException($"Unknown gameId: {gameId}")
        };

        Games[gameId] = game;

        // Khởi tạo kho hàm Cloud Script cho Athens
        if (gameId == AppConstants.GameAoM)
            CloudScriptFunctionStore.Initialize();
    }
}
