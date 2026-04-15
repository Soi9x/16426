using AgeLanServer.Common;
using AgeLanServer.LauncherCommon;
using System.Security.Cryptography.X509Certificates;

namespace AgeLanServer.Launcher.Internal.CmdUtils.Logger;

/// <summary>
/// Logger chuyên dụng cho launcher.
/// Tương đương package logger trong cmdUtils/log.go của Go.
/// </summary>
public static class LauncherLogger
{
    /// <summary>
    /// Có bật ghi log ra file không.
    /// </summary>
    public static bool LogEnabled { get; set; }

    /// <summary>
    /// Certificate CA của game (để ghi log).
    /// </summary>
    public static object? Cacert { get; set; }

    private static readonly string[] ProcessesLog = { "agent", "config-admin-agent" };
    private static List<string> _allHosts = new();

    /// <summary>
    /// Mở file log chính cho launcher.
    /// </summary>
    public static Exception? OpenMainFileLog(string gameId)
    {
        if (!LogEnabled)
            return null;

        try
        {
            var logRoot = Path.Combine("resources", "logs");
            AppLogger.SetupFileLogger("launcher", logRoot, gameId, false);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>
    /// Ghi log trạng thái vào file.
    /// </summary>
    public static void WriteFileLog(string gameId, string name)
    {
        if (AppLogger.LogFolder() == null)
            return;

        AppLogger.SetPrefix(name);
        _allHosts = GameDomains.GetAllHosts(gameId).ToList();

        try { WriteProcessesStatus(); } catch { }
        try { WritePcCertificateInfo(); } catch { }
        if (Cacert != null)
        {
            try { WriteGameCertificateInfo(); } catch { }
        }
        if (gameId != AppConstants.GameAoE1)
        {
            try { WriteMetadataInfo(); } catch { }
        }
        try { WriteProfilesInfo(); } catch { }
        try { WriteHostInfo(); } catch { }
        try { WriteRevertConfigArgs(); } catch { }
        try { WriteRevertCommandArgs(); } catch { }
    }

    /// <summary>
    /// Ghi nội dung file vào log.
    /// </summary>
    public static void PrintFile(string name, string path)
    {
        if (AppLogger.LogFolder() != null && File.Exists(path))
        {
            var data = File.ReadAllText(path);
            AppLogger.WithPrefix(name, data);
        }
    }

    /// <summary>
    /// Ghi log có định dạng.
    /// </summary>
    public static void InfoF(string format, params object[] args)
    {
        AppLogger.Info(string.Format(format, args));
        Console.WriteLine(format, args);
    }

    /// <summary>
    /// Ghi thông tin.
    /// </summary>
    public static void Info(string message)
    {
        AppLogger.WithPrefix("main", message);
        Console.WriteLine(message);
    }

    /// <summary>
    /// Ghi cảnh báo.
    /// </summary>
    public static void Warn(string message)
    {
        AppLogger.WithPrefix("main", $"WARN: {message}");
        Console.WriteLine($"WARN: {message}");
    }

    /// <summary>
    /// Ghi lỗi.
    /// </summary>
    public static void Error(string message)
    {
        AppLogger.WithPrefix("main", $"ERROR: {message}");
        Console.Error.WriteLine($"ERROR: {message}");
    }

    private static void WriteProcessesStatus()
    {
        foreach (var processName in ProcessesLog)
        {
            var str = $"{processName}: ";
            var exeName = OperatingSystem.IsWindows() ? $"{processName}.exe" : processName;
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                str += "dead";
            }
            else
            {
                str += "alive";
                foreach (var p in processes) p.Dispose();
            }
            AppLogger.Info(str);
        }
    }

    private static void WriteHostInfo()
    {
        try
        {
            var hostsPath = OperatingSystem.IsWindows()
                ? @"C:\Windows\System32\drivers\etc\hosts"
                : "/etc/hosts";

            if (!File.Exists(hostsPath))
                return;

            var lines = File.ReadAllLines(hostsPath);
            var allHostsSet = new HashSet<string>(_allHosts);
            var addedSomeEntry = false;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;

                for (int i = 1; i < parts.Length; i++)
                {
                    if (allHostsSet.Contains(parts[i]))
                    {
                        AppLogger.Info(line);
                        addedSomeEntry = true;
                        break;
                    }
                }
            }

            if (!addedSomeEntry)
                AppLogger.Info("No matchings.");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Error reading hosts: {ex.Message}");
        }
    }

    private static void WriteRevertCommandArgs()
    {
        var args = ConfigRevertManager.LoadRevertArgs();
        if (args == null)
        {
            AppLogger.Info("No arguments.");
            return;
        }

        AppLogger.Info($"RevertCommand - GameId: {args.GameId}, Flags: {args.Flags}");
        if (!string.IsNullOrEmpty(args.BattleServerExe))
            AppLogger.Info($"  BattleServerExe: {args.BattleServerExe}");
        if (!string.IsNullOrEmpty(args.BattleServerRegion))
            AppLogger.Info($"  BattleServerRegion: {args.BattleServerRegion}");
    }

