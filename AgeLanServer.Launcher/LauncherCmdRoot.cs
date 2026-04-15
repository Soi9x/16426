using System.CommandLine;
using System.CommandLine.Parsing;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using AgeLanServer.BattleServerManager;
using AgeLanServer.Common;
using AgeLanServer.Launcher.Internal;
using AgeLanServer.Launcher.Internal.CmdUtils;
using AgeLanServer.Launcher.Internal.CmdUtils.Logger;
using AgeLanServer.Launcher.Internal.Executor;
using AgeLanServer.BattleServerBroadcast;
using AgeLanServer.Launcher.Internal.Game.Executor;
using AgeLanServer.Launcher.Internal.Server;
using AgeLanServer.LauncherCommon;

namespace AgeLanServer.Launcher.Internal.Cmd;

/// <summary>
/// Lá»‡nh gá»‘c cá»§a launcher - xá»­ lÃ½ toÃ n bá»™ luá»“ng chÃ­nh.
/// TÆ°Æ¡ng Ä‘Æ°Æ¡ng package cmd/root.go trong Go.
/// </summary>
public static class LauncherCmdRoot
{
    private const string AutoValue = "auto";
    private const string TrueValue = "true";
    private const string FalseValue = "false";

    private static readonly string[] ConfigPaths = { "resources", "." };
    private static readonly LauncherConfigManager Config = new();

    // GiÃ¡ trá»‹ há»£p lá»‡ cho cÃ¡c tham sá»‘
    private static readonly HashSet<string> AutoTrueFalseValues = new() { AutoValue, TrueValue, FalseValue };
    private static HashSet<string> CanTrustCertificateValues = new() { FalseValue, "user", "local" };
    private static readonly HashSet<string> CanBroadcastBattleServerValues = new() { AutoValue, FalseValue };
    private static readonly HashSet<string> RequiredTrueFalseValues = new() { TrueValue, FalseValue, "required" };

    public static string? Version { get; set; }

