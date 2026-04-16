using System.Globalization;
using System.Text.RegularExpressions;
using AgeLanServer.WpfConfigManager.Models;
using Tomlyn;
using Tomlyn.Model;

namespace AgeLanServer.WpfConfigManager.Services;

/// <summary>
/// Dịch vụ nạp/ghi toàn bộ các trường cấu hình TOML cho profile đang chọn.
/// </summary>
public sealed class TomlConfigService
{
    public void LoadProfileFromToml(GameProfile profile)
    {
        LoadGlobalConfig(profile);
        LoadGameConfig(profile);
    }

    public void SaveProfileToToml(GameProfile profile)
    {
        SaveGlobalConfig(profile);
        SaveGameConfig(profile);
    }

    private static void LoadGlobalConfig(GameProfile profile)
    {
        if (!File.Exists(profile.GlobalTomlPath))
            return;

        var model = Toml.ToModel(File.ReadAllText(profile.GlobalTomlPath));
        if (model is not TomlTable root)
            return;

        var configTable = GetTable(root, "Config");
        profile.CanAddHost = GetBool(configTable, "CanAddHost", profile.CanAddHost);
        profile.CanBroadcastBattleServer = GetString(configTable, "CanBroadcastBattleServer", profile.CanBroadcastBattleServer);
        profile.CoreLogEnabled = GetBool(configTable, "Log", profile.CoreLogEnabled);

        var certTable = GetTable(configTable, "Certificate");
        profile.CertTrustInPcMode = GetString(certTable, "CanTrustInPc", profile.CertTrustInPcMode);
        profile.CertTrustInGame = GetBool(certTable, "CanTrustInGame", profile.CertTrustInGame);

        var serverTable = GetTable(root, "Server");
        profile.ServerStartMode = GetString(serverTable, "Start", profile.ServerStartMode);
        profile.ServerStopMode = GetString(serverTable, "Stop", profile.ServerStopMode);
        profile.SingleAutoSelect = GetBool(serverTable, "SingleAutoSelect", profile.SingleAutoSelect);
        profile.StartWithoutConfirmation = GetBool(serverTable, "StartWithoutConfirmation", profile.StartWithoutConfirmation);
        profile.ServerExecutableToml = GetString(serverTable, "Executable", profile.ServerExecutableToml);
        profile.ServerExecutableArgsToml = string.Join(' ', ReadStringArray(serverTable.TryGetValue("ExecutableArgs", out var argsRaw) ? argsRaw : null));
        profile.ServerHost = GetString(serverTable, "Host", profile.ServerHost);
        profile.AnnouncePortsToml = string.Join(",", ReadIntArray(serverTable.TryGetValue("AnnouncePorts", out var portsRaw) ? portsRaw : null).Select(i => i.ToString(CultureInfo.InvariantCulture)));
        profile.AnnounceMulticastToml = string.Join(",", ReadStringArray(serverTable.TryGetValue("AnnounceMulticastGroups", out var groupsRaw) ? groupsRaw : null));

        var bsmTable = GetTable(serverTable, "BattleServerManager");
        profile.BattleServerManagerRunMode = GetString(bsmTable, "Run", profile.BattleServerManagerRunMode);
        profile.BattleServerManagerExecutable = GetString(bsmTable, "Executable", profile.BattleServerManagerExecutable);
        profile.BattleServerManagerArgs = string.Join(' ', ReadStringArray(bsmTable.TryGetValue("ExecutableArgs", out var bsmArgsRaw) ? bsmArgsRaw : null));

        var gamesTable = GetTable(root, "Games");
        var gameTable = GetTable(gamesTable, profile.GameId);
        if (gameTable.TryGetValue("BattleServer", out var battleServerRaw) && battleServerRaw is TomlTableArray battleServerArray && battleServerArray.Count > 0)
        {
            var first = battleServerArray[0];
            profile.BattleServerIp = GetString(first, "Ip", profile.BattleServerIp);
        }

        profile.AutoStartServer = profile.ServerStartMode is "true" or "auto";
        profile.AutoStopServer = profile.ServerStopMode is "true" or "auto";
        profile.MapHosts = profile.CanAddHost;
        profile.TrustCertificate = profile.CertTrustInPcMode is not "false";
        profile.LogToFile = profile.CoreLogEnabled;
    }

