using System.CommandLine;
using AgeLanServer.BattleServerManager;
using AgeLanServer.BattleServerManager.CmdUtils;

namespace AgeLanServer.BattleServerManager.Commands;

/// <summary>
/// Lệnh "remove": Xóa cấu hình của một khu vực (region) cụ thể.
/// - Đọc tất cả cấu hình của game
/// - Lọc theo region
/// - Kill tiến trình và xóa file TOML
/// </summary>
public static class CmdRemove
{
    public static Command CreateCommand()
    {
        var command = new Command("remove", "Remove a specific region's Battle Server config files");

        var gamesOption = new Option<IEnumerable<string>>(
            new[] { "--games", "-g" },
            description: "Game IDs to remove from (default: all supported games)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var regionOption = new Option<string>(
            new[] { "--region", "-r" },
            description: "Region of the Battle Server to remove")
        {
            IsRequired = true
        };

        command.AddOption(gamesOption);
        command.AddOption(regionOption);
        command.SetHandler(
            (games, region) => HandleRemove(games?.ToList() ?? new List<string>(), region),
            gamesOption, regionOption);

        return command;
    }

    private static void HandleRemove(List<string> games, string region)
    {
        // Thiết lập game IDs nếu có
        if (games.Count > 0)
        {
            GameIds.Ids = games;
        }

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
            Console.WriteLine($"\tRemoving '{region}' region...");

            // Đọc tất cả cấu hình của game
            var configs = BattleServerConfigLib.Configs(gameId, onlyValid: false);
            if (configs.Count == 0)
            {
                Console.WriteLine("\tNo configuration needs it.");
                continue;
            }

            // Lọc chỉ giữ lại cấu hình của region cần xóa
            var filteredConfigs = configs.Where(c => c.Region == region).ToList();
            if (filteredConfigs.Count == 0)
            {
                Console.WriteLine("\tNo configuration needs it.");
                continue;
            }

            // Xóa cấu hình (không chỉ invalid, mà xóa theo region)
            var removedAny = Remove.RemoveConfigs(gameId, filteredConfigs, onlyInvalid: false);
            if (!removedAny)
            {
                Console.WriteLine("\tNo configuration needs it.");
            }
        }
    }
}
