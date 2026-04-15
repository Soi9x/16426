namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện định nghĩa bảng xếp hạng.
/// </summary>
public interface ILeaderboardDefinitions
{
    void Initialize(object[] leaderboards);
    IAvatarStatDefinitions AvatarStatDefinitions();
}

/// <summary>
/// Lớp triển khai chính của định nghĩa bảng xếp hạng.
/// </summary>
public class MainLeaderboardDefinitions : ILeaderboardDefinitions
{
    private MainAvatarStatDefinitions? _avatarStatDefinitions;

    public void Initialize(object[] leaderboards)
    {
        _avatarStatDefinitions = AvatarStatDefinitionsHelper.NewAvatarStatDefinitions(leaderboards);
    }

    public IAvatarStatDefinitions AvatarStatDefinitions() => _avatarStatDefinitions!;
}

/// <summary>
/// Giao diện định nghĩa thống kê avatar.
/// Ánh xạ giữa tên và ID của thống kê.
/// </summary>
public interface IAvatarStatDefinitions
{
    bool GetIdByName(string name, out int id);
    bool GetNameById(int id, out string name);
}

/// <summary>
/// Lớp triển khai ánh xạ tên <-> ID cho thống kê avatar.
/// </summary>
public class MainAvatarStatDefinitions : IAvatarStatDefinitions
{
    private readonly Dictionary<string, int> _nameToId = new();
    private readonly Dictionary<int, string> _idToName = new();

    internal void Add(int id, string name)
    {
        _nameToId[name] = id;
        _idToName[id] = name;
    }

    public bool GetIdByName(string name, out int id) => _nameToId.TryGetValue(name, out id);
    public bool GetNameById(int id, out string name) => _idToName.TryGetValue(id, out name);
}

/// <summary>
/// Helper để tạo AvatarStatDefinitions từ dữ liệu bảng xếp hạng.
/// </summary>
internal static class AvatarStatDefinitionsHelper
{
    public static MainAvatarStatDefinitions NewAvatarStatDefinitions(object[] leaderboards)
    {
        var result = new MainAvatarStatDefinitions();
        // Parse avatar stats from leaderboards data
        return result;
    }
}
