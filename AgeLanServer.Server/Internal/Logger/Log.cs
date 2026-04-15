// Port từ server/internal/logger/log.go
// Tiện ích log chính của server.

using AgeLanServer.Common;

namespace AgeLanServer.Server.Internal.Logger;

/// <summary>
/// ServerLogger: Tiện ích log cho server module.
/// </summary>
public static class ServerLogger
{
    /// <summary>Thời điểm server khởi động (UTC).</summary>
    public static DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Mở file log chính nếu được bật.
    /// </summary>
    public static int OpenMainFileLog(string root, bool logEnabled)
    {
        if (!logEnabled)
            return ErrorCodes.Success;

        try
        {
            AppLogger.SetupFileLogger("server", root, string.Empty, true);
            return ErrorCodes.Success;
        }
        catch
        {
            return ErrorCodes.FileLog;
        }
    }

    /// <summary>
    /// In nội dung file log (nếu có).
    /// </summary>
    public static void PrintFile(string name, string path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            var data = File.ReadAllText(path);
            AppLogger.WithPrefix(name, data);
        }
    }

    /// <summary>
    /// In log có tiền tố "main" ra file logger và console.
    /// </summary>
    public static void Printf(string format, params object[] args)
    {
        var message = string.Format(format, args);
        AppLogger.WithPrefix("main", message);
        Console.Write(format, args);
    }

    /// <summary>
    /// In dòng log có tiền tố "main" ra file logger và console.
    /// </summary>
    public static void Println(params object[] args)
    {
        var message = string.Join(" ", args);
        AppLogger.WithPrefix("main", message);
        Console.WriteLine(message);
    }
}
