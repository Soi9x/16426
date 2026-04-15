namespace AgeLanServer.LauncherConfigAdmin;

/// <summary>
/// Trạng thái toàn cục theo dõi xem chương trình đang ở chế độ setup hay revert.
/// Chuyển thể từ launcher-config-admin/internal/state.go (biến global SetUp bool).
/// Giá trị này ảnh hưởng đến hậu tố log khi thực hiện FlushDns.
/// </summary>
public static class AdminState
{
    /// <summary>
    /// Cờ trạng thái: true khi đang chạy lệnh setup, false khi đang chạy lệnh revert.
    /// Mặc định là false (chế độ revert).
    /// </summary>
    public static bool SetUp { get; set; } = false;
}
