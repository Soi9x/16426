using System.Net;
using System.Net.Sockets;
using Tomlyn;
using Tomlyn.Model;

namespace AgeLanServer.Common;

/// <summary>
/// Cấu hình Battle Server được lưu dưới dạng file TOML.
/// Tương đương common/battleServerConfig/battleServerConfig.go trong bản Go gốc.
/// </summary>
public record BattleServerBaseConfig
{
    /// <summary>
    /// Khu vực (region) của battle server. Không được là UUID để tránh nhầm lẫn với LAN.
    /// </summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>
    /// Tên server (chỉ dùng cho AoE2).
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Địa chỉ IPv4 lắng nghe.
    /// </summary>
    public string IPv4 { get; init; } = string.Empty;

    /// <summary>
    /// Cổng battle server chính.
    /// </summary>
    public int BsPort { get; init; }

    /// <summary>
    /// Cổng WebSocket.
    /// </summary>
    public int WebSocketPort { get; init; }

    /// <summary>
    /// Cổng Out-of-Band (dùng cho tất cả game trừ AoE1).
    /// </summary>
    public int OutOfBandPort { get; init; }
}

/// <summary>
/// Cấu hình battle server đầy đủ bao gồm PID tiến trình.
/// </summary>
public record BattleServerConfig : BattleServerBaseConfig
{
    /// <summary>PID của tiến trình battle server.</summary>
    public uint PID { get; init; }

    /// <summary>Chỉ mục nội bộ (không ghi ra TOML).</summary>
    public int Index { get; init; }

    /// <summary>
    /// Kiểm tra xem cấu hình có hợp lệ không (PID còn sống, các cổng còn lắng nghe).
    /// </summary>
    public bool Validate()
    {
        if (string.IsNullOrEmpty(Region) || PID == 0 || string.IsNullOrEmpty(IPv4) ||
            BsPort == 0 || WebSocketPort == 0)
            return false;

        var proc = ProcessManager.FindProcessByPid((int)PID);
        if (proc == null || proc.HasExited)
        {
            proc?.Dispose();
            return false;
        }
        proc.Dispose();

        var candidateHosts = BuildValidationHosts(IPv4);
        if (candidateHosts.Count == 0)
            return false;

        var ports = new List<int> { BsPort, WebSocketPort };
        if (OutOfBandPort != 0)
            ports.Add(OutOfBandPort);

        foreach (var port in ports)
        {
            var reachable = false;

            foreach (var host in candidateHosts)
            {
                if (IsPortReachable(host, port))
                {
                    reachable = true;
                    break;
                }
            }

            if (!reachable)
                return false;
        }

        return true;
    }

    private static List<string> BuildValidationHosts(string configuredIp)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.Equals(configuredIp, "auto", StringComparison.OrdinalIgnoreCase) ||
            configuredIp == IPAddress.Any.ToString() ||
            configuredIp == "::")
        {
            hosts.Add(IPAddress.Loopback.ToString());

            try
            {
                foreach (var address in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                        continue;

                    hosts.Add(address.ToString());
                }
            }
            catch
            {
            }
        }
        else
        {
            hosts.Add(configuredIp);
        }

        return new List<string>(hosts);
    }

    private static bool IsPortReachable(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completed = connectTask.Wait(TimeSpan.FromMilliseconds(250));
            return completed && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Đường dẫn file TOML của cấu hình này.</summary>
    public string GetPath() => GetConfigFileName(Index);

    /// <summary>Tên file TOML cho chỉ mục đã cho.</summary>
    public static string GetConfigFileName(int index) => $"{index}.toml";
}

/// <summary>
/// Tiện ích quản lý file cấu hình Battle Server.
/// </summary>
public static class BattleServerConfigManager
{
    /// <summary>
    /// Lấy thư mục chứa cấu hình battle server cho game.
    /// </summary>
    public static string GetConfigFolder(string gameId)
    {
        return Path.Combine(Path.GetTempPath(), AppConstants.Name, "battle-servers", gameId);
    }

    /// <summary>
    /// Đọc tất cả cấu hình battle server cho game.
    /// </summary>
    /// <param name="gameId">ID game.</param>
    /// <param name="onlyValid">Chỉ trả về cấu hình hợp lệ.</param>
    public static List<BattleServerConfig> LoadConfigs(string gameId, bool onlyValid = false)
    {
        var configs = new List<BattleServerConfig>();
        var folder = GetConfigFolder(gameId);

        if (!Directory.Exists(folder))
            return configs;

        foreach (var file in Directory.EnumerateFiles(folder, "*.toml"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (!int.TryParse(fileName, out var index))
                continue;

            try
            {
                var content = File.ReadAllText(file);
                var model = Toml.ToModel(content);

                var config = new BattleServerConfig
                {
                    Index = index,
                    Region = GetString(model, "Region"),
                    Name = GetString(model, "Name"),
                    IPv4 = GetString(model, "IPv4"),
                    BsPort = GetInt(model, "BsPort"),
                    WebSocketPort = GetInt(model, "WebSocketPort"),
                    OutOfBandPort = GetInt(model, "OutOfBandPort"),
                    PID = (uint)GetInt(model, "PID")
                };

                if (!onlyValid || config.Validate())
                    configs.Add(config);
            }
            catch
            {
                // Bỏ qua file bị lỗi
            }
        }

        return configs;
    }

    /// <summary>
    /// Lưu cấu hình battle server ra file TOML.
    /// </summary>
    public static void SaveConfig(string gameId, BattleServerConfig config)
    {
        var folder = GetConfigFolder(gameId);
        Directory.CreateDirectory(folder);

        var configDict = new Dictionary<string, object>
        {
            ["Region"] = config.Region,
            ["Name"] = config.Name,
            ["IPv4"] = config.IPv4,
            ["BsPort"] = config.BsPort,
            ["WebSocketPort"] = config.WebSocketPort,
            ["OutOfBandPort"] = config.OutOfBandPort,
            ["PID"] = (int)config.PID
        };

        var toml = Toml.FromModel(configDict);
        var filePath = Path.Combine(folder, config.GetPath());
        File.WriteAllText(filePath, toml);
    }

    /// <summary>
    /// Xóa file cấu hình battle server.
    /// </summary>
    public static void RemoveConfig(string gameId, int index)
    {
        var folder = GetConfigFolder(gameId);
        var filePath = Path.Combine(folder, BattleServerConfig.GetConfigFileName(index));

        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    /// <summary>
    /// Xóa toàn bộ thư mục cấu hình battle server cho game.
    /// </summary>
    public static void RemoveAllConfigs(string gameId)
    {
        var folder = GetConfigFolder(gameId);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    private static string GetString(TomlTable table, string key)
    {
        return table.TryGetValue(key, out var val) ? val?.ToString() ?? string.Empty : string.Empty;
    }

    private static int GetInt(TomlTable table, string key)
    {
        if (table.TryGetValue(key, out var val) && val != null)
        {
            if (val is int i) return i;
            if (val is long l) return (int)l;
            if (int.TryParse(val.ToString(), out var parsed)) return parsed;
        }
        return 0;
    }
}
