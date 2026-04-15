using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace AgeLanServer.BattleServerManager;

/// <summary>
/// Cấu hình cơ sở của Battle Server.
/// Chỉ dùng cho AoE2; các game khác không dùng Name.
/// </summary>
public sealed class BaseConfig
{
    /// <summary>
    /// Khu vực máy chủ. Không được là UUID vì có thể nhầm với LAN UUID.
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Tên máy chủ. Chỉ dùng cho AoE2.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Địa chỉ IPv4 của máy chủ.
    /// </summary>
    public string IPv4 { get; set; } = string.Empty;

    /// <summary>
    /// Cổng Battle Server chính.
    /// </summary>
    public int BsPort { get; set; }

    /// <summary>
    /// Cổng WebSocket.
    /// </summary>
    public int WebSocketPort { get; set; }

    /// <summary>
    /// Cổng Out-of-Band. Dùng cho tất cả game trừ AoE1.
    /// </summary>
    public int OutOfBandPort { get; set; }
}

/// <summary>
/// Cấu hình đầy đủ của một Battle Server, bao gồm PID.
/// Tương ứng với battleServerConfig.Config trong Go.
/// </summary>
public sealed class Config
{
    public string Region
    {
        get => _baseConfig.Region;
        set => _baseConfig.Region = value;
    }

    public string Name
    {
        get => _baseConfig.Name;
        set => _baseConfig.Name = value;
    }

    public string IPv4
    {
        get => _baseConfig.IPv4;
        set => _baseConfig.IPv4 = value;
    }

    public int BsPort
    {
        get => _baseConfig.BsPort;
        set => _baseConfig.BsPort = value;
    }

    public int WebSocketPort
    {
        get => _baseConfig.WebSocketPort;
        set => _baseConfig.WebSocketPort = value;
    }

    public int OutOfBandPort
    {
        get => _baseConfig.OutOfBandPort;
        set => _baseConfig.OutOfBandPort = value;
    }

    /// <summary>
    /// PID của tiến trình Battle Server.
    /// </summary>
    public uint PID { get; set; }

    private readonly BaseConfig _baseConfig = new();
    private int _index;

    /// <summary>
    /// Tạo đường dẫn file cấu hình dựa trên index.
    /// </summary>
    public string Path() => $"{_index}.toml";

    /// <summary>
    /// Thiết lập index (không tuần tự hóa ra TOML).
    /// </summary>
    public void SetIndex(int index) => _index = index;

    /// <summary>
    /// Kiểm tra cấu hình có hợp lệ không:
    /// - Các trường bắt buộc phải có giá trị
    /// - Tiến trình PID phải đang chạy
    /// - (Bỏ qua kiểm tra kết nối port vì BattleServer đã bind port khi khởi động)
    /// </summary>
    public bool Validate()
    {
        if (string.IsNullOrEmpty(Region) ||
            PID == 0 ||
            string.IsNullOrEmpty(IPv4) ||
            BsPort == 0 ||
            WebSocketPort == 0)
        {
            return false;
        }

        // Kiểm tra tiến trình có đang chạy không
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById((int)PID);
            if (proc is null || proc.HasExited)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        // Đã bỏ qua kiểm tra kết nối port vì:
        // 1. BattleServer đã in "running on ports" tức là bind thành công
        // 2. IPv4 có thể là "auto" hoặc "0.0.0.0" không thể kết nối TCP
        // 3. Firewall có thể chặn kết nối local

        return true;
    }
}

/// <summary>
/// Tiện ích quản lý file cấu hình Battle Server.
/// </summary>
public static class BattleServerConfigLib
{
    /// <summary>
    /// Tên ứng dụng chung, dùng để tạo đường dẫn thư mục cấu hình.
    /// </summary>
    public const string Name = "ageLANServer";

    /// <summary>
    /// Trả về đường dẫn thư mục chứa cấu hình của game.
    /// Nằm trong thư mục Temp của hệ điều hành.
    /// </summary>
    public static string Folder(string gameId)
    {
        return Path.Combine(
            Path.GetTempPath(),
            Name,
            "battle-servers",
            gameId);
    }

    /// <summary>
    /// Đọc tất cả file cấu hình của game.
    /// Nếu onlyValid = true, chỉ trả về các cấu hình hợp lệ.
    /// </summary>
    public static List<Config> Configs(string gameId, bool onlyValid)
    {
        var configs = new List<Config>();
        var folder = Folder(gameId);

        if (!Directory.Exists(folder))
        {
            return configs;
        }

        foreach (var entry in Directory.EnumerateFiles(folder, "*.toml"))
        {
            var fileName = Path.GetFileNameWithoutExtension(entry);
            if (!int.TryParse(fileName, out int index))
            {
                continue;
            }

            try
            {
                var data = File.ReadAllText(entry, Encoding.UTF8);
                var config = ParseTomlConfig(data);
                config.SetIndex(index);

                if (!onlyValid || config.Validate())
                {
                    configs.Add(config);
                }
            }
            catch
            {
                // Bỏ qua file lỗi
            }
        }

        return configs;
    }

    /// <summary>
    /// Phân tích chuỗi TOML thành đối tượng Config.
    /// </summary>
    private static Config ParseTomlConfig(string tomlText)
    {
        var config = new Config();
        var lines = tomlText.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex < 0) continue;

            var key = trimmed.Substring(0, eqIndex).Trim();
            var value = trimmed.Substring(eqIndex + 1).Trim();

            switch (key)
            {
                case "Region":
                    config.Region = UnquoteToml(value);
                    break;
                case "Name":
                    config.Name = UnquoteToml(value);
                    break;
                case "IPv4":
                    config.IPv4 = UnquoteToml(value);
                    break;
                case "BsPort":
                    if (int.TryParse(value, out var bsPort)) config.BsPort = bsPort;
                    break;
                case "WebSocketPort":
                    if (int.TryParse(value, out var wsPort)) config.WebSocketPort = wsPort;
                    break;
                case "OutOfBandPort":
                    if (int.TryParse(value, out var oobPort)) config.OutOfBandPort = oobPort;
                    break;
                case "PID":
                    if (uint.TryParse(value, out var pid)) config.PID = pid;
                    break;
            }
        }

        return config;
    }

    /// <summary>
    /// Loại bỏ dấu ngoặc kép khỏi giá trị TOML.
    /// </summary>
    private static string UnquoteToml(string value)
    {
        if (value.StartsWith('"') && value.EndsWith('"'))
        {
            return value.Substring(1, value.Length - 2)
                       .Replace("\\n", "\n")
                       .Replace("\\r", "\r")
                       .Replace("\\t", "\t")
                       .Replace("\\\"", "\"")
                       .Replace("\\\\", "\\");
        }
        return value;
    }
}
