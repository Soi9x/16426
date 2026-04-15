namespace AgeLanServer.Server.Models;

/// <summary>
/// Thống kê avatar của người chơi.
/// Chứa các chỉ số như số trận thắng, thua, kỹ năng, v.v.
/// </summary>
public class AvatarStats
{
    public Dictionary<string, object> Stats { get; set; } = new();

    /// <summary>
    /// Mã hóa thống kê avatar thành mảng đối tượng.
    /// </summary>
    public object[] Encode(int profileId)
    {
        var result = new List<object> { profileId };
        foreach (var (key, value) in Stats)
        {
            result.Add(key);
            result.Add(value);
        }
        return result.ToArray();
    }
}

/// <summary>
/// Dữ liệu mặc định có thể nâng cấp cho AvatarStats.
/// Khởi tạo thống kê avatar dựa trên gameId và định nghĩa.
/// </summary>
public class AvatarStatsUpgradableDefaultData : IUpgradableDefaultData<AvatarStats?>
{
    private readonly string _gameId;
    private readonly IAvatarStatDefinitions _definitions;

    public AvatarStatsUpgradableDefaultData(string gameId, IAvatarStatDefinitions definitions)
    {
        _gameId = gameId;
        _definitions = definitions;
    }

    public AvatarStats? Default()
    {
        return new AvatarStats();
    }
}
