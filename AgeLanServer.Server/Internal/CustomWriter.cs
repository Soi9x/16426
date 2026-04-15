// Port từ server/internal/errorLogger.go
// CustomWriter lọc bỏ log TLS handshake error.

namespace AgeLanServer.Server.Internal;

/// <summary>
/// CustomWriter: Wrapper lọc bỏ các log chứa "TLS handshake error".
/// Tương đương CustomWriter trong Go.
/// </summary>
public sealed class CustomWriter : TextWriter
{
    private readonly TextWriter _originalWriter;

    public CustomWriter(TextWriter originalWriter)
    {
        _originalWriter = originalWriter;
    }

    public override System.Text.Encoding Encoding => _originalWriter.Encoding;

    public override void Write(string? value)
    {
        if (value != null && value.Contains("TLS handshake error", StringComparison.OrdinalIgnoreCase))
        {
            // Bỏ qua log TLS handshake error
            return;
        }
        _originalWriter.Write(value);
    }

    public override void WriteLine(string? value)
    {
        if (value != null && value.Contains("TLS handshake error", StringComparison.OrdinalIgnoreCase))
        {
            // Bỏ qua log TLS handshake error
            return;
        }
        _originalWriter.WriteLine(value);
    }
}
