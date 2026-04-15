using AgeLanServer.Common;
using AgeLanServer.Server.Models;

namespace AgeLanServer.Server.Models.Athens.User;

/// <summary>
/// Người dùng Athens (AoM) mở rộng từ MainUser.
/// Thêm dữ liệu PlayFab đặc thù cho Age of Mythology: Retold.
/// </summary>
public class AthensUser : MainUser
{
    /// <summary>Dữ liệu PlayFab đặc thù cho Athens</summary>
    public PersistentJsonData<Data?> PlayfabData { get; set; } = null!;
}

/// <summary>
/// Quản lý người dùng Athens.
/// Ghi phương thức Generate để thêm dữ liệu PlayFab.
/// </summary>
public class Users : MainUsers
{
    public Users()
    {
        GenerateFn = Generate;
    }

    public new void Initialize()
    {
        base.Initialize();
    }

    /// <summary>
    /// Tạo người dùng Athens với dữ liệu PlayFab bổ sung.
    /// </summary>
    public new IUser Generate(string _gameId, PersistentStringJsonMap persistentData, IItems? itemDefinitions,
        IAvatarStatDefinitions? avatarStatsDefinitions, string identifier, bool isXbox, ulong platformUserId, string alias)
    {
        var d = PersistentJsonData<Data?>.Create(
            persistentData,
            "playfab",
            new PlayfabUpgradableDefaultData());

        var mainUser = base.Generate(AppConstants.GameAoM, persistentData, itemDefinitions,
            avatarStatsDefinitions, identifier, isXbox, platformUserId, alias);

        return new AthensUser
        {
            Id = mainUser.Id,
            StatId = mainUser.StatId,
            Alias = mainUser.Alias,
            PlatformUserId = mainUser.PlatformUserId,
            ProfileId = mainUser.ProfileId,
            Reliclink = mainUser.Reliclink,
            IsXbox = mainUser.IsXbox,
            AvatarMetadata = mainUser.AvatarMetadata,
            Items = mainUser.Items,
            ItemLoadouts = mainUser.ItemLoadouts,
            ProfileProperties = mainUser.ProfileProperties,
            AvatarStats = mainUser.AvatarStats,
            PersistentData = mainUser.PersistentData,
            PresenceProperties = mainUser.PresenceProperties,
            Auth = mainUser.Auth,
            PlayfabData = d
        };
    }
}
