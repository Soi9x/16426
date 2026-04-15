using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;

namespace AgeLanServer.BattleServerManager.CmdUtils;

/// <summary>
/// Biến toàn cục lưu danh sách game ID từ dòng lệnh.
/// </summary>
public static class GameIds
{
    public static List<string> Ids { get; set; } = new();
}

/// <summary>
/// Tập hợp các game được hỗ trợ.
/// </summary>
public static class SupportedGames
{
    public const string GameAoE1 = "age1";
    public const string GameAoE2 = "age2";
    public const string GameAoE3 = "age3";
    public const string GameAoE4 = "age4";
    public const string GameAoM = "athens";

    private static readonly HashSet<string> _supported = new()
    {
        GameAoE1, GameAoE2, GameAoE3, GameAoE4, GameAoM
    };

    /// <summary>
    /// Kiểm tra xem game ID có được hỗ trợ không.
    /// </summary>
    public static bool IsSupported(string gameId) => _supported.Contains(gameId);

    /// <summary>
    /// Trả về tập hợp tất cả game được hỗ trợ.
    /// </summary>
    public static IReadOnlySet<string> AllGames => _supported;

    /// <summary>
    /// Kiểm tra xem tập hợp gameIds có là tập con của game được hỗ trợ không.
    /// </summary>
    public static bool IsSuperset(IEnumerable<string> gameIds) => _supported.IsSupersetOf(gameIds);
}

/// <summary>
/// Trình đọc cấu hình: phân tích game ID, kiểm tra server đang tồn tại.
/// </summary>
public static class ConfigReader
{
    /// <summary>
    /// Phân tích danh sách game ID.
    /// Nếu gameIds null hoặc rỗng, trả về tất cả game được hỗ trợ.
    /// Nếu có game không được hỗ trợ, trả về lỗi.
    /// </summary>
    public static (HashSet<string>? Games, string? Error) ParsedGameIds(List<string>? gameIds)
    {
        var ids = gameIds ?? GameIds.Ids;

        if (ids.Count == 0)
        {
            return (new HashSet<string>(SupportedGames.AllGames), null);
        }

        if (!SupportedGames.IsSuperset(ids))
        {
            return (null, "game(s) not supported");
        }

        return (new HashSet<string>(ids), null);
    }

    /// <summary>
    /// Đọc tất cả cấu hình server hiện có của một game.
    /// Trả về tập hợp tên (names) và khu vực (regions) đã tồn tại.
    /// </summary>
    public static (string? Error, HashSet<string> Names, HashSet<string> Regions) ExistingServers(string gameId)
    {
        var names = new HashSet<string>();
        var regions = new HashSet<string>();

        var configs = BattleServerConfigLib.Configs(gameId, onlyValid: true);

        foreach (var config in configs)
        {
            names.Add(config.Name.ToLowerInvariant());
            regions.Add(config.Region.ToLowerInvariant());
        }

        return (null, names, regions);
    }
}

/// <summary>
/// Trình ghi cấu hình Battle Server ra file TOML.
/// </summary>
public static class ConfigWriter
{
    /// <summary>
    /// Ghi cấu hình Battle Server ra file TOML.
    /// Tự động tìm index tiếp theo và tạo file trong thư mục cấu hình.
    /// </summary>
    public static string? WriteConfig(string gameId, Config config)
    {
        var folder = BattleServerConfigLib.Folder(gameId);

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            return $"error while creating folder \"{folder}\": {ex.Message}";
        }