    /// <summary>
    /// Táº¡o lá»‡nh gá»‘c vÃ  thá»±c thi.
    /// </summary>
    public static async Task<int> ExecuteAsync(string[] args)
    {
        // Äá»‹nh nghÄ©a cÃ¡c tÃ¹y chá»n dÃ²ng lá»‡nh
        var configOption = new Option<string?>(new[] { "--config" },
            $"File cáº¥u hÃ¬nh (máº·c Ä‘á»‹nh config.toml trong cÃ¡c thÆ° má»¥c {string.Join(", ", ConfigPaths)})");
        var gameConfigOption = new Option<string?>(new[] { "--gameConfig" },
            $"File cáº¥u hÃ¬nh game (máº·c Ä‘á»‹nh config.game.toml trong cÃ¡c thÆ° má»¥c {string.Join(", ", ConfigPaths)})");
        var logOption = new Option<bool>(new[] { "--log" },
            "CÃ³ ghi log chi tiáº¿t ra file khÃ´ng. Báº­t khi cÃ³ lá»—i.");
        var canAddHostOption = new Option<string>(new[] { "--canAddHost", "-t" }, () => TrueValue,
            "ThÃªm má»¥c DNS cá»¥c bá»™ náº¿u cáº§n káº¿t ná»‘i Ä‘áº¿n 'server' vá»›i tÃªn miá»n chÃ­nh thá»©c.");
        var canTrustCertOption = new Option<string>(new[] { "--canTrustCertificate", "-c" }, () => "local",
            "Tin cáº­y certificate cá»§a 'server' náº¿u cáº§n. \"false\", \"user\" (Windows), hoáº·c \"local\" (admin).");

        var canBroadcastBattleServerOption = new Option<string>(new[] { "--canBroadcastBattleServer", "-b" }, () => AutoValue,
            "CÃ³ broadcast BattleServer game Ä‘áº¿n táº¥t cáº£ interface trong LAN khÃ´ng.");
        var gameOption = new Option<string>(new[] { "--game", "-g" }, "ID game (báº¯t buá»™c)") { IsRequired = true };
        var isolateMetadataOption = new Option<string>(new[] { "--isolateMetadata", "-m" }, () => "required",
            "CÃ´ láº­p bá»™ nhá»› Ä‘á»‡m metadata cá»§a game. KhÃ´ng tÆ°Æ¡ng thÃ­ch vá»›i AoE:DE.");
        var isolateProfilesOption = new Option<string>(new[] { "--isolateProfiles", "-p" }, () => "required",
            "CÃ´ láº­p profile ngÆ°á»i dÃ¹ng cá»§a game.");
        var setupCommandOption = new Option<string?>(new[] { "--setupCommand" },
            "Executable cháº¡y Ä‘á»ƒ thiáº¿t láº­p ban Ä‘áº§u.");
        var revertCommandOption = new Option<string?>(new[] { "--revertCommand" },
            "Executable cháº¡y Ä‘á»ƒ khÃ´i phá»¥c sau khi thoÃ¡t.");
        var serverStartOption = new Option<string>(new[] { "--serverStart", "-a" }, () => AutoValue,
            "Khá»Ÿi Ä‘á»™ng 'server' náº¿u cáº§n: \"auto\", \"true\", \"false\".");
        var serverStopOption = new Option<string>(new[] { "--serverStop", "-o" }, () => AutoValue,
            "Dá»«ng 'server' náº¿u Ä‘Ã£ khá»Ÿi Ä‘á»™ng: \"auto\", \"true\", \"false\".");
        var serverAnnouncePortsOption = new Option<List<string>>(new[] { "--serverAnnouncePorts", "-n" },
            () => new List<string> { "7778" },
            "Cá»•ng announce Ä‘á»ƒ láº¯ng nghe.");
        var serverAnnounceMulticastGroupsOption = new Option<List<string>>(new[] { "--serverAnnounceMulticastGroups", "-g" },
            () => new List<string> { "239.255.0.1" },
            "NhÃ³m multicast Ä‘á»ƒ announce.");
        var serverOption = new Option<string?>(new[] { "--server", "-s" },
            "Hostname cá»§a 'server' Ä‘á»ƒ káº¿t ná»‘i.");
        var serverSingleAutoSelectOption = new Option<bool>(new[] { "--serverSingleAutoSelect" },
            "Tá»± Ä‘á»™ng chá»n server khi chá»‰ tÃ¬m tháº¥y má»™t server.");
        var serverPathOption = new Option<string>(new[] { "--serverPath", "-z" }, () => AutoValue,
            "ÄÆ°á»ng dáº«n executable cá»§a 'server'.");
        var serverPathArgsOption = new Option<string?>(new[] { "--serverPathArgs", "-r" },
            "Tham sá»‘ truyá»n cho executable 'server'.");
        var clientExeOption = new Option<string>(new[] { "--clientExe", "-l" }, () => AutoValue,
            "Loáº¡i client game hoáº·c Ä‘Æ°á»ng dáº«n: \"auto\", \"steam\", \"msstore\", hoáº·c Ä‘Æ°á»ng dáº«n.");
        var clientExeArgsOption = new Option<string?>(new[] { "--clientExeArgs", "-i" },
            "Tham sá»‘ truyá»n cho launcher client tÃ¹y chá»‰nh.");

        var rootCommand = new RootCommand("Age LAN Server Launcher");
        rootCommand.AddOption(configOption);
        rootCommand.AddOption(gameConfigOption);
        rootCommand.AddOption(logOption);
        rootCommand.AddOption(canAddHostOption);
        rootCommand.AddOption(canTrustCertOption);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            rootCommand.AddOption(canBroadcastBattleServerOption);
        rootCommand.AddOption(gameOption);
        rootCommand.AddOption(isolateMetadataOption);
        rootCommand.AddOption(isolateProfilesOption);
        rootCommand.AddOption(setupCommandOption);
        rootCommand.AddOption(revertCommandOption);
        rootCommand.AddOption(serverStartOption);
        rootCommand.AddOption(serverStopOption);
        rootCommand.AddOption(serverAnnouncePortsOption);
        rootCommand.AddOption(serverAnnounceMulticastGroupsOption);
        rootCommand.AddOption(serverOption);
        rootCommand.AddOption(serverSingleAutoSelectOption);
        rootCommand.AddOption(serverPathOption);
        rootCommand.AddOption(serverPathArgsOption);
        rootCommand.AddOption(clientExeOption);
        rootCommand.AddOption(clientExeArgsOption);

        rootCommand.SetHandler(async (context) =>
        {
            var parseResult = context.ParseResult;
            var gameId = parseResult.GetValueForOption(gameOption);
            if (string.IsNullOrEmpty(gameId))
            {
                LauncherLogger.Error("Thiáº¿u tham sá»‘ báº¯t buá»™c '--game'");
                Environment.Exit(ErrorCodes.General);
                return;
            }

            // KhÃ³a file PID
            var lockObj = new PidFileLock();
            var locked = lockObj.TryAcquire(out var existingPidPath);
            if (!locked)
            {
                LauncherLogger.Error("KhÃ´ng thá»ƒ khÃ³a file PID. Kill process 'launcher' náº¿u Ä‘ang cháº¡y trong task manager.");
                Environment.Exit(ErrorCodes.PidLock);
                return;
            }

            var cfg = InitConfig(parseResult, gameId);
            LauncherLogger.LogEnabled = cfg.Config.Log;

            if (LauncherLogger.LogEnabled)
            {
                var logErr = LauncherLogger.OpenMainFileLog(gameId);
                if (logErr != null)
                {
                    LauncherLogger.Error("KhÃ´ng thá»ƒ má»Ÿ file log");
                    LauncherLogger.Error(logErr.Message);
                    Environment.Exit(ErrorCodes.FileLog);
                    return;
                }
            }

            var errorCode = ErrorCodes.Success;

            try
            {
                LauncherLogger.WriteFileLog(gameId, "start");
                var isAdmin = new System.Security.Principal.WindowsPrincipal(
                    System.Security.Principal.WindowsIdentity.GetCurrent())
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

                var canTrustCertificate = cfg.Config.Certificate.CanTrustInPc;
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var tmp = new HashSet<string>(CanTrustCertificateValues);
                    tmp.Remove("user");
                    CanTrustCertificateValues = tmp;
                }

                if (!CanTrustCertificateValues.Contains(canTrustCertificate))
                {
                    LauncherLogger.Error($"GiÃ¡ trá»‹ khÃ´ng há»£p lá»‡ cho canTrustCertificate ({string.Join("/", CanTrustCertificateValues)}): {canTrustCertificate}");
                    errorCode = LauncherErrorCodes.InvalidGame + 1; // ErrInvalidCanTrustCertificate
                    return;
                }

                var canBroadcastBattleServer = FalseValue;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && gameId != "aom" && gameId != "age4")
                {
                    canBroadcastBattleServer = cfg.Config.CanBroadcastBattleServer;
                    if (!CanBroadcastBattleServerValues.Contains(canBroadcastBattleServer))
                    {
                        LauncherLogger.Error($"GiÃ¡ trá»‹ khÃ´ng há»£p lá»‡ cho canBroadcastBattleServer (auto/false): {canBroadcastBattleServer}");
                        errorCode = LauncherErrorCodes.InvalidGame + 2; // ErrInvalidCanBroadcastBattleServer
                        return;
                    }
                }

                var serverStart = cfg.Server.Start;
                if (!AutoTrueFalseValues.Contains(serverStart))
                {
                    LauncherLogger.Error($"GiÃ¡ trá»‹ khÃ´ng há»£p lá»‡ cho serverStart (auto/true/false): {serverStart}");
                    errorCode = LauncherErrorCodes.InvalidGame + 3; // ErrInvalidServerStart
                    return;
                }

                var serverStop = cfg.Server.Stop;
                var serverStopValues = new HashSet<string>(AutoTrueFalseValues);
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && isAdmin)
                {
                    serverStopValues.Remove(FalseValue);
                }
                if (!serverStopValues.Contains(serverStop))
                {
                    LauncherLogger.Error($"GiÃ¡ trá»‹ khÃ´ng há»£p lá»‡ cho serverStop ({string.Join("/", serverStopValues)}): {serverStop}");
                    errorCode = LauncherErrorCodes.InvalidGame + 4; // ErrInvalidServerStop
                    return;
                }

                var battleServerManagerRun = cfg.Server.BattleServerManager.Run;
                if (!RequiredTrueFalseValues.Contains(battleServerManagerRun))
                {
                    LauncherLogger.Error($"GiÃ¡ trá»‹ khÃ´ng há»£p lá»‡ cho Server.BattleServerManager.Run ({string.Join("/", RequiredTrueFalseValues)}): {battleServerManagerRun}");
                    errorCode = LauncherErrorCodes.InvalidGame + 5; // ErrInvalidServerBattleServerManagerRun
                    return;
                }

                var isolateMetadataStr = cfg.Config.IsolateMetadata;
                if (!RequiredTrueFalseValues.Contains(isolateMetadataStr))
                {
                    LauncherLogger.Error($"GiÃ¡ trá»‹ khÃ´ng há»£p lá»‡ cho Config.IsolateMetadata ({string.Join("/", RequiredTrueFalseValues)}): {isolateMetadataStr}");
                    errorCode = LauncherErrorCodes.InvalidGame + 6; // ErrInvalidIsolateMetadata
                    return;
                }

                var isolateProfilesStr = cfg.Config.IsolateProfiles;
                if (!RequiredTrueFalseValues.Contains(isolateProfilesStr))
                {
                    LauncherLogger.Error($"GiÃ¡ trá»‹ khÃ´ng há»£p lá»‡ cho Config.IsolateProfiles ({string.Join("/", RequiredTrueFalseValues)}): {isolateProfilesStr}");
                    errorCode = LauncherErrorCodes.InvalidGame + 7; // ErrInvalidIsolateProfiles
                    return;
                }

                var supportedGames = new HashSet<string> { "aoe1", "aoe2", "age4", "aom" };
                if (!supportedGames.Contains(gameId))
                {
                    LauncherLogger.Error("Loáº¡i game khÃ´ng há»£p lá»‡");
                    errorCode = LauncherErrorCodes.InvalidGame;
                    return;
                }

                Config.SetGameId(gameId);

                // PhÃ¢n tÃ­ch server args
                var serverValues = new Dictionary<string, string>
                {
                    ["Game"] = gameId,
                    ["Id"] = Guid.NewGuid().ToString()
                };

                List<string> serverArgs;
                try
                {
                    serverArgs = LauncherCmdUtils.ParseCommandArgs(cfg.Server.Executable.Args, serverValues);
                }
                catch
                {
                    LauncherLogger.Error("KhÃ´ng thá»ƒ phÃ¢n tÃ­ch tham sá»‘ executable 'server'");
                    errorCode = LauncherErrorCodes.InvalidGame + 8; // ErrInvalidServerArgs
                    return;
                }

                Guid serverId = Guid.Empty;
                for (int i = 0; i < serverArgs.Count; i++)
                {
                    if (serverArgs[i] == "--id" && i + 1 < serverArgs.Count)
                    {
                        if (Guid.TryParse(serverArgs[i + 1], out var parsedId))
                        {
                            serverId = parsedId;
                        }
                        break;
                    }
                }

                if (serverId == Guid.Empty)
                {
                    LauncherLogger.Error("Báº¡n pháº£i cung cáº¥p UUID há»£p lá»‡ cho server ID báº±ng tham sá»‘ '--id' trong tham sá»‘ executable 'server'");
                    errorCode = LauncherErrorCodes.InvalidGame + 8; // ErrInvalidServerArgs
                    return;
                }

                // PhÃ¢n tÃ­ch battle-server-manager args
                List<string> battleServerManagerArgs;
                try
                {
                    battleServerManagerArgs = LauncherCmdUtils.ParseCommandArgs(
                        cfg.Server.BattleServerManager.Executable.Args, serverValues);
                }
                catch
                {
                    LauncherLogger.Error("KhÃ´ng thá»ƒ phÃ¢n tÃ­ch tham sá»‘ executable 'battle-server-manager'");
                    errorCode = LauncherErrorCodes.InvalidGame + 9; // ErrInvalidServerBattleServerManagerArgs
                    return;
                }

                // PhÃ¢n tÃ­ch setup command
                List<string> setupCommand;
                try
                {
                    setupCommand = LauncherCmdUtils.ParseCommandArgs(cfg.Config.SetupCommand, null);
                }
                catch
                {
                    LauncherLogger.Error("KhÃ´ng thá»ƒ phÃ¢n tÃ­ch setup command");
                    errorCode = LauncherErrorCodes.InvalidGame + 10; // ErrInvalidSetupCommand
                    return;
                }

                // PhÃ¢n tÃ­ch revert command
                List<string> revertCommand;
                try
                {
                    revertCommand = LauncherCmdUtils.ParseCommandArgs(cfg.Config.RevertCommand, null);
                }
                catch
                {
                    LauncherLogger.Error("KhÃ´ng thá»ƒ phÃ¢n tÃ­ch revert command");
                    errorCode = LauncherErrorCodes.InvalidGame + 11; // ErrInvalidRevertCommand
                    return;
                }

                var canAddHost = cfg.Config.CanAddHost;
                var clientExecutable = cfg.Client.Executable.Path;
                var clientExecutableOfficial = clientExecutable == AutoValue || clientExecutable == "steam" || clientExecutable == "msstore";

                var isolateMetadata = gameId != "aoe1"
                    ? LauncherCmdUtils.ResolveIsolateValue(isolateMetadataStr, clientExecutableOfficial)
                    : false;
                var isolateProfiles = LauncherCmdUtils.ResolveIsolateValue(isolateProfilesStr, clientExecutableOfficial);

                // Xá»­ lÃ½ server executable
                var serverExecutable = cfg.Server.Executable.Path;
                if (serverExecutable != AutoValue)
                {
                    if (!File.Exists(serverExecutable))
                    {
                        LauncherLogger.Error("Executable 'server' khÃ´ng há»£p lá»‡");
                        errorCode = LauncherErrorCodes.InvalidGame + 12; // ErrInvalidServerPath
                        return;
                    }
                }

                // Xá»­ lÃ½ battle-server-manager executable
                var battleServerManagerExecutable = cfg.Server.BattleServerManager.Executable.Path;
                if (battleServerManagerExecutable != AutoValue)
                {
                    if (!File.Exists(battleServerManagerExecutable))
                    {
                        LauncherLogger.Error("Executable 'battle-server-manager' khÃ´ng há»£p lá»‡");
                        errorCode = LauncherErrorCodes.InvalidGame + 13; // ErrInvalidClientPath
                        return;
                    }
                }

                // Xá»­ lÃ½ client executable
                if (!clientExecutableOfficial)
                {
                    if (!File.Exists(clientExecutable))
                    {
                        LauncherLogger.Error("Executable client khÃ´ng há»£p lá»‡");
                        errorCode = LauncherErrorCodes.InvalidGame + 13; // ErrInvalidClientPath
                        return;
                    }
                }
                else if (!isolateProfiles || (gameId != "aoe1" && !isolateMetadata))
                {
                    LauncherLogger.Error("CÃ´ láº­p profile vÃ  metadata lÃ  báº¯t buá»™c khi sá»­ dá»¥ng launcher chÃ­nh thá»©c.");
                    errorCode = LauncherErrorCodes.InvalidGame + 14; // ErrRequiredIsolation
                    return;
                }
                else
                {
                    LauncherLogger.Info("HÃ£y Ä‘áº£m báº£o báº¡n táº¯t cloud saves trong cÃ i Ä‘áº·t launcher Ä‘á»ƒ trÃ¡nh váº¥n Ä‘á».");
                }

                if (isAdmin)
                {
                    LauncherLogger.Info("Äang cháº¡y vá»›i quyá»n admin, Ä‘iá»u nÃ y khÃ´ng Ä‘Æ°á»£c khuyáº¿n khÃ­ch vÃ¬ lÃ½ do báº£o máº­t.");
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        LauncherLogger.Info(" NÃ³ cÅ©ng cÃ³ thá»ƒ gÃ¢y ra váº¥n Ä‘á» vÃ  háº¡n cháº¿ chá»©c nÄƒng.");
                    }
                }

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && isAdmin &&
                    (clientExecutable == AutoValue || clientExecutable == "steam"))
                {
                    LauncherLogger.Error("Steam khÃ´ng thá»ƒ cháº¡y vá»›i quyá»n admin. HÃ£y cháº¡y vá»›i user thÆ°á»ng hoáº·c Ä‘áº·t Client.Executable thÃ nh launcher tÃ¹y chá»‰nh.");
                    errorCode = LauncherErrorCodes.InvalidGame + 15; // ErrSteamRoot
                    return;
                }

                if (LauncherCmdUtils.IsGameRunning())
                {
                    errorCode = LauncherErrorCodes.InvalidGame + 16; // ErrGameAlreadyRunning
                    return;
                }

                var serverHost = cfg.Server.Host;
                LauncherLogger.Info($"Game {gameId}.");

                if (clientExecutable == "msstore" && gameId == "aom")
                {
                    LauncherLogger.Error("PhiÃªn báº£n Microsoft Store (Xbox) khÃ´ng Ä‘Æ°á»£c há»— trá»£ trÃªn game nÃ y.");
                    errorCode = LauncherErrorCodes.InvalidGame + 17; // ErrGameUnsupportedLauncherCombo
                    return;
                }

                LauncherLogger.Info("Äang tÃ¬m game...");
                string? gamePath = null;
                var executer = GameExecutor.MakeExec(gameId, clientExecutable);
                IGameExec? gameExec = executer;
                CustomExec? customExecutor = executer as CustomExec;

                switch (executer)
                {
                    case SteamExec steamExec:
                        LauncherLogger.Info("Game tÃ¬m tháº¥y trÃªn Steam.");
                        if (gameId != "aoe1" && gameId != "age4")
                        {
                            gamePath = steamExec.GamePath();
                        }
                        break;

                    case XboxExec xboxExec:
                        LauncherLogger.Info("Game tÃ¬m tháº¥y trÃªn Xbox.");
                        if (gameId != "aoe1" && gameId != "age4")
                        {
                            gamePath = xboxExec.GamePath();
                        }
                        break;

                    case CustomExec customExec:
                        customExecutor = customExec;
                        LauncherLogger.Info("Game tÃ¬m tháº¥y trÃªn Ä‘Æ°á»ng dáº«n tÃ¹y chá»‰nh.");
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        {
                            if (isolateMetadata)
                            {
                                LauncherLogger.Info("CÃ´ láº­p metadata khÃ´ng Ä‘Æ°á»£c há»— trá»£.");
                                isolateMetadata = false;
                            }
                            if (isolateProfiles)
                            {
                                LauncherLogger.Info("CÃ´ láº­p profile khÃ´ng Ä‘Æ°á»£c há»— trá»£.");
                                isolateProfiles = false;
                            }
                        }
                        if (gameId != "aoe1" && gameId != "age4")
                        {
                            var clientPath = cfg.Client.Path;
                            if (!string.IsNullOrEmpty(clientPath) && clientPath != "auto" && Directory.Exists(clientPath))
                            {
                                gamePath = clientPath;
                            }
                            else
                            {
                                LauncherLogger.Error("ÄÆ°á»ng dáº«n client khÃ´ng há»£p lá»‡");
                                errorCode = LauncherErrorCodes.InvalidGame + 13; // ErrInvalidClientPath
                                return;
                            }
                        }
                        break;

                    default:
                        LauncherLogger.Error("KhÃ´ng tÃ¬m tháº¥y game.");
                        errorCode = LauncherErrorCodes.InvalidGame + 18; // ErrGameLauncherNotFound
                        return;
                }

                string? gameCaCertPath = null;
                if (!string.IsNullOrEmpty(gamePath))
                {
                    gameCaCertPath = Path.Combine(gamePath, "resources", "cacert.pem");
                }

                Config.SetGameId(gameId);

                // Xá»­ lÃ½ tÃ­n hiá»‡u ngáº¯t
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    Config.Revert();
                    AppLogger.CloseFileLog();
                    lockObj.Release();
                    Environment.Exit(errorCode);
                };

                // Cleanup ban Ä‘áº§u
                LauncherLogger.Info("Äang dá»n dáº¹p (náº¿u cáº§n)...");
                Config.KillAgent();

                if (revertCommand.Count > 0)
                {
                    try
                    {
                        ConfigRevertManager.StoreRevertArgs(new ConfigRevertManager.RevertArgs
                        {
                            GameId = gameId
                        });
                    }
                    catch
                    {
                        LauncherLogger.Error("KhÃ´ng thá»ƒ lÆ°u revert command");
                        errorCode = LauncherErrorCodes.InvalidGame + 11; // ErrInvalidRevertCommand
                        return;
                    }
                }

                // Thiáº¿t láº­p
                LauncherLogger.Info("Äang thiáº¿t láº­p...");

                string? serverIP = null;
                var effectiveServerStart = serverStart;
                var effectiveServerStop = serverStop;

                if (effectiveServerStart == AutoValue)
                {
                    var announcePorts = cfg.Server.AnnouncePorts.Select(p => (ushort)p).ToHashSet();
                    var multicastIPs = new HashSet<IPAddress>();
                    foreach (var str in cfg.Server.AnnounceMulticastGroups)
                    {
                        if (IPAddress.TryParse(str, out var ip) &&
                            ip.AddressFamily == AddressFamily.InterNetwork &&
                            ip.GetAddressBytes()[0] >= 224 && ip.GetAddressBytes()[0] <= 239)
                        {
                            multicastIPs.Add(ip);
                        }
                        else
                        {
                            LauncherLogger.Error($"NhÃ³m multicast khÃ´ng há»£p lá»‡ \"{str}\"");
                            errorCode = LauncherErrorCodes.InvalidGame + 19; // ErrAnnouncementMulticastGroup
                            return;
                        }
                    }

                    var (discoveredServerId, selectedServerIp) = ServerModule.DiscoverServersAndSelectBestIpAddr(
                        gameId,
                        cfg.Server.SingleAutoSelect,
                        multicastIPs,
                        announcePorts);

                    if (discoveredServerId != Guid.Empty)
                    {
                        var preferLoopback = ServerModule.LanServerHost(
                            discoveredServerId,
                            gameId,
                            "127.0.0.1",
                            true,
                            null);

                        if (preferLoopback)
                        {
                            serverIP = "127.0.0.1";
                            LauncherLogger.Info("Đã phát hiện server local, ưu tiên dùng 127.0.0.1 để khớp certificate.");
                        }
                        else
                        {
                            serverIP = selectedServerIp?.ToString();
                        }
                        effectiveServerStart = FalseValue;
                        if (effectiveServerStop == AutoValue && (!isAdmin || RuntimeInformation.IsOSPlatform(OSPlatform.Windows)))
                        {
                            effectiveServerStop = FalseValue;
                        }
                    }
                    else
                    {
                        effectiveServerStart = TrueValue;
                        if (effectiveServerStop == AutoValue)
                        {
                            effectiveServerStop = TrueValue;
                        }
                    }
                }

                if (effectiveServerStart == FalseValue)
                {
                    if (effectiveServerStop == TrueValue)
                    {
                        LauncherLogger.Info("serverStart lÃ  false. Bá» qua serverStop lÃ  true.");
                    }
                    if (string.IsNullOrEmpty(serverIP))
                    {
                        if (string.IsNullOrEmpty(serverHost))
                        {
                            LauncherLogger.Error("serverStart lÃ  false. serverHost pháº£i Ä‘Æ°á»£c Ä‘iá»n vÃ¬ cáº§n biáº¿t host Ä‘á»ƒ káº¿t ná»‘i.");
                            errorCode = LauncherErrorCodes.InvalidGame + 20; // ErrInvalidServerHost
                            return;
                        }
                        if (IPAddress.TryParse(serverHost, out var addr) &&
                            addr.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            LauncherLogger.Error("serverStart lÃ  false. serverHost pháº£i lÃ  hostname hoáº·c Ä‘á»‹a chá»‰ IPv4.");
                            errorCode = LauncherErrorCodes.InvalidGame + 20; // ErrInvalidServerHost
                            return;
                        }

                        var hostIps = CommonUtilities.HostOrIpToIps(serverHost);
                        var ipSet = new HashSet<IPAddress>();
                        foreach (var ipStr in hostIps)
                        {
                            if (IPAddress.TryParse(ipStr, out var parsedIp))
                                ipSet.Add(parsedIp);
                        }

                        var filterResult = ServerModule.FilterServerIPs(
                            Guid.Empty,
                            serverHost,
                            gameId,
                            ipSet
                        );

                        if (filterResult.Data == null)
                        {
                            LauncherLogger.Error("serverStart lÃ  false. KhÃ´ng thá»ƒ phÃ¢n giáº£i serverHost thÃ nh IP há»£p lá»‡ vÃ  cÃ³ thá»ƒ káº¿t ná»‘i.");
                            errorCode = LauncherErrorCodes.InvalidGame + 20; // ErrInvalidServerHost
                            return;
                        }

                        serverIP = filterResult.MeasuredIpAddresses[0].Ip.ToString();
                        serverId = filterResult.ActualId;
                    }
                }
                else
                {
                    var logRoot = AppLogger.LogFolder();
                    if (!string.IsNullOrEmpty(logRoot))
                    {
                        if (!serverArgs.Contains("--log"))
                            serverArgs.Add("--log");
                        if (!serverArgs.Contains("--logRoot"))
                            serverArgs.AddRange(new[] { "--logRoot", logRoot });
                        if (!serverArgs.Contains("--flatLog"))
                            serverArgs.Add("--flatLog");
                        if (!serverArgs.Contains("--deterministic"))
                            serverArgs.Add("--deterministic");
                    }

                    if ((gameId == "aom" || gameId == "age4") && battleServerManagerRun == FalseValue)
                    {
                        LauncherLogger.Error("Game nÃ y cáº§n Battle Server khá»Ÿi Ä‘á»™ng nhÆ°ng báº¡n khÃ´ng cho phÃ©p, " +
                            "hÃ£y Ä‘áº£m báº£o báº¡n cÃ³ má»™t server Ä‘ang cháº¡y vÃ  server Ä‘Ã£ Ä‘Æ°á»£c cáº¥u hÃ¬nh.");
                    }

                    var runBattleServerManager = battleServerManagerRun == TrueValue ||
                        (battleServerManagerRun == "required" && (gameId == "aom" || gameId == "age4"));

                    if (cfg.Server.Start == AutoValue)
                    {
                        var str = "KhÃ´ng tÃ¬m tháº¥y 'server' nÃ o, tiáº¿n hÃ nh";
                        if (runBattleServerManager)
                        {
                            str += " khá»Ÿi Ä‘á»™ng battle server (náº¿u cáº§n) vÃ  sau Ä‘Ã³";
                        }
                        if (!cfg.Server.StartWithoutConfirmation)
                        {
                            LauncherLogger.Info(str + " khá»Ÿi Ä‘á»™ng 'server'. Nháº¥n Enter Ä‘á»ƒ tiáº¿p tá»¥c...");
                            Console.ReadLine();
                        }
                    }

                    var serverExecutablePath = ServerModule.GetExecutablePath(serverExecutable);
                    if (string.IsNullOrEmpty(serverExecutablePath))
                    {
                        LauncherLogger.Error("KhÃ´ng thá»ƒ tÃ¬m Ä‘Æ°á»ng dáº«n executable 'server'. Äáº·t thá»§ cÃ´ng trong Server.Executable.");
                        errorCode = LauncherErrorCodes.InvalidGame + 21; // ErrServerExecutable
                        return;
                    }

                    if (serverExecutable != serverExecutablePath)
                    {
                        LauncherLogger.Info($"TÃ¬m tháº¥y Ä‘Æ°á»ng dáº«n executable 'server': {serverExecutablePath}");
                    }

                    var certEc = ServerModule.GenerateServerCertificates(serverExecutablePath, canTrustCertificate != FalseValue);
                    if (certEc != ErrorCodes.Success)
                    {
                        errorCode = certEc;
                        return;
                    }

                    if (runBattleServerManager)
                    {
                        // Battle server manager sáº½ Ä‘Æ°á»£c khá»Ÿi Ä‘á»™ng cÃ¹ng server local
                        // qua ServerModule.StartServerLocal() bÃªn dÆ°á»›i.
                        // Cáº¥u hÃ¬nh region Ä‘Æ°á»£c quáº£n lÃ½ bá»Ÿi BattleServerConfigManager.
                        LauncherLogger.Info("Battle server manager sáº½ Ä‘Æ°á»£c khá»Ÿi Ä‘á»™ng náº¿u cáº§n.");
                    }

                    var (startEc, startIp) = ServerModule.StartServerLocal(
                        gameId, serverExecutablePath, serverArgs, effectiveServerStop == TrueValue, serverId);
                    if (startEc != ErrorCodes.Success)
                    {
                        errorCode = startEc;
                        return;
                    }
                    serverIP = startIp;
                    if (effectiveServerStop == TrueValue)
                    {
                        Config.ServerExe = serverExecutablePath;
                    }
                }

                if (string.IsNullOrEmpty(serverIP))
                {
                    LauncherLogger.Error("KhÃ´ng thá»ƒ láº¥y IP server.");
                    errorCode = LauncherErrorCodes.InvalidGame + 22; // ErrServerStart
                    return;
                }

                var serverCertificate = ServerModule.ReadCACertificateFromServer(serverIP);
                if (serverCertificate == null)
                {
                    LauncherLogger.Error($"KhÃ´ng thá»ƒ Ä‘á»c certificate tá»« {serverIP}.");
                    errorCode = LauncherErrorCodes.InvalidGame + 23; // ErrReadCert
                    return;
                }

                // MapHosts, AddCert, IsolateUserData, AddCACertToGame, LaunchAgentAndGame
                // 1. ThÃªm certificate vÃ o há»‡ thá»‘ng
                byte[]? serverCertData = null;
                try
                {
                    serverCertData = serverCertificate.Export(X509ContentType.Cert);
                    if (canTrustCertificate != FalseValue)
                    {
                        var certDataBase64 = Convert.ToBase64String(serverCertData);
                        await CertificateUtilities.TrustLocalCertificateAsync(certDataBase64);
                        LauncherLogger.Info("ÄÃ£ thÃªm certificate vÃ o há»‡ thá»‘ng.");
                    }
                }
                catch (Exception ex)
                {
                    LauncherLogger.Warn($"Lá»—i thÃªm certificate: {ex.Message}");
                }

                // 2. Isolate metadata vÃ  profiles
                if (isolateMetadata)
                {
                    try
                    {
                        UserDataManager.BackupAllUserData(gameId);
                        LauncherLogger.Info("ÄÃ£ backup vÃ  cÃ´ láº­p dá»¯ liá»‡u user.");
                    }
                    catch (Exception ex)
                    {
                        LauncherLogger.Warn($"Lá»—i cÃ´ láº­p dá»¯ liá»‡u user: {ex.Message}");
                    }
                }

                // 3. ThÃªm CA cert vÃ o game (náº¿u há»— trá»£)
                if (canTrustCertificate != FalseValue && GameCertificateManager.SupportsCaCertModification(gameId))
                {
                    try
                    {
                        GameCertificateManager.BackupCaCertificate(gameId);
                        if (serverCertData != null)
                        {
                            var certPem = System.Text.Encoding.UTF8.GetString(serverCertData);
                            await GameCertificateManager.AppendCaCertificateAsync(gameId, certPem);
                            LauncherLogger.Info("ÄÃ£ thÃªm CA cert vÃ o game.");
                        }
                    }
                    catch (Exception ex)
                    {
                        LauncherLogger.Warn($"Lá»—i thÃªm CA cert vÃ o game: {ex.Message}");
                    }
                }

                // 4. Map hosts náº¿u cáº§n
                if (canAddHost)
                {
                    try
                    {
                        var hosts = GameDomains.GetAllHosts(gameId);
                        HostsManager.AddHostMappings(serverIP, hosts);
                        HostsManager.FlushDnsCache();
                        LauncherLogger.Info($"ÄÃ£ Ã¡nh xáº¡ {serverIP} tá»›i {hosts.Length} domain.");
                    }
                    catch (Exception ex)
                    {
                        LauncherLogger.Warn($"Lá»—i Ã¡nh xáº¡ hosts: {ex.Message}");
                    }
                }

                // 5. Launch agent vÃ  game
                try
                {
                    var isSteam = clientExecutable == "steam" || clientExecutable == AutoValue;
                    var isXbox = clientExecutable == "msstore";

                    var logRoot = AppLogger.LogFolder() ?? string.Empty;
                    var battleServerRegion = Config.BattleServerRegion;
                    var broadcastBattleServer = canBroadcastBattleServer == AutoValue
                        ? BattleServerBroadcastModule.Required()
                        : canBroadcastBattleServer == TrueValue;

                    // Khá»Ÿi Ä‘á»™ng agent
                    var agentResult = ExecutorModule.StartAgent(
                        gameId,
                        isSteam,
                        isXbox,
                        serverExecutable,
                        broadcastBattleServer,
                        battleServerManagerExecutable,
                        battleServerRegion,
                        logRoot,
                        null,
                        opts => { });

                    if (agentResult.Success)
                    {
                        LauncherLogger.Info($"ÄÃ£ khá»Ÿi Ä‘á»™ng agent (PID: {agentResult.Pid}).");
                    }

                    // Khá»Ÿi Ä‘á»™ng game
                    var gameExecResult = gameExec?.Do(new List<string>(), opts => { });
                    if (gameExecResult != null && gameExecResult.Success)
                    {
                        LauncherLogger.Info("ÄÃ£ khá»Ÿi Ä‘á»™ng game thÃ nh cÃ´ng.");
                    }
                }
                catch (Exception ex)
                {
                    LauncherLogger.Error($"Lá»—i khá»Ÿi Ä‘á»™ng agent/game: {ex.Message}");
                    errorCode = LauncherErrorCodes.InvalidGame + 27; // ErrLaunchAgent
                }

                LauncherLogger.Info($"ÄÃ£ cáº¥u hÃ¬nh xong server táº¡i {serverIP}.");
            }
            finally
            {
                if (errorCode != ErrorCodes.Success)
                {
                    Config.Revert();
                }
                LauncherLogger.WriteFileLog(gameId, "before exit");
                AppLogger.CloseFileLog();
                lockObj.Release();
                Environment.Exit(errorCode);
            }
        });

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Khá»Ÿi táº¡o cáº¥u hÃ¬nh tá»« file vÃ  tham sá»‘ dÃ²ng lá»‡nh.
    /// </summary>
    private static FullConfiguration InitConfig(ParseResult parseResult, string gameId)
    {
        var cfg = new FullConfiguration();

        // Ãp dá»¥ng defaults
        cfg.Config.CanAddHost = true;
        cfg.Config.Certificate.CanTrustInPc = "local";
        cfg.Config.Certificate.CanTrustInGame = true;
        cfg.Config.CanBroadcastBattleServer = AutoValue;
        cfg.Config.IsolateMetadata = "required";
        cfg.Config.IsolateProfiles = "required";
        cfg.Server.Start = AutoValue;
        cfg.Server.Stop = AutoValue;
        cfg.Server.SingleAutoSelect = false;
        cfg.Server.StartWithoutConfirmation = false;
        cfg.Server.Executable.Path = AutoValue;
        cfg.Server.Executable.Args = new List<string> { "-e", gameId, "--id", Guid.NewGuid().ToString() };
        cfg.Server.Host = "127.0.0.1";
        cfg.Server.AnnouncePorts = new List<int> { 7778 };
        cfg.Server.AnnounceMulticastGroups = new List<string> { "239.255.0.1" };
        cfg.Server.BattleServerManager.Run = TrueValue;
        cfg.Server.BattleServerManager.Executable.Path = AutoValue;
        cfg.Server.BattleServerManager.Executable.Args = new List<string> { "-e", gameId, "-r" };
        cfg.Client.Executable.Path = AutoValue;

        return cfg;
    }
}


