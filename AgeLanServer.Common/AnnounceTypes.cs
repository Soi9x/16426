using System.Text.Json.Serialization;

namespace AgeLanServer.Common;

/// <summary>
/// Các phiên bản giao thức thông báo UDP.
/// Phiên bản 0: 1.1.X - 1.4.X
/// Phiên bản 1: 1.5.X - 1.10.X
/// Phiên bản 2: 1.11.X+ (dữ liệu lấy từ /test thay vì trong announce)
/// </summary>
public static class AnnounceVersions
{
    public const int Version0 = 0;
    public const int Version1 = 1;
    public const int Version2 = 2;
    public const int Latest = Version2;
}

/// <summary>
/// Dữ liệu thông báo UDP phiên bản 0 (1.1.X - 1.4.X) - trống.
/// </summary>
public record AnnounceMessageDataV0;

/// <summary>
/// Dữ liệu thông báo UDP phiên bản 1 (1.5.X - 1.10.X) - chứa danh sách game.
/// </summary>
public record AnnounceMessageDataV1
{
    [JsonPropertyName("game_titles")]
    public string[] GameTitles { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Dữ liệu thông báo UDP phiên bản 2 (1.11.X+) - game đơn và phiên bản.
/// </summary>
public record AnnounceMessageDataV2
{
    [JsonPropertyName("game_title")]
    public string GameTitle { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
}
