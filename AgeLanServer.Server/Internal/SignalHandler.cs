// Port từ server/internal/rootSignal.go
// Xử lý tín hiệu dừng (SIGINT, SIGTERM, SIGHUP).

namespace AgeLanServer.Server.Internal;

/// <summary>
/// Quản lý tín hiệu dừng server.
/// Trong .NET, sử dụng CancellationTokenSource để mô phỏng channel signal từ Go.
/// </summary>
public static class SignalHandler
{
    private static CancellationTokenSource? _cts;

    /// <summary>
    /// Token được kích hoạt khi nhận tín hiệu dừng.
    /// </summary>
    public static CancellationToken StopToken => _cts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Khởi tạo bộ xử lý tín hiệu.
    /// Lắng nghe Ctrl+C (SIGINT) và các tín hiệu kết thúc trên Unix.
    /// </summary>
    public static void Initialize()
    {
        _cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // Ngăn thoát ngay lập tức
            _cts?.Cancel();
        };

        // Trên Unix, có thể dùng PosixSignalRegistration (.NET 6+)
        // để lắng nghe SIGTERM và SIGHUP
#if NET6_0_OR_GREATER
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            System.Runtime.InteropServices.PosixSignalRegistration.Create(
                System.Runtime.InteropServices.PosixSignal.SIGTERM,
                _ => _cts?.Cancel());

            System.Runtime.InteropServices.PosixSignalRegistration.Create(
                System.Runtime.InteropServices.PosixSignal.SIGHUP,
                _ => _cts?.Cancel());
        }
#endif
    }

    /// <summary>
    /// Gửi tín hiệu dừng thủ công.
    /// </summary>
    public static void RequestStop()
    {
        _cts?.Cancel();
    }
}