        // Tìm index lớn nhất hiện có
        int maxIndex = -1;
        try
        {
            if (Directory.Exists(folder))
            {
                foreach (var entry in Directory.EnumerateFiles(folder, "*.toml"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(entry);
                    if (int.TryParse(fileName, out var index))
                    {
                        maxIndex = Math.Max(maxIndex, index);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return $"error while reading battle server config directory \"{folder}\": {ex.Message}";
        }

        var nextIndex = maxIndex + 1;
        var name = $"{nextIndex}.toml";
        var fullPath = Path.Combine(folder, name);

        try
        {
            var tomlText = SerializeConfigToToml(config);
            File.WriteAllText(fullPath, tomlText, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            return $"error while writing battle server config to file \"{name}\": {ex.Message}";
        }

        return null;
    }

    /// <summary>
    /// Tuần tự hóa Config thành chuỗi TOML.
    /// </summary>
    private static string SerializeConfigToToml(Config config)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Region = \"{EscapeTomlString(config.Region)}\"");
        sb.AppendLine($"Name = \"{EscapeTomlString(config.Name)}\"");
        sb.AppendLine($"IPv4 = \"{EscapeTomlString(config.IPv4)}\"");
        sb.AppendLine($"BsPort = {config.BsPort}");
        sb.AppendLine($"WebSocketPort = {config.WebSocketPort}");
        sb.AppendLine($"PID = {config.PID}");
        if (config.OutOfBandPort != 0)
        {
            sb.AppendLine($"OutOfBandPort = {config.OutOfBandPort}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Thoát ký tự đặc biệt trong chuỗi TOML.
    /// </summary>
    private static string EscapeTomlString(string value)
    {
        return value.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
    }
}

/// <summary>
/// Tiện ích mạng: kiểm tra cổng khả dụng, sinh cổng tự động.
/// </summary>
public static class Network
{
    /// <summary>
    /// Kiểm tra xem một cổng TCP có khả dụng (không bị chiếm) không.
    /// Thử bind vào cổng, nếu thành công thì cổng khả dụng.
    /// </summary>
    public static bool Available(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sinh các cổng TCP khả dụng.
    /// Với mỗi port = 0 trong danh sách, tìm một cổng khả dụng thay thế.
    /// </summary>
    public static (int[]? Ports, string? Error) GeneratePortsAsNeeded(int[] ports)
    {
        int portsToGenerate = ports.Count(p => p == 0);

        if (portsToGenerate == 0)
        {
            return (ports, null);
        }

        Console.WriteLine("Generating ports...");

        var generated = FindUnusedPorts(portsToGenerate);
        if (generated.Error is not null)
        {
            return (null, generated.Error);
        }

        var finalPorts = new int[ports.Length];
        int genIndex = 0;
        for (int i = 0; i < ports.Length; i++)
        {
            if (ports[i] == 0)
            {
                finalPorts[i] = generated.Ports![genIndex++];
            }
            else
            {
                finalPorts[i] = ports[i];
            }
        }

        return (finalPorts, null);
    }

    /// <summary>
    /// Tìm số lượng cổng TCP khả dụng bằng cách bind vào port 0.
    /// Hệ điều hành sẽ tự động chọn cổng khả dụng.
    /// </summary>
    private static (int[]? Ports, string? Error) FindUnusedPorts(int count)
    {
        var ports = new int[count];
        var listeners = new List<TcpListener>();

        for (int i = 0; i < count; i++)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, 0);
                listener.Start();
                listeners.Add(listener);
                var localEndpoint = (IPEndPoint)listener.LocalEndpoint;
                ports[i] = localEndpoint.Port;
            }
            catch (Exception ex)
            {
                foreach (var l in listeners)
                {
                    try { l.Stop(); } catch { }
                }
                return (null, ex.Message);
            }
        }

        foreach (var l in listeners)
        {
            try { l.Stop(); } catch { }
        }

        return (ports, null);
    }
}

/// <summary>
/// Tiện ích SSL: tự động tìm chứng chỉ SSL từ game.
/// </summary>
public static class SslHelper
{
    /// <summary>
    /// Phân giải đường dẫn file chứng chỉ SSL và khóa SSL.
    /// Nếu ssl.Auto = true, tự động tìm từ chứng chỉ của game.
    /// Nếu ssl.Auto = false, sử dụng đường dẫn do người dùng cung cấp.
    /// </summary>
    public static (string? CertFile, string? KeyFile, string? Error) ResolveSslFilesPath(
        string gameId,
        SslConfig ssl)
    {
        if (ssl.Auto)
        {
            Console.WriteLine("Auto resolving SSL certificate and key files...");

            var serverExe = FindServerExecutablePath();

            if (serverExe is null)
            {
                // Fallback: tìm cert trong thư mục hiện tại
                Console.WriteLine("Server exe not found, looking for certificates in current directory...");
                var certs = FindCertificatePairsInDirectory(".");
                if (!certs.Found)
                {
                    return (null, null, "could not find server executable and no local certificates found");
                }

                // AoE4 và AoM dùng cert thường, game khác dùng self-signed
                if (gameId == SupportedGames.GameAoE4 || gameId == SupportedGames.GameAoM)
                {
                    return (certs.Cert, certs.Key, null);
                }
                else
                {
                    return (certs.SelfSignedCert, certs.SelfSignedKey, null);
                }
            }
            else
            {
                var certs = FindCertificatePairs(serverExe);
                if (!certs.Found)
                {
                    return (null, null, "no SSL certificate and keys found");
                }

                // AoE4 và AoM dùng cert thường, game khác dùng self-signed
                if (gameId == SupportedGames.GameAoE4 || gameId == SupportedGames.GameAoM)
                {
                    return (certs.Cert, certs.Key, null);
                }
                else
                {
                    return (certs.SelfSignedCert, certs.SelfSignedKey, null);
                }
            }
        }

        // Khi Auto = false, kiểm tra đường dẫn người dùng cung cấp
        if (string.IsNullOrWhiteSpace(ssl.CertFile))
        {
            return (null, null, "invalid certificate file");
        }
        if (string.IsNullOrWhiteSpace(ssl.KeyFile))
        {
            return (null, null, "invalid key file");
        }

        if (!File.Exists(ssl.CertFile))
        {
            return (null, null, "invalid certificate file");
        }
        if (!File.Exists(ssl.KeyFile))
        {
            return (null, null, "invalid key file");
        }

        return (ssl.CertFile, ssl.KeyFile, null);
    }

    /// <summary>
    /// Tìm đường dẫn file thực thi server để tìm chứng chỉ SSL.
    /// </summary>
    private static string? FindServerExecutablePath()
    {
        // Tìm server.exe trong các thư mục thường gặp
        var candidates = new[]
        {
            "server.exe",
            Path.Combine("bin", "server.exe"),
            Path.Combine("Age of Empires IV", "server.exe"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    /// <summary>
    /// Tìm các cặp chứng chỉ SSL từ thư mục của game.
    /// </summary>
    private static (bool Found, string? Cert, string? Key, string? SelfSignedCert, string? SelfSignedKey)
        FindCertificatePairs(string serverExe)
    {
        var dir = Path.GetDirectoryName(serverExe);
        if (string.IsNullOrEmpty(dir))
        {
            return (false, null, null, null, null);
        }

        string? cert = null;
        string? key = null;
        string? selfSignedCert = null;
        string? selfSignedKey = null;

        // Tìm các file chứng chỉ thường gặp
        var certNames = new[] { "server.crt", "cert.pem", "server.pem" };
        var keyNames = new[] { "server.key", "key.pem", "server-key.pem" };
        var selfSignedCertNames = new[] { "self-signed.crt", "selfsigned.crt" };
        var selfSignedKeyNames = new[] { "self-signed.key", "selfsigned.key" };

        foreach (var name in certNames)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
            {
                cert = path;
                break;
            }
        }

        foreach (var name in keyNames)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
            {
                key = path;
                break;
            }
        }

        foreach (var name in selfSignedCertNames)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
            {
                selfSignedCert = path;
                break;
            }
        }

        foreach (var name in selfSignedKeyNames)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path)) { selfSignedKey = path; break; }
        }

        // Kiểm tra thêm trong thư mục resources/certificates (quan trọng)
        var resCertDir = Path.Combine(dir, "resources", "certificates");
        if (Directory.Exists(resCertDir))
        {
            cert ??= certNames.Select(n => Path.Combine(resCertDir, n)).FirstOrDefault(File.Exists);
            key ??= keyNames.Select(n => Path.Combine(resCertDir, n)).FirstOrDefault(File.Exists);
            selfSignedCert ??= selfSignedCertNames.Select(n => Path.Combine(resCertDir, n)).FirstOrDefault(File.Exists);
            selfSignedKey ??= selfSignedKeyNames.Select(n => Path.Combine(resCertDir, n)).FirstOrDefault(File.Exists);
        }

        bool found = cert is not null && key is not null;
        bool selfSignedFound = selfSignedCert is not null && selfSignedKey is not null;

        return (found || selfSignedFound, cert, key, selfSignedCert, selfSignedKey);
    }

