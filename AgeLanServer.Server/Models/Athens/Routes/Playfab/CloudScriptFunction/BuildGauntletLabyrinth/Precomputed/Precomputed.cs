using AgeLanServer.Server.Models.Athens.Routes.Playfab;

namespace AgeLanServer.Server.Models.Athens.Routes.Playfab.CloudScriptFunction.BuildGauntletLabyrinth.Precomputed;

/// <summary>
/// Phước lành với tên hiệu ứng và độ hiếm.
/// </summary>
public record BlessingItem(string EffectName, int Rarity)
{
    public string GauntletItem() => PlayfabItems.ItemName("Gauntlet", EffectName, Rarity);
}

/// <summary>
/// Helper tính toán trước các giá trị cho Gauntlet.
/// </summary>
public static class PrecomputedHelper
{
    /// <summary>
    /// Tính danh sách phước lành được cho phép trong Gauntlet.
    /// Loại trừ các phước lành không được phép từ phần thưởng.
    /// </summary>
    public static Dictionary<int, List<string>> AllowedGauntletBlessings(Gauntlet gauntlet, List<Blessing> knownBlessings)
    {
        var disallowedKeys = BlessingKeys(gauntlet.Rewards.ExcludeFromRegularRewards);
        var allKeys = BlessingKeys(knownBlessings);
        var allowedKeys = allKeys.Except(disallowedKeys).ToHashSet();

        var blessingLevels = allowedKeys.Select(b => b.Rarity).Distinct().ToHashSet();
        var blessings = new Dictionary<int, List<string>>();

        foreach (var level in blessingLevels)
            blessings[level] = new List<string>();

        foreach (var blessing in allowedKeys)
            blessings[blessing.Rarity].Add(blessing.GauntletItem());

        return blessings;
    }

    private static HashSet<BlessingItem> BlessingKeys(List<Blessing> blessingsList)
    {
        var keys = new HashSet<BlessingItem>();
        foreach (var blessing in blessingsList)
        {
            // KnownRarities rỗng nghĩa là bao gồm tất cả độ hiếm
            if (blessing.KnownRarities.Count == 0)
            {
                for (int r = 0; r <= 5; r++)
                    keys.Add(new BlessingItem(blessing.EffectName, r));
            }
            else
            {
                foreach (var rarity in blessing.KnownRarities)
                    keys.Add(new BlessingItem(blessing.EffectName, rarity));
            }
        }
        return keys;
    }

    /// <summary>
    /// Ánh xạ tên nhóm -> chỉ số trong danh sách nhóm.
    /// </summary>
    public static Dictionary<string, int> PoolNamesToIndex(GauntletMissionPools missionPools)
    {
        var poolNamesToIndex = new Dictionary<string, int>();
        for (int index = 0; index < missionPools.Count; index++)
            poolNamesToIndex[missionPools[index].Name] = index;
        return poolNamesToIndex;
    }

    /// <summary>
    /// Ánh xạ độ khó -> chỉ số nhóm cho mỗi cột.
    /// </summary>
    public static Dictionary<string, List<int>> PoolsIndexByDifficulty(Gauntlet gauntlet, Dictionary<string, int> poolNamesToIndex)
    {
        var poolsIndexes = new Dictionary<string, List<int>>();

        foreach (var config in gauntlet.LabyrinthConfigs)
        {
            var poolDifficultyIndex = new int[config.ColumnConfigs.Count + 1];

            for (int column = 0; column < config.ColumnConfigs.Count; column++)
            {
                poolDifficultyIndex[column] = poolNamesToIndex[config.ColumnConfigs[column].MissionPool];
            }
            poolDifficultyIndex[config.ColumnConfigs.Count] = poolNamesToIndex[config.BossMissionPool];

            foreach (var difficulty in config.ForGauntletDifficulties)
                poolsIndexes[difficulty] = poolDifficultyIndex.ToList();
        }

        return poolsIndexes;
    }
}
