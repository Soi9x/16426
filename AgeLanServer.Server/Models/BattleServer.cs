using System.Runtime.InteropServices;
using AgeLanServer.Server.Internal;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện máy chủ battle.
/// Đại diện cho một máy chủ chơi game với các cấu hình vùng, cổng, và chế độ LAN.
/// </summary>
public interface IBattleServer
{
    void SetLAN(bool lan);
    void SetIPv4(string ipv4);
    void SetBsPort(int bsPort);
    void SetWebSocketPort(int webSocketPort);
    void SetOutOfBandPort(int outOfBandPort);
    void SetHasOobPort(bool hasOobPort);
    void SetBattleServerName(string battleServerName);
    void SetName(string name);
    bool LAN { get; }
    string Region { get; }
    void AppendName(ref object[] encoded);
    object[] EncodeLogin(HttpRequest r);
    object[] EncodePorts();
    object[] EncodeAdvertisement(HttpRequest r);
    string ResolveIPv4(HttpRequest r);
    string ToString();
}

/// <summary>
/// Lớp triển khai chính của máy chủ battle.
/// </summary>
public class MainBattleServer : IBattleServer
{
    public string Region { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string IPv4 { get; set; } = null!;
    public int BsPort { get; set; }
    public int WebSocketPort { get; set; }
    public int OutOfBandPort { get; set; }

    private bool _hasOobPort;
    private string _battleServerName = null!;
    private bool? _lan;
    private readonly object _lanMu = new();

    public bool LAN
    {
        get
        {
            lock (_lanMu)
            {
                if (_lan == null)
                {
                    if (Guid.TryParse(Region, out var guid) && guid.Version == 4)
                        _lan = true;
                    else
                        _lan = false;
                }
                return _lan.Value;
            }
        }
    }

    public void SetBattleServerName(string battleServerName) => _battleServerName = battleServerName;
    public void SetHasOobPort(bool hasOobPort) => _hasOobPort = hasOobPort;
    public void SetIPv4(string ipv4) => IPv4 = ipv4;
    public void SetBsPort(int bsPort) => BsPort = bsPort;
    public void SetWebSocketPort(int webSocketPort) => WebSocketPort = webSocketPort;
    public void SetOutOfBandPort(int outOfBandPort) => OutOfBandPort = outOfBandPort;
    public void SetName(string name) => Name = name;

    public void SetLAN(bool enable)
    {
        lock (_lanMu) _lan = enable;
    }

    public void AppendName(ref object[] encoded)
    {
        switch (_battleServerName)
        {
            case "omit":
                break;
            case "null":
                encoded = encoded.Append((object?)null).ToArray();
                break;
            default:
                encoded = encoded.Append(Name).ToArray();
                break;
        }
    }

    public object[] EncodeLogin(HttpRequest r)
    {
        var encoded = new List<object> { Region };
        var arr = encoded.ToArray();
        AppendName(ref arr);
        encoded.Clear();
        encoded.AddRange(arr);
        encoded.Add(ResolveIPv4(r));
        encoded.AddRange(EncodePorts());
        return encoded.ToArray();
    }

    public object[] EncodePorts()
    {
        var encoded = new List<object> { BsPort, WebSocketPort };
        if (_hasOobPort)
            encoded.Add(OutOfBandPort);
        return encoded.ToArray();
    }

    public object[] EncodeAdvertisement(HttpRequest r)
    {
        var encoded = new List<object> { ResolveIPv4(r) };
        encoded.AddRange(EncodePorts());
        return encoded.ToArray();
    }

    public string ResolveIPv4(HttpRequest r)
{
    if (IPv4 == "auto")
    {
        var remoteAddr = r.HttpContext.Connection.RemoteIpAddress;
        // Nếu client kết nối từ localhost → trả về 127.0.0.1
        if (remoteAddr != null && 
            (remoteAddr.ToString() == "127.0.0.1" || remoteAddr.ToString() == "::1"))
        {
            return "127.0.0.1";
        }
        // Client từ LAN → trả về LAN IP
        var localAddr = r.HttpContext.Features
            .Get<Microsoft.AspNetCore.Http.Features.IHttpConnectionFeature>()?.LocalIpAddress;
        return localAddr?.ToString() ?? "127.0.0.1";
    }
    return IPv4;
}

    public override string ToString()
    {
        var ports = string.Join(", ", EncodePorts());
        return $"Region: {Region} (Name: {Name}), IPv4: {IPv4}, Ports: [{ports}]";
    }
}

/// <summary>
/// Tùy chọn cấu hình máy chủ battle.
/// </summary>
public class BattleServerOpts
{
    public bool OobPort { get; set; } = true;
    public string Name { get; set; } = "true";
}

/// <summary>
/// Giao diện quản lý tập hợp máy chủ battle.
/// </summary>
public interface IBattleServers
{
    void Initialize(IEnumerable<IBattleServer> battleServers, BattleServerOpts? opts);
    IEnumerable<KeyValuePair<string, IBattleServer>> Iter();
    object[] Encode(HttpRequest r);
    IBattleServer? Get(string region);
    IBattleServer NewLANBattleServer(string region);
    IBattleServer NewBattleServer(string region);
}

/// <summary>
/// Lớp triển khai chính quản lý tập hợp máy chủ battle.
/// </summary>
public class MainBattleServers : IBattleServers
{
    private ReadOnlyOrderedMap<string, IBattleServer> _store = null!;
    private bool _haveOobPort;
    private string _battleServerName = null!;

    public void Initialize(IEnumerable<IBattleServer> battleServers, BattleServerOpts? opts)
    {
        opts ??= new BattleServerOpts { OobPort = true };
        if (string.IsNullOrEmpty(opts.Name))
            opts.Name = "true";

        var keyOrder = new List<string>();
        var mapping = new Dictionary<string, IBattleServer>();

        foreach (var bs in battleServers)
        {
            bs.SetHasOobPort(opts.OobPort);
            bs.SetBattleServerName(opts.Name);
            keyOrder.Add(bs.Region);
            mapping[bs.Region] = bs;
        }

        _battleServerName = opts.Name;
        _haveOobPort = opts.OobPort;
        _store = new ReadOnlyOrderedMap<string, IBattleServer>(keyOrder, mapping);
    }

    public IEnumerable<KeyValuePair<string, IBattleServer>> Iter() =>
        _store.Iter().Select(kv => new KeyValuePair<string, IBattleServer>(kv.key, kv.value));

    public object[] Encode(HttpRequest r)
    {
        var encoded = new object[_store.Len()];
        int i = 0;
        foreach (var (_, bs) in _store.Iter())
        {
            encoded[i++] = bs.EncodeLogin(r);
        }
        return encoded;
    }

    public IBattleServer? Get(string region) => _store.Load(region);

    public IBattleServer NewLANBattleServer(string region)
    {
        var bs = NewBattleServer(region);
        bs.SetLAN(true);
        return bs;
    }

    public IBattleServer NewBattleServer(string region)
    {
        var bs = new MainBattleServer
        {
            Region = region,
            IPv4 = "auto"
        };
        bs.SetHasOobPort(_haveOobPort);
        bs.SetBattleServerName(_battleServerName);
        return bs;
    }
}