    /// <summary>
    /// Tìm cặp chứng chỉ trong thư mục hiện tại (không dựa vào server exe).
    /// </summary>
    private static (bool Found, string? Cert, string? Key, string? SelfSignedCert, string? SelfSignedKey)
        FindCertificatePairsInDirectory(string directory)
    {
        string? cert = null;
        string? key = null;
        string? selfSignedCert = null;
        string? selfSignedKey = null;

        var certNames = new[] { "server.crt", "cert.pem", "server.pem" };
        var keyNames = new[] { "server.key", "key.pem", "server-key.pem" };
        var selfSignedCertNames = new[] { "self-signed.crt", "selfsigned_cert.pem", "selfsigned.crt" };
        var selfSignedKeyNames = new[] { "self-signed.key", "selfsigned_key.pem", "selfsigned.key" };

        foreach (var name in certNames)
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path)) { cert = path; break; }
        }
        foreach (var name in keyNames)
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path)) { key = path; break; }
        }
        foreach (var name in selfSignedCertNames)
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path)) { selfSignedCert = path; break; }
        }
        foreach (var name in selfSignedKeyNames)
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path)) { selfSignedKey = path; break; }
        }

        // Kiểm tra thêm thư mục resources/certificates
        var resCertDir = Path.Combine(directory, "resources", "certificates");
        if (Directory.Exists(resCertDir))
        {
            cert ??= certNames.Select(n => Path.Combine(resCertDir, n)).FirstOrDefault(File.Exists);
            key ??= keyNames.Select(n => Path.Combine(resCertDir, n)).FirstOrDefault(File.Exists);
            selfSignedCert ??= selfSignedCertNames.Select(n => Path.Combine(resCertDir, n)).FirstOrDefault(File.Exists);
            selfSignedKey ??= selfSignedKeyNames.Select(n => Path.Combine(resCertDir, n)).FirstOrDefault(File.Exists);
        }

        bool found = cert is not null && key is not null;
        bool selfSignedFound = selfSignedCert is not null && selfSignedKey is not null;

        return (found || selfSignedFound, cert, key, selfSignedCert, selfSignedKey);
    }
}

