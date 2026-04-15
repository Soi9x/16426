using System.CommandLine;
using AgeLanServer.Common;
using AgeLanServer.LauncherAgent;
using AgeLanServer.LauncherCommon;

// 1. Cấu hình Unicode cho console (khắc phục lỗi ký tự ?)
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

/// <summary>
/// Điểm vào Launcher Agent - giám sát tiến trình game và xử lý cleanup.
/// </summary>
AppLogger.Initialize();
AppLogger.SetPrefix("AGENT");
CommandExecutor.ChangeWorkingDirectoryToExecutable();

var gameIdOption = new Option<string>(
    aliases: new[] { "--game", "-g" },
    description: "ID game (age1, age2, age3, age4, athens)")
{ IsRequired = true };

var steamFlag = new Option<bool>(
    aliases: new[] { "--steam" },
    description: "Game chạy từ Steam");

var xboxFlag = new Option<bool>(
    aliases: new[] { "--xbox" },
    description: "Game chạy từ Xbox/Microsoft Store");

var logDirOption = new Option<string?>(
    aliases: new[] { "--logDir", "-l" },
    description: "Thư mục đích cho log game");

var serverExeOption = new Option<string?>(
    aliases: new[] { "--serverExe" },
    description: "Đường dẫn server executable (để kill khi game thoát)");

var broadcastBsFlag = new Option<bool>(
    aliases: new[] { "--broadcastBs" },
    description: "Bật rebroadcast battle server");

var battleServerExeOption = new Option<string?>(
    aliases: new[] { "--bsExe" },
    description: "Đường dẫn battle-server-manager executable");

var battleServerRegionOption = new Option<string?>(
    aliases: new[] { "--bsRegion" },
    description: "Tên vùng battle server");

var rootCommand = new RootCommand("Launcher Agent - Giám sát tiến trình game")
{
    gameIdOption,
    steamFlag,
    xboxFlag,
    logDirOption,
    serverExeOption,
    broadcastBsFlag,
    battleServerExeOption,
    battleServerRegionOption
};

rootCommand.SetHandler(async (gameId, isSteam, isXbox, logDir, serverExe, broadcastBs, bsExe, bsRegion) =>
{
    if (!GameIds.IsValid(gameId))
    {
        AppLogger.Error($"Game ID không hợp lệ: {gameId}");
        Environment.ExitCode = LauncherErrorCodes.InvalidGame;
        return;
    }

    AppLogger.Info($"Bắt đầu giám sát game: {gameId} (Steam: {isSteam}, Xbox: {isXbox})");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true;
        AppLogger.Info("Nhận tín hiệu dừng...");
        cts.Cancel();
    };

    var exitCode = await ProcessWatcher.WatchGameProcessAsync(
        gameId, isSteam, isXbox, logDir, serverExe, broadcastBs, bsExe, bsRegion, cts.Token);

    Environment.ExitCode = exitCode;
}, gameIdOption, steamFlag, xboxFlag, logDirOption, serverExeOption, broadcastBsFlag, battleServerExeOption, battleServerRegionOption);

return await rootCommand.InvokeAsync(args);
