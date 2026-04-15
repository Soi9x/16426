using AgeLanServer.Server.Models.Athens.User;
using AgeLanServer.Server.Models.Playfab;
using AgeLanServer.Server.Models;

namespace AgeLanServer.Server.Models.Athens.Routes.Playfab.CloudScriptFunction;

/// <summary>
/// Tham số trao phần thưởng nhiệm vụ.
/// </summary>
public class AwardMissionRewardsParameters
{
    public string CdnPath { get; set; } = null!;
    public string Difficulty { get; set; } = null!;
    public string MissionId { get; set; } = null!;
    public bool Won { get; set; }
}

/// <summary>
/// Vật phẩm đã thêm khi trao phần thưởng.
/// </summary>
public class AwardMissionRewardsResultItemsAdded
{
    public int Amount { get; set; }
    public string ItemFriendlyId { get; set; } = null!;
}

/// <summary>
/// Kết quả trao phần thưởng nhiệm vụ.
/// </summary>
public class AwardMissionRewardsResult
{
    public List<AwardMissionRewardsResultItemsAdded> ItemsAdded { get; set; } = new();
}

/// <summary>
/// Hàm Cloud Script: Trao phần thưởng nhiệm vụ.
/// Nếu người chơi thắng, thêm phần thưởng vào kho và cập nhật tiến trình.
/// </summary>
public class AwardMissionRewardsFunction : ISpecificCloudScriptFunction<AwardMissionRewardsParameters, AwardMissionRewardsResult>
{
    public string Name() => "AwardMissionRewards";

    public AwardMissionRewardsResult? RunTyped(IGame game, IUser user, AwardMissionRewardsParameters parameters)
    {
        var result = new AwardMissionRewardsResult { ItemsAdded = new List<AwardMissionRewardsResultItemsAdded>() };

        var elements = parameters.MissionId.Split('_');
        if (elements.Length > 1 && elements[1] == "Gauntlet")
        {
            var actualMissionId = string.Join('_', elements.Skip(2));
            var athensUser = (AthensUser)user;

            athensUser.PlayfabData.WithReadWrite(data =>
            {
                if (data?.Challenge.Progress is not { } progress)
                    throw new Exception("No challenge progress found");

                var progressValue = progress.Value;
                if (progressValue?.MissionBeingPlayedRightNow != parameters.MissionId)
                    throw new Exception("Challenge progress not correct");

                if (parameters.Won)
                {
                    progressValue.CompletedMissions.Add(parameters.MissionId);

                    if (data.Challenge.Labyrinth?.Value is { } labyrinth)
                    {
                        foreach (var mission in labyrinth.Missions)
                        {
                            if (mission.Id != actualMissionId) continue;

                            foreach (var reward in mission.Rewards)
                            {
                                var rewardElements = reward.ItemId.Split('_');
                                var rarity = int.Parse(rewardElements[3]);

                                progressValue.Inventory.Add(new ProgressInventory
                                {
                                    SeasonId = "Gauntlet",
                                    Item = rewardElements[2],
                                    Rarity = rarity
                                });

                                result.ItemsAdded.Add(new AwardMissionRewardsResultItemsAdded
                                {
                                    Amount = reward.Amount,
                                    ItemFriendlyId = reward.ItemId
                                });
                            }
                        }
                    }

                    progressValue.MissionBeingPlayedRightNow = "";
                    progress.UpdateLastUpdated();
                    data.DataVersion++;
                }

                return Task.CompletedTask;
            });
        }

        return result;
    }
}

/// <summary>
/// Factory tạo AwardMissionRewardsFunction.
/// </summary>
public static class AwardMissionRewardsFactory
{
    public static CloudScriptFunctionBase<AwardMissionRewardsParameters, AwardMissionRewardsResult> Create()
    {
        return new CloudScriptFunctionBase<AwardMissionRewardsParameters, AwardMissionRewardsResult>(
            new AwardMissionRewardsFunction());
    }
}