    private static void WriteRevertConfigArgs()
    {
        var args = ConfigRevertManager.LoadRevertArgs();
        if (args == null)
        {
            AppLogger.Info("No arguments.");
            return;
        }

        AppLogger.Info($"ConfigRevert - ServerIp: {args.ServerIp}, CertData: {(string.IsNullOrEmpty(args.CertData) ? "none" : "present")}");
        AppLogger.Info($"  Flags: {args.Flags}");
        AppLogger.Info($"  GameId: {args.GameId}");
    }

    private static void WriteCertificateInfo(List<X509Certificate2> certs)
    {
        var matchingCerts = FilterMatchingCerts(certs, _allHosts);
        if (matchingCerts.Count == 0)
        {
            AppLogger.Info("No certificates.");
        }
        else
        {
            foreach (var crt in matchingCerts)
            {
                var dnsName = crt.GetNameInfo(X509NameType.DnsName, false);
                var dnsGames = !string.IsNullOrEmpty(dnsName)
                    ? dnsName
                    : "No DNS Names.";
                AppLogger.Info($"{crt.Subject}: {dnsGames}");
            }
        }
    }

    private static void WriteGameCertificateInfo()
    {
        // Đọc certificate từ game qua GameCertificateManager
        if (Cacert == null)
        {
            AppLogger.Info("Game certificate: not configured.");
            return;
        }

        try
        {
            var gameCaCertInfo = Cacert.ToString() ?? "unknown";
            AppLogger.Info($"Game CA certificate: {gameCaCertInfo}");
        }
        catch
        {
            AppLogger.Info("Game certificate info not available.");
        }
    }

    private static void WritePcCertificateInfo()
    {
        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(X509FindType.FindByTimeValid, DateTime.Now, false);
            WriteCertificateInfo(certs.OfType<X509Certificate2>().ToList());
            store.Close();
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to enumerate certificates: {ex.Message}");
        }
    }

    private static void WriteMetadataInfo()
    {
        // Ghi thông tin metadata folder từ UserDataManager
        var args = ConfigRevertManager.LoadRevertArgs();
        var gameId = args?.GameId ?? string.Empty;
        if (string.IsNullOrEmpty(gameId))
        {
            AppLogger.Info("Metadata info: no game ID available.");
            return;
        }

        var metadataPath = UserDataManager.GetMetadataPath(gameId);
        AppLogger.Info($"Metadata - BasePath: {metadataPath.BasePath}");
        AppLogger.Info($"  Active: {metadataPath.ActivePath} (exists: {Directory.Exists(metadataPath.ActivePath)})");
        AppLogger.Info($"  LAN: {metadataPath.LanPath} (exists: {Directory.Exists(metadataPath.LanPath)})");
        AppLogger.Info($"  Backup: {metadataPath.BackupPath} (exists: {Directory.Exists(metadataPath.BackupPath)})");
    }

    private static void WriteProfilesInfo()
    {
        // Ghi thông tin profiles từ UserDataManager
        var args = ConfigRevertManager.LoadRevertArgs();
        var gameId = args?.GameId ?? string.Empty;
        if (string.IsNullOrEmpty(gameId))
        {
            AppLogger.Info("Profiles info: no game ID available.");
            return;
        }

        var profilePaths = UserDataManager.GetProfilePaths(gameId);
        if (profilePaths.Count == 0)
        {
            AppLogger.Info("Profiles: none found.");
            return;
        }

        foreach (var profile in profilePaths)
        {
            AppLogger.Info($"Profile - BasePath: {profile.BasePath}");
            AppLogger.Info($"  Active: {profile.ActivePath} (exists: {Directory.Exists(profile.ActivePath)})");
            AppLogger.Info($"  LAN: {profile.LanPath} (exists: {Directory.Exists(profile.LanPath)})");
            AppLogger.Info($"  Backup: {profile.BackupPath} (exists: {Directory.Exists(profile.BackupPath)})");
        }
    }

    private static List<X509Certificate2> FilterMatchingCerts(List<X509Certificate2> certs, List<string> hosts)
    {
        var matchingCerts = new List<X509Certificate2>();
        foreach (var crt in certs)
        {
            if (crt.Subject.Contains(AppConstants.Name))
            {
                matchingCerts.Add(crt);
            }
            else
            {
                var dnsName = crt.GetNameInfo(X509NameType.DnsName, false);
                if (!string.IsNullOrEmpty(dnsName))
                {
                    if (MatchPattern(dnsName, hosts))
                    {
                        matchingCerts.Add(crt);
                    }
                }
                else
                {
                    if (MatchPattern(crt.Subject, hosts))
                    {
                        matchingCerts.Add(crt);
                    }
                }
            }
        }
        return matchingCerts;
    }

    private static bool MatchPattern(string pattern, List<string> hosts)
    {
        foreach (var host in hosts)
        {
            if (pattern == host)
                return true;

            if (pattern.Length > 1 && pattern.StartsWith("*."))
            {
                var suffix = pattern[1..];
                if (host.Length <= suffix.Length)
                    continue;
                if (!host.EndsWith(suffix))
                    continue;
                var prefix = host[..^suffix.Length];
                if (prefix.Length > 0 && !prefix.Contains("."))
                    return true;
            }
        }
        return false;
    }
}
