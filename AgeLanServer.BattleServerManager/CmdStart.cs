using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Net;
using AgeLanServer.BattleServerManager;
using AgeLanServer.BattleServerManager.CmdUtils;
using AgeLanServer.Common;

namespace AgeLanServer.BattleServerManager.Commands;

/// <summary>
/// Lệnh "start": Khởi chạy một Battle Server mới.
/// - Kiểm tra game ID hợp lệ
/// - Đọc cấu hình từ file TOML
/// - Kiểm tra server đã tồn tại
/// - Tự động sinh name/region/cổng nếu cần
/// - Phân giải đường dẫn SSL từ chứng chỉ game
/// - Phân giải đường dẫn thực thi từ Steam/Xbox
/// - Spawn tiến trình BattleServer
/// - Đợi 10 giây để khởi tạo
/// - Ghi cấu hình ra file TOML
/// </summary>
public static class CmdStart
{
    // Các thư mục tìm kiếm file cấu hình game
    private static readonly string[] ConfigPaths = { "Resources", "." };

    public static Command CreateCommand()
    {
        var command = new Command("start", "Start a new Battle Server");

        var gameOption = new Option<string>(
            new[] { "--game", "-g" },
            description: "Game ID (age1, age2, age3, age4, athens)")
        {
            IsRequired = true
        };

        var gameConfigOption = new Option<string>(
            "--gameConfig",
            "Game config TOML file (default: config.<game>.toml in Resources or current dir)");

        var hideWindowOption = new Option<bool>(
            new[] { "--hideWindow", "-w" },
            "Hide Battle Server window");

        var forceOption = new Option<bool>(
            new[] { "--force", "-f" },
            "Force start more than one Battle Server per game");

        var noErrExistingOption = new Option<bool>(
            new[] { "--noErrExisting", "-e" },
            "When force is true and one exists, exit without error");

        var logRootOption = new Option<string>(
            "--logRoot",
            "Root directory for log files");

        var regionOption = new Option<string>(
            new[] { "--region", "-r" },
            () => "auto",
            "Khu vực (region) của battle server (mặc định: auto)");

        var nameOption = new Option<string>(
            new[] { "--name", "-n" },
            () => "auto",
            "Tên hiển thị của battle server (mặc định: auto)");

        command.AddOption(gameOption);
        command.AddOption(gameConfigOption);
        command.AddOption(hideWindowOption);
        command.AddOption(forceOption);
        command.AddOption(noErrExistingOption);
        command.AddOption(logRootOption);
        command.AddOption(regionOption);
        command.AddOption(nameOption);

        command.SetHandler(
            (game, gameConfig, hideWindow, force, noErrExisting, logRoot, region, name) =>
                HandleStart(game, gameConfig, hideWindow, force, noErrExisting, logRoot, region, name),
            gameOption, gameConfigOption, hideWindowOption, forceOption, noErrExistingOption, logRootOption, regionOption, nameOption);

        return command;
    }

