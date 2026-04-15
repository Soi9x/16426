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

    /// <summary>Tập hợp tất cả game được hỗ trợ.</summary>
    public static readonly IReadOnlySet<string> SupportedGames = new HashSet<string>
    {
        AgeOfEmpires1,
        AgeOfEmpires2,
        AgeOfEmpires3,
        AgeOfEmpires4,
        AgeOfMythology
    };

    /// <summary>Kiểm tra game ID có hợp lệ không.</summary>
    public static bool IsValid(string gameId) => SupportedGames.Contains(gameId);
}
