namespace AgeLanServer.ServerGenCert;

/// <summary>
/// Chứa các mã lỗi liên quan đến việc tạo chứng chỉ.
/// Chuyển thể từ errors.go trong server-genCert/internal.
/// </summary>
public static class Errors
{
    /// <summary>
    /// Lỗi: Không thể xác định hoặc tạo thư mục chứa chứng chỉ.
    /// Tương đương ErrCertDirectory trong Go.
    /// </summary>
    public const int CertDirectory = 100;

    /// <summary>
    /// Lỗi: Không thể tạo chứng chỉ (lỗi trong quá trình sinh chứng chỉ).
    /// Tương đương ErrCertCreate trong Go.
    /// </summary>
    public const int CertCreate = 101;

    /// <summary>
    /// Lỗi: Chứng chỉ đã tồn tại và không được phép ghi đè.
    /// Tương đương ErrCertCreateExisting trong Go.
    /// </summary>
    public const int CertCreateExisting = 102;
}
