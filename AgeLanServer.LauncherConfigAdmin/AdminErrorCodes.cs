namespace AgeLanServer.LauncherConfigAdmin;

/// <summary>
/// Mã lỗi riêng của module Launcher-Config-Admin.
/// Chuyển thể từ launcher-config-admin/internal/errors.go.
/// Các giá trị bắt đầu từ một offset đủ lớn để không trùng với mã lỗi của các module khác.
/// Trong Go: ErrLocalCertRemove = iota + launcherCommon.ErrLast
/// </summary>
public static class AdminErrorCodes
{
    /// <summary>
    /// Mã lỗi cơ sở. Trong Go dùng launcherCommon.ErrLast, ở đây chọn giá trị 100
    /// để tránh xung đột với các mã lỗi đã định nghĩa trong Common (0-6) và LauncherCommon.
    /// </summary>
    private const int Base = 100;

    /// <summary>Lỗi khi gỡ chứng chỉ cục bộ (tương đương ErrLocalCertRemove).</summary>
    public const int ErrLocalCertRemove = Base + 0;

    /// <summary>Lỗi khi xóa ánh xạ IP trong chế độ revert (tương đương ErrIpMapRemove).</summary>
    public const int ErrIpMapRemove = Base + 1;

    /// <summary>
    /// Lỗi khi xóa ánh xạ IP trong chế độ revert và không thể khôi phục chứng chỉ
    /// (tương đương ErrIpMapRemoveRevert).
    /// </summary>
    public const int ErrIpMapRemoveRevert = Base + 2;

    /// <summary>Lỗi khi thêm chứng chỉ cục bộ (tương đương ErrLocalCertAdd).</summary>
    public const int ErrLocalCertAdd = Base + 3;

    /// <summary>Lỗi khi phân tích cú pháp chứng chỉ cục bộ (tương đương ErrLocalCertAddParse).</summary>
    public const int ErrLocalCertAddParse = Base + 4;

    /// <summary>Lỗi khi thêm ánh xạ IP (tương đương ErrIpMapAdd).</summary>
    public const int ErrIpMapAdd = Base + 5;

    /// <summary>
    /// Lỗi khi thêm ánh xạ IP và không thể gỡ chứng chỉ đã thêm trước đó
    /// (tương đương ErrIpMapAddRevert).
    /// </summary>
    public const int ErrIpMapAddRevert = Base + 6;
}
