using AgeLanServer.Server.Internal;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện đại diện cho một người dùng trong hệ thống.
/// Chứa thông tin định danh, thống kê, vật phẩm, và trạng thái hiện diện.
/// </summary>
public interface IUser
{
    int Id { get; }
    bool Xbox { get; }
    bool IsXbox { get; }
    int StatId { get; }
    int ProfileId { get; }
    int Reliclink { get; }
    string Alias { get; }
    string PlatformPath { get; }
    int PlatformId { get; }
    ulong PlatformUserId { get; }

    /// <summary>Mã hóa thông tin hồ sơ bổ sung</summary>
    object[] EncodeExtraProfileInfo(ushort clientLibVersion);

    /// <summary>Mã hóa thông tin hồ sơ</summary>
    object[] EncodeProfileInfo(ushort clientLibVersion);

    /// <summary>Trạng thái hiện diện</summary>
    int Presence { get; }
    void SetPresence(int presence);
    void SetPresenceProperty(int id, string value);

    /// <summary>Siêu dữ liệu avatar</summary>
    IPersistentJsonData<string?> AvatarMetadata { get; }

    /// <summary>Thuộc tính hồ sơ</summary>
    IPersistentJsonData<Dictionary<string, string>?> ProfileProperties { get; }

    /// <summary>Thuộc tính hiện diện</summary>
    SafeMap<int, string>? PresenceProperties { get; }

    /// <summary>Kinh nghiệm hồ sơ</summary>
    uint ProfileExperience { get; }

    /// <summary>Cấp độ hồ sơ</summary>
    ushort ProfileLevel { get; }

    byte PlatformRelated { get; }

    /// <summary>Thống kê avatar</summary>
    IPersistentJsonData<AvatarStats?> AvatarStats { get; }

    /// <summary>Dữ liệu liên tục</summary>
    PersistentStringJsonMap PersistentData { get; }

    /// <summary>Vật phẩm</summary>
    IPersistentJsonData<Dictionary<int, MainItem>?> Items { get; }

    /// <summary>Bộ vật phẩm (loadout)</summary>
    IPersistentJsonData<MainItemLoadouts?> ItemLoadouts { get; }

    /// <summary>Thông tin xác thực</summary>
    IPersistentJsonData<DateTime?> Auth { get; }

    /// <summary>Mã hóa thống kê avatar</summary>
    object[] EncodeAvatarStats();

    /// <summary>Mã hóa trạng thái hiện diện</summary>
    object[] EncodePresence(IPresenceDefinitions definitions);
}
