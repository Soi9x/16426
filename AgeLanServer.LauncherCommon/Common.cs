namespace AgeLanServer.LauncherCommon;

/// <summary>
/// Các hằng số và tiện ích chung dùng bởi các module khác.
/// Chuyển đổi từ Go package: common/common.go, common/game.go, common/domain.go
/// </summary>
public static class Common
{
    /// <summary>Tên của ứng dụng</summary>
    public const string Name = "ageLANServer";

    /// <summary>Organization của chứng chỉ</summary>
    public const string CertSubjectOrganization = "github.com/luskaner/" + Name;

    // Game IDs
    public const string GameAoE1 = "age1";
    public const string GameAoE2 = "age2";
    public const string GameAoE3 = "age3";
    public const string GameAoE4 = "age4";
    public const string GameAoM = "athens";

    // Domain constants
    private const string Tld = "com";
    private const string LinkMainDomainSuffix = "link";
    private const string ApiSubdomainSuffix = "-api";
    private const string SubDomain = "aoe" + ApiSubdomainSuffix;
    private const string RelicMainDomain = "relic" + LinkMainDomainSuffix;
    private const string RelicDomain = SubDomain + "." + RelicMainDomain + "." + Tld;
    private const string WorldsEdgeMainDomain = "worldsedge" + LinkMainDomainSuffix;
    private const string WorldsEdge = "." + WorldsEdgeMainDomain;
    private const string ApiWorldsEdge = ApiSubdomainSuffix + WorldsEdge + "." + Tld;
    private const string PlayFabDomain = "playfabapi";
    private const string AgeOfEmpires = "ageofempires";
    private const string ApiAgeOfEmpiresSubdomain = "api";
    private const string CdnAgeOfEmpiresSubdomain = "cdn";
    private const string ApiAgeOfEmpiresSuffix = "." + AgeOfEmpires + "." + Tld;
    private const string ApiAgeOfEmpires = ApiAgeOfEmpiresSubdomain + ApiAgeOfEmpiresSuffix;
    private const string Aoe4ApiAgeOfEmpires = ApiAgeOfEmpiresSubdomain + "-dr" + ApiAgeOfEmpiresSuffix;
    private const string CdnAgeOfEmpires = CdnAgeOfEmpiresSubdomain + "." + AgeOfEmpires + "." + Tld;
    private const string PlayFabSuffix = "." + PlayFabDomain + "." + Tld;
    private const string SubDomainAge2Prefix = "pb";
    private const string StdSubDomainReleasePart = "-live-release";
    private const string Aoe4SubDomainPrefix = "aoeliverelease";
    private const string Aoe4Marker = "dr";

    // Cache cho AllHosts
    private static readonly Dictionary<string, List<string>> _hostsCache = new();

    /// <summary>
    /// Lấy tất cả các host domains cần ánh xạ cho một game.
    /// Có cache để tránh tính toán lại.
    /// </summary>
    /// <param name="gameId">ID của game</param>
    /// <returns>Danh sách host domains</returns>
    public static List<string> AllHosts(string gameId)
    {
        if (_hostsCache.TryGetValue(gameId, out var cached))
        {
            return new List<string>(cached);
        }

        var domains = GameHostsDirect(gameId);

        switch (gameId)
        {
            case GameAoM:
                domains.Add("c15f9" + PlayFabSuffix);
                break;
            case GameAoE4:
                domains.Add("ed603" + PlayFabSuffix);
                break;
        }

        domains.Add(CdnAgeOfEmpires);
        domains.Add(ApiAgeOfEmpires);

        if (gameId == GameAoE4)
        {
            domains.Add(Aoe4ApiAgeOfEmpires);
        }

        _hostsCache[gameId] = new List<string>(domains);
        return new List<string>(domains);
    }

    /// <summary>
    // Sinh danh sách host domains trực tiếp cho game.
    /// </summary>
    private static List<string> GameHostsDirect(string gameId)
    {
        var domains = new List<string>();

        switch (gameId)
        {
            case GameAoE4:
                for (int i = 1; i <= 2; i++)
                {
                    domains.Add($"{Aoe4SubDomainPrefix}{i}{ApiWorldsEdge}");
                }
                // fallthrough
                goto case GameAoE1;

            case GameAoE1:
            case GameAoE2:
            case GameAoE3:
                domains = new List<string> { RelicDomain, SubDomain + WorldsEdge + "." + Tld };
                break;

            case GameAoM:
                domains = new List<string> { "athens-live" + ApiWorldsEdge };
                break;
        }

        domains.AddRange(GenerateDomains(gameId));
        return domains;
    }

    /// <summary>
    /// Sinh các domain phụ thuộc vào game ID và release number.
    /// </summary>
    private static List<string> GenerateDomains(string gameId)
    {
        var domains = new List<string>();

        string? prefix = null;
        int releaseMin = 0;
        string? subDomainReleasePart = null;

        switch (gameId)
        {
            case GameAoE2:
                prefix = SubDomainAge2Prefix;
                releaseMin = 2;
                subDomainReleasePart = StdSubDomainReleasePart;
                break;
            case GameAoE4:
                prefix = Aoe4Marker;
                releaseMin = 2;
                subDomainReleasePart = "-activerelease";
                break;
            case GameAoM:
                prefix = "andromeda";
                releaseMin = 15;
                subDomainReleasePart = StdSubDomainReleasePart;
                break;
            default:
                return domains;
        }

        string GenerateDomainName(int release)
        {
            return $"{prefix}{subDomainReleasePart}{release}{ApiWorldsEdge}";
        }

        for (int release = 1; release <= releaseMin; release++)
        {
            domains.Add(GenerateDomainName(release));
        }

        return domains;
    }
}