    private static void HandleStart(
        string gameId,
        string? gameConfigFile,
        bool hideWindow,
        bool force,
        bool noErrExisting,
        string? logRoot,
        string region,
        string name)
    {
        Console.WriteLine("Checking and resolving configuration...");

        // Kiểm tra quyền admin (không cần thiết và có thể gây vấn đề)
        if (IsAdmin())
        {
            Console.WriteLine("Running as administrator, this is not needed and might cause issues.");
        }

        // Đọc cấu hình từ file TOML
        var cfg = LoadConfig(gameId, gameConfigFile);

        // Kiểm tra server đã tồn tại
        var (err, names, regions) = ConfigReader.ExistingServers(gameId);
        if (err is not null)
        {
            Console.WriteLine($"could not get existing servers: {err}");
            Environment.Exit(ErrorCodes.ErrReadConfig);
        }

        // Nếu không force và đã có server đang chạy
        if (!force && regions.Count > 0)
        {
            if (noErrExisting)
            {
                return;
            }
            Console.WriteLine("a Battle Server is already running, use --force to start another one");
            Environment.Exit(ErrorCodes.ErrAlreadyRunning);
        }

        // Tự động sinh name/region nếu "auto" - ưu tiên CLI over config file
        var finalName = name != "auto" ? name : cfg.Name;
        var finalRegion = region != "auto" ? region : cfg.Region;

        if (finalName == "auto" || finalRegion == "auto")
        {
            if (finalName == "auto")
            {
                if (names.Contains("server") || regions.Contains("server"))
                {
                    for (int i = 1; ; i++)
                    {
                        var currentName = $"Server ({i})";
                        if (!names.Contains(currentName.ToLowerInvariant()) &&
                            !regions.Contains(currentName.ToLowerInvariant()))
                        {
                            finalName = currentName;
                            break;
                        }
                    }
                }
                else
                {
                    finalName = "Server";
                }
                Console.WriteLine($"Auto-generated name: {finalName}");
            }

            if (finalRegion == "auto")
            {
                finalRegion = finalName;
                Console.WriteLine($"Auto-generated region: {finalRegion}");
            }
        }

        // Kiểm tra name/region không trùng với server đã tồn tại
        if (names.Contains(finalRegion.ToLowerInvariant()) || regions.Contains(finalRegion.ToLowerInvariant()))
        {
            Console.WriteLine($"a Battle Server with the name/region '{finalRegion}' already exists");
            if (!force) { Environment.Exit(ErrorCodes.ErrAlreadyExists); return; }
        }

        if (names.Contains(finalName.ToLowerInvariant()) || regions.Contains(finalName.ToLowerInvariant()))
        {
            Console.WriteLine($"a Battle Server with the name/region '{finalName}' already exists");
            if (!force) { Environment.Exit(ErrorCodes.ErrAlreadyExists); return; }
        }

        // Phân giải IP từ host
        var host = cfg.Host;
        string? ip = null;

        if (host != "auto")
        {
            var ips = HostOrIpToIps(host);
            if (ips.Length == 0)
            {
                Console.WriteLine("could not resolve host to an IP address");
                Environment.Exit(ErrorCodes.ErrResolveHost);
            }

            // Chọn IP không phải loopback
            foreach (var currentIp in ips)
            {
                if (IPAddress.TryParse(currentIp, out var addr) && !IPAddress.IsLoopback(addr))
                {
                    ip = currentIp;
                }
            }

            if (ip is null)
            {
                Console.WriteLine("ip not valid or could not resolve host to a suitable IP address");
                Environment.Exit(ErrorCodes.ErrInvalidHost);
            }

            if (ip != host)
            {
                Console.WriteLine($"Resolved host to IP address: {ip}");
            }
        }
        else
        {
            ip = host;
        }

        // Kiểm tra cổng có sẵn
        var bsPort = cfg.Ports.Bs;
        var websocketPort = cfg.Ports.WebSocket;
        var outOfBandPort = gameId == SupportedGames.GameAoE1 ? -1 : cfg.Ports.OutOfBand;

        if (bsPort > 0 && !Network.Available(bsPort))
        {
            Console.WriteLine($"bs port {bsPort} is already in use");
            Environment.Exit(ErrorCodes.ErrBsPortInUse);
        }

        if (websocketPort > 0 && !Network.Available(websocketPort))
        {
            Console.WriteLine($"websocket port {websocketPort} is already in use");
            Environment.Exit(ErrorCodes.ErrWsPortInUse);
        }

        if (outOfBandPort > 0 && !Network.Available(outOfBandPort))
        {
            Console.WriteLine($"out of band port {outOfBandPort} is already in use");
            Environment.Exit(ErrorCodes.ErrOobPortInUse);
        }

        // Sinh cổng tự động nếu cần
        var portsToCheck = new[] { bsPort, websocketPort, outOfBandPort };
        var (allPorts, genError) = Network.GeneratePortsAsNeeded(portsToCheck);
        if (genError is not null || allPorts is null)
        {
            Console.WriteLine($"could not generate ports: {genError}");
            Environment.Exit(ErrorCodes.ErrGenPorts);
        }

        if (bsPort != allPorts[0])
            Console.WriteLine($"\tAuto-generated BsPort port: {allPorts[0]}");
        if (websocketPort != allPorts[1])
            Console.WriteLine($"\tAuto-generated WebSocketPort port: {allPorts[1]}");
        if (outOfBandPort != allPorts[2])
            Console.WriteLine($"\tAuto-generated Out Of Band Port: {allPorts[2]}");

        // Phân giải file SSL
        var sslConfig = new SslConfig
        {
            Auto = cfg.SSL.Auto,
            CertFile = cfg.SSL.CertFile,
            KeyFile = cfg.SSL.KeyFile
        };

        var (certFile, keyFile, sslError) = SslHelper.ResolveSslFilesPath(gameId, sslConfig);
        if (sslError is not null || certFile is null || keyFile is null)
        {
            Console.WriteLine($"could not resolve SSL files: {sslError}");
            Environment.Exit(ErrorCodes.ErrResolveSslFiles);
        }

        // Phân giải đường dẫn thực thi
        var (resolvedPath, pathError) = Executor.ResolvePath(gameId, cfg.Executable.Path);
        if (pathError is not null || resolvedPath is null)
        {
            Console.WriteLine($"could not resolve path: {pathError}");
            Environment.Exit(ErrorCodes.ErrResolvePath);
        }

        // Thực thi Battle Server
        var extraArgs = cfg.Executable.ExtraArgs ?? Array.Empty<string>();
        var (pid, execError) = Executor.ExecuteBattleServer(
            gameId,
            resolvedPath,
            finalRegion,
            finalName,
            allPorts,
            certFile,
            keyFile,
            extraArgs,
            hideWindow,
            logRoot);

        if (execError is not null || pid is null)
        {
            Console.WriteLine($"could not execute BattleServer: {execError}");
            Environment.Exit(ErrorCodes.ErrStartBattleServer);
        }

        // Tạo cấu hình để lưu
        var saveConfig = new Config
        {
            Region = finalRegion,
            Name = finalName,
            IPv4 = ip!,
            BsPort = allPorts[0],
            WebSocketPort = allPorts[1],
            PID = pid.Value,
        };

        if (allPorts[2] != -1)
        {
            saveConfig.OutOfBandPort = allPorts[2];
        }

        // Đợi tối đa 30 giây để khởi tạo
        Console.WriteLine("Waiting up to 30s for the initialization to complete...");
        if (!Connection.WaitForBattleServerInit(saveConfig))
        {
            Console.WriteLine("battle server initialization did not complete in time");

            // Kill tiến trình không khởi tạo được
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById((int)saveConfig.PID);
                if (proc is not null && !proc.HasExited)
                {
                    proc.Kill(true);
                    Console.WriteLine("OK.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not find the process to kill: {ex.Message}");
            }

            Environment.Exit(ErrorCodes.ErrInitBattleServer);
        }

        // Ghi cấu hình ra file
        var writeError = ConfigWriter.WriteConfig(gameId, saveConfig);
        if (writeError is not null)
        {
            Console.WriteLine($"could not write config: {writeError}");
            Console.WriteLine("Stopping started Battle Server...");
            Remove.Kill(saveConfig);
            Environment.Exit(ErrorCodes.ErrConfigWrite);
        }

        Console.WriteLine($"Battle Server started successfully with PID: {pid}");
    }

