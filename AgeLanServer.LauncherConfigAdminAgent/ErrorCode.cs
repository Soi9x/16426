namespace AgeLanServer.LauncherConfigAdminAgent;

/// <summary>
/// Chứa tất cả các mã lỗi được sử dụng trong agent cấu hình launcher.
/// Các giá trị được kế thừa từ Go code (common/errors.go, launcher-common/errors.go, internal/errors.go).
/// </summary>
public static class ErrorCode
{
    // =============================================
    // Lỗi từ common/errors.go
    // =============================================

    /// <summary>Thành công, không có lỗi.</summary>
    public const int ErrSuccess = 0;

    /// <summary>Lỗi chung không xác định.</summary>
    public const int ErrGeneral = 1;

    /// <summary>Ứng dụng bị ngắt bởi tín hiệu (SIGINT/SIGTERM).</summary>
    public const int ErrSignal = 2;

    /// <summary>Không thể khóa file PID - tiến trình khác đang chạy.</summary>
    public const int ErrPidLock = 3;

    /// <summary>Lỗi ghi log vào file.</summary>
    public const int ErrFileLog = 4;

    /// <summary>Không thể phân tích cú pháp file cấu hình.</summary>
    public const int ErrConfigParse = 5;

    /// <summary>Đánh dấu: mã lỗi cuối cùng trong common (không phải lỗi thực tế).</summary>
    public const int ErrLast = 6;

    // =============================================
    // Lỗi từ launcher-common/errors.go
    // =============================================

    /// <summary>Chương trình không được chạy với quyền admin.</summary>
    public const int ErrNotAdmin = ErrLast + 1; // 7

    /// <summary>Game ID không hợp lệ.</summary>
    public const int ErrInvalidGame = ErrLast + 2; // 8

    /// <summary>Đánh dấu: mã lỗi cuối cùng trong launcher-common (không phải lỗi thực tế).</summary>
    public const int ErrLastLauncher = ErrInvalidGame; // 8

    // =============================================
    // Lỗi từ launcher-config-admin-agent/internal/errors.go
    // =============================================

    /// <summary>Không thể lắng nghe kết nối IPC (named pipe / unix socket).</summary>
    public const int ErrListen = ErrLastLauncher + 1; // 9

    /// <summary>Không thể giải mã dữ liệu nhận được từ client.</summary>
    public const int ErrDecode = ErrLastLauncher + 2; // 10

    /// <summary>Hành động nhận được không tồn tại.</summary>
    public const int ErrNonExistingAction = ErrLastLauncher + 3; // 11

    /// <summary>Lỗi khi đóng kết nối IPC.</summary>
    public const int ErrConnectionClosing = ErrLastLauncher + 4; // 12

    /// <summary>Chứng chỉ SSL đã được thêm trước đó.</summary>
    public const int ErrCertAlreadyAdded = ErrLastLauncher + 5; // 13

    /// <summary>Các địa chỉ IP đã được ánh xạ trước đó.</summary>
    public const int ErrIpsAlreadyMapped = ErrLastLauncher + 6; // 14

    /// <summary>Chứng chỉ SSL không hợp lệ (không thể phân tích hoặc đã hết hạn).</summary>
    public const int ErrCertInvalid = ErrLastLauncher + 7; // 15
}
