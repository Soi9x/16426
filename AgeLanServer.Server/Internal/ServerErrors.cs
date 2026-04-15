// Port từ server/internal/errors.go
// Định nghĩa mã lỗi riêng của module server.

using AgeLanServer.Common;

namespace AgeLanServer.Server.Internal;

/// <summary>
/// Mã lỗi riêng của module server.
/// Bắt đầu tiếp sau ErrLast của common.
/// </summary>
public static class ServerErrorCodes
{
    /// <summary>Lỗi thư mục chứng chỉ.</summary>
    public const int CertDirectory = ErrorCodes.Last + 1;

    /// <summary>Lỗi phân giải host.</summary>
    public const int ResolveHost = ErrorCodes.Last + 2;

    /// <summary>Lỗi tạo file log.</summary>
    public const int CreateLogFile = ErrorCodes.Last + 3;

    /// <summary>Lỗi khởi động server.</summary>
    public const int StartServer = ErrorCodes.Last + 4;

    /// <summary>Lỗi multicast group.</summary>
    public const int MulticastGroup = ErrorCodes.Last + 5;

    /// <summary>Lỗi games.</summary>
    public const int Games = ErrorCodes.Last + 6;

    /// <summary>Lỗi game.</summary>
    public const int Game = ErrorCodes.Last + 7;

    /// <summary>Lỗi thông báo (announce).</summary>
    public const int Announce = ErrorCodes.Last + 8;

    /// <summary>ID không hợp lệ.</summary>
    public const int InvalidId = ErrorCodes.Last + 9;

    /// <summary>Phương thức xác thực không hợp lệ.</summary>
    public const int InvalidAuthentication = ErrorCodes.Last + 10;
}
