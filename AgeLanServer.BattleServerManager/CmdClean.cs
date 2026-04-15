using System.CommandLine;
using AgeLanServer.BattleServerManager;
using AgeLanServer.BattleServerManager.CmdUtils;

namespace AgeLanServer.BattleServerManager.Commands;

/// <summary>
/// Lệnh "clean": Đọc tất cả cấu hình của các game,
/// kiểm tra tính hợp lệ, và xóa CHỈ các cấu hình không hợp lệ.
/// Hữu ích để dọn dẹp các server đã dừng hoặc crash.
/// </summary>
public static class CmdClean
{
    public static Command CreateCommand()
    {
        var command = new Command("clean", "Remove only invalid (stopped/crashed) Battle Server configs");

        var gamesOption = new Option<IEnumerable<string>>(
            new[] { "--games", "-g" },
            description: "Game IDs to clean (default: all supported games)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        command.AddOption(gamesOption);
        command.SetHandler(
            (games) => HandleClean(games?.ToList() ?? new List<string>()),
            gamesOption);

        return command;
    }

    private static void HandleClean(List<string> games)
    {
        // Thiết lập game IDs nếu có
        if (games.Count > 0)
        {
            GameIds.Ids = games;
        }

        Console.WriteLine("Cleaning up...");

        // Phân tích game IDs hợp lệ
        var (parsedGames, error) = ConfigReader.ParsedGameIds(games.Count > 0 ? games : null);
        if (error is not null || parsedGames is null)
        {
            Console.WriteLine(error ?? "game(s) not supported");
            Environment.Exit(ErrorCodes.ErrGames);
        }

        // Duyệt qua từng game
        foreach (var gameId in parsedGames)
        {
            Console.WriteLine($"Game: {gameId}");

            // Đọc tất cả cấu hình của game (cả hợp lệ và không hợp lệ)
            var configs = BattleServerConfigLib.Configs(gameId, onlyValid: false);
            if (configs.Count == 0)
            {
                Console.WriteLine("\tNo configuration needs it.");
                continue;
            }

            // Xóa chỉ các cấu hình không hợp lệ
            var removedAny = Remove.RemoveConfigs(gameId, configs, onlyInvalid: true);
            if (!removedAny)
            {
                Console.WriteLine("\tNo configuration needs it.");
            }
        }
    }
}
