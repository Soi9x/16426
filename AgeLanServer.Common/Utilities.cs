namespace AgeLanServer.Common;

/// <summary>
/// Hằng số đường dẫn tài nguyên và cấu hình.
/// Tương đương common/paths/paths.go trong bản Go gốc.
/// </summary>
public static class PathConstants
{
    /// <summary>Thư mục tài nguyên.</summary>
    public const string ResourcesDir = "resources";

    /// <summary>Thư mục cấu hình con.</summary>
    public const string ConfigDir = "config";

    /// <summary>Đường dẫn đầy đủ tới thư mục cấu hình.</summary>
    public static readonly string ConfigsPath = Path.Combine(ResourcesDir, ConfigDir);
}

/// <summary>
/// Tiện ích User-Agent cho HTTP requests.
/// </summary>
public static class HttpUtilities
{
    /// <summary>Tạo chuỗi User-Agent cho ứng dụng.</summary>
    public static string GetUserAgent() => $"{AppConstants.Name}/1.0";
}

/// <summary>
/// Kiểm tra chế độ tương tác (terminal tương tác).
/// </summary>
public static class InteractiveUtilities
{
    /// <summary>
    /// Kiểm tra xem ứng dụng có đang chạy trong chế độ tương tác không.
    /// Trên Windows luôn trả về true nếu có console; trên Unix kiểm tra stdin/stdout.
    /// </summary>
    public static bool IsInteractive()
    {
        try
        {
            // Kiểm tra xem console có được kết nối không
            return !Console.IsInputRedirected && !Console.IsOutputRedirected;
        }
        catch
        {
            return false;
        }
    }
}
