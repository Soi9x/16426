// Port từ common/game/steam/ (4 file: steam.go, steam_windows.go, steam_unix.go, steam_linux.go)
// Tích hợp Steam: tìm thư viện Steam, xác định đường dẫn game, tạo URI khởi động game.
// Hỗ trợ đa nền tảng: Windows (registry), Linux (thư mục chuẩn).

using System.Runtime.InteropServices;
using AgeLanServer.Common;

namespace AgeLanServer.Common;

/// <summary>
/// Thông tin game trên Steam.
/// </summary>
public class SteamGame
{
    /// <summary>
    /// App ID của game trên Steam.
    /// </summary>
    public string AppId { get; }

    public SteamGame(string appId)
    {
        AppId = appId;
    }

    /// <summary>
    /// Tạo SteamGame từ game title.
    /// </summary>
    public static SteamGame FromGameId(string gameId)
    {
        return new SteamGame(GetAppId(gameId));
    }

    /// <summary>
    /// Trả về App ID tương ứng với game title.
    /// </summary>
    private static string GetAppId(string gameId)
    {
        return gameId switch
        {
            AppConstants.GameAoE1 => "1017900",
            AppConstants.GameAoE2 => "813780",
            AppConstants.GameAoE3 => "933110",
            AppConstants.GameAoE4 => "1466860",
            AppConstants.GameAoM => "1934680",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Tạo Steam URI để khởi động game từ trình duyệt/launcher.
    /// Ví dụ: steam://rungameid/1466860
    /// </summary>
    public string OpenUri()
    {
        return $"steam://rungameid/{AppId}";
    }

    /// <summary>
    /// Tìm thư mục thư viện Steam chứa game này.
    /// Đọc file config/libraryfolders.vdf để tìm game theo AppId.
    /// </summary>
    public string? FindLibraryFolder()
    {
        var configPath = GetSteamConfigPath();
        if (string.IsNullOrEmpty(configPath))
            return null;

        // Thử mở libraryfolders.vdf
        var libraryFile = Path.Combine(configPath, "config", "libraryfolders.vdf");
        if (!TryParseLibraryFile(libraryFile, out var folder))
        {
            // Thử phương án dự phòng
            var altPath = GetSteamConfigPathAlt();
            if (string.IsNullOrEmpty(altPath))
                return null;

            libraryFile = Path.Combine(altPath, "config", "libraryfolders.vdf");
            if (!TryParseLibraryFile(libraryFile, out folder))
                return null;
        }

        return folder;
    }

    /// <summary>
    /// Tìm đường dẫn cài đặt game từ thư mục thư viện.
    /// Đọc file appmanifest_{AppId}.acf để lấy installdir.
    /// </summary>
    public string? FindGamePath(string libraryFolder)
    {
        var basePath = Path.Combine(libraryFolder, "steamapps");
        var manifestFile = Path.Combine(basePath, $"appmanifest_{AppId}.acf");

        if (!File.Exists(manifestFile))
            return null;

        try
        {
            var installDir = ParseInstallDirFromManifest(manifestFile);
            if (string.IsNullOrEmpty(installDir))
                return null;

            var gamePath = Path.Combine(basePath, "common", installDir);
            return Directory.Exists(gamePath) ? gamePath : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Phân tích file libraryfolders.vdf (định dạng VDF đơn giản).
    /// Tìm thư mục chứa AppId của game.
    /// </summary>
    private bool TryParseLibraryFile(string filePath, out string? folder)
    {
        folder = null;
        if (!File.Exists(filePath))
            return false;

        try
        {
            // Phân tích VDF thủ công (không dùng thư viện ngoài)
            // Format: "path" "C:\\Games\\Steam" ... "apps" { "AppId" {} }
            var lines = File.ReadAllLines(filePath);
            string? currentPath = null;
            bool inAppsSection = false;
            int braceDepth = 0;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                // Tìm path
                if (line.StartsWith("\"path\""))
                {
                    currentPath = ExtractQuotedValue(line);
                }

                // Vào section apps
                if (line.StartsWith("\"apps\""))
                {
                    inAppsSection = true;
                    braceDepth = 0;
                    continue;
                }

                if (inAppsSection && !string.IsNullOrEmpty(currentPath))
                {
                    // Đếm brace
                    foreach (var ch in line)
                    {
                        if (ch == '{') braceDepth++;
                        if (ch == '}') braceDepth--;
                    }

                    // Kiểm tra xem dòng có chứa AppId không
                    if (line.StartsWith($"\"{AppId}\""))
                    {
                        folder = currentPath;
                        return true;
                    }

                    // Thoát section apps
                    if (braceDepth <= 0 && line.Contains('}'))
                    {
                        inAppsSection = false;
                        currentPath = null;
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Trích xuất giá trị trong dấu ngoặc kép từ dòng VDF.
    /// Ví dụ: "path"  "C:\\Games"  => C:\\Games
    /// </summary>
    private static string? ExtractQuotedValue(string line)
    {
        var firstQuote = line.IndexOf('"');
        if (firstQuote < 0) return null;
        var secondQuote = line.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0) return null;
        var thirdQuote = line.IndexOf('"', secondQuote + 1);
        if (thirdQuote < 0) return null;
        var fourthQuote = line.IndexOf('"', thirdQuote + 1);
        if (fourthQuote < 0) return null;
        return line.Substring(thirdQuote + 1, fourthQuote - thirdQuote - 1);
    }

    /// <summary>
    /// Phân tích file appmanifest để lấy installdir.
    /// </summary>
    private static string? ParseInstallDirFromManifest(string filePath)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                // "installdir"  "GameFolder"
                if (line.StartsWith("\"installdir\""))
                {
                    var firstQuote = line.IndexOf('"');
                    var secondQuote = line.IndexOf('"', firstQuote + 1);
                    var thirdQuote = line.IndexOf('"', secondQuote + 1);
                    var fourthQuote = line.IndexOf('"', thirdQuote + 1);
                    if (fourthQuote > thirdQuote)
                        return line.Substring(thirdQuote + 1, fourthQuote - thirdQuote - 1);
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Lấy đường dẫn cấu hình Steam tùy hệ điều hành.
    /// </summary>
    private static string? GetSteamConfigPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetSteamConfigPathWindows();
        return GetSteamConfigPathLinux();
    }

    private static string? GetSteamConfigPathAlt()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetSteamConfigPathAltWindows();
        return null; // Linux không có alt
    }

    #region Windows

    /// <summary>
    /// [Windows] Đọc đường dẫn Steam từ registry HKCU\SOFTWARE\Valve\Steam.
    /// </summary>
    private static string? GetSteamConfigPathWindows()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// [Windows] Đọc đường dẫn Steam từ registry uninstall (dự phòng).
    /// </summary>
    private static string? GetSteamConfigPathAltWindows()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam");
            var uninstallStr = key?.GetValue("UninstallString") as string;
            if (!string.IsNullOrEmpty(uninstallStr))
                return Path.GetDirectoryName(uninstallStr);
        }
        catch { }
        return null;
    }

    #endregion

    #region Linux

    /// <summary>
    /// [Linux] Tìm thư mục Steam trong các vị trí phổ biến.
    /// Hỗ trợ: cài đặt chính thức, Snap, Flatpak, Lutris.
    /// </summary>
    private static string? GetSteamConfigPathLinux()
    {
        var homeDir = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(homeDir))
            return null;

        var dirs = new[]
        {
            // Chính thức
            Path.Combine(homeDir, ".steam", "steam"),
            // Thay thế chính thức
            Path.Combine(homeDir, ".local", "share", "Steam"),
            // Snap
            Path.Combine(homeDir, "snap", "steam", "common", ".steam", "steam"),
            // Flatpak
            Path.Combine(homeDir, ".var", "app", "com.valvesoftware.Steam", ".steam", "steam"),
            // Ít phổ biến hơn
            Path.Combine(homeDir, ".steam", "debian-installation"),
            Path.Combine(homeDir, ".steam"),
            Path.Combine(homeDir, ".local", "share", "steam"),
            Path.Combine(homeDir, "snap", "steam", "common", ".local", "share", "Steam"),
            Path.Combine(homeDir, ".var", "app", "com.valvesoftware.Steam", "data", "Steam"),
            "/usr/share/steam",
            "/usr/local/share/steam"
        };

        foreach (var dir in dirs)
        {
            if (Directory.Exists(dir))
                return dir;
        }

        return null;
    }

    #endregion

    #region Linux - UserProfile

    /// <summary>
    /// [Linux] Đường dẫn user profile cho game thông qua Proton/compatdata.
    /// Chỉ áp dụng trên Linux.
    /// </summary>
    public static string? GetUserProfilePath(string gameId)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return null;

        var steamPath = GetSteamConfigPathLinux();
        if (string.IsNullOrEmpty(steamPath))
            return null;

        var appId = GetAppId(gameId);
        if (string.IsNullOrEmpty(appId))
            return null;

        return Path.Combine(
            steamPath,
            "steamapps",
            "compatdata",
            appId,
            "pfx",
            "drive_c",
            "users",
            "steamuser"
        );
    }

    #endregion
}
