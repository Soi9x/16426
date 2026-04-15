using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Shared;
using System.Runtime.InteropServices;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện quảng cáo (lobby/phòng chơi).
/// Đại diện cho một phòng chơi trong game với các thông tin như host, người chơi,
/// mật khẩu, bản đồ, mod, v.v.
/// </summary>
public interface IAdvertisement
{
    string XboxSessionId { get; }
    int? UnsafeGetModDllChecksum();
    string? UnsafeGetModDllFile();
    string? UnsafeGetPasswordValue();
    long? UnsafeGetStartTime();
    sbyte UnsafeGetState();
    string? UnsafeGetDescription();
    string RelayRegion { get; }
    int Party { get; }
    bool UnsafeGetVisible();
    bool UnsafeGetJoinable();
    int UnsafeGetAppBinaryChecksum();
    int UnsafeGetDataChecksum();
    byte UnsafeGetMatchType();
    string? UnsafeGetModName();
    string? UnsafeGetModVersion();
    uint UnsafeGetVersionFlags();
    ulong UnsafeGetPlatformSessionId();
    uint UnsafeGetObserversDelay();
    bool UnsafeGetObserversEnabled();
    void UnsafeSetHostId(int hostId);
    void UnsafeUpdateState(sbyte state);
    void UnsafeUpdatePlatformSessionId(ulong sessionId);
    void UnsafeUpdateTags(Dictionary<string, int> integer, Dictionary<string, string> text);
    bool UnsafeMatchesTags(Dictionary<string, int> integer, Dictionary<string, string> text);
    object[] UnsafeEncode(string gameId, IBattleServers battleServers);
    void UnsafeUpdate(AdvertisementUpdateRequest advFrom);
    int Id { get; }
    string Ip { get; }
    int? UnsafeGetHostId();
    SafeOrderedMap<int, IPeer> GetPeers();
    SafeOrderedMap<int, IPeer> Peers { get; }
    IMessage MakeMessage(bool broadcast, string content, byte typeId, IUser sender, IEnumerable<IUser> receivers);
    void StartObserving(int userId);
    void StopObserving(int userId);
    object[][] EncodePeers();
}

/// <summary>
/// Lớp triển khai chính của quảng cáo (phòng chơi).
/// </summary>
public class MainAdvertisement : IAdvertisement
{
    public int Id { get; internal set; }
    public string Ip { get; internal set; } = null!;
    private int _automatchPollId;
    public string RelayRegion { get; internal set; } = null!;
    private int _appBinaryChecksum;
    private string _mapName = null!;
    private string _description = null!;
    private int _dataChecksum;
    private ModDll _modDll = new();
    private string _modName = null!;
    private string _modVersion = null!;
    private Observers _observers = new();
    private Password _password = new();
    private bool _visible;
    public int Party { get; internal set; }
    private int _race;
    private int _team;
    private int _statGroup;
    private uint _versionFlags;
    private bool _joinable;
    private byte _matchType;
    private byte _maxPlayers;
    private string _options = null!;
    private string _slotInfo = null!;
    private ulong _platformSessionId;
    private sbyte _state;
    private bool _lan;
    private long _startTime;
    public SafeOrderedMap<int, IPeer> Peers { get; internal set; } = null!;
    public string XboxSessionId { get; internal set; } = null!;
    private Tags _tags = new();

    private int? _hostId;

    public int? UnsafeGetHostId() => _hostId;
    public SafeOrderedMap<int, IPeer> GetPeers() => Peers;

    public int? UnsafeGetModDllChecksum() => _modDll.Checksum;
    public string? UnsafeGetModDllFile() => _modDll.File;
    public string? UnsafeGetPasswordValue() => _password.Value;
    public long? UnsafeGetStartTime() => _startTime != 0 ? _startTime : null;
    public sbyte UnsafeGetState() => _state;
    public string? UnsafeGetDescription() => _description;
    public bool UnsafeGetJoinable() => _joinable;
    public bool UnsafeGetVisible() => _visible;
    public int UnsafeGetAppBinaryChecksum() => _appBinaryChecksum;
    public int UnsafeGetDataChecksum() => _dataChecksum;
    public byte UnsafeGetMatchType() => _matchType;
    public string? UnsafeGetModName() => _modName;
    public string? UnsafeGetModVersion() => _modVersion;
    public uint UnsafeGetVersionFlags() => _versionFlags;
    public ulong UnsafeGetPlatformSessionId() => _platformSessionId;
    public uint UnsafeGetObserversDelay() => _observers.Delay;
    public bool UnsafeGetObserversEnabled() => _observers.Enabled;

    public void UnsafeSetHostId(int hostId) => _hostId = hostId;

    public void UnsafeUpdateState(sbyte state)
    {
        var previousState = _state;
        _state = state;
        if (_state == 1 && previousState != 1)
        {
            _startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _visible = false;
            _joinable = false;
            _observers.UserIds = new SafeSet<int>();
        }
    }

    public void UnsafeUpdatePlatformSessionId(ulong sessionId) => _platformSessionId = sessionId;