    private static void SaveGlobalConfig(GameProfile profile)
    {
        var root = new TomlTable();

        var configTable = new TomlTable
        {
            ["CanAddHost"] = profile.CanAddHost,
            ["CanBroadcastBattleServer"] = profile.CanBroadcastBattleServer,
            ["Log"] = profile.CoreLogEnabled
        };

        configTable["Certificate"] = new TomlTable
        {
            ["CanTrustInPc"] = profile.CertTrustInPcMode,
            ["CanTrustInGame"] = profile.CertTrustInGame
        };

        root["Config"] = configTable;

        var serverTable = new TomlTable
        {
            ["Start"] = profile.ServerStartMode,
            ["Stop"] = profile.ServerStopMode,
            ["SingleAutoSelect"] = profile.SingleAutoSelect,
            ["StartWithoutConfirmation"] = profile.StartWithoutConfirmation,
            ["Executable"] = profile.ServerExecutableToml,
            ["ExecutableArgs"] = ToTomlArray(TokenizeCommandLine(profile.ServerExecutableArgsToml)),
            ["Host"] = profile.ServerHost,
            ["AnnouncePorts"] = ToTomlArray(ParseIntegerList(profile.AnnouncePortsToml)),
            ["AnnounceMulticastGroups"] = ToTomlArray(ParseStringList(profile.AnnounceMulticastToml))
        };

        serverTable["BattleServerManager"] = new TomlTable
        {
            ["Run"] = profile.BattleServerManagerRunMode,
            ["Executable"] = profile.BattleServerManagerExecutable,
            ["ExecutableArgs"] = ToTomlArray(TokenizeCommandLine(profile.BattleServerManagerArgs))
        };

        root["Server"] = serverTable;

        var gamesTable = new TomlTable();
        var gameTable = new TomlTable();
        var battleServers = new TomlTableArray
        {
            new TomlTable
            {
                ["Ip"] = profile.BattleServerIp
            }
        };

        gameTable["BattleServer"] = battleServers;
        gamesTable[profile.GameId] = gameTable;
        root["Games"] = gamesTable;

        EnsureParentFolder(profile.GlobalTomlPath);
        File.WriteAllText(profile.GlobalTomlPath, Toml.FromModel(root));
    }

    private static void LoadGameConfig(GameProfile profile)
    {
        if (!File.Exists(profile.GameTomlPath))
            return;

        var model = Toml.ToModel(File.ReadAllText(profile.GameTomlPath));
        if (model is not TomlTable root)
            return;

        var configTable = GetTable(root, "Config");
        profile.IsolateMetadataModeToml = GetString(configTable, "IsolateMetadata", profile.IsolateMetadataModeToml);
        profile.IsolateProfilesModeToml = GetString(configTable, "IsolateProfiles", profile.IsolateProfilesModeToml);
        profile.SetupCommandToml = string.Join(' ', ReadStringArray(configTable.TryGetValue("SetupCommand", out var setupRaw) ? setupRaw : null));
        profile.RevertCommandToml = string.Join(' ', ReadStringArray(configTable.TryGetValue("RevertCommand", out var revertRaw) ? revertRaw : null));

        var clientTable = GetTable(root, "Client");
        profile.ClientExecutableToml = GetString(clientTable, "Executable", profile.ClientExecutableToml);
        profile.ClientExecutableArgsToml = string.Join(' ', ReadStringArray(clientTable.TryGetValue("ExecutableArgs", out var cliArgsRaw) ? cliArgsRaw : null));
        profile.ClientPathToml = GetString(clientTable, "Path", profile.ClientPathToml);

        profile.IsolateMetadata = profile.IsolateMetadataModeToml is "required" or "true";
        profile.IsolateProfiles = profile.IsolateProfilesModeToml is "required" or "true";

        if (!string.IsNullOrWhiteSpace(profile.ClientExecutableToml))
            profile.ClientExecutable = profile.ClientExecutableToml;

        if (!string.IsNullOrWhiteSpace(profile.ClientPathToml) && !string.Equals(profile.ClientPathToml, "auto", StringComparison.OrdinalIgnoreCase))
            profile.ClientGamePath = profile.ClientPathToml;

        profile.ClientExtraArgs = profile.ClientExecutableArgsToml;
    }

