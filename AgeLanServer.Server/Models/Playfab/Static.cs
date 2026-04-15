// Port từ server/internal/models/playfab/static.go
// Cấu hình tĩnh cho CDN PlayFab, tự động quét thư mục và sinh cấu hình JSON.

using System.Text.Json;
using System.Text.Json.Serialization;
using AgeLanServer.Common;
using AgeLanServer.Server.Models;

namespace AgeLanServer.Server.Models.Playfab;

/// <summary>
/// Target cấu hình CDN.
/// </summary>
public class CdnTarget
{
    [JsonPropertyName("Target")]
    public CdnTargetInner Target { get; set; } = new();
}

public class CdnTargetInner
{
    [JsonPropertyName("CdnBranch")]
    public string CdnBranch { get; set; } = string.Empty;
}

/// <summary>
/// Bundle CDN với thông tin phiên bản game.
/// </summary>
public class CdnBundle
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("CdnBranch")]
    public string CdnBranch { get; set; } = string.Empty;

    [JsonPropertyName("RequiresGameVersion")]
    public int RequiresGameVersion { get; set; }
}

/// <summary>
/// Cấu hình đường dẫn CDN hoàn chỉnh.
/// </summary>
public class CdnPathConfig
{
    [JsonPropertyName("Rules")]
    public List<CdnTarget> Rules { get; set; } = new();

    [JsonPropertyName("CdnBundles")]
    public List<CdnBundle> CdnBundles { get; set; } = new();
}

/// <summary>
/// Lớp tĩnh chứa cấu hình PlayFab đã được khởi tạo sẵn.
/// Tương đương các biến package-level và hàm init() trong Go.
/// </summary>
public static class PlayfabStaticConfig
{
    /// <summary>
    /// Thư mục gốc chứa dữ liệu PlayFab responses.
    /// Đường dẫn: responses/aom/playfab
    /// </summary>
    public static string BaseDir { get; } = Path.Combine(
        MainResources.ResponsesFolder,
        AppConstants.GameAoM,
        "playfab"
    );

    /// <summary>
    /// Cấu hình tĩnh đã được serialize sang JSON.
    /// Được khởi tạo một lần khi lớp được load (tương đương init() trong Go).
    /// </summary>
    public static string StaticConfig { get; }

    /// <summary>
    /// Hậu tố cho đường dẫn static.
    /// </summary>
    public const string StaticSuffix = "/static";

    /// <summary>
    /// Nhánh CDN mặc định.
    /// </summary>
    private const string Branch = "public/production";

    /// <summary>
    /// Static constructor - chạy một lần khi lớp được truy cập lần đầu.
    /// Quét thư mục BaseDir, đọc các thư mục con (tên là số nguyên = phiên bản game),
    /// sắp xếp giảm dần theo RequiresGameVersion, rồi serialize thành JSON.
    /// </summary>
    static PlayfabStaticConfig()
    {
        var config = new CdnPathConfig
        {
            Rules = new List<CdnTarget>
            {
                new() { Target = new CdnTargetInner { CdnBranch = Branch } }
            }
        };

        // Quét các thư mục con trong BaseDir, tên thư mục là phiên bản game (số nguyên)
        if (Directory.Exists(BaseDir))
        {
            foreach (var dir in Directory.EnumerateDirectories(BaseDir))
            {
                var dirName = Path.GetFileName(dir);
                if (int.TryParse(dirName, out var version))
                {
                    config.CdnBundles.Add(new CdnBundle
                    {
                        Id = dirName,
                        CdnBranch = Branch,
                        RequiresGameVersion = version
                    });
                }
            }
        }

        // Sắp xếp giảm dần theo RequiresGameVersion (tương đương slices.SortFunc với so sánh ngược)
        config.CdnBundles.Sort((a, b) => b.RequiresGameVersion.CompareTo(a.RequiresGameVersion));

        // Serialize thành JSON
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            StaticConfig = JsonSerializer.Serialize(config, options);
        }
        catch
        {
            StaticConfig = string.Empty;
        }
    }
}
