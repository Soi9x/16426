using System.Runtime.InteropServices;
using AgeLanServer.Common;
using AgeLanServer.Launcher.Internal.Executor;

namespace AgeLanServer.Launcher.Internal.Game.Executor;

/// <summary>
/// Interface thực thi game - tương đương Exec interface trong executor.go
/// </summary>
public interface IGameExec
{
    ExecResult Do(List<string> args, Action<ExecOptions> optionsFn);
    (bool steamProcess, bool xboxProcess) GameProcesses();
}

/// <summary>
/// Thực thi game qua Steam.
/// Tương đương SteamExec trong executor.go
/// </summary>
public record SteamExec : IGameExec
{
    private readonly string _gameId;
    private readonly string _libraryFolder;

    public SteamExec(string gameId, string libraryFolder)
    {
        _gameId = gameId;
        _libraryFolder = libraryFolder;
    }

    public ExecResult Do(List<string> args, Action<ExecOptions> optionsFn)
    {
        var uri = GetSteamUri();
        var options = new ExecOptions
        {
            File = uri,
            Shell = true,
            SpecialFile = true,
            ShowWindow = true
        };
        optionsFn(options);
        return options.Exec();
    }

    public (bool steamProcess, bool xboxProcess) GameProcesses()
    {
        return (true, false);
    }

    public string GamePath()
    {
        return Path.Combine(_libraryFolder, "steamapps", "common", GetGameFolderName(_gameId));
    }

    private string GetSteamUri()
    {
        return $"steam://run/{GetSteamAppId(_gameId)}";
    }

    private static string GetSteamAppId(string gameId)
    {
        return gameId switch
        {
            var g when g == GameIds.AgeOfEmpires1 => "1017900",
            var g when g == GameIds.AgeOfEmpires2 => "813780",
            var g when g == GameIds.AgeOfEmpires3 => "933110",
            var g when g == GameIds.AgeOfEmpires4 => "1466860",
            var g when g == GameIds.AgeOfMythology => "1934680",
            _ => string.Empty
        };
    }

    private static string GetGameFolderName(string gameId)
    {
        return gameId switch
        {
            var g when g == GameIds.AgeOfEmpires1 => "Age of Empires DE",
            var g when g == GameIds.AgeOfEmpires2 => "Age of Empires II DE",
            var g when g == GameIds.AgeOfEmpires3 => "Age of Empires III DE",
            var g when g == GameIds.AgeOfEmpires4 => "Age of Empires IV",
            var g when g == GameIds.AgeOfMythology => "Age of Mythology Retold",
            _ => gameId
        };
    }
}

/// <summary>
/// Thực thi game qua Xbox (Microsoft Store).
/// Tương đương XboxExec trong executor.go
/// </summary>
public record XboxExec : IGameExec
{
    private readonly string _gameId;
    private readonly string _gamePath;

    public XboxExec(string gameId, string gamePath)
    {
        _gameId = gameId;
        _gamePath = gamePath;
    }

    public ExecResult Do(List<string> args, Action<ExecOptions> optionsFn)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var familyName = GetFamilyName(_gameId);
            var shellPath = $@"shell:appsfolder\{familyName}!App";

            var options = new ExecOptions
            {
                File = shellPath,
                Shell = true,
                SpecialFile = true,
                ShowWindow = true
            };
            optionsFn(options);
            return options.Exec();
        }

        return new ExecResult
        {
            ExitCode = ErrorCodes.General,
            Error = new PlatformNotSupportedException("Xbox chỉ hỗ trợ trên Windows.")
        };
    }

    public (bool steamProcess, bool xboxProcess) GameProcesses()
    {
        return (false, true);
    }

    public string GamePath() => _gamePath;

    private static string GetFamilyName(string gameId)
    {
        return gameId switch
        {
            var g when g == GameIds.AgeOfEmpires1 => "Microsoft.AgeofEmpiresDefinitiveEdition_8wekyb3d8bbwe",
            var g when g == GameIds.AgeOfEmpires2 => "Microsoft.AgeofEmpiresIIDefinitiveEdition_8wekyb3d8bbwe",
            var g when g == GameIds.AgeOfEmpires4 => "Microsoft.AgeofEmpiresIV_8wekyb3d8bbwe",
            var g when g == GameIds.AgeOfMythology => "Microsoft.AgeofMythologyRetold_8wekyb3d8bbwe",
            _ => string.Empty
        };
    }
}

/// <summary>
/// Thực thi game qua launcher tùy chỉnh.
/// Tương đương CustomExec trong executor.go
/// </summary>
public record CustomExec : IGameExec
{
    public string Executable { get; init; } = string.Empty;

    private ExecResult Execute(List<string> args, bool admin, Action<ExecOptions> optionsFn)
    {
        var options = new ExecOptions
        {
            File = Executable,
            Args = args,
            ShowWindow = true,
            GUI = true
        };

        if (admin)
        {
            options = options with { AsAdmin = true };
        }

        optionsFn(options);
        return options.Exec();
    }

    public ExecResult Do(List<string> args, Action<ExecOptions> optionsFn)
    {
        return Execute(args, false, optionsFn);
    }

    public ExecResult DoElevated(List<string> args, Action<ExecOptions> optionsFn)
    {
        return Execute(args, true, optionsFn);
    }

    public (bool steamProcess, bool xboxProcess) GameProcesses()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return (true, true);
        return (true, false);
    }
}