    private static void SaveGameConfig(GameProfile profile)
    {
        var root = new TomlTable();

        root["Config"] = new TomlTable
        {
            ["IsolateMetadata"] = profile.IsolateMetadataModeToml,
            ["IsolateProfiles"] = profile.IsolateProfilesModeToml,
            ["SetupCommand"] = ToTomlArray(TokenizeCommandLine(profile.SetupCommandToml)),
            ["RevertCommand"] = ToTomlArray(TokenizeCommandLine(profile.RevertCommandToml))
        };

        root["Client"] = new TomlTable
        {
            ["Executable"] = profile.ClientExecutableToml,
            ["ExecutableArgs"] = ToTomlArray(TokenizeCommandLine(profile.ClientExecutableArgsToml)),
            ["Path"] = profile.ClientPathToml
        };

        EnsureParentFolder(profile.GameTomlPath);
        File.WriteAllText(profile.GameTomlPath, Toml.FromModel(root));
    }

    private static TomlTable GetTable(TomlTable root, string key)
    {
        if (root.TryGetValue(key, out var value) && value is TomlTable table)
            return table;

        return new TomlTable();
    }

    private static string GetString(TomlTable table, string key, string fallback)
    {
        if (!table.TryGetValue(key, out var value) || value is null)
            return fallback;

        return value.ToString() ?? fallback;
    }

    private static bool GetBool(TomlTable table, string key, bool fallback)
    {
        if (!table.TryGetValue(key, out var value) || value is null)
            return fallback;

        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => fallback
        };
    }

    private static List<string> ReadStringArray(object? value)
    {
        return value switch
        {
            null => new List<string>(),
            TomlArray array => array.Select(v => v?.ToString() ?? string.Empty).Where(v => !string.IsNullOrWhiteSpace(v)).ToList(),
            string str => TokenizeCommandLine(str),
            _ => new List<string>()
        };
    }

    private static List<int> ReadIntArray(object? value)
    {
        if (value is not TomlArray array)
            return new List<int>();

        var result = new List<int>();
        foreach (var item in array)
        {
            switch (item)
            {
                case int i:
                    result.Add(i);
                    break;
                case long l:
                    result.Add((int)l);
                    break;
                case string s when int.TryParse(s, out var parsed):
                    result.Add(parsed);
                    break;
            }
        }

        return result;
    }

    private static TomlArray ToTomlArray(IEnumerable<string> values)
    {
        var array = new TomlArray();
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                array.Add(value.Trim());
        }

        return array;
    }

    private static TomlArray ToTomlArray(IEnumerable<int> values)
    {
        var array = new TomlArray();
        foreach (var value in values)
            array.Add(value);

        return array;
    }

    private static List<string> ParseStringList(string raw)
    {
        return raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
    }

    private static List<int> ParseIntegerList(string raw)
    {
        return raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Select(v => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : (int?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
    }

    private static List<string> TokenizeCommandLine(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        var matches = Regex.Matches(raw, "\"[^\"]*\"|[^\\s]+");
        return matches
            .Select(m => m.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim('"'))
            .ToList();
    }

    private static void EnsureParentFolder(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);
    }
}
