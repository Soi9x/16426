namespace AgeLanServer.Server.Models.Athens.Routes.Game.CommunityEvent;

/// <summary>
/// Điểm số thử thách thiên thể hàng ngày.
/// Công thức: A, B, P dùng để tính toán điểm.
/// </summary>
public class DailyCelestialChallengeScore
{
    public int A { get; set; }
    public int B { get; set; }
    public double P { get; set; }
}

/// <summary>
/// Thử thách thiên thể hàng ngày mở rộng.
/// </summary>
public class DailyCelestialChallengePlus
{
    public DailyCelestialChallengeScore Score { get; set; } = new();
    public DailyCelestialChallengeScore ScoreCalcParams { get; set; } = new();
}

/// <summary>
/// Entry trong bản đồ giá trị bảng xếp hạng.
/// </summary>
public class DailyCelestialChallengeLeaderboardValueMapEntry
{
    public int MatchType { get; set; }
    public int Race { get; set; }
    public int StatGroupType { get; set; }
}

/// <summary>
/// Giá trị bảng xếp hạng thử thách thiên thể.
/// </summary>
public class DailyCelestialChallengeLeaderboardValue
{
    public string Name { get; set; } = null!;
    public int ScoringType { get; set; }
    public bool VisibleToPublic { get; set; }
    public List<DailyCelestialChallengeLeaderboardValueMapEntry> MapEntries { get; set; } = new();
}

/// <summary>
/// Cập nhật điểm bảng xếp hạng.
/// </summary>
public class DailyCelestialChallengeLeaderboardPointUpdate
{
    public int WinPtsDefault { get; set; }
    public int LosePtsDefault { get; set; }
    public int MaxWinPts { get; set; }
    public int MinWinPts { get; set; }
    public int MaxLosePts { get; set; }
    public int MinLosePts { get; set; }
    public int BonusPtsPerDay { get; set; }
    public int MaxPtsAllowBonus { get; set; }
    public int MaxWinPtsWithBonus { get; set; }
    public int MaxBonusPtsDefault { get; set; }
    public int PlacementMatch { get; set; }
    public int WinDifferenceToApply { get; set; }
    public int LoseDifferenceToApply { get; set; }
}

/// <summary>
/// Bảng xếp hạng thử thách thiên thể hàng ngày.
/// </summary>
public class DailyCelestialChallengeLeaderboard
{
    public List<DailyCelestialChallengeLeaderboardValue> Values { get; set; } = new();
    public DailyCelestialChallengeLeaderboardPointUpdate PointUpdate { get; set; } = new();
}

/// <summary>
/// Thử thách thiên thể hàng ngày đầy đủ.
/// </summary>
public class DailyCelestialChallenge
{
    public DailyCelestialChallengePlus Plus { get; set; } = new();
    public DailyCelestialChallengeLeaderboard Leaderboard { get; set; } = new();
}

/// <summary>
/// Sự kiện cộng đồng.
/// Bao gồm sự kiện hàng ngày và hàng tháng.
/// </summary>
public class CommunityEvent
{
    public ulong Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public DateTime ExpiryTime { get; set; }
    public object? CustomData { get; set; }
    public int EventState { get; set; }
    public List<EventLeaderBoard> Leaderboards { get; set; } = new();

    /// <summary>Mã hóa sự kiện thành mảng đối tượng</summary>
    public object[] Encode(bool marshalCustomData)
    {
        var res = new List<object>
        {
            Name,
            new DateTimeOffset(Start).ToUnixTimeSeconds(),
            new DateTimeOffset(End).ToUnixTimeSeconds(),
            new DateTimeOffset(ExpiryTime).ToUnixTimeSeconds(),
            Id,
            EventState,
            null!
        };

        if (marshalCustomData && CustomData != null)
        {
            // res.Add(JsonSerializer.Serialize(CustomData));
        }
        else
        {
            res.Add(CustomData!);
        }

        return res.ToArray();
    }

    /// <summary>Mã hóa bảng xếp hạng</summary>
    public object[] EncodeLeaderboards()
    {
        var leaderboards = new List<object[]>();
        foreach (var lb in Leaderboards)
        {
            var isRankedInt = lb.IsRanked ? 1 : 0;
            leaderboards.Add(new object[] { Id, lb.Id, lb.Name, isRankedInt, lb.ScoringType });
        }
        return leaderboards.ToArray();
    }

    /// <summary>Mã hóa bản đồ bảng xếp hạng</summary>
    public object[] EncodeLeaderboardsMaps()
    {
        var maps = new List<object[]>();
        foreach (var lb in Leaderboards)
        {
            foreach (var map in lb.Maps)
            {
                maps.Add(new object[] { lb.Id, map.MatchtypeId, map.StatgroupType, map.CivilizationId, Id });
            }
        }
        return maps.ToArray();
    }
}

/// <summary>
/// Bảng xếp hạng sự kiện.
/// </summary>
public class EventLeaderBoard
{
    public ulong Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsRanked { get; set; }
    public int ScoringType { get; set; }
    public List<EventLeaderboardMap> Maps { get; set; } = new();
}

/// <summary>
/// Bản đồ trong bảng xếp hạng sự kiện.
/// </summary>
public class EventLeaderboardMap
{
    public int MatchtypeId { get; set; }
    public int StatgroupType { get; set; }
    public int CivilizationId { get; set; }
}
