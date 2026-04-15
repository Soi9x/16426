using AgeLanServer.Server.Models.Athens.User;
using AgeLanServer.Server.Models.Playfab;
using AgeLanServer.Server.Models.Playfab.Data;
using AgeLanServer.Server.Models;

namespace AgeLanServer.Server.Models.Athens.Routes.Playfab.CloudScriptFunction.BuildGauntletLabyrinth;

/// <summary>
/// Kết quả xây dựng mê cung Gauntlet.
/// </summary>
public class BuildGauntletLabyrinthResult
{
    public Labyrinth? Labyrinth { get; set; }
    public Progress? Progress { get; set; }
}

/// <summary>
/// Tham số xây dựng mê cung Gauntlet.
/// </summary>
public class BuildGauntletLabyrinthParameters
{
    public string CdnPath { get; set; } = null!;
    public string GauntletDifficulty { get; set; } = null!;
}

/// <summary>
/// Hàm Cloud Script: Xây dựng mê cung Gauntlet.
/// Tạo mê cung ngẫu nhiên với các nhiệm vụ, phước lành, và tiến trình.
/// </summary>
public class BuildGauntletLabyrinthFunction : ISpecificCloudScriptFunction<BuildGauntletLabyrinthParameters, BuildGauntletLabyrinthResult>
{
    public string Name() => "BuildGauntletLabyrinth";

    public BuildGauntletLabyrinthResult? RunTyped(IGame game, IUser user, BuildGauntletLabyrinthParameters parameters)
    {
        var athensGame = (Athens.Game)game;
        var athensUser = (AthensUser)user;

        var nodes = GauntletLabyrinthGenerator.GenerateNumberOfNodes();
        var nodeRows = GauntletLabyrinthGenerator.GenerateNodeRows(nodes);
        var blessings = GauntletLabyrinthGenerator.RandomizedBlessings(athensGame.AllowedBlessings);
        var poolIndexes = athensGame.GauntletPoolIndexByDifficulty[parameters.GauntletDifficulty];
        var missionColumns = GauntletLabyrinthGenerator.GenerateMissions(nodeRows, poolIndexes, athensGame.GauntletMissionPools!, blessings);

        var missions = new List<ChallengeMission>();
        foreach (var column in missionColumns)
            missions.AddRange(column);

        BuildGauntletLabyrinthResult? result = null;

        athensUser.PlayfabData.WithReadWrite(data =>
        {
            int id = data?.Challenge.Labyrinth?.Value?.Id + 1 ?? 1;

            if (data != null)
            {
                data.Challenge = new Challenge
                {
                    Labyrinth = BaseValueFactory.NewPrivateBaseValue(new Labyrinth
                    {
                        Id = id,
                        Difficulty = parameters.GauntletDifficulty,
                        Missions = missions
                    }),
                    Progress = BaseValueFactory.NewPrivateBaseValue(new Progress
                    {
                        Lives = 3,
                        CompletedMissions = new List<string>(),
                        Inventory = new List<ProgressInventory>()
                    })
                };
                data.DataVersion++;

                result = new BuildGauntletLabyrinthResult
                {
                    Labyrinth = data.Challenge.Labyrinth.Value,
                    Progress = data.Challenge.Progress.Value
                };
            }

            return Task.CompletedTask;
        });

        return result;
    }
}

/// <summary>
/// Factory tạo BuildGauntletLabyrinthFunction.
/// </summary>
public static class BuildGauntletLabyrinthFactory
{
    public static CloudScriptFunctionBase<BuildGauntletLabyrinthParameters, BuildGauntletLabyrinthResult> Create()
    {
        return new CloudScriptFunctionBase<BuildGauntletLabyrinthParameters, BuildGauntletLabyrinthResult>(
            new BuildGauntletLabyrinthFunction());
    }
}
