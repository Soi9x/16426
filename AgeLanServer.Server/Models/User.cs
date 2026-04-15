using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện quản lý tập hợp người dùng.
/// </summary>
public interface IUsers
{
    void Initialize();
    IUser GetOrCreateUser(string gameId, IItems itemDefinitions, IAvatarStatDefinitions avatarStatsDefinitions,
        string remoteAddr, string remoteMacAddress, bool isXbox, ulong platformUserId, string alias);
    IUser? GetUserByStatId(int id);
    IUser? GetUserById(int id);
    IEnumerable<int> GetUserIds();
    object[][] EncodeProfileInfo(IPresenceDefinitions definitions, Func<IUser, bool> matches, ushort clientLibVersion);
    IUser? GetUserByPlatformUserId(bool xbox, ulong id);
}

/// <summary>
/// Lớp triển khai chính quản lý tập hợp người dùng.
/// Sử dụng SafeMap để lưu trữ an toàn luồng.
/// </summary>
public class MainUsers : IUsers
{
    private SafeMap<string, IUser> _store = null!;

    public Func<string, PersistentStringJsonMap, IItems?, IAvatarStatDefinitions?, string, bool, ulong, string, IUser>? GenerateFn { get; set; }

    public void Initialize()
    {
        _store = new SafeMap<string, IUser>();
        GenerateFn ??= Generate;
    }

    /// <summary>
    /// Tạo người dùng mới với các tham số đã cho.
    /// Sinh ID ngẫu nhiên và khởi tạo các thành phần dữ liệu liên tục.
    /// </summary>
    public virtual IUser Generate(string gameId, PersistentStringJsonMap persistentData, IItems? itemDefinitions,
        IAvatarStatDefinitions? avatarStatsDefinitions, string identifier, bool isXbox, ulong platformUserId, string alias)
    {
        using var hasher = SHA256.Create();
        var hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(identifier));
        var seed = BitConverter.ToUInt64(hash, 0);
        var rng = new Random((int)(seed ^ (seed >> 32)));

        IPersistentJsonData<AvatarStats?>? avatarStats = null;
        if (avatarStatsDefinitions != null)
        {
            avatarStats = PersistentJsonData<AvatarStats?>.Create(
                persistentData,
                "avatarStats",
                new AvatarStatsUpgradableDefaultData(gameId, avatarStatsDefinitions));
        }

        IPersistentJsonData<Dictionary<string, string>?>? profileProperties = null;
        if (gameId == AppConstants.GameAoE3 || gameId == AppConstants.GameAoE4 || gameId == AppConstants.GameAoM)
        {
            profileProperties = PersistentJsonData<Dictionary<string, string>?>.Create(
                persistentData,
                "profileProperties",
                new ProfilePropertiesUpgradableDefaultData());
        }

        IPersistentJsonData<Dictionary<int, MainItem>?>? items = null;
        if (itemDefinitions != null)
        {
            items = PersistentJsonData<Dictionary<int, MainItem>?>.Create(
                persistentData,
                "items",
                new ItemsUpgradableDefaultData(gameId, itemDefinitions));
        }

        IPersistentJsonData<MainItemLoadouts?>? itemLoadouts = null;
        if (gameId != AppConstants.GameAoE1)
        {
            itemLoadouts = PersistentJsonData<MainItemLoadouts?>.Create(
                persistentData,
                "itemLoadouts",
                new ItemLoadoutsUpgradableDefaultData());
        }

        var avatarMetadata = PersistentJsonData<string?>.Create(
            persistentData,
            "avatarMetadata",
            new AvatarMetadataUpgradableDefaultData(gameId));

        var auth = PersistentJsonData<DateTime?>.Create(
            persistentData,
            "auth",
            new AuthUpgradableDefaultData());

        SafeMap<int, string>? presenceProperties = null;
        if (gameId != AppConstants.GameAoE1)
        {
            presenceProperties = new SafeMap<int, string>();
        }