/// <summary>
/// Cấu hình SSL dùng trong ResolveSslFilesPath.
/// </summary>
public sealed class SslConfig
{
    public bool Auto { get; set; } = true;
    public string CertFile { get; set; } = string.Empty;
    public string KeyFile { get; set; } = string.Empty;
}

/// <summary>
/// Trình thực thi Battle Server: phân giải đường dẫn, sinh tham số, khởi chạy tiến trình.
/// </summary>
public static class Executor
{
    /// <summary>
    /// Phân giải đường dẫn file thực thi Battle Server.
    /// Nếu executablePath = "auto", tự động tìm trong Steam hoặc Xbox.
    /// </summary>
    public static (string? ResolvedPath, string? Error) ResolvePath(string gameId, string executablePath)
    {
        bool ValidPath(string path) => File.Exists(path);

        if (executablePath == "auto")
        {
            Console.WriteLine("Auto resolving executable path...");

            // AoE4 và AoM dùng BattleServer của AoE2 (theo TODO trong code gốc)
            var resolvedGameId = gameId;
            if (gameId == SupportedGames.GameAoE4 || gameId == SupportedGames.GameAoM)
            {
                resolvedGameId = SupportedGames.GameAoE2;
            }

            var battleServerPath = resolvedGameId == SupportedGames.GameAoE2
                ? Path.Combine("BattleServer", "BattleServer.exe")
                : "BattleServer.exe";

            // Tìm trong Steam
            var steamPath = FindInSteam(resolvedGameId, battleServerPath);
            if (steamPath is not null && ValidPath(steamPath))
            {
                Console.WriteLine("\tFound in Steam");
                return (steamPath, null);
            }

            // Tìm trong Xbox (Microsoft Store)
            var xboxPath = FindInXbox(resolvedGameId, battleServerPath);
            if (xboxPath is not null && ValidPath(xboxPath))
            {
                Console.WriteLine("\tFound on Xbox");
                return (xboxPath, null);
            }

            // Tìm trong thư mục hiện tại (fallback cho LAN server)
            // Thử cả 2 khả năng: trực tiếp hoặc trong thư mục con BattleServer
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "BattleServer.exe"),
                Path.Combine(baseDir, "BattleServer", "BattleServer.exe"),
                Path.Combine(baseDir, battleServerPath)
            };
            