    public void UnsafeUpdateTags(Dictionary<string, int> integer, Dictionary<string, string> text)
    {
        _tags.Integer = integer;
        _tags.Text = text;
    }

    public bool UnsafeMatchesTags(Dictionary<string, int> integer, Dictionary<string, string> text)
    {
        return ContainsFilter(integer, _tags.Integer) && ContainsFilter(text, _tags.Text);
    }

    private static bool ContainsFilter<K, V>(Dictionary<K, V> filter, Dictionary<K, V> tags) where K : notnull where V : notnull
    {
        foreach (var (fk, fv) in filter)
        {
            if (!tags.TryGetValue(fk, out var tv) || !fv!.Equals(tv))
                return false;
        }
        return true;
    }

    public void UnsafeUpdate(AdvertisementUpdateRequest advFrom)
    {
        _automatchPollId = advFrom.AutomatchPollId;
        _appBinaryChecksum = advFrom.AppBinaryChecksum;
        _mapName = advFrom.MapName;
        _description = advFrom.Description;
        _dataChecksum = advFrom.DataChecksum;
        _modDll.Checksum = advFrom.ModDllChecksum;
        _modDll.File = advFrom.ModDllFile;
        _modName = advFrom.ModName;
        _modVersion = advFrom.ModVersion;
        _observers.Delay = advFrom.ObserverDelay;
        _observers.Enabled = advFrom.Observable;
        _observers.Password = advFrom.ObserverPassword;
        _password.Enabled = advFrom.Passworded;
        _password.Value = advFrom.Password;
        _visible = advFrom.Visible;
        _versionFlags = advFrom.VersionFlags;
        _joinable = advFrom.Joinable;
        _matchType = advFrom.MatchType;
        _maxPlayers = advFrom.MaxPlayers;
        _options = advFrom.Options;
        _slotInfo = advFrom.SlotInfo;
        UnsafeUpdateState(advFrom.State);
    }

    public object[] UnsafeEncode(string gameId, IBattleServers battleServers)
    {
        long? startTime = _startTime != 0 ? _startTime : null;
        var response = new List<object> { Id, _platformSessionId };

        if (gameId == AppConstants.GameAoE2 || gameId == AppConstants.GameAoM || gameId == AppConstants.GameAoE4)
        {
            if (gameId == AppConstants.GameAoE4)
                response.Add(0);
            else
                response.Add("0");
            response.AddRange(new[] { "", "" });
        }

        response.Add(XboxSessionId);
        response.Add(_hostId ?? 0);
        response.Add(_state);
        response.Add(_description);

        if (gameId == AppConstants.GameAoE2 || gameId == AppConstants.GameAoM || gameId == AppConstants.GameAoE4)
            response.Add(_description);

        response.AddRange(new object[]
        {
            _visible ? 1 : 0, _mapName, _options, _password.Enabled ? 1 : 0,
            _maxPlayers, _slotInfo, _matchType, EncodePeers(),
            _observers.UserIds?.Len() ?? 0, 0, _observers.Enabled ? 1 : 0,
            _observers.Delay, !string.IsNullOrEmpty(_observers.Password) ? 1 : 0,
            _lan ? 1 : 0, startTime, RelayRegion
        });

        if (_lan)
        {
            response.Add(null!);
        }
        else
        {
            if (battleServers.Get(RelayRegion) is { } bs)
            {
                var arr = response.ToArray();
                bs.AppendName(ref arr);
                response.Clear();
                response.AddRange(arr);
            }
        }

        return response.ToArray();
    }

    public object[][] EncodePeers()
    {
        var peers = Peers.Values().ToArray();
        return peers.Select(p => p.Encode()).ToArray();
    }

    public IMessage MakeMessage(bool broadcast, string content, byte typeId, IUser sender, IEnumerable<IUser> receivers)
    {
        return new MainMessage
        {
            AdvertisementId = Id,
            Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Broadcast = broadcast,
            Content = content,
            Typ = typeId,
            Sender = sender,
            Receivers = receivers.ToList()
        };
    }

    public void StartObserving(int userId) => _observers.UserIds?.Store(userId);
    public void StopObserving(int userId) => _observers.UserIds?.Delete(userId);
}

// Các lớp phụ trợ
class ModDll
{
    public string? File { get; set; }
    public int? Checksum { get; set; }
}

class Observers
{
    public bool Enabled { get; set; }
    public uint Delay { get; set; }
    public string? Password { get; set; }
    public SafeSet<int>? UserIds { get; set; }
}

class Password
{
    public string? Value { get; set; }
    public bool Enabled { get; set; }
}

class Tags
{
    public Dictionary<string, int> Integer { get; set; } = new();
    public Dictionary<string, string> Text { get; set; } = new();
}

