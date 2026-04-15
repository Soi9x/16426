using AgeLanServer.Server.Internal;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện bộ vật phẩm (loadout) - tập hợp các vật phẩm được chọn.
/// </summary>
public interface IItemLoadout
{
    int Id { get; }
    string Name { get; }
    HashSet<int> ItemOrLocIds { get; }
    int Type { get; }
    object[] Encode(int userId);
    void Update(string name, int type, HashSet<int> itemOrLocIds);
}

/// <summary>
/// Giao diện quản lý tập hợp bộ vật phẩm.
/// </summary>
public interface IItemLoadouts
{
    IItemLoadout? Get(int id);
    object[] NewItemLoadout(string name, int type, HashSet<int> itemOrLocIds, int userId);
    IEnumerable<IItemLoadout> Iter();
}

/// <summary>
/// Lớp triển khai chính của bộ vật phẩm.
/// </summary>
public class MainItemLoadout : IItemLoadout
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public HashSet<int> ItemOrLocIds { get; set; } = new();
    public int Type { get; set; }

    public object[] Encode(int userId)
    {
        return new object[] { Id, userId, Name, Type, "[]" };
    }

    public void Update(string name, int type, HashSet<int> itemOrLocIds)
    {
        Name = name;
        Type = type;
        ItemOrLocIds = itemOrLocIds;
    }
}

/// <summary>
/// Lớp triển khai chính quản lý bộ vật phẩm.
/// </summary>
public class MainItemLoadouts : IItemLoadouts
{
    public Dictionary<int, IItemLoadout> ItemLoadouts { get; set; } = new();

    public IItemLoadout? Get(int id)
    {
        ItemLoadouts.TryGetValue(id, out var loadout);
        return loadout;
    }

    public object[] NewItemLoadout(string name, int type, HashSet<int> itemOrLocIds, int userId)
    {
        var rng = new Random();
        int loadoutId;
        do { loadoutId = rng.Next(int.MinValue, int.MaxValue); }
        while (ItemLoadouts.ContainsKey(loadoutId));

        var loadout = new MainItemLoadout
        {
            Id = loadoutId,
            Name = name,
            ItemOrLocIds = itemOrLocIds,
            Type = type
        };
        ItemLoadouts[loadoutId] = loadout;
        return loadout.Encode(userId);
    }

    public IEnumerable<IItemLoadout> Iter() => ItemLoadouts.Values;
}

/// <summary>
/// Dữ liệu mặc định có thể nâng cấp cho ItemLoadouts.
/// </summary>
public class ItemLoadoutsUpgradableDefaultData : IUpgradableDefaultData<MainItemLoadouts?>
{
    public MainItemLoadouts? Default() => new MainItemLoadouts { ItemLoadouts = new Dictionary<int, IItemLoadout>() };
}
