namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện định nghĩa trạng thái hiện diện (presence).
/// </summary>
public interface IPresenceDefinitions
{
    void Initialize(object[] presenceData);
    IPresenceDefinition? Get(int id);
}

/// <summary>
/// Giao diện một định nghĩa hiện diện cụ thể.
/// </summary>
public interface IPresenceDefinition
{
    int Id { get; }
    string Label { get; }
}

/// <summary>
/// Lớp triển khai chính của định nghĩa hiện diện.
/// </summary>
public class MainPresenceDefinitions : IPresenceDefinitions
{
    private Dictionary<int, IPresenceDefinition> _definitions = new();

    public void Initialize(object[] presenceData)
    {
        // Parse presence definitions from data
    }

    public IPresenceDefinition? Get(int id)
    {
        _definitions.TryGetValue(id, out var def);
        return def;
    }
}

/// <summary>
/// Dữ liệu hiện diện cụ thể.
/// </summary>
public class MainPresenceDefinition : IPresenceDefinition
{
    public int Id { get; set; }
    public string Label { get; set; } = null!;
}