    /// <summary>
    /// Đọc cấu hình từ file TOML, với giá trị mặc định.
    /// Thứ tự ưu tiên: file TOML -> giá trị mặc định.
    /// </summary>
    private static Configuration LoadConfig(string gameId, string? gameConfigFile)
    {
        var config = new Configuration
        {
            Region = "auto",
            Name = "auto",
            Host = "auto",
            Executable = new Executable
            {
                Path = "auto",
                ExtraArgs = Array.Empty<string>()
            },
            Ports = new Ports(),
            SSL = new Ssl { Auto = true }
        };

        // Tìm file cấu hình
        var fileCandidates = new List<string>();
        if (!string.IsNullOrEmpty(gameConfigFile))
        {
            fileCandidates.Add(gameConfigFile);
        }
        else
        {
            foreach (var configPath in ConfigPaths)
            {
                fileCandidates.Add(Path.Combine(configPath, $"config.{gameId}.toml"));
            }
        }

        var usedFile = fileCandidates.FirstOrDefault(File.Exists);
        if (usedFile is not null)
        {
            Console.WriteLine($"Using config file: {usedFile}");
            try
            {
                var tomlText = File.ReadAllText(usedFile);
                ParseTomlIntoConfig(tomlText, config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing config file: {usedFile}: {ex.Message}");
                Environment.Exit(ErrorCodes.ErrConfigParse);
            }
        }
        else if (!string.IsNullOrEmpty(gameConfigFile))
        {
            Console.WriteLine("No config file found, using defaults.");
        }

        return config;
    }

    /// <summary>
    /// Phân tích file TOML thành đối tượng Configuration.
    /// </summary>
    private static void ParseTomlIntoConfig(string tomlText, Configuration config)
    {
        var lines = tomlText.Split('\n');
        string currentSection = "";

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            // Phát hiện section [Section]
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed.Substring(1, trimmed.Length - 2);
                continue;
            }

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex < 0) continue;