/// <summary>
/// Factory tạo executor phù hợp.
/// Tương đương MakeExec trong executor.go
/// </summary>
public static class GameExecutor
{
    /// <summary>
    /// Tạo executor cho game.
    /// </summary>
    public static IGameExec? MakeExec(string gameId, string executable)
    {
        gameId = GameIds.Normalize(gameId) ?? gameId;

        if (executable != "auto")
        {
            switch (executable)
            {
                case "steam":
                    return TryCreateSteamExec(gameId);

                case "msstore":
                    return TryCreateXboxExec(gameId);

                default:
                    return new CustomExec { Executable = executable };
            }
        }

        // Chế độ auto: thử Steam trước, rồi Xbox
        return TryCreateSteamExec(gameId) ?? TryCreateXboxExec(gameId);
    }

    private static IGameExec? TryCreateSteamExec(string gameId)
    {
        var libraryFolder = FindSteamLibraryFolder(gameId);
        if (!string.IsNullOrEmpty(libraryFolder))
        {
            return new SteamExec(gameId, libraryFolder);
        }
        return null;
    }

    private static IGameExec? TryCreateXboxExec(string gameId)
    {
        var (ok, gameLocation) = FindXboxGameLocation(gameId);
        if (ok)
        {
            return new XboxExec(gameId, gameLocation);
        }

        return null;
    }

    /// <summary>
    /// Khởi động URI (steam:// hoặc shell:).
    /// Tương đương startUri trong executor_windows.go / executor_unix.go
    /// </summary>
    private static ExecResult StartUri(string uri, Action<ExecOptions> optionsFn)
    {
        var options = new ExecOptions
        {
            File = uri,
            Shell = true,
            SpecialFile = true,
            ShowWindow = true
        };
        optionsFn(options);
        return options.Exec();
    }

    /// <summary>
    /// Tìm thư mục Steam library chứa game.
    /// </summary>
    private static string? FindSteamLibraryFolder(string gameId)
    {
        var steamPaths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            steamPaths.Add(Path.Combine(programFiles, "Steam"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            steamPaths.Add(Path.Combine(home, ".steam", "steam"));
            steamPaths.Add(Path.Combine(home, ".local", "share", "Steam"));
        }

        foreach (var steamPath in steamPaths)
        {
            var libraryFolders = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(libraryFolders))
            {
                var content = File.ReadAllText(libraryFolders);
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("\"path\""))
                    {
                        var pathStart = line.IndexOf('"', line.IndexOf("path") + 4);
                        var pathEnd = line.IndexOf('"', pathStart + 1);
                        if (pathStart > 0 && pathEnd > pathStart)
                        {
                            var path = line.Substring(pathStart + 1, pathEnd - pathStart - 1);
                            var appId = GetSteamAppId(gameId);
                            var appManifest = Path.Combine(path, "steamapps", $"appmanifest_{appId}.acf");
                            if (File.Exists(appManifest))
                                return path;
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Tìm vị trí cài đặt game Xbox.
    /// </summary>
    private static (bool ok, string gameLocation) FindXboxGameLocation(string gameId)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return (false, string.Empty);

        try
        {
            var familyName = GetXboxFamilyName(gameId);
            var psScript = $@"Get-AppxPackage -Name ""*{familyName.Split('_')[0]}*"" | Select-Object -ExpandProperty InstallLocation";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{psScript}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd()?.Trim();
            process?.WaitForExit();

            if (!string.IsNullOrEmpty(output) && Directory.Exists(output))
            {
                return (true, output);
            }
        }
        catch
        {
            // Không thể lấy vị trí
        }

        return (false, string.Empty);
    }

    private static string GetSteamAppId(string gameId)
    {
        return gameId switch
        {
            var g when g == GameIds.AgeOfEmpires1 => "1017900",
            var g when g == GameIds.AgeOfEmpires2 => "813780",
            var g when g == GameIds.AgeOfEmpires3 => "933110",
            var g when g == GameIds.AgeOfEmpires4 => "1466860",
            var g when g == GameIds.AgeOfMythology => "1934680",
            _ => string.Empty
        };
    }

    private static string GetXboxFamilyName(string gameId)
    {
        return gameId switch
        {
            var g when g == GameIds.AgeOfEmpires1 => "Microsoft.AgeofEmpiresDefinitiveEdition_8wekyb3d8bbwe",
            var g when g == GameIds.AgeOfEmpires2 => "Microsoft.AgeofEmpiresIIDefinitiveEdition_8wekyb3d8bbwe",
            var g when g == GameIds.AgeOfEmpires4 => "Microsoft.AgeofEmpiresIV_8wekyb3d8bbwe",
            var g when g == GameIds.AgeOfMythology => "Microsoft.AgeofMythologyRetold_8wekyb3d8bbwe",
            _ => string.Empty
        };
    }
}

/// <summary>
/// Module BattleServerBroadcast - kiểm tra có cần broadcast BattleServer không.
/// Tương đương package battleServerBroadcast trong Go
/// </summary>
public static class BattleServerBroadcastModule
{
    /// <summary>
    /// Kiểm tra có cần broadcast BattleServer không.
    /// Chỉ trả về true trên Windows khi có nhiều interface mạng.
    /// Tương đương Required() trong battleServerBroadcast_windows.go
    /// </summary>
    public static bool Required()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                             ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .ToList();

            if (interfaces.Count > 1)
            {
                return true;
            }
        }
        catch
        {
            // Không thể lấy thông tin interface
        }

        return false;
    }
}
