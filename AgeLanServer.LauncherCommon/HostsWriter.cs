using System.Net;
using System.Text;

namespace AgeLanServer.LauncherCommon.Hosts;

/// <summary>
/// Trình ghi và cập nhật file hosts.
/// Chuyển đổi từ Go package: launcher-common/hosts/hosts.go, hosts_windows.go, hosts_unix.go
/// </summary>
public static class HostsWriter
{
    // Đường dẫn file hosts tùy theo hệ điều hành
    public static string HostsFilePath
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                string windir = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
                return Path.Combine(windir, "System32", "drivers", "etc", "hosts");
            }
            return "/etc/hosts";
        }
    }

    /// <summary>
    /// Lấy đường dẫn file hosts.bak (file backup).
    /// </summary>
    public static string BackupFilePath
    {
        get
        {
            string dir = Path.GetDirectoryName(HostsFilePath) ?? ".";
            return Path.Combine(dir, "hosts.bak");
        }
    }

    /// <summary>
    /// Lấy dòng xuống dòng mặc định tùy theo hệ điều hành.
    /// </summary>
    public static string DefaultLineEnding => OperatingSystem.IsWindows() ? "\r\n" : "\n";

    /// <summary>
    /// Thêm các ánh xạ host vào file hosts.
    /// Tạo backup file trước khi thay đổi.
    /// </summary>
    /// <param name="gameId">ID của game (dùng để xác định mappings)</param>
    /// <param name="hostFilePath">Đường dẫn file hosts (null = dùng file hệ thống)</param>
    /// <param name="lineEnding">Ký tự xuống dòng (null = dùng mặc định)</param>
    /// <param name="mappings">Ánh xạ host -> IP cần thêm</param>
    /// <returns>True nếu thành công</returns>
    public static async Task<bool> AddHostsAsync(
        string gameId,
        string? hostFilePath,
        string? lineEnding,
        HostMappings mappings)
    {
        bool systemHosts = string.IsNullOrEmpty(hostFilePath);
        if (systemHosts)
        {
            hostFilePath = HostsFilePath;
        }
        if (string.IsNullOrEmpty(lineEnding))
        {
            lineEnding = DefaultLineEnding;
        }

        // Lọc các mappings chưa tồn tại trong file
        var restLines = await GetMissingMappingsAsync(mappings, hostFilePath!);

        if (mappings.Count == 0)
        {
            return true;
        }

        // Mở file để ghi
        string tempFilePath = hostFilePath + ".tmp";

        try
        {
            // Đọc nội dung hiện tại
            string currentContent;
            using (var reader = new StreamReader(hostFilePath!, Encoding.UTF8, true))
            {
                currentContent = await reader.ReadToEndAsync();
            }

            // Tạo backup nếu là file hệ thống
            if (systemHosts)
            {
                string backupPath = BackupFilePath;
                // Chỉ tạo backup nếu chưa tồn tại
                if (!File.Exists(backupPath))
                {
                    File.Copy(hostFilePath!, backupPath, overwrite: false);
                }
            }

            // Ghi nội dung mới
            var sb = new StringBuilder();
            sb.Append(currentContent.TrimEnd('\r', '\n'));

            // Thêm các dòng còn lại
            foreach (var line in restLines)
            {
                sb.AppendLine();
                sb.Append(line.ToString());
            }

            // Thêm mappings mới
            sb.AppendLine();
            sb.Append(mappings.ToString(lineEnding));

            // Ghi file tạm rồi rename (atomic)
            using (var writer = new StreamWriter(tempFilePath, false, Encoding.UTF8))
            {
                await writer.WriteAsync(sb.ToString());
            }

            // Replace file gốc
            File.Replace(tempFilePath, hostFilePath!, null);

            return true;
        }
        catch
        {
            // Xóa file tạm nếu có lỗi
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
            throw;
        }
    }

    /// <summary>
    /// Lấy các mappings còn thiếu từ file hosts hiện tại.
    /// Trả về danh sách các dòng cần giữ lại sau khi loại bỏ các mappings đã được cung cấp.
    /// </summary>
    private static async Task<List<HostsLine>> GetMissingMappingsAsync(HostMappings mappings, string hostFilePath)
    {
        var restLines = new List<HostsLine>();

        if (!File.Exists(hostFilePath))
        {
            return restLines;
        }

        using var reader = new StreamReader(hostFilePath, Encoding.UTF8, true);
        string content = await reader.ReadToEndAsync();
        string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            bool ok = HostsParser.ParseLine(line, false, out bool overLimit, out HostsLine? parsedLine);

            // Nếu không parse được, thử comment hóa
            if (!ok)
            {
                if (HostsParser.CommentLine(line, out HostsLine? commentedLine))
                {
                    parsedLine = commentedLine;
                    ok = true;
                }
                else
                {
                    continue;
                }
            }

            // Dòng chỉ có comment thì giữ nguyên
            if (HostsParser.IsOnlyComments(parsedLine!))
            {
                restLines.Add(parsedLine!);
                continue;
            }

            IPAddress lineIp = parsedLine!.Ip!;
            var hosts = parsedLine.Hosts;
            var indexesToAvoid = new HashSet<int>();

            for (int i = 0; i < hosts.Count; i++)
            {
                if (mappings.TryGetValue(hosts[i], out IPAddress? ip) && ip.Equals(lineIp))
                {
                    mappings.Remove(hosts[i]);
                    indexesToAvoid.Add(i);
                }
            }

            var keptHosts = new List<string>();
            bool removedHosts = false;
            for (int i = 0; i < hosts.Count; i++)
            {
                if (indexesToAvoid.Contains(i))
                {
                    removedHosts = true;
                }
                else
                {
                    keptHosts.Add(hosts[i]);
                }
            }

            if (overLimit)
            {
                if (HostsParser.CommentLine(line, out HostsLine? commentedLine))
                {
                    parsedLine = commentedLine;
                    removedHosts = true;
                }
            }

            if (removedHosts)
            {
                HostsParser.CommentLine(line, out HostsLine? commentedLine);
                if (commentedLine != null)
                {
                    restLines.Add(HostsParser.WithOwnMarking(commentedLine));
                }
            }

            if (keptHosts.Count > 0)
            {
                var newLine = new HostsLine
                {
                    Ip = lineIp,
                    Hosts = keptHosts,
                    Comments = new List<string>()
                };
                if (removedHosts)
                {
                    newLine = HostsParser.WithOwnMarking(newLine);
                }
                restLines.Add(newLine);
            }
        }

        return restLines;
    }

    /// <summary>
    /// Lấy tất cả các dòng từ file hosts.
    /// </summary>
    public static async Task<List<HostsLine>> GetAllLinesAsync(string? hostFilePath = null)
    {
        string path = hostFilePath ?? HostsFilePath;
        var lines = new List<HostsLine>();

        if (!File.Exists(path))
        {
            return lines;
        }

        using var reader = new StreamReader(path, Encoding.UTF8, true);
        string content = await reader.ReadToEndAsync();
        string[] rawLines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var addedHosts = new HashSet<string>();

        foreach (string line in rawLines)
        {
            bool ok = HostsParser.ParseLine(line, true, out _, out HostsLine? parsedLine);
            if (!ok || parsedLine == null || HostsParser.IsOnlyComments(parsedLine))
            {
                continue;
            }

            var finalHosts = new List<string>();
            foreach (var host in parsedLine.Hosts)
            {
                if (!addedHosts.Contains(host))
                {
                    finalHosts.Add(host);
                    addedHosts.Add(host);
                }
            }

            if (finalHosts.Count > 0)
            {
                var newLine = new HostsLine
                {
                    Ip = parsedLine.Ip,
                    Hosts = finalHosts,
                    Comments = new List<string>()
                };
                lines.Add(HostsParser.WithOwnMarking(newLine));
            }
        }

        return lines;
    }
}