        return new MainUser
        {
            Id = rng.Next(int.MinValue, int.MaxValue),
            StatId = rng.Next(int.MinValue, int.MaxValue),
            ProfileId = rng.Next(int.MinValue, int.MaxValue),
            AvatarMetadata = avatarMetadata,
            Items = items,
            ItemLoadouts = itemLoadouts,
            ProfileProperties = profileProperties,
            Reliclink = rng.Next(int.MinValue, int.MaxValue),
            Alias = alias,
            PlatformUserId = platformUserId,
            IsXbox = isXbox,
            AvatarStats = avatarStats,
            PersistentData = persistentData,
            PresenceProperties = presenceProperties,
            Auth = auth
        };
    }

    public IUser GetOrCreateUser(string gameId, IItems itemDefinitions, IAvatarStatDefinitions? avatarStatsDefinitions,
        string remoteAddr, string remoteMacAddress, bool isXbox, ulong platformUserId, string alias)
    {
        if (AppConstants.GeneratePlatformUserId)
        {
            var entropy = new byte[16];
            try
            {
                var mac = System.Net.NetworkInformation.PhysicalAddress.Parse(remoteMacAddress);
                var macBytes = mac.GetAddressBytes();
                Array.Copy(macBytes, 0, entropy, 0, Math.Min(macBytes.Length, 6));
            }
            catch { }

            try
            {
                if (System.Net.IPAddress.TryParse(remoteAddr.Split(':')[0], out var ip))
                {
                    var ipBytes = ip.GetAddressBytes();
                    if (ipBytes.Length >= 4)
                        Array.Copy(ipBytes, 0, entropy, 6, 4);
                }
            }
            catch { }

            var aliasLen = Math.Min(alias.Length, 6);
            Encoding.UTF8.GetBytes(alias.Substring(0, aliasLen)).CopyTo(entropy, 10);

            var seed1 = BitConverter.ToUInt64(entropy, 0);
            var seed2 = BitConverter.ToUInt64(entropy, 8);
            var rng = new Random((int)(seed1 ^ seed2));

            platformUserId = isXbox ? GeneratePlatformUserIdXbox(rng) : GeneratePlatformUserIdSteam(rng);
        }

        var identifier = GetPlatformPath(isXbox, platformUserId);

        return _store.GetOrStore(identifier, () =>
        {
            var persistentData = PersistentStringJsonMap.Create(
                UserDataPath(gameId, !isXbox, platformUserId.ToString()),
                new InitialUpgradableData<PersistentStringJsonMapRaw>());
            return GenerateFn!(gameId, persistentData, itemDefinitions, avatarStatsDefinitions,
                identifier, isXbox, platformUserId, alias);
        });
    }

    public IUser? GetUserByStatId(int id) => GetFirst(u => u.StatId == id);
    public IUser? GetUserById(int id) => GetFirst(u => u.Id == id);
    public IUser? GetUserByPlatformUserId(bool xbox, ulong id) => GetFirst(u => u.Xbox == xbox && u.PlatformUserId == id);

    private IUser? GetFirst(Func<IUser, bool> fn)
    {
        foreach (var u in _store.Values())
        {
            if (fn(u)) return u;
        }
        return null;
    }

    public IEnumerable<int> GetUserIds()
    {
        foreach (var u in _store.Values())
            yield return u.Id;
    }

    public object[][] EncodeProfileInfo(IPresenceDefinitions definitions, Func<IUser, bool> matches, ushort clientLibVersion)
    {
        var profileInfo = new List<object[]>();
        foreach (var u in _store.Values())
        {
            if (matches(u))
            {
                var currentProfileInfo = u.EncodeProfileInfo(clientLibVersion);
                if (definitions != null)
                {
                    currentProfileInfo = currentProfileInfo.Concat(u.EncodePresence(definitions)).ToArray();
                }
                profileInfo.Add(currentProfileInfo);
            }
        }
        return profileInfo.ToArray();
    }

    private static ulong GeneratePlatformUserIdSteam(Random rng)
    {
        var z = (long)rng.Next(0, 1 << 31);
        var y = z % 2;
        var id = z * 2 + y + 76561197960265728;
        return (ulong)id;
    }

    private static ulong GeneratePlatformUserIdXbox(Random rng)
    {
        return (ulong)(rng.NextInt64(9_000_000_000_000_000) + 1_000_000_000_000_000);
    }

    private static string GetPlatformPath(bool isXbox, ulong platformUserId)
    {
        string prefix = isXbox ? "xboxlive" : "steam";
        string fullId = isXbox ? GenerateFullPlatformUserIdXbox((long)platformUserId) : platformUserId.ToString();
        return $"/{prefix}/{fullId}";
    }

    private static string GenerateFullPlatformUserIdXbox(long platformUserId)
    {
        var rng = new Random((int)(platformUserId ^ (platformUserId >> 32)));
        const string chars = "0123456789ABCDEF";
        var id = new char[40];
        for (int j = 0; j < 40; j++)
            id[j] = chars[rng.Next(chars.Length)];
        return new string(id);
    }

    /// <summary>
    /// Lấy đường dẫn dữ liệu người dùng dựa trên game và nền tảng.
    /// </summary>
    public static string UserDataPath(string gameId, bool steam, string platformUserId)
    {
        var platform = steam ? "STEAM" : "XBOX";
        var folder = Path.Combine(AppConstants.ResourcesDir, "userData", gameId);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{platform}_{platformUserId}.json");
    }
}

