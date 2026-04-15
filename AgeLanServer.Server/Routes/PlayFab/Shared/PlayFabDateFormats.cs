// Port từ server/internal/routes/playfab/Client/shared/common.go
// Các hằng số và hàm trợ dùng chung cho PlayFab Client endpoints.

namespace AgeLanServer.Server.Routes.PlayFab.Shared;

/// <summary>
/// Định dạng ngày giờ theo chuẩn PlayFab API.
/// Tương đương dateFormat = "2006-01-02T15:04:05.000Z" trong Go.
/// </summary>
public static class PlayFabDateFormats
{
    /// <summary>
    /// Định dạng ngày giờ ISO 8601 với milliseconds và hậu tố Z.
    /// Ví dụ: 2025-11-12T03:34:00.000Z
    /// </summary>
    public const string Iso8601 = "yyyy-MM-ddTHH:mm:ss.000Z";

    /// <summary>
    /// Định dạng ngày giờ thành chuỗi theo chuẩn PlayFab.
    /// </summary>
    public static string FormatDate(DateTime dateTime)
    {
        return dateTime.ToUniversalTime().ToString(Iso8601);
    }
}
