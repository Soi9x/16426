using System.Net;
using System.Text;

namespace AgeLanServer.LauncherCommon.Hosts;

/// <summary>
/// Ánh xạ host tới IP. Một IP có thể được ánh xạ đến nhiều host.
/// Chuyển đổi từ Go package: launcher-common/hosts/mappings.go
/// </summary>
public class HostMappings : Dictionary<string, IPAddress>
{
    /// <summary>
    /// Thiết lập ánh xạ cho một host.
    /// </summary>
    /// <param name="host">Tên host</param>
    /// <param name="ip">Địa chỉ IP</param>
    public void SetMapping(string host, IPAddress ip)
    {
        this[host] = ip;
    }

    /// <summary>
    /// Lấy địa chỉ IP cho một host.
    /// </summary>
    /// <param name="host">Tên host cần tra cứu</param>
    /// <param name="ip">Địa chỉ IP trả về</param>
    /// <returns>True nếu tìm thấy ánh xạ</returns>
    public bool TryGetMapping(string host, out IPAddress? ip)
    {
        return TryGetValue(host, out ip);
    }

    /// <summary>
    /// Xóa ánh xạ cho một host.
    /// </summary>
    /// <param name="host">Tên host cần xóa</param>
    public void RemoveMapping(string host)
    {
        Remove(host);
    }

    /// <summary>
    /// Chuyển mappings thành chuỗi để ghi vào file hosts.
    /// Mỗi mapping tạo thành một dòng với marking của chương trình.
    /// </summary>
    /// <param name="lineEnding">Ký tự xuống dòng</param>
    /// <returns>Chuỗi biểu diễn các mappings</returns>
    public string ToString(string lineEnding)
    {
        var sb = new StringBuilder();

        foreach (var kvp in this)
        {
            var line = new HostsLine
            {
                Ip = kvp.Value,
                Hosts = new List<string> { kvp.Key },
                Comments = new List<string>()
            };
            // Thêm marking của chương trình
            line = HostsParser.WithOwnMarking(line);

            sb.Append(lineEnding);
            sb.Append(line.ToString());
        }

        sb.Append(lineEnding);
        return sb.ToString();
    }

    /// <summary>
    /// Tạo mappings từ gameId và địa chỉ IP cấu hình.
    /// </summary>
    /// <param name="gameId">ID của game</param>
    /// <param name="mapIp">Địa chỉ IP cần ánh xạ (null = không ánh xạ)</param>
    /// <returns>HostMappings chứa các ánh xạ</returns>
    public static HostMappings CreateMappings(string gameId, IPAddress? mapIp)
    {
        var mappings = new HostMappings();

        if (mapIp != null)
        {
            // Lấy tất cả hosts cho gameId
            var hosts = Common.AllHosts(gameId);
            foreach (var host in hosts)
            {
                mappings.SetMapping(host, mapIp);
            }
        }

        return mappings;
    }
}
