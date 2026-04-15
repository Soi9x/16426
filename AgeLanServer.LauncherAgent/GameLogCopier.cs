using AgeLanServer.Common;
using AgeLanServer.LauncherCommon;

namespace AgeLanServer.LauncherAgent;

/// <summary>
/// Sao chép file log game sau khi game thoát.
/// Tương đương launcher-agent/internal/gameLogs/ (gameLogs.go, age1.go, age2.go, age3.go, age4.go, athens.go) trong bản Go gốc.
/// </summary>
public static class GameLogCopier
{
    // Các mẫu glob cho tên file log động (tương đương các hằng số trong gameLogs.go).
    // Pattern: ?? đại diện cho 2 ký tự bất kỳ (ngày/giờ).
    private const string ElementSepDot = ".";
    private const string ElementSepDash = "-";
    private const string MinElement = "??";
    private const string MinElementSepDot = MinElement + ElementSepDot;
    private const string MinElementSepDash = MinElement + ElementSepDash;

    // Date patterns: ??.??.?? (ngày) và ??-??-?? (ngày với dash)
    private const string DateDotPattern = MinElement + MinElementSepDot + MinElementSepDot + MinElement;
    private const string DateDashPattern = MinElement + MinElement + ElementSepDash + MinElement + ElementSepDash + MinElement;

    // Time patterns
    private const string TimeDashPattern = MinElement + ElementSepDash + MinElement + ElementSepDash + MinElement;

    // DateTime patterns
    private const string DateTimePrefixGlob = DateDotPattern + "-" + MinElement;
    private const string DateTimeGlob = DateTimePrefixGlob + ElementSepDot + MinElementSepDot + MinElement;
    private const string DateTimeNoDotGlob = DateTimePrefixGlob + MinElementSepDot + MinElement;
    private const string DateTimeDashGlob = DateDashPattern + ElementSepDot + TimeDashPattern;

    /// <summary>
    /// Interface cho chiến lược lấy đường dẫn log theo từng game.
    /// Tương đương Game interface trong Go.
    /// </summary>
    private interface IGameLogStrategy
    {
        /// <summary>
        /// Trả về danh sách đường dẫn file/thư mục log cần sao chép.
        /// basePath: đường dẫn thư mục dữ liệu user của game (từ UserDataManager.GetBasePath).
        /// </summary>
        IReadOnlyList<string> GetPaths(string basePath);
    }

    /// <summary>
    /// Chiến lược log cho AoE1: Definitive Edition.
    /// - Logs/StartupLog.txt
    /// - Logs/&lt;DateTime&gt;_base_log.txt (file mới nhất khớp pattern)
    /// </summary>
    private sealed class GameAoE1LogStrategy : IGameLogStrategy
    {
        public IReadOnlyList<string> GetPaths(string basePath)
        {
            var paths = new List<string>();
            var logsPath = Path.Combine(basePath, "Games", "Age of Empires DE", "Logs");

            if (!Directory.Exists(logsPath))
                return paths;

            // StartupLog.txt
            var startupLog = Path.Combine(logsPath, "StartupLog.txt");
            if (File.Exists(startupLog))
                paths.Add(startupLog);

            // File base_log mới nhất khớp pattern dateTime + "_base_log.txt"
            var newest = FindNewestFile(logsPath, "*_base_log.txt");
            if (newest != null)
                paths.Add(newest);

            return paths;
        }
    }

    /// <summary>
    /// Chiến lược log cho AoE2: Definitive Edition.
    /// - logs/Age2SessionData.txt
    /// - Thư mục log mới nhất khớp pattern dateTime (không dấu chấm)
    /// </summary>
    private sealed class GameAoE2LogStrategy : IGameLogStrategy
    {
        public IReadOnlyList<string> GetPaths(string basePath)
        {
            var paths = new List<string>();
            var logsPath = Path.Combine(basePath, "Games", "Age of Empires 2 DE", "logs");

            if (!Directory.Exists(logsPath))
                return paths;

            // Age2SessionData.txt
            var sessionData = Path.Combine(logsPath, "Age2SessionData.txt");
            if (File.Exists(sessionData))
                paths.Add(sessionData);

            // Thư mục log mới nhất
            var newestDir = FindNewestDirectory(logsPath);
            if (newestDir != null)
                paths.Add(newestDir);

            return paths;
        }
    }

