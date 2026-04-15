namespace AgeLanServer.Launcher.Internal;

/// <summary>
/// Cáº¥u hÃ¬nh executable (Ä‘Æ°á»ng dáº«n vÃ  tham sá»‘).
/// TÆ°Æ¡ng Ä‘Æ°Æ¡ng struct Executable trong config.go
/// </summary>
public record ExecutableConfig
{
    public string Path { get; set; } = string.Empty;
    public List<string> Args { get; set; } = new();
}

/// <summary>
/// Cáº¥u hÃ¬nh certificate cá»§a launcher.
/// TÆ°Æ¡ng Ä‘Æ°Æ¡ng ConfigCertificate trong config.go
/// </summary>
public record LauncherCertificateConfig
{
    /// <summary>
    /// CÃ³ thá»ƒ tin cáº­y certificate trÃªn mÃ¡y khÃ´ng: "false", "user" (Windows), "local" (admin).
    /// </summary>
    public string CanTrustInPc { get; set; } = "local";

    /// <summary>
    /// CÃ³ thá»ƒ tin cáº­y certificate trong game khÃ´ng.
    /// </summary>
    public bool CanTrustInGame { get; set; } = true;
}

/// <summary>
/// Cáº¥u hÃ¬nh chÃ­nh cá»§a launcher.
/// TÆ°Æ¡ng Ä‘Æ°Æ¡ng Config trong config.go
/// </summary>
public record LauncherConfig
{
    /// <summary>
    /// CÃ³ thá»ƒ thÃªm host vÃ o file hosts khÃ´ng.
    /// </summary>
    public bool CanAddHost { get; set; } = true;

    /// <summary>
    /// CÃ³ thá»ƒ broadcast BattleServer trong LAN khÃ´ng: "auto" hoáº·c "false".
    /// </summary>
    public string CanBroadcastBattleServer { get; set; } = "auto";

    /// <summary>
    /// Báº­t ghi log ra file.
    /// </summary>
    public bool Log { get; set; }

    /// <summary>
    /// CÃ´ láº­p thÆ° má»¥c metadata: "true", "false", "required".
    /// </summary>
    public string IsolateMetadata { get; set; } = "required";

    /// <summary>
    /// CÃ´ láº­p thÆ° má»¥c profile ngÆ°á»i dÃ¹ng: "true", "false", "required".
    /// </summary>
    public string IsolateProfiles { get; set; } = "required";

    /// <summary>
    /// Lá»‡nh thiáº¿t láº­p ban Ä‘áº§u (executable + args).
    /// </summary>
    public List<string> SetupCommand { get; set; } = new();

    /// <summary>
    /// Lá»‡nh khÃ´i phá»¥c sau khi thoÃ¡t.
    /// </summary>
    public List<string> RevertCommand { get; set; } = new();

    /// <summary>
    /// Cáº¥u hÃ¬nh certificate.
    /// </summary>
    public LauncherCertificateConfig Certificate { get; set; } = new();
}

/// <summary>
/// Cáº¥u hÃ¬nh BattleServerManager.
/// TÆ°Æ¡ng Ä‘Æ°Æ¡ng BattleServerManager trong config.go
/// </summary>
public record BattleServerManagerConfig
{
    public ExecutableConfig Executable { get; set; } = new();
    /// <summary>
    /// Cháº¿ Ä‘á»™ cháº¡y: "true", "false", "required".
    /// </summary>
    public string Run { get; set; } = "true";
}

/// <summary>
/// Cáº¥u hÃ¬nh server.
/// TÆ°Æ¡ng Ä‘Æ°Æ¡ng Server trong config.go
/// </summary>
public record ServerConfig
{
    public ExecutableConfig Executable { get; set; } = new();
    /// <summary>
    /// Cháº¿ Ä‘á»™ khá»Ÿi Ä‘á»™ng: "auto", "true", "false".
    /// </summary>
    public string Start { get; set; } = "auto";

    /// <summary>
    /// Hostname cá»§a server Ä‘á»ƒ káº¿t ná»‘i.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Cháº¿ Ä‘á»™ dá»«ng: "auto", "true", "false".
    /// </summary>
    public string Stop { get; set; } = "auto";

    /// <summary>
    /// Tá»± Ä‘á»™ng chá»n khi chá»‰ tÃ¬m tháº¥y má»™t server.
    /// </summary>
    public bool SingleAutoSelect { get; set; }

    /// <summary>
    /// Bá» qua xÃ¡c nháº­n khi khá»Ÿi Ä‘á»™ng server.
    /// </summary>
    public bool StartWithoutConfirmation { get; set; }

    /// <summary>
    /// Danh sÃ¡ch cá»•ng announce.
    /// </summary>
    public List<int> AnnouncePorts { get; set; } = new() { 7778 };

    /// <summary>
    /// Danh sÃ¡ch nhÃ³m multicast Ä‘á»ƒ announce.
    /// </summary>
    public List<string> AnnounceMulticastGroups { get; set; } = new() { "239.255.0.1" };

    /// <summary>
    /// Cáº¥u hÃ¬nh BattleServerManager.
    /// </summary>
    public BattleServerManagerConfig BattleServerManager { get; set; } = new();
}

/// <summary>
/// Cáº¥u hÃ¬nh client.
/// TÆ°Æ¡ng Ä‘Æ°Æ¡ng Client trong config.go
/// </summary>
public record ClientConfig
{
    public ExecutableConfig Executable { get; set; } = new();
    /// <summary>
    /// ÄÆ°á»ng dáº«n Ä‘áº¿n thÆ° má»¥c game.
    /// </summary>
    public string Path { get; set; } = "auto";
}

/// <summary>
/// Cáº¥u hÃ¬nh tá»•ng thá»ƒ cá»§a toÃ n bá»™ launcher.
/// TÆ°Æ¡ng Ä‘Æ°Æ¡ng Configuration trong config.go
/// </summary>
public record FullConfiguration
{
    public LauncherConfig Config { get; set; } = new();
    public ServerConfig Server { get; set; } = new();
    public ClientConfig Client { get; set; } = new();
}

