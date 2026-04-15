namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện vật phẩm (item) trong game.
/// </summary>
public interface IItem
{
    int Id { get; }
    string Name { get; }
    int Type { get; }
    object[] Encode(int userId);
}

/// <summary>
/// Lớp triển khai chính của vật phẩm.
/// </summary>
public class MainItem : IItem
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int Type { get; set; }

    public object[] Encode(int userId)
    {
        return new object[] { Id, userId, Name, Type };
    }
}

/// <summary>
/// Giao diện quản lý tập hợp vật phẩm.
/// </summary>
public interface IItems
{
    void Initialize(byte[]? itemDefinitions, object[] itemLocations);
    IItem? Get(int id);
    object[] Encode(int userId);
}

/// <summary>
/// Lớp triển khai chính quản lý vật phẩm.
/// </summary>
public class MainItems : IItems
{
    private Dictionary<int, IItem> _items = new();
    private object[] _itemLocations = Array.Empty<object>();

    public void Initialize(byte[]? itemDefinitions, object[] itemLocations)
    {
        _itemLocations = itemLocations;
        // Parse item definitions from JSON
    }

    public IItem? Get(int id)
    {
        _items.TryGetValue(id, out var item);
        return item;
    }

    public object[] Encode(int userId)
    {
        return _items.Values.Select(item => item.Encode(userId)).Cast<object[]>().ToArray();
    }
}

/// <summary>
/// Dữ liệu mặc định có thể nâng cấp cho Items.
/// </summary>
public class ItemsUpgradableDefaultData : IUpgradableDefaultData<Dictionary<int, MainItem>?>
{
    private readonly string _gameId;
    private readonly IItems _itemDefinitions;

    public ItemsUpgradableDefaultData(string gameId, IItems itemDefinitions)
    {
        _gameId = gameId;
        _itemDefinitions = itemDefinitions;
    }

    public Dictionary<int, MainItem>? Default() => new();
}
