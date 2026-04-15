// Port từ server/internal/models/athens/routes/playfab/blessings.go
// Tiện ích đọc dữ liệu blessings từ file JSON cho Age of Mythology.
// Lưu ý: Các lớp Blessing và BlessingsJson đã được định nghĩa trong Gauntlet.cs.

using System.Text.Json;
using AgeLanServer.Server.Models.Playfab;

namespace AgeLanServer.Server.Models.Athens.Routes.Playfab;

/// <summary>
/// Tiện ích đọc dữ liệu blessings từ file known_blessings.json.
/// Sử dụng các lớp Blessing và BlessingsJson đã có trong Gauntlet.cs.
/// </summary>
public static class BlessingsReader
{
    /// <summary>
    /// Đọc danh sách blessings từ file known_blessings.json.
    /// Trả về danh sách rỗng nếu file không tồn tại hoặc có lỗi.
    /// </summary>
    public static List<Blessing> ReadBlessings()
    {
        var blessingsPath = Path.Combine(
            PlayfabStaticConfig.BaseDir,
            "public-production",
            "2",
            "known_blessings.json"
        );

        if (!File.Exists(blessingsPath))
            return new List<Blessing>();

        try
        {
            var json = File.ReadAllText(blessingsPath);
            var blessingsJson = JsonSerializer.Deserialize<BlessingsJson>(json);
            return blessingsJson?.KnownBlessings ?? new List<Blessing>();
        }
        catch
        {
            return new List<Blessing>();
        }
    }
}
