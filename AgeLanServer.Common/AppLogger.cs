namespace AgeLanServer.Common;

/// <summary>
/// Hệ thống ghi log cho toàn bộ ứng dụng.
/// Tương đương common/logger/logger.go trong bản Go gốc.
/// </summary>
public static class AppLogger
{
    private static readonly object LockObj = new();
    private static string? _currentPrefix;
    private static readonly List<string> Buffer = new();
    private static TextWriter? _fileWriter;
    private static string? _logFolder;

    /// <summary>
    /// Khởi tạo logger với TextWriter tùy chọn. Nếu null sẽ dùng Console.Out.
    /// </summary>
    public static void Initialize(TextWriter? writer = null)
    {
        // Logger initialized
    }

    /// <summary>
    /// Thiết lập tiền tố cho các thông báo log (ví dụ: "|SERVER|").
    /// </summary>
    public static void SetPrefix(string name)
    {
        lock (LockObj)
        {
            _currentPrefix = $"|{name.ToUpperInvariant()}| ";
        }
    }

    /// <summary>
    /// Ghi thông báo log ra console và file (nếu có).
    /// </summary>
    public static void Info(string message)
    {
        WriteLog("INFO", message);
    }

    /// <summary>
    /// Ghi cảnh báo.
    /// </summary>
    public static void Warn(string message)
    {
        WriteLog("WARN", message);
    }

    /// <summary>
    /// Ghi lỗi.
    /// </summary>
    public static void Error(string message)
    {
        WriteLog("ERROR", message);
    }

    /// <summary>
    /// Ghi thông báo với tiền tố tùy chỉnh.
    /// </summary>
    public static void WithPrefix(string prefix, string message)
    {
        WriteLog(prefix.ToUpperInvariant(), message);
    }

    private static void WriteLog(string level, string message)
    {
        lock (LockObj)
        {
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            var prefix = _currentPrefix ?? "";
            var logLine = $"{timestamp} {prefix}[{level}] {message}";

            // Buffer cho đến khi có file writer
            Buffer.Add(logLine);

            // Ghi ra console nếu chưa có file
            if (_fileWriter == null)
            {
                Console.WriteLine(logLine);
            }
            else
            {
                // Ghi toàn bộ buffer + dòng mới
                foreach (var line in Buffer)
                    _fileWriter.WriteLine(line);
                _fileWriter.Flush();
                Buffer.Clear();
            }
        }
    }

    /// <summary>
    /// Tạo file logger mới và chuyển toàn bộ buffer sang file.
    /// </summary>
    public static void SetupFileLogger(string name, string root, string gameId, bool finalRoot = false)
    {
        lock (LockObj)
        {
            var folder = finalRoot
                ? Path.Combine(root, "logs")
                : Path.Combine(root, "logs", gameId, GetUtcTimestampString());

            folder = Path.GetFullPath(folder);
            Directory.CreateDirectory(folder);
            _logFolder = folder;

            var filePath = Path.Combine(folder, $"{name}.txt");
            _fileWriter = new StreamWriter(filePath, append: true) { AutoFlush = true };

            // Ghi toàn bộ buffer sang file
            foreach (var line in Buffer)
                _fileWriter.WriteLine(line);
            _fileWriter.Flush();
            Buffer.Clear();
        }
    }

    /// <summary>
    /// Đóng file logger và đồng bộ buffer còn lại.
    /// </summary>
    public static void CloseFileLogger()
    {
        lock (LockObj)
        {
            if (_fileWriter != null)
            {
                foreach (var line in Buffer)
                    _fileWriter.WriteLine(line);
                _fileWriter.Flush();
                _fileWriter.Close();
                _fileWriter = null;
                Buffer.Clear();
            }
        }
    }

    /// <summary>
    /// Lấy thư mục log hiện tại.
    /// </summary>
    public static string? GetLogFolder() => _logFolder;

    /// <summary>
    /// Bí danh cho GetLogFolder().
    /// </summary>
    public static string? LogFolder() => GetLogFolder();

    /// <summary>
    /// Bí danh cho CloseFileLogger().
    /// </summary>
    public static void CloseFileLog() => CloseFileLogger();

    private static string GetUtcTimestampString()
    {
        var now = DateTime.UtcNow;
        return $"{now:yyyy-MM-dd}T{now:HH-mm-ss}";
    }
}