            foreach (var candidate in candidates.Distinct())
            {
                if (ValidPath(candidate))
                {
                    Console.WriteLine($"\tFound in current directory: {Path.GetFileName(Path.GetDirectoryName(candidate))}/{Path.GetFileName(candidate)}");
                    return (candidate, null);
                }
            }

            return (null, "could not find battle server executable");
        }

        // Đường dẫn do người dùng cung cấp
        if (File.Exists(executablePath))
        {
            return (Path.GetFullPath(executablePath), null);
        }

        return (null, "invalid battle server executable path");
    }

    /// <summary>
    /// Tìm Battle Server trong thư mục Steam.
    /// Đọc từ steamapps/common hoặc thư viện Steam.
    /// </summary>
    private static string? FindInSteam(string gameId, string battleServerPath)
    {
        // ID ứng dụng Steam cho từng game
        var steamAppIds = new Dictionary<string, string>
        {
            { SupportedGames.GameAoE1, "221380" },  // Age of Empires: DE
            { SupportedGames.GameAoE2, "813780" },  // Age of Empires II: DE
            { SupportedGames.GameAoE3, "933110" },  // Age of Empires III: DE
        };

        if (!steamAppIds.TryGetValue(gameId, out var appId))
        {
            return null;
        }

        // Tìm thư mục Steam từ registry hoặc biến môi trường
        var steamPaths = GetSteamLibraryFolders();

        foreach (var libraryFolder in steamPaths)
        {
            var gameFolder = Path.Combine(libraryFolder, "steamapps", "common", $"Age of Empires {GetGameNumber(gameId)}");
            var fullPath = Path.Combine(gameFolder, battleServerPath);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    /// <summary>
    /// Tìm thư mục thư viện Steam từ registry và libraryfolders.vdf.
    /// </summary>
    private static List<string> GetSteamLibraryFolders()
    {
        var folders = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Valve\Steam");
                if (key is not null)
                {
                    var steamPath = key.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(steamPath))
                    {
                        folders.Add(steamPath);

                        // Thêm các thư viện bổ sung
                        var libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                        if (File.Exists(libraryFile))
                        {
                            var lines = File.ReadAllLines(libraryFile);
                            foreach (var line in lines)
                            {
                                if (line.Contains("\"path\""))
                                {
                                    var path = line.Split('"')[3];
                                    if (Directory.Exists(path))
                                    {
                                        folders.Add(path);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi registry
            }
        }

        return folders;
    }

    /// <summary>
    /// Tìm Battle Server trong thư mục Xbox (Microsoft Store / AppX).
    /// </summary>
    private static string? FindInXbox(string gameId, string battleServerPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        // Package Family Names cho từng game trên Microsoft Store
        var packageNames = new Dictionary<string, string>
        {
            { SupportedGames.GameAoE1, "Microsoft.AgeofEmpiresUltimateEdition" },
            { SupportedGames.GameAoE2, "Microsoft.AgeofEmpiresIIDE" },
            { SupportedGames.GameAoE3, "Microsoft.AgeofEmploysIIIDEF" },
            { SupportedGames.GameAoE4, "Microsoft.AgeofEmpiresIV" },
        };

        if (!packageNames.TryGetValue(gameId, out var packageFamily))
        {
            return null;
        }

        // Tìm trong thư mục WindowsApps
        var windowsAppsDirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps"),
        };

        foreach (var baseDir in windowsAppsDirs)
        {
            if (!Directory.Exists(baseDir)) continue;

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(baseDir, $"{packageFamily}*"))
                {
                    var fullPath = Path.Combine(dir, battleServerPath);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }
            catch
            {
                // Có thể không có quyền truy cập WindowsApps
            }
        }

        return null;
    }

    /// <summary>
    /// Lấy số game để hiển thị trong tên thư mục.
    /// </summary>
    private static string GetGameNumber(string gameId)
    {
        return gameId switch
        {
            SupportedGames.GameAoE1 => "DE",
            SupportedGames.GameAoE2 => "II",
            SupportedGames.GameAoE3 => "III",
            SupportedGames.GameAoE4 => "IV",
            _ => "",
        };
    }

    /// <summary>
    /// Thực thi Battle Server với các tham số cần thiết.
    /// Trả về PID của tiến trình hoặc lỗi.
    /// </summary>
    public static (uint? Pid, string? Error) ExecuteBattleServer(
        string gameId,
        string path,
        string region,
        string name,
        int[] ports,
        string certFile,
        string keyFile,
        string[] extraArgs,
        bool hideWindow,
        string? logRoot)
    {
        // simulationPeriod phụ thuộc vào game
        var simulationPeriod = gameId switch
        {
            SupportedGames.GameAoE1 => 25,
            SupportedGames.GameAoE3 or SupportedGames.GameAoM => 50,
            _ => 125, // AoE2, AoE4
        };

        // Xây dựng danh sách tham số
        var args = new List<string>
        {
            "-region", region,
            "-name", name,
            "-relaybroadcastPort", "0",
            "-simulationPeriod", simulationPeriod.ToString(),
            "-bsPort", ports[0].ToString(),
            "-webSocketPort", ports[1].ToString(),
            "-sslCert", certFile,
            "-sslKey", keyFile,
        };

        // Thêm outOfBandPort nếu có (AoE1 không dùng)
        if (ports.Length > 2 && ports[2] != -1)
        {
            args.Add("-outOfBandPort");
            args.Add(ports[2].ToString());
        }

        // Thêm tham số bổ sung
        args.AddRange(extraArgs);

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = string.Join(" ", args.Select(EscapeArg)),
            UseShellExecute = false,
            CreateNoWindow = hideWindow,
            RedirectStandardOutput = hideWindow && !string.IsNullOrEmpty(logRoot),
            RedirectStandardError = hideWindow && !string.IsNullOrEmpty(logRoot),
            WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory,
        };

        // Nếu ẩn cửa sổ, ghi log ra file
        FileStream? logFile = null;
        if (hideWindow && !string.IsNullOrEmpty(logRoot))
        {
            try
            {
                Directory.CreateDirectory(logRoot);
                var logPath = Path.Combine(logRoot, $"battle-server-{DateTimeOffset.Now.ToUnixTimeSeconds()}.log");
                logFile = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
            }
            catch
            {
                // Bỏ qua lỗi tạo log
            }
        }

        Console.WriteLine($"Executing: {startInfo.FileName} {startInfo.Arguments}");

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return (null, "could not start process");
            }

            // Ghi log nếu có
            if (logFile is not null)
            {
                _ = Task.Run(async () =>
                {
                    using var reader = new StreamReader(process.StandardOutput.BaseStream);
                    var buffer = new char[1024];
                    int read;
                    while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await logFile.WriteAsync(Encoding.UTF8.GetBytes(buffer.AsSpan(0, read).ToArray()), 0, read);
                    }
                });

                _ = Task.Run(async () =>
                {
                    using var reader = new StreamReader(process.StandardError.BaseStream);
                    var buffer = new char[1024];
                    int read;
                    while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await logFile.WriteAsync(Encoding.UTF8.GetBytes(buffer.AsSpan(0, read).ToArray()), 0, read);
                    }
                });
            }

            return ((uint)process.Id, null);
        }
        catch (Exception ex)
        {
            logFile?.Dispose();
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// Thoát ký tự đặc biệt trong tham số dòng lệnh.
    /// </summary>
    private static string EscapeArg(string arg)
    {
        if (arg.IndexOfAny(new[] { ' ', '"', '\\', '\t', '\n' }) >= 0)
        {
            return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
        return arg;
    }
}

/// <summary>
/// Tiện ích xóa: Kill tiến trình, xóa file cấu hình.
/// </summary>
public static class Remove
{
    /// <summary>
    /// Xóa cấu hình cho một game, với tùy chọn chỉ xóa server không hợp lệ.
    /// </summary>
    public static bool RemoveConfigs(string gameId, List<Config> configs, bool onlyInvalid)
    {
        bool removedAny = false;

        foreach (var config in configs)
        {
            bool doRemove = !onlyInvalid || !config.Validate();

            if (doRemove)
            {
                bool removed = RemoveConfig(gameId, config);
                removedAny = removedAny || removed;
            }
        }

        return removedAny;
    }

    /// <summary>
    /// Xóa một cấu hình cụ thể: kill tiến trình và xóa file TOML.
    /// </summary>
    private static bool RemoveConfig(string gameId, Config config)
    {
        Console.WriteLine($"\tRemoving: {config.Region}");
        Kill(config);

        var folder = BattleServerConfigLib.Folder(gameId);
        if (!Directory.Exists(folder))
        {
            return false;
        }

        var fullPath = Path.Combine(folder, config.Path());
        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                Console.WriteLine("\t\tRemoving config file... OK");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\t\tRemoving config file... failed with error: {ex.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// Kill tiến trình Battle Server theo PID.
    /// </summary>
    public static bool Kill(Config config)
    {
        try
        {
            var proc = Process.GetProcessById((int)config.PID);
            if (proc is not null && !proc.HasExited)
            {
                Console.Write("\t\tProcess still running, killing it... ");
                proc.Kill(true);
                proc.WaitForExit(TimeSpan.FromSeconds(5));
                Console.WriteLine("OK");
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\t\tProcess kill failed: {ex.Message}");
        }
        return false;
    }
}

/// <summary>
/// Đợi Battle Server khởi tạo xong.
/// Kiểm tra Validate liên tục trong tối đa 10 giây.
/// </summary>
public static class Connection
{
    /// <summary>
    /// Đợi tối đa 30 giây để Battle Server hoàn tất khởi tạo.
    /// Kiểm tra Validate() mỗi 500ms.
    /// </summary>
    public static bool WaitForBattleServerInit(Config config)
    {
        var timeout = TimeSpan.FromSeconds(30);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            if (config.Validate())
            {
                return true;
            }
            Thread.Sleep(500);
        }

        return false;
    }
}