/// <summary>
/// Lớp triển khai chính của giao diện IUser.
/// </summary>
public class MainUser : IUser
{
    public int Id { get; internal set; }
    public int StatId { get; internal set; }
    public string Alias { get; internal set; } = null!;
    public ulong PlatformUserId { get; internal set; }
    public int ProfileId { get; internal set; }
    public uint ProfileExperience => 0;
    public ushort ProfileLevel => 9999;
    public int Reliclink { get; internal set; }
    public bool IsXbox { get; internal set; }
    public bool Xbox => IsXbox;
    public PersistentStringJsonMap PersistentData { get; internal set; } = null!;
    public IPersistentJsonData<Dictionary<string, string>?> ProfileProperties { get; internal set; } = null!;
    public IPersistentJsonData<string?> AvatarMetadata { get; internal set; } = null!;
    private int _presence;
    public SafeMap<int, string>? PresenceProperties { get; internal set; }
    public IPersistentJsonData<AvatarStats?> AvatarStats { get; internal set; } = null!;
    public IPersistentJsonData<Dictionary<int, MainItem>?> Items { get; internal set; } = null!;
    public IPersistentJsonData<MainItemLoadouts?> ItemLoadouts { get; internal set; } = null!;
    public IPersistentJsonData<DateTime?> Auth { get; internal set; } = null!;

    public int Presence => _presence;
    public void SetPresence(int presence) => _presence = presence;
    public void SetPresenceProperty(int id, string value)
    {
        if (string.IsNullOrEmpty(value))
            PresenceProperties?.Delete(id);
        else
            PresenceProperties?.Store(id, value, _ => true);
    }

    public string PlatformPath => GetPlatformPathInternal(IsXbox, PlatformUserId);

    public int PlatformId => IsXbox ? 9 : 3;

    public byte PlatformRelated => (byte)(IsXbox ? 3 : 0);

    public object[] EncodeAvatarStats()
    {
        object[]? result = null;
        AvatarStats?.WithReadOnly(data =>
        {
            if (data != null)
                result = data.Encode(ProfileId);
            return Task.CompletedTask;
        });
        return result ?? Array.Empty<object>();
    }

    public object[] EncodeExtraProfileInfo(ushort clientLibVersion)
    {
        var info = new object[]
        {
            StatId, 0, 0, 1, -1, 0, 0, -1, -1, -1, -1, -1, 1000,
            1713372625, // Một mốc thời gian trong quá khứ
            0, 0, 0
        };
        if (clientLibVersion >= 190)
        {
            info = info.Concat(new object[] { 0, 0 }).ToArray();
        }
        return info;
    }

    public object[] EncodeProfileInfo(ushort clientLibVersion)
    {
        var profileInfo = new List<object>
        {
            new DateTimeOffset(2024, 5, 2, 3, 34, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
            Id,
            PlatformPath,
            AvatarMetadata,
            Alias
        };
        if (clientLibVersion >= 190)
            profileInfo.Add(Alias);

        profileInfo.AddRange(new object[]
        {
            "", StatId, ProfileExperience, ProfileLevel, PlatformRelated,
            null!, PlatformUserId.ToString(), PlatformId, Array.Empty<object>()
        });
        return profileInfo.ToArray();
    }

    public object[] EncodePresence(IPresenceDefinitions definitions)
    {
        if (definitions == null || PresenceProperties == null)
            return Array.Empty<object>();

        var presenceId = Presence;
        var presenceDef = definitions.Get(presenceId);
        if (presenceDef == null)
            return Array.Empty<object>();

        var presenceProps = new List<object[]>();
        var pp = PresenceProperties;
        foreach (var kv in pp.Iter())
        {
            presenceProps.Add(new object[] { kv.key, kv.value });
        }

        return new object[] { presenceId, presenceDef.Label, presenceProps.ToArray() };
    }

    private static string GetPlatformPathInternal(bool isXbox, ulong platformUserId)
    {
        string prefix = isXbox ? "xboxlive" : "steam";
        string fullId = isXbox ? GenerateFullPlatformUserIdXbox((long)platformUserId) : platformUserId.ToString();
        return $"/{prefix}/{fullId}";
    }

    private static string GenerateFullPlatformUserIdXbox(long platformUserId)
    {
        var rng = new Random((int)(platformUserId ^ (platformUserId >> 32)));
        const string chars = "0123456789ABCDEF";
        var id = new char[40];
        for (int j = 0; j < 40; j++)
            id[j] = chars[rng.Next(chars.Length)];
        return new string(id);
    }
}
