using AgeLanServer.Server.Models.Playfab.Data;

namespace AgeLanServer.Server.Models.Athens.User;

/// <summary>
/// Tiến trình thẻ đục lỗ (punch card) - theo dõi tiến trình điểm danh hàng ngày.
/// </summary>
public class PunchCardProgress
{
    /// <summary>Số lỗ đã đục</summary>
    public byte Holes { get; set; }

    /// <summary>Ngày đục lỗ gần nhất</summary>
    public CustomTime DateOfMostRecentHolePunch { get; set; }
}

/// <summary>
/// Phần thưởng nhiệm vụ.
/// </summary>
public class MissionRewards
{
    public int Amount { get; set; }
    public string Scaling { get; set; } = null!;
    public string ItemId { get; set; } = null!;
}

/// <summary>
/// World Twist - hiệu ứng đặc biệt của thế giới trong game.
/// </summary>
public class WorldTwist
{
    public string Id { get; set; } = null!;
    public string ArenaEffectName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string OwnerIcon { get; set; } = null!;
    public string OwnerPortrait { get; set; } = null!;
    public string Visualization { get; set; } = null!;
}

/// <summary>
/// Đối thủ trong nhiệm vụ.
/// </summary>
public class Opponent
{
    public string Civ { get; set; } = null!;
    public int Team { get; set; }
    public string Personality { get; set; } = null!;
    public int DifficultyOffset { get; set; }
}

/// <summary>
/// Nhiệm vụ thử thách (Challenge Mission).
/// </summary>
public class ChallengeMission
{
    /// <summary>Chỉ số hàng (không được serialize)</summary>
    public int RowIndex { get; set; }

    public string Id { get; set; } = null!;
    public List<string> Predecessors { get; set; } = new();
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public string Visualization { get; set; } = null!;
    public string Map { get; set; } = null!;
    public string Size { get; set; } = null!;
    public string VictoryCondition { get; set; } = null!;
    public string GameType { get; set; } = null!;
    public string MapVisibility { get; set; } = null!;
    public string StartingResources { get; set; } = null!;
    public bool AllowTitans { get; set; }
    public List<WorldTwist> WorldTwists { get; set; } = new();
    public List<Opponent> Opponents { get; set; } = new();
    public List<Opponent> OpponentsFor2PlayerCoop { get; set; } = new();
    public List<MissionRewards> Rewards { get; set; } = new();
    public string MinimapImage { get; set; } = null!;
    public string MapPreviewImage { get; set; } = null!;
}

/// <summary>
/// Mê cung (Labyrinth) trong chế độ Gauntlet.
/// </summary>
public class Labyrinth
{
    public int Id { get; set; }
    public string Difficulty { get; set; } = null!;
    public List<ChallengeMission> Missions { get; set; } = new();
}

/// <summary>
/// Vật phẩm trong kho tiến trình.
/// </summary>
public class ProgressInventory
{
    public string SeasonId { get; set; } = null!;
    public string Item { get; set; } = null!;
    public int Rarity { get; set; }
}

/// <summary>
/// Tiến trình của người chơi trong Gauntlet.
/// </summary>
public class Progress
{
    public int Lives { get; set; }
    public List<string> CompletedMissions { get; set; } = new();
    public List<ProgressInventory> Inventory { get; set; } = new();
    public string MissionBeingPlayedRightNow { get; set; } = null!;
}

/// <summary>
/// Thử thách (Challenge) bao gồm mê cung và tiến trình.
/// </summary>
public class Challenge
{
    public BaseValue<Labyrinth>? Labyrinth { get; set; }
    public BaseValue<Progress>? Progress { get; set; }
}

/// <summary>
/// Nhiệm vụ cốt truyện.
/// </summary>
public class StoryMission
{
    public string State { get; set; } = null!;
    public string RewardsAwarded { get; set; } = null!;
    public uint CompletionCountEasy { get; set; }
    public uint CompletionCountMedium { get; set; }
    public uint CompletionCountHard { get; set; }
}

/// <summary>
/// Dữ liệu người dùng Athens (AoM).
/// Bao gồm thử thách, nhiệm vụ cốt truyện, và thẻ đục lỗ.
/// </summary>
public class Data
{
    public Challenge Challenge { get; set; } = new();
    public Dictionary<string, BaseValue<StoryMission>> StoryMissions { get; set; } = new();
    public BaseValue<PunchCardProgress> PunchCardProgress { get; set; } = new();
    public uint DataVersion { get; set; }
}

/// <summary>
/// Dữ liệu mặc định có thể nâng cấp cho dữ liệu PlayFab của Athens.
/// Khởi tạo với các nhiệm vụ cốt truyện đã hoàn thành.
/// </summary>
public class PlayfabUpgradableDefaultData : IUpgradableDefaultData<Data?>
{
    // Danh sách ID nhiệm vụ cốt truyện
    private static readonly string[] StoryMissions = new[]
    {
        "Mission_Season0_L0P0C1M1", "Mission_Season0_L0P0C2M1", "Mission_Season0_L0P0C3M1",
        "Mission_Season0_L0P0C4M1", "Mission_Season0_L0P0C5M1", "Mission_Season0_L0P0C6M1",
        "Mission_Season0_L0P0C6M2", "Mission_Season0_L0P0C7M3", "Mission_Season0_L0P0C7M2",
        "Mission_Season0_L0P0C7M1", "Mission_Season0_L0P0C8M2", "Mission_Season0_L0P0C8M1",
        "Mission_Season0_L0P0C9M3", "Mission_Season0_L0P0C9M1", "Mission_Season0_L0P0C9M2",
        "Mission_Season0_L0P0C10M1", "Mission_Season0_L0P1C1M1", "Mission_Season0_L0P1C2M1",
        "Mission_Season0_L0P1C2M2", "Mission_Season0_L0P1C3M1", "Mission_Season0_L0P1C3M2",
        "Mission_Season0_L0P1C3M3", "Mission_Season0_L0P1C4M2", "Mission_Season0_L0P1C4M1",
        "Mission_Season0_L0P1C5M1", "Mission_Season0_L0P1C6M2", "Mission_Season0_L0P1C6M1",
        "Mission_Season0_L0P1C6M3", "Mission_Season0_L0P1C7M1", "Mission_Season0_L0P1C7M2",
        "Mission_Season0_L0P1C8M2", "Mission_Season0_L0P1C8M3", "Mission_Season0_L0P1C9M2",
        "Mission_Season0_L0P1C9M1", "Mission_Season0_L0P1C10M1"
    };

    public Data? Default()
    {
        var lastUpdated = new CustomTime { Time = DateTime.UtcNow, Format = CustomTime.CustomTimeFormat };
        const string permission = "Private";

        var missions = new Dictionary<string, BaseValue<StoryMission>>();
        foreach (var missionId in StoryMissions)
        {
            missions[missionId] = new BaseValue<StoryMission>
            {
                LastUpdated = lastUpdated,
                Permission = permission,
                Value = new StoryMission
                {
                    State = "Completed",
                    RewardsAwarded = "Hard",
                    CompletionCountHard = 1
                }
            };
        }

        return new Data
        {
            StoryMissions = missions,
            PunchCardProgress = new BaseValue<PunchCardProgress>
            {
                LastUpdated = lastUpdated,
                Permission = permission,
                Value = new PunchCardProgress
                {
                    DateOfMostRecentHolePunch = new CustomTime
                    {
                        Time = new DateTime(2024, 5, 2, 3, 34, 0, DateTimeKind.Utc),
                        Format = CustomTime.CustomTimeFormat
                    }
                }
            },
            DataVersion = 0
        };
    }
}
