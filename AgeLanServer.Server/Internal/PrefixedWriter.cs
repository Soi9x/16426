// Port từ server/internal/writerPrefixer.go
// Writer thêm tiền tố vào đầu mỗi lần ghi.

namespace AgeLanServer.Server.Internal;

/// <summary>
/// Mutex toàn cục cho PrefixedWriter để tránh ghi chồng chéo.
/// </summary>
public static class PrefixerMutex
{
    private static readonly object _lock = new();

    public static void Lock() => Monitor.Enter(_lock);
    public static void Unlock() => Monitor.Exit(_lock);
}

/// <summary>
/// PrefixedWriter: Wrapper thêm tiền tố vào mỗi lần ghi.
/// Tương đương PrefixedWriter trong Go.
/// </summary>
public sealed class PrefixedWriter : TextWriter
{
    private readonly TextWriter _writer;
    private readonly byte[] _prefix;
    private readonly object _mutex = new();

    /// <summary>
    /// Tạo PrefixedWriter với tiền tố từ game và tên.
    /// </summary>
    /// <param name="writer">Stream ghi gốc.</param>
    /// <param name="game">Tên game.</param>
    /// <param name="name">Tên thành phần.</param>
    /// <param name="useFileLogger">Có đang dùng file logger không (để quyết định format prefix).</param>
    public PrefixedWriter(TextWriter writer, string game, string name, bool useFileLogger)
    {
        _writer = writer;
        _prefix = useFileLogger
            ? System.Text.Encoding.UTF8.GetBytes($"[{name}] ")
            : System.Text.Encoding.UTF8.GetBytes($"[{game}] [{name}] ");
    }

    /// <summary>
    /// Tạo PrefixedWriter từ stream.
    /// </summary>
    public PrefixedWriter(Stream stream, string game, string name, bool useFileLogger)
        : this(new StreamWriter(stream), game, name, useFileLogger)
    {
    }

    public override System.Text.Encoding Encoding => _writer.Encoding;

    /// <summary>
    /// Ghi dữ liệu có kèm tiền tố.
    /// </summary>
    public void WriteWithPrefix(byte[] data)
    {
        PrefixerMutex.Lock();
        try
        {
            lock (_mutex)
            {
                if (_writer is StreamWriter sw)
                {
                    sw.BaseStream.Write(_prefix);
                    sw.BaseStream.Write(data);
                }
                else
                {
                    _writer.Write(System.Text.Encoding.UTF8.GetString(_prefix));
                    _writer.Write(System.Text.Encoding.UTF8.GetString(data));
                }
            }
        }
        finally
        {
            PrefixerMutex.Unlock();
        }
    }

    public override void Write(string? value)
    {
        if (value == null) return;
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        WriteWithPrefix(bytes);
    }

    public override void WriteLine(string? value)
    {
        Write(value);
        _writer.WriteLine();
    }
}
