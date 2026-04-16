// Port từ server/internal/runtime.go
/// Các biến runtime toàn cục.

using System.Text.Json.Serialization;
using AgeLanServer.Common;

namespace AgeLanServer.Server.Internal;

/// <summary>
/// Chứa các biến runtime toàn cục của server.
/// Tương đương các biến package-level trong Go.
/// </summary>
public static class ServerRuntime
{
    /// <summary>ID duy nhất của server (UUID).</summary>
    public static Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Dữ liệu thông báo cho các phiên bản client khác nhau.
    /// Key là phiên bản, value là dữ liệu tương ứng.
    /// </summary>
    public static Dictionary<int, AnnounceMessageDataV2> AnnounceMessageData { get; set; } = new();

    /// <summary>Có sinh PlatformUserId hay không.</summary>
    public static bool GeneratePlatformUserId { get; set; }

    /// <summary>Có bật kiểm tra kết nối hay không.</summary>
    public static bool Connectivity { get; set; }

    /// <summary>Phương thức xác thực đang dùng.</summary>
    public static string Authentication { get; set; } = string.Empty;

    /// <summary>Game hiện tại mà server đang phục vụ.</summary>
    public static string CurrentGameId { get; set; } = GameIds.AgeOfEmpires4;
}