/// <summary>
/// Giao diện quản lý tập hợp quảng cáo (phòng chơi).
/// </summary>
public interface IAdvertisements
{
    void Initialize(IUsers users, IBattleServers battleServers);
    IAdvertisement Store(AdvertisementUpdateRequest advFrom, bool generateMetadata, string gameId);
    void WithReadLock(int id, Action action);
    void WithWriteLock(int id, Action action);
    IAdvertisement? GetAdvertisement(int id);
    IPeer UnsafeNewPeer(int advertisementId, string advertisementIp, int userId, int userStatId, int party, int race, int team);
    bool UnsafeRemovePeer(int advertisementId, int userId);
    void UnsafeDelete(IAdvertisement adv);
    IAdvertisement? UnsafeFirstAdvertisement(Func<IAdvertisement, bool> matches);
    object[] LockedFindAdvertisementsEncoded(string gameId, int length, int offset, bool preMatchesLocking, Func<IAdvertisement, bool> matches);
    IAdvertisement? GetUserAdvertisement(int userId);
}

/// <summary>
/// Lớp triển khai chính quản lý tập hợp quảng cáo.
/// Sử dụng SafeOrderedMap và KeyRWMutex cho truy cập an toàn.
/// </summary>
public class MainAdvertisements : IAdvertisements
{
    private SafeOrderedMap<int, IAdvertisement> _store = null!;
    private KeyRwMutex<int> _locks = null!;
    private IUsers _users = null!;
    private IBattleServers _battleServers = null!;

    public void Initialize(IUsers users, IBattleServers battleServers)
    {
        _store = new SafeOrderedMap<int, IAdvertisement>();
        _locks = new KeyRwMutex<int>();
        _users = users;
        _battleServers = battleServers;
    }

    public IAdvertisement Store(AdvertisementUpdateRequest advFrom, bool generateXboxSessionId, string gameId)
    {
        var adv = new MainAdvertisement();
        var rng = new Random();
        adv.Ip = $"/10.0.11.{rng.Next(1, 255)}";
        adv.RelayRegion = advFrom.RelayRegion;

        if (generateXboxSessionId)
        {
            var scidEnd = gameId switch
            {
                AppConstants.GameAoM => "00006fe8b971",
                AppConstants.GameAoE4 => "00007d18f66e",
                _ => "000068a451d4"
            };
            adv.XboxSessionId = $"{{\"templateName\":\"GameSession\",\"name\":\"{Guid.NewGuid()}\",\"scid\":\"00000000-0000-0000-0000-{scidEnd}\"}}";
        }
        else
        {
            adv.XboxSessionId = "0";
        }

        adv.Party = advFrom.Party;
        adv.Peers = new SafeOrderedMap<int, IPeer>();
        adv.UnsafeUpdate(advFrom);

        while (true)
        {
            adv.Id = rng.Next(int.MinValue, int.MaxValue);
            var (exists, storedAdv) = _store.Store(adv.Id, adv, _ => false);
            if (!exists) return storedAdv;
        }
    }

    public void WithReadLock(int id, Action action)
    {
        _locks.RLock(id);
        try { action(); }
        finally { _locks.RUnlock(id); }
    }

    public void WithWriteLock(int id, Action action)
    {
        _locks.Lock(id);
        try { action(); }
        finally { _locks.Unlock(id); }
    }

    public IAdvertisement? GetAdvertisement(int id) => _store.Load(id);

    public IPeer UnsafeNewPeer(int advertisementId, string advertisementIp, int userId, int userStatId, int party, int race, int team)
    {
        if (GetAdvertisement(advertisementId) is not { } adv) return null!;
        var peer = Peer.New(advertisementId, advertisementIp, userId, userStatId, party, race, team);
        var (_, storedPeer) = adv.Peers.Store(peer.UserId, peer, _ => false);
        return storedPeer;
    }

    public bool UnsafeRemovePeer(int advertisementId, int userId)
    {
        if (GetAdvertisement(advertisementId) is not { } adv) return false;
        if (!adv.Peers.Delete(userId)) return false;
        if (adv.Peers.Len() == 0)
            UnsafeDelete(adv);
        return true;
    }

    public void UnsafeDelete(IAdvertisement adv) => _store.Delete(adv.Id);

    public IAdvertisement? UnsafeFirstAdvertisement(Func<IAdvertisement, bool> matches)
    {
        foreach (var adv in _store.Values())
        {
            if (matches(adv)) return adv;
        }
        return null;
    }

    public object[] LockedFindAdvertisementsEncoded(string gameId, int length, int offset, bool preMatchesLocking, Func<IAdvertisement, bool> matches)
    {
        var res = new List<object[]>();
        foreach (var adv in _store.Values())
        {
            if (preMatchesLocking)
            {
                _locks.RLock(adv.Id);
                try
                {
                    if (matches(adv))
                        res.Add(adv.UnsafeEncode(gameId, _battleServers));
                }
                finally { _locks.RUnlock(adv.Id); }
            }
            else
            {
                WithReadLock(adv.Id, () => res.Add(adv.UnsafeEncode(gameId, _battleServers)));
            }
        }

        if (offset >= res.Count) return Array.Empty<object[]>();
        if (length == 0) length = res.Count;
        var end = Math.Min(length + offset, res.Count);
        return res.GetRange(offset, end - offset).ToArray();
    }

    public IAdvertisement? GetUserAdvertisement(int userId)
    {
        return UnsafeFirstAdvertisement(adv =>
        {
            foreach (var usId in adv.GetPeers().Keys())
            {
                if (usId == userId) return true;
            }
            return false;
        });
    }
}
