using System.CommandLine;
using AgeLanServer.BattleServerManager;
using AgeLanServer.BattleServerManager.CmdUtils;

namespace AgeLanServer.BattleServerManager.Commands;

/// <summary>
/// Lệnh "remove-all": Xóa TẤT CẢ cấu hình của TẤT CẢ game.
/// - Đọc danh sách game được hỗ trợ (hoặc do người dùng chỉ định)
/// - Đọc tất cả cấu hình của mỗi game
/// - Kill tiến trình và xóa file TOML cho mỗi cấu hình
/// Cảnh báo: Hành động này không thể hoàn tác.
/// </summary>
public static class CmdRemoveAll
{
    public static Command CreateCommand()
    {
        var command = new Command("remove-all", "Remove ALL Battle Server configs for ALL games");

        var gamesOption = new Option<IEnumerable<string>>(
            new[] { "--games", "-g" },
            description: "Game IDs to remove all from (default: all supported games)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        command.AddOption(gamesOption);
        command.SetHandler(
            (games) => HandleRemoveAll(games?.ToList() ?? new List<string>()),
            gamesOption);

        return command;
    }

    private static void HandleRemoveAll(List<string> games)
    {
        // Thiết lập game IDs nếu có
        if (games.Count > 0)
        {
            GameIds.Ids = games;
        }

        Console.WriteLine("Removing all...");

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

            // Xóa TẤT CẢ cấu hình (không lọc invalid)
            var removedAny = Remove.RemoveConfigs(gameId, configs, onlyInvalid: false);
            if (!removedAny)
            {
                Console.WriteLine("\tNo configuration needs it.");
            }
        }
    }
}
