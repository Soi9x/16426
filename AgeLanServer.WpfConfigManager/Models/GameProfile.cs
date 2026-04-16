using AgeLanServer.Common;

namespace AgeLanServer.WpfConfigManager.Models;

/// <summary>
/// Hồ sơ cấu hình theo từng game. Mỗi game có thể có một profile riêng.
/// </summary>
public sealed class GameProfile
{
    public string ProfileName { get; set; } = string.Empty;
    public string GameId { get; set; } = GameIds.AgeOfEmpires4;

    public string GlobalTomlPath { get; set; } = string.Empty;
    public string GameTomlPath { get; set; } = string.Empty;

    public string ServerExecutablePath { get; set; } = string.Empty;
    public bool AutoStartServer { get; set; } = true;
    public bool AutoStopServer { get; set; } = true;
    public int AnnouncePort { get; set; } = AppConstants.AnnouncePort;

    public string ClientExecutable { get; set; } = "steam";
    public string ClientGamePath { get; set; } = string.Empty;
    public string ClientExtraArgs { get; set; } = string.Empty;

    public bool TrustCertificate { get; set; } = true;
    public bool MapHosts { get; set; } = true;
    public bool IsolateMetadata { get; set; } = true;
    public bool IsolateProfiles { get; set; } = true;
    public bool LogToFile { get; set; } = true;

    public bool CanAddHost { get; set; } = true;
    public string CanBroadcastBattleServer { get; set; } = "auto";
    public bool CoreLogEnabled { get; set; } = true;

    public string CertTrustInPcMode { get; set; } = "local";
    public bool CertTrustInGame { get; set; } = true;

    public string ServerStartMode { get; set; } = "auto";
    public string ServerStopMode { get; set; } = "auto";
    public bool SingleAutoSelect { get; set; }
    public bool StartWithoutConfirmation { get; set; }
    public string ServerExecutableToml { get; set; } = "auto";
    public string ServerExecutableArgsToml { get; set; } = "-e {Game} --id {Id}";
    public string ServerHost { get; set; } = "127.0.0.1";
    public string AnnouncePortsToml { get; set; } = "31978";
    public string AnnounceMulticastToml { get; set; } = "239.31.97.8";

    public string BattleServerManagerRunMode { get; set; } = "true";
    public string BattleServerManagerExecutable { get; set; } = "auto";
    public string BattleServerManagerArgs { get; set; } = "-e {Game} -r";
    public string BattleServerIp { get; set; } = "127.0.0.1";

    public string IsolateMetadataModeToml { get; set; } = "required";
    public string IsolateProfilesModeToml { get; set; } = "required";
    public string SetupCommandToml { get; set; } = string.Empty;
    public string RevertCommandToml { get; set; } = string.Empty;
    public string ClientExecutableToml { get; set; } = "auto";
    public string ClientExecutableArgsToml { get; set; } = string.Empty;
    public string ClientPathToml { get; set; } = "auto";
}
