namespace AgeLanServer.Common;

/// <summary>
/// Mã lỗi chung cho toàn bộ ứng dụng.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Thành công.</summary>
    public const int Success = 0;

    /// <summary>Lỗi chung.</summary>
    public const int General = 1;

    /// <summary>Do tín hiệu ngắt (SIGINT/SIGTERM).</summary>
    public const int Signal = 2;

    /// <summary>Lỗi khóa file PID (đã có instance khác đang chạy).</summary>
    public const int PidLock = 3;

    /// <summary>Lỗi ghi file log.</summary>
    public const int FileLog = 4;

    /// <summary>Lỗi phân tích cú pháp file cấu hình.</summary>
    public const int ConfigParse = 5;

    /// <summary>
    /// Mã lỗi cuối cùng trong nhóm chung - dùng làm điểm bắt đầu cho mã lỗi riêng của module.
    /// </summary>
    public const int Last = 6;
}