            var key = trimmed.Substring(0, eqIndex).Trim();
            var value = trimmed.Substring(eqIndex + 1).Trim();

            switch (currentSection)
            {
                case "":
                    SetValueTopLevel(key, value, config);
                    break;
                case "Executable":
                    SetExecutableValue(key, value, config);
                    break;
                case "Ports":
                    SetPortsValue(key, value, config);
                    break;
                case "SSL":
                    SetSslValue(key, value, config);
                    break;
            }
        }
    }

    private static void SetValueTopLevel(string key, string value, Configuration config)
    {
        switch (key)
        {
            case "Region": config.Region = UnquoteToml(value); break;
            case "Name": config.Name = UnquoteToml(value); break;
            case "Host": config.Host = UnquoteToml(value); break;
        }
    }

    private static void SetExecutableValue(string key, string value, Configuration config)
    {
        switch (key)
        {
            case "Path": config.Executable.Path = UnquoteToml(value); break;
            case "ExtraArgs": config.Executable.ExtraArgs = ParseTomlArray(value); break;
        }
    }

    private static void SetPortsValue(string key, string value, Configuration config)
    {
        if (int.TryParse(value, out var port))
        {
            switch (key)
            {
                case "Bs": config.Ports.Bs = port; break;
                case "WebSocket": config.Ports.WebSocket = port; break;
                case "OutOfBand": config.Ports.OutOfBand = port; break;
            }
        }
    }

    private static void SetSslValue(string key, string value, Configuration config)
    {
        switch (key)
        {
            case "Auto":
                config.SSL.Auto = bool.TryParse(value, out var auto) && auto;
                break;
            case "CertFile": config.SSL.CertFile = UnquoteToml(value); break;
            case "KeyFile": config.SSL.KeyFile = UnquoteToml(value); break;
        }
    }

    /// <summary>
    /// Loại bỏ dấu ngoặc kép khỏi giá trị TOML.
    /// </summary>
    private static string UnquoteToml(string value)
    {
        if (value.StartsWith('"') && value.EndsWith('"'))
        {
            return value.Substring(1, value.Length - 2)
                       .Replace("\\n", "\n")
                       .Replace("\\r", "\r")
                       .Replace("\\t", "\t")
                       .Replace("\\\"", "\"")
                       .Replace("\\\\", "\\");
        }
        return value;
    }

    /// <summary>
    /// Phân tích mảng TOML ["a", "b"] thành string[].
    /// </summary>
    private static string[] ParseTomlArray(string value)
    {
        value = value.Trim();
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            value = value.Substring(1, value.Length - 2).Trim();
            if (string.IsNullOrEmpty(value))
                return Array.Empty<string>();

            return value.Split(',')
                       .Select(s => UnquoteToml(s.Trim()))
                       .ToArray();
        }
        return Array.Empty<string>();
    }

    /// <summary>
    /// Phân giải host thành danh sách IP.
    /// Thử DNS lookup, hoặc trả về IP gốc nếu đã là địa chỉ IP.
    /// </summary>
    private static string[] HostOrIpToIps(string host)
    {
        try
        {
            if (IPAddress.TryParse(host, out _))
            {
                return new[] { host };
            }
            var addresses = Dns.GetHostAddresses(host);
            return addresses.Select(a => a.ToString()).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Kiểm tra tiến trình có đang chạy với quyền admin không.
    /// </summary>
    private static bool IsAdmin()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}

/// <summary>
/// Mã lỗi cho các tình huống trong Battle Server Manager.
/// </summary>
public static class ErrorCodes
{
    public const int ErrGames = 100;
    public const int ErrReadConfig = 101;
    public const int ErrAlreadyRunning = 102;
    public const int ErrAlreadyExists = 103;
    public const int ErrResolveHost = 104;
    public const int ErrInvalidHost = 105;
    public const int ErrBsPortInUse = 106;
    public const int ErrWsPortInUse = 107;
    public const int ErrOobPortInUse = 108;
    public const int ErrGenPorts = 109;
    public const int ErrResolveSslFiles = 110;
    public const int ErrResolvePath = 111;
    public const int ErrParseArgs = 112;
    public const int ErrStartBattleServer = 113;
    public const int ErrInitBattleServer = 114;
    public const int ErrConfigWrite = 115;
    public const int ErrConfigParse = 116;
    public const int ErrPidLock = 117;
}
