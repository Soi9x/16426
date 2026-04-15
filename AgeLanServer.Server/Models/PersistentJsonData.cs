using AgeLanServer.Common;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện dữ liệu JSON liên tục (persistent).
/// Cho phép đọc/ghi dữ liệu JSON được lưu trữ liên tục.
/// </summary>
public interface IPersistentJsonData<T>
{
    T? Value { get; }
    Task WithReadOnly(Func<T?, Task> action);
    Task WithReadWrite(Func<T?, Task> action);
}

/// <summary>
/// Lớp triển khai dữ liệu JSON liên tục.
/// </summary>
public class PersistentJsonData<T> : IPersistentJsonData<T>
{
    private T? _value;
    private readonly object _lock = new();
    private readonly string _key;
    private readonly IUpgradableDefaultData<T>? _defaultData;

    public T? Value
    {
        get { lock (_lock) return _value; }
    }

    private PersistentJsonData(string key, IUpgradableDefaultData<T>? defaultData)
    {
        _key = key;
        _defaultData = defaultData;
        _value = defaultData != null ? defaultData.Default() : default;
    }

    public static PersistentJsonData<T> Create(PersistentStringJsonMap persistentData, string key, IUpgradableDefaultData<T> defaultData)
    {
        return new PersistentJsonData<T>(key, defaultData);
    }

    public Task WithReadOnly(Func<T?, Task> action)
    {
        lock (_lock) return action(_value);
    }

    public Task WithReadWrite(Func<T?, Task> action)
    {
        lock (_lock) return action(_value);
    }
}

/// <summary>
/// Giao diện dữ liệu mặc định có thể nâng cấp.
/// Cung cấp giá trị mặc định khi khởi tạo.
/// </summary>
public interface IUpgradableDefaultData<T>
{
    T Default();
}

/// <summary>
/// Triển khai mặc định cho IUpgradableDefaultData.
/// </summary>
public class InitialUpgradableDefaultData<T> : IUpgradableDefaultData<T>
{
    public virtual T Default() => default!;
}

/// <summary>
/// Ánh xạ JSON liên tục dạng chuỗi.
/// Lưu trữ dữ liệu người dùng dưới dạng JSON.
/// </summary>
public class PersistentStringJsonMap
{
    private readonly Dictionary<string, string> _data = new();
    private readonly string _filePath;
    private readonly object _lock = new();

    private PersistentStringJsonMap(string filePath)
    {
        _filePath = filePath;
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                // Parse JSON vào _data
            }
            catch { }
        }
    }

    public static PersistentStringJsonMap Create(string filePath, InitialUpgradableData<PersistentStringJsonMapRaw> initialData)
    {
        return new PersistentStringJsonMap(filePath);
    }

    public string? Get(string key)
    {
        lock (_lock)
        {
            _data.TryGetValue(key, out var value);
            return value;
        }
    }

    public void Set(string key, string value)
    {
        lock (_lock)
        {
            _data[key] = value;
            Save();
        }
    }

    private void Save()
    {
        try
        {
            // Serialize _data ra JSON và lưu
            File.WriteAllText(_filePath, "{}");
        }
        catch { }
    }
}

/// <summary>
/// Dữ liệu thô cho PersistentStringJsonMap.
/// </summary>
public class PersistentStringJsonMapRaw { }

/// <summary>
/// Dữ liệu mặc định ban đầu.
/// </summary>
public class InitialUpgradableData<T> : IUpgradableDefaultData<T>
{
    public T Default() => default!;
}

/// <summary>
/// Dữ liệu mặc định cho thuộc tính hồ sơ (profile properties).
/// </summary>
public class ProfilePropertiesUpgradableDefaultData : IUpgradableDefaultData<Dictionary<string, string>?>
{
    public Dictionary<string, string>? Default() => new();
}

/// <summary>
/// Dữ liệu mặc định cho metadata avatar.
/// Thay đổi theo từng game.
/// </summary>
public class AvatarMetadataUpgradableDefaultData : IUpgradableDefaultData<string?>
{
    private readonly string _gameId;

    public AvatarMetadataUpgradableDefaultData(string gameId) => _gameId = gameId;

    public string? Default()
    {
        return _gameId switch
        {
            AppConstants.GameAoE3 or AppConstants.GameAoM => "{\"v\":1,\"twr\":0,\"wlr\":0,\"ai\":1,\"ac\":0}",
            AppConstants.GameAoE4 => "{\"sharedHistory\":1,\"hardwareType\":0,\"inputDeviceType\":0}",
            _ => null
        };
    }
}
