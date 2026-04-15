namespace AgeLanServer.Common;

/// <summary>
/// Quản lý tên miền và host mappings cho từng game.
/// Tương đương common/domain.go trong bản Go gốc.
/// </summary>
public static class GameDomains
{
    // === Các hằng số tên miền ===
    public const string Tld = "com";
    private const string LinkMainDomainSuffix = "link";
    private const string ApiSubdomainSuffix = "-api";
    private const string SubDomain = "aoe" + ApiSubdomainSuffix;
    private const string RelicMainDomain = "relic" + LinkMainDomainSuffix;
    private const string RelicDomain = SubDomain + "." + RelicMainDomain + "." + Tld;
    private const string WorldsEdgeMainDomain = "worldsedge" + LinkMainDomainSuffix;
    private const string WorldsEdge = "." + WorldsEdgeMainDomain;
    private const string ApiWorldsEdge = ApiSubdomainSuffix + WorldsEdge + "." + Tld;
    public const string PlayFabDomain = "playfabapi.com";
    private const string AgeOfEmpires = "ageofempires";
    public const string ApiAgeOfEmpires = "api." + AgeOfEmpires + "." + Tld;
    public const string CdnAgeOfEmpires = "cdn." + AgeOfEmpires + "." + Tld;
    public const string Aoe4ApiAgeOfEmpires = "api-dr." + AgeOfEmpires + "." + Tld;

    /// <summary>
    /// Danh sách tên miền cần thiết cho chứng chỉ SSL self-signed.
    /// </summary>
    public static string[] SelfSignedCertDomains { get; } =
    {
        RelicDomain,
        "*" + WorldsEdge + "." + Tld,
        "*." + AgeOfEmpires + "." + Tld
    };

    /// <summary>
    /// Tên miền wildcard PlayFab.
    /// </summary>
    public static string[] CertDomains() =>
        new[] { "*." + PlayFabDomain }.Concat(SelfSignedCertDomains).ToArray();

    /// <summary>
    /// Kiểm tra game có dùng chứng chỉ self-signed không (không phải AoE4 hoặc AoM).
    /// </summary>
    public static bool UsesSelfSignedCert(string gameId) =>
        gameId != GameIds.AgeOfEmpires4 && gameId != GameIds.AgeOfMythology;

    /// <summary>
    /// Lấy danh sách tên miền game cần ánh xạ tới LAN server.
    /// </summary>
    public static string[] GetGameHosts(string gameId)
    {
        var domains = new List<string>();

        switch (gameId)
        {
            case GameIds.AgeOfEmpires4:
                for (int i = 1; i <= 2; i++)
                {
                    domains.Add($"aoeliverelease{i}{ApiWorldsEdge}");
                }
                goto case GameIds.AgeOfEmpires1;

            case GameIds.AgeOfEmpires1:
            case GameIds.AgeOfEmpires2:
            case GameIds.AgeOfEmpires3:
                domains.Add(RelicDomain);
                domains.Add(SubDomain + WorldsEdge + "." + Tld);
                break;

            case GameIds.AgeOfMythology:
                domains.Add("athens-live" + ApiWorldsEdge);
                break;
        }

        // Thêm các tên miền sinh tự động
        domains.AddRange(GenerateDomains(gameId));

        return domains.ToArray();
    }

    /// <summary>
    /// Lấy tất cả tên miền (bao gồm PlayFab, CDN, API) cho game.
    /// </summary>
    public static string[] GetAllHosts(string gameId)
    {
        var domains = new List<string>(GetGameHosts(gameId));

        switch (gameId)
        {
            case GameIds.AgeOfMythology:
                domains.Add("c15f9." + PlayFabDomain);
                break;
            case GameIds.AgeOfEmpires4:
                domains.Add("ed603." + PlayFabDomain);
                break;
        }

        domains.Add(CdnAgeOfEmpires);
        domains.Add(ApiAgeOfEmpires);

        if (gameId == GameIds.AgeOfEmpires4)
            domains.Add(Aoe4ApiAgeOfEmpires);

        return domains.ToArray();
    }

    /// <summary>
    /// Sinh các tên miền bổ sung dựa trên game ID (release versions).
    /// </summary>
    private static string[] GenerateDomains(string gameId)
    {
        var domains = new List<string>();

        string prefix;
        int releaseMax;
        string subDomainReleasePart;

        switch (gameId)
        {
            case GameIds.AgeOfEmpires2:
                prefix = "pb";
                releaseMax = 2;
                subDomainReleasePart = "-live-release";
                break;

            case GameIds.AgeOfEmpires4:
                prefix = "dr";
                releaseMax = 2;
                subDomainReleasePart = "-activerelease";
                break;

            case GameIds.AgeOfMythology:
                prefix = "andromeda";
                releaseMax = 15;
                subDomainReleasePart = "-live-release";
                break;

            default:
                return Array.Empty<string>();
        }

        string GenerateDomainName(int release) =>
            $"{prefix}{subDomainReleasePart}{release}{ApiWorldsEdge}";

        for (int release = 1; release <= releaseMax; release++)
        {
            domains.Add(GenerateDomainName(release));
        }

        return domains.ToArray();
    }
}
