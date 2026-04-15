using AgeLanServer.Server.Models.Athens.Routes.Playfab;
using AgeLanServer.Server.Models.Athens.User;

namespace AgeLanServer.Server.Models.Athens.Routes.Playfab.CloudScriptFunction.BuildGauntletLabyrinth;

/// <summary>
/// Bộ sinh mê cung Gauntlet.
/// Tạo cấu trúc mê cung 10 cột x 6 hàng với các kết nối ngẫu nhiên.
/// </summary>
public static class GauntletLabyrinthGenerator
{
    private const int Columns = 10;
    private const int Rows = 6;
    private static readonly int[] Window = { -1, 0, +1 };
    private static readonly int WindowLen = Window.Length;

    /// <summary>Phạm vi sinh số ngẫu nhiên</summary>
    public record Range(int Min, int Max)
    {
        public int RandomValue() => Random.Shared.Next(Min, Max + 1);
    }

    /// <summary>Số node mỗi cột</summary>
    private static readonly Range[] NodesPerColumn =
    {
        new(2, 3),   // Cột đầu
        new(2, 6), new(2, 6), new(2, 6), new(2, 6),
        new(2, 6), new(2, 6), new(2, 6), new(2, 6), // Cột giữa
        new(1, 1)   // Cột Boss
    };

    /// <summary>Mức phước lành theo cột</summary>
    private static readonly int[] BlessingLevelPerColumn = { 0, 0, 1, 1, 2, 2, 3, 3, 4, 5 };

    /// <summary>Sinh số lượng node cho mỗi cột</summary>
    public static int[] GenerateNumberOfNodes()
    {
        var nodes = new int[Columns];
        nodes[0] = NodesPerColumn[0].RandomValue();

        for (int col = 0; col < Columns - 2; col++)
        {
            int index = col + 1;
            int previousNodes = nodes[index - 1];
            double maxNodes = Math.Min(Rows, previousNodes * 2 + 1);
            int minNodes = (previousNodes + WindowLen - 1) / WindowLen;
            var finalRng = new Range(
                Math.Max(NodesPerColumn[index].Min, minNodes),
                Math.Min(NodesPerColumn[index].Max, (int)maxNodes));
            nodes[index] = finalRng.RandomValue();
        }

        nodes[Columns - 1] = NodesPerColumn[Columns - 1].RandomValue();
        return nodes;
    }

    /// <summary>Các vị trí có thể kết nối từ một vị trí</summary>
    private static List<int> ConnectablePositions(int position)
    {
        var positions = new List<int>();
        foreach (var pos in Window)
        {
            var computedPos = position + pos;
            if (computedPos > -1 && computedPos < Rows)
                positions.Add(computedPos);
        }
        return positions;
    }

    /// <summary>Xáo trộn mảng</summary>
    private static void Shuffle<T>(T[] slice)
    {
        Random.Shared.Shuffle(slice);
    }

    /// <summary>Sinh node cho mỗi hàng</summary>
    public static int[][] GenerateNodeRows(int[] numberOfNodes)
    {
        var nodes = new int[Columns][];
        var positions = Enumerable.Range(0, Rows).ToArray();
        Shuffle(positions);
        nodes[0] = positions[..numberOfNodes[0]];

        for (int col = 0; col < Columns - 1; col++)
        {
            int finalCol = col + 1;
            nodes[finalCol] = ComputePositions(nodes[col], numberOfNodes[finalCol]);
        }

        nodes[Columns - 1] = new[] { 0 }; // Boss node
        return nodes;
    }

    private static int[] ComputePositions(int[] previousNodes, int numberOfNodes)
    {
        // Đơn giản hóa: chọn ngẫu nhiên các vị trí
        var positions = Enumerable.Range(0, Rows).ToArray();
        Shuffle(positions);
        return positions[..Math.Min(numberOfNodes, Rows)];
    }

    /// <summary>Xáo trộn phước lành ngẫu nhiên</summary>
    public static Dictionary<int, List<string>> RandomizedBlessings(Dictionary<int, List<string>> blessings)
    {
        var randomBlessings = new Dictionary<int, List<string>>();
        foreach (var (k, v) in blessings)
        {
            var newSlice = v.ToArray();
            Shuffle(newSlice);
            randomBlessings[k] = newSlice.ToList();
        }
        return randomBlessings;
    }

    /// <summary>
    /// Sinh nhiệm vụ cho mê cung.
    /// Gán nhiệm vụ từ nhóm, phước lành, và vị trí cho mỗi node.
    /// </summary>
    public static List<ChallengeMission>[] GenerateMissions(
        int[][] nodeRows,
        List<int> poolsIndexes,
        GauntletMissionPools missionsPools,
        Dictionary<int, List<string>> blessings)
    {
        var missionColumns = new List<ChallengeMission>[nodeRows.Length];
        var blessingIndexes = new Dictionary<int, int>();
        var nodePosToIndex = new Dictionary<int, int>[nodeRows.Length];

        for (int col = 0; col < nodeRows.Length; col++)
        {
            var nodes = nodeRows[col];
            nodePosToIndex[col] = new Dictionary<int, int>();
            var missionsColumn = new List<ChallengeMission>(nodes.Length);
            var pool = missionsPools[poolsIndexes[col]];
            var allMissions = pool.Missions;
            var indexesChosen = Enumerable.Range(0, Math.Min(nodes.Length, allMissions.Count)).ToArray();
            Shuffle(indexesChosen);
            var blessingLevel = BlessingLevelPerColumn[col];
            var allBlessings = blessings[blessingLevel];

            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                var predecessors = new List<string>();
                int x;
                int y = 0;
                string visualization;

                if (col == Columns - 1)
                {
                    // Boss column
                    if (col > 0 && missionColumns[col - 1] != null)
                    {
                        foreach (var pred in missionColumns[col - 1])
                            predecessors.Add(pred.Id);
                    }
                    x = 1600;
                    visualization = "UberBoss";
                }
                else
                {
                    if (col > 0 && missionColumns[col - 1] != null)
                    {
                        foreach (var prevMission in missionColumns[col - 1])
                        {
                            var connectable = ConnectablePositions(prevMission.RowIndex);
                            if (connectable.Contains(node))
                                predecessors.Add(prevMission.Id);
                        }
                    }
                    y = -350 + 140 * node;
                    x = 80 + 160 * col;
                    visualization = "Regular";
                }

                if (!blessingIndexes.ContainsKey(blessingLevel))
                    blessingIndexes[blessingLevel] = 0;

                var unusedBlessing = allBlessings[blessingIndexes[blessingLevel]];
                var mission = allMissions[indexesChosen[i]];

                missionsColumn.Add(new ChallengeMission
                {
                    RowIndex = node,
                    Id = $"{col}_{node}_{pool.Name}/{mission.Id}",
                    Predecessors = predecessors,
                    PositionX = x,
                    PositionY = y,
                    Visualization = visualization,
                    Size = mission.Size,
                    VictoryCondition = "Standard",
                    GameType = "Standard",
                    MapVisibility = mission.MapVisibility,
                    StartingResources = mission.StartingResources,
                    WorldTwists = new List<WorldTwist>(),
                    Opponents = new List<Opponent>(),
                    OpponentsFor2PlayerCoop = new List<Opponent>(),
                    Rewards = new List<MissionRewards>
                    {
                        new() { Amount = 1, Scaling = "None", ItemId = unusedBlessing }
                    }
                });

                nodePosToIndex[col][node] = i;
                blessingIndexes[blessingLevel]++;
            }

            missionColumns[col] = missionsColumn;
        }

        return missionColumns;
    }
}