    /// <summary>
    /// Chiến lược log cho AoE3: Definitive Edition.
    /// - Logs/Age3SessionData.txt
    /// - Logs/Age3Log.txt
    /// </summary>
    private sealed class GameAoE3LogStrategy : IGameLogStrategy
    {
        public IReadOnlyList<string> GetPaths(string basePath)
        {
            var paths = new List<string>();
            var logsPath = Path.Combine(basePath, "Games", "Age of Empires 3 DE", "Logs");

            if (!Directory.Exists(logsPath))
                return paths;

            // Age3SessionData.txt
            var sessionData = Path.Combine(logsPath, "Age3SessionData.txt");
            if (File.Exists(sessionData))
                paths.Add(sessionData);

            // Age3Log.txt
            var logFile = Path.Combine(logsPath, "Age3Log.txt");
            if (File.Exists(logFile))
                paths.Add(logFile);

            return paths;
        }
    }

    /// <summary>
    /// Chiến lược log cho AoE4: Anniversary Edition.
    /// - session_data.txt
    /// - warnings.log
    /// - LogFiles/unhandled.&lt;DateTime&gt;.txt (file mới nhất khớp pattern)
    /// </summary>
    private sealed class GameAoE4LogStrategy : IGameLogStrategy
    {
        public IReadOnlyList<string> GetPaths(string basePath)
        {
            var paths = new List<string>();

            // AoE4 không có prefix "Games", dùng trực tiếp basePath
            var possibleFiles = new[] { "session_data.txt", "warnings.log" };
            foreach (var fileName in possibleFiles)
            {
                var fullPath = Path.Combine(basePath, fileName);
                if (File.Exists(fullPath))
                    paths.Add(fullPath);
            }

            // LogFiles/unhandled.<DateTime>.txt
            var logsPath = Path.Combine(basePath, "LogFiles");
            if (!Directory.Exists(logsPath))
                return paths;

            var newest = FindNewestFile(logsPath, "unhandled.*.txt");
            if (newest != null)
                paths.Add(newest);

            return paths;
        }
    }

    /// <summary>
    /// Chiến lược log cho Age of Mythology: Retold.
    /// - temp/Logs/mythsessiondata.txt
    /// - temp/Logs/mythlog.txt
    /// </summary>
    private sealed class GameAoMLogStrategy : IGameLogStrategy
    {
        public IReadOnlyList<string> GetPaths(string basePath)
        {
            var paths = new List<string>();
            var logsPath = Path.Combine(basePath, "Games", "Age of Mythology Retold", "temp", "Logs");

            if (!Directory.Exists(logsPath))
                return paths;

            // mythsessiondata.txt
            var sessionData = Path.Combine(logsPath, "mythsessiondata.txt");
            if (File.Exists(sessionData))
                paths.Add(sessionData);

            // mythlog.txt
            var logFile = Path.Combine(logsPath, "mythlog.txt");
            if (File.Exists(logFile))
                paths.Add(logFile);

            return paths;
        }
    }

    // Bảng ánh xạ gameId -> chiến lược log
    private static readonly Dictionary<string, IGameLogStrategy> GameIdToStrategy = new()
    {
        { GameIds.AgeOfEmpires1, new GameAoE1LogStrategy() },
        { GameIds.AgeOfEmpires2, new GameAoE2LogStrategy() },
        { GameIds.AgeOfEmpires3, new GameAoE3LogStrategy() },
        { GameIds.AgeOfEmpires4, new GameAoE4LogStrategy() },
        { GameIds.AgeOfMythology, new GameAoMLogStrategy() }
    };

