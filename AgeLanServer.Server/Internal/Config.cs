// Port từ server/internal/config.go
// Cấu hình chính của server.

using AgeLanServer.Common;

namespace AgeLanServer.Server.Internal;

/// <summary>
/// Cấu hình thông báo (announcement).
/// </summary>
public record Announcement
{
    /// <summary>Có bật thông báo hay không.</summary>
    public bool Enabled { get; init; }

    /// <summary>Có sử dụng multicast hay không.</summary>
    public bool Multicast { get; init; }

    /// <summary>Địa chỉ multicast group.</summary>
    public string MulticastGroup { get; init; } = string.Empty;

    /// <summary>Cổng thông báo.</summary>
    public int Port { get; init; }
}

/// <summary>
/// Cấu hình Battle Server (mở rộng từ BaseConfig chung).
/// </summary>
public record BattleServerConfig : BattleServerBaseConfig;

/// <summary>
/// Cấu hình cho một game cụ thể.
/// </summary>
public record GameConfig
{
    /// <summary>Danh sách host của game.</summary>
    public List<string> Hosts { get; init; } = new();

    /// <summary>Danh sách Battle Server của game.</summary>
    public List<BattleServerConfig> BattleServers { get; init; } = new();
}

/// <summary>
/// Nhóm cấu hình cho tất cả game.
/// </summary>
public record GamesConfig
{
    /// <summary>Danh sách game được bật.</summary>
    public List<string> Enabled { get; init; } = new();

    /// <summary>Cấu hình Age of Empires 1.</summary>
    public GameConfig Age1 { get; init; } = new();

    /// <summary>Cấu hình Age of Empires 2.</summary>
    public GameConfig Age2 { get; init; } = new();

    /// <summary>Cấu hình Age of Empires 3.</summary>
    public GameConfig Age3 { get; init; } = new();

    /// <summary>Cấu hình Age of Empires 4.</summary>
    public GameConfig Age4 { get; init; } = new();

    /// <summary>Cấu hình Athens (Age of Empires Online).</summary>
    public GameConfig Athens { get; init; } = new();
}

/// <summary>
/// Cấu hình chính của server.
/// </summary>
public record ServerConfiguration
{
    /// <summary>Có bật log hay không.</summary>
    public bool Log { get; init; }

    /// <summary>Có sinh PlatformUserId hay không.</summary>
    public bool GeneratePlatformUserId { get; init; }

    /// <summary>Phương thức xác thực.</summary>
    public string Authentication { get; init; } = string.Empty;

    /// <summary>Cấu hình thông báo.</summary>
    public Announcement Announcement { get; init; } = new();

    /// <summary>Cấu hình game.</summary>
    public GamesConfig Games { get; init; } = new();

    /// <summary>
    /// Lấy danh sách host theo gameId.
    /// </summary>
    public IReadOnlyList<string> GetGameHosts(string gameId) => gameId switch
    {
        "age1" => Games.Age1.Hosts,
        "age2" => Games.Age2.Hosts,
        "age3" => Games.Age3.Hosts,
        "age4" => Games.Age4.Hosts,
        "athens" => Games.Athens.Hosts,
        _ => Array.Empty<string>()
    };

    /// <summary>
    /// Lấy danh sách Battle Server theo gameId.
    /// </summary>
    public IReadOnlyList<BattleServerConfig> GetGameBattleServers(string gameId) => gameId switch
    {
        "age1" => Games.Age1.BattleServers,
        "age2" => Games.Age2.BattleServers,
        "age3" => Games.Age3.BattleServers,
        "age4" => Games.Age4.BattleServers,
        "athens" => Games.Athens.BattleServers,
        _ => Array.Empty<BattleServerConfig>()
    };
}
