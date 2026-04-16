namespace AgeLanServer.Common;

/// <summary>
/// Hằng số định danh các game được hỗ trợ.
/// </summary>
public static class GameIds
{
    /// <summary>Age of Empires: Definitive Edition.</summary>
    public const string AgeOfEmpires1 = "age1";

    /// <summary>Age of Empires II: Definitive Edition.</summary>
    public const string AgeOfEmpires2 = "age2";

    /// <summary>Age of Empires III: Definitive Edition.</summary>
    public const string AgeOfEmpires3 = "age3";

    /// <summary>Age of Empires IV: Anniversary Edition.</summary>
    public const string AgeOfEmpires4 = "age4";

    /// <summary>Age of Mythology: Retold.</summary>
    public const string AgeOfMythology = "athens";

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        [AgeOfEmpires1] = AgeOfEmpires1,
        ["aoe1"] = AgeOfEmpires1,
        [AgeOfEmpires2] = AgeOfEmpires2,
        ["aoe2"] = AgeOfEmpires2,
        ["aoe2de"] = AgeOfEmpires2,
        [AgeOfEmpires3] = AgeOfEmpires3,
        ["aoe3"] = AgeOfEmpires3,
        ["aoe3de"] = AgeOfEmpires3,
        [AgeOfEmpires4] = AgeOfEmpires4,
        ["aoe4"] = AgeOfEmpires4,
        [AgeOfMythology] = AgeOfMythology,
        ["aom"] = AgeOfMythology,
    };

    /// <summary>Tập hợp tất cả game được hỗ trợ.</summary>
    public static readonly IReadOnlySet<string> SupportedGames = new HashSet<string>
    {
        AgeOfEmpires1,
        AgeOfEmpires2,
        AgeOfEmpires3,
        AgeOfEmpires4,
        AgeOfMythology
    };

    /// <summary>Chuẩn hóa game ID về dạng canonical (age*, athens).</summary>
    public static string? Normalize(string? gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        var key = gameId.Trim();
        return Aliases.TryGetValue(key, out var normalized) ? normalized : null;
    }

    /// <summary>Kiểm tra game ID có hợp lệ không (hỗ trợ cả alias cũ).</summary>
    public static bool IsValid(string gameId) => Normalize(gameId) is not null;
}