    /// <summary>
    /// Tìm file mới nhất (theo ModTime) khớp với glob pattern trong thư mục.
    /// Tương đương addNewestPath trong Go.
    /// </summary>
    private static string? FindNewestFile(string directory, string searchPattern)
    {
        try
        {
            var files = Directory.GetFiles(directory, searchPattern)
                .Select(f => new FileInfo(f))
                .Where(f => !f.Attributes.HasFlag(FileAttributes.Directory))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            return files.Count > 0 ? files[0].FullName : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tìm thư mục con mới nhất (theo ModTime) trong thư mục.
    /// Dùng cho AoE2 - thư mục log có tên theo ngày.
    /// </summary>
    private static string? FindNewestDirectory(string parentDir)
    {
        try
        {
            var dirs = Directory.GetDirectories(parentDir)
                .Select(d => new DirectoryInfo(d))
                .OrderByDescending(d => d.LastWriteTimeUtc)
                .ToList();

            return dirs.Count > 0 ? dirs[0].FullName : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sao chép nội dung file từ src sang dst.
    /// Trả về true nếu thành công.
    /// Tương đương copyFileContent trong Go.
    /// </summary>
    private static bool CopyFileContent(string src, string dst)
    {
        try
        {
            using var sourceStream = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destStream = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None);
            sourceStream.CopyTo(destStream);

            // Sao chép permissions
            var sourceInfo = new FileInfo(src);
            var destInfo = new FileInfo(dst);
            destInfo.Attributes = sourceInfo.Attributes;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sao chép đường dẫn (file hoặc thư mục) vào thư mục đích.
    /// Trả về true nếu thành công.
    /// Tương đương copyPathToDir trong Go.
    /// </summary>
    private static bool CopyPathToDir(string srcPath, string dstDir)
    {
        try
        {
            var info = new FileInfo(srcPath);
            var baseName = Path.GetFileName(srcPath);
            var finalDstPath = Path.Combine(dstDir, baseName);

            // Nếu là file, sao chép trực tiếp
            if (!info.Attributes.HasFlag(FileAttributes.Directory))
            {
                Directory.CreateDirectory(dstDir);
                return CopyFileContent(srcPath, finalDstPath);
            }

            // Nếu là thư mục, sao chép đệ quy
            Directory.CreateDirectory(finalDstPath);
            CopyDirectory(srcPath, finalDstPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sao chép toàn bộ thư mục (đệ quy).
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            try
            {
                File.Copy(file, destFile, overwrite: true);
            }
            catch
            {
                // Bỏ qua file không sao chép được
            }
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }

    /// <summary>
    /// Sao chép tất cả log game tới thư mục đích.
    /// Tương đương CopyGameLogs trong Go.
    /// </summary>
    /// <param name="gameId">ID game (age1, age2, age3, age4, athens)</param>
    /// <param name="destinationDir">Thư mục đích cho log</param>
    /// <param name="ct">Cancellation token</param>
    public static async Task CopyGameLogsAsync(string gameId, string destinationDir, CancellationToken ct = default)
    {
        AppLogger.Info("Đang sao chép log game...");

        if (!GameIdToStrategy.TryGetValue(gameId, out var strategy))
        {
            AppLogger.Warn($"Không có chiến lược log cho game: {gameId}");
            return;
        }

        // Lấy đường dẫn cơ sở cho dữ liệu user game
        var basePath = UserDataManager.GetBasePath(gameId);

        if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
        {
            AppLogger.Warn($"Không tìm thấy thư mục dữ liệu user cho game: {gameId}");
            return;
        }

        var logPaths = strategy.GetPaths(basePath);

        if (logPaths.Count == 0)
        {
            AppLogger.Info($"Không tìm thấy file log nào cho game: {gameId}");
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var logPath in logPaths)
        {
            if (ct.IsCancellationRequested)
                break;

            var status = $"Sao chép {logPath}... ";
            var ok = CopyPathToDir(logPath, destinationDir);
            status += ok ? "OK" : "KO";
            AppLogger.Info(status);
        }
    }
}
