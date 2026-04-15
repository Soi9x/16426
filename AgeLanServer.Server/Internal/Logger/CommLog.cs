// Port từ server/internal/logger/commLog.go
/// Buffer log giao tiếp (communication log buffer).

using System.Text.Json;

namespace AgeLanServer.Server.Internal.Logger;

/// <summary>
/// Buffer: Bộ đệm log dạng JSON ra file.
/// Tương đương Buffer trong Go - dùng buffered writer và JSON encoder.
/// </summary>
public sealed class CommLogBuffer : IDisposable
{
    private readonly FileStream _fileStream;
    private readonly StreamWriter _streamWriter;
    private readonly Utf8JsonWriter _jsonWriter;
    private readonly object _lock = new();

    public CommLogBuffer(FileStream fileStream)
    {
        _fileStream = fileStream;
        _streamWriter = new StreamWriter(fileStream) { AutoFlush = false };
        _jsonWriter = new Utf8JsonWriter(_streamWriter.BaseStream, new JsonWriterOptions
        {
            Indented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    /// <summary>
    /// Instance toàn cục (tương đương CommBuffer trong Go).
    /// </summary>
    public static CommLogBuffer? Instance { get; private set; }

    /// <summary>
    /// Ghi đối tượng dưới dạng JSON vào buffer.
    /// </summary>
    public void Log(object value)
    {
        lock (_lock)
        {
            if (_jsonWriter == null) return;
            JsonSerializer.Serialize(_jsonWriter, value);
            _jsonWriter.Flush();
        }
    }

    /// <summary>
    /// Tạo buffer mới và gán vào Instance toàn cục.
    /// </summary>
    public static CommLogBuffer Create(FileStream fileStream)
    {
        Instance = new CommLogBuffer(fileStream);
        return Instance;
    }

    /// <summary>
    /// Đóng và flush buffer.
    /// </summary>
    public void Close()
    {
        lock (_lock)
        {
            _jsonWriter?.Flush();
            _streamWriter.Flush();
            _streamWriter.Close();
            _fileStream.Close();
        }
    }

    public void Dispose()
    {
        Close();
    }
}

/// <summary>
/// Tiện ích tính uptime.
/// </summary>
public static class UptimeHelper
{
    /// <summary>
    /// Tính thời gian hoạt động từ thời điểm bắt đầu.
    /// </summary>
    /// <param name="startTime">Thời điểm bắt đầu (mặc định dùng Logger.StartTime).</param>
    /// <returns>Thời gian đã hoạt động.</returns>
    public static TimeSpan GetUptime(DateTimeOffset? startTime = null)
    {
        var start = startTime ?? ServerLogger.StartTime;
        return DateTimeOffset.UtcNow - start;
    }
}
