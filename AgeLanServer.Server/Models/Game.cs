using AgeLanServer.Server.Models;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện chính đại diện cho một trò chơi (game) trong hệ thống.
/// Cung cấp quyền truy cập vào tất cả các thành phần của game như người dùng, phiên chơi,
/// máy chủ battle, tài nguyên, v.v.
/// </summary>
public interface IGame
{
    /// <summary>Tiêu đề của game (ví dụ: "aoe1", "aoe2", ...)</summary>
    string Title { get; }

    /// <summary>Tài nguyên của game</summary>
    IResources Resources { get; }

    /// <summary>Định nghĩa vật phẩm</summary>
    IItems Items { get; }

    /// <summary>Định nghĩa bảng xếp hạng</summary>
    ILeaderboardDefinitions LeaderboardDefinitions { get; }

    /// <summary>Định nghĩa trạng thái hiện diện</summary>
    IPresenceDefinitions PresenceDefinitions { get; }

    /// <summary>Các máy chủ battle</summary>
    IBattleServers BattleServers { get; }

    /// <summary>Quản lý người dùng</summary>
    IUsers Users { get; }

    /// <summary>Quản lý quảng cáo (lobby)</summary>
    IAdvertisements Advertisements { get; }

    /// <summary>Kênh chat</summary>
    IChatChannels ChatChannels { get; }

    /// <summary>Quản lý phiên đăng nhập</summary>
    ISessions Sessions { get; }
}

/// <summary>
/// Lớp triển khai chính của giao diện IGame.
/// </summary>
public class MainGame : IGame
{
    internal IBattleServers BattleServersInternal { get; set; } = null!;
    internal IResources ResourcesInternal { get; set; } = null!;
    internal IUsers UsersInternal { get; set; } = null!;
    internal IAdvertisements AdvertisementsInternal { get; set; } = null!;
    internal IChatChannels ChatChannelsInternal { get; set; } = null!;
    internal ISessions SessionsInternal { get; set; } = null!;
    internal ILeaderboardDefinitions LeaderboardDefinitionsInternal { get; set; } = null!;
    internal IItems ItemsInternal { get; set; } = null!;
    internal IPresenceDefinitions PresenceDefinitionsInternal { get; set; } = null!;
    internal string TitleInternal { get; set; } = null!;

    public string Title => TitleInternal;
    public IResources Resources => ResourcesInternal;
    public IItems Items => ItemsInternal;
    public ILeaderboardDefinitions LeaderboardDefinitions => LeaderboardDefinitionsInternal;
    public IPresenceDefinitions PresenceDefinitions => PresenceDefinitionsInternal;
    public IBattleServers BattleServers => BattleServersInternal;
    public IUsers Users => UsersInternal;
    public IAdvertisements Advertisements => AdvertisementsInternal;
    public IChatChannels ChatChannels => ChatChannelsInternal;
    public ISessions Sessions => SessionsInternal;
}

/// <summary>
/// Tùy chọn khi tạo một phiên bản MainGame.
/// </summary>
public class CreateMainGameOpts
{
    public ResourcesOpts? Resources { get; set; }
    public BattleServerOpts? BattleServer { get; set; }
    public InstanceOpts? Instances { get; set; }
}

/// <summary>
/// Tùy chọn cho từng phiên bản cụ thể của các thành phần game.
/// </summary>
public class InstanceOpts
{
    public IResources? Resources { get; set; }
    public IBattleServers? BattleServers { get; set; }
    public IUsers? Users { get; set; }
    public IAdvertisements? Advertisements { get; set; }
    public IChatChannels? ChatChannels { get; set; }
    public ISessions? Sessions { get; set; }
    public ILeaderboardDefinitions? LeaderboardDefinitions { get; set; }
    public IPresenceDefinitions? PresenceDefinitions { get; set; }
    public IItems? Items { get; set; }
}
