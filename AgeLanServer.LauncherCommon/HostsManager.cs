using System.Text;

namespace AgeLanServer.LauncherCommon;

/// <summary>
/// Quản lý file hosts hệ thống: đọc, ghi, backup, thêm/xóa ánh xạ IP-host.
/// Tương đương launcher-common/hosts/ trong bản Go gốc.
/// </summary>
public static class HostsManager
{
    private const string CommentMarker = "#";
    private const string OwnMarking = "ageLANServer";
    private const string BackupExtension = ".bak";

    /// <summary>Ký tự xuống dòng phù hợp với hệ điều hành.</summary>
    public static string LineEnding => OperatingSystem.IsWindows() ? "\r\n" : "\n";

    /// <summary>Đường dẫn tới file hosts hệ thống.</summary>
    public static string HostsFilePath
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var windir = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
                return Path.Combine(windir, "System32", "drivers", "etc", "hosts");
            }
            return "/etc/hosts";
        }
    }

    /// <summary>
    /// Một dòng trong file hosts.
    /// </summary>
    public record HostsLine
    {
        public string Ip { get; init; } = string.Empty;
        public List<string> Hosts { get; init; } = new();
        public List<string> Comments { get; init; } = new();
        public bool IsCommentOnly => string.IsNullOrEmpty(Ip);
        public bool IsEmpty => IsCommentOnly && Comments.Count == 0;

        /// <summary>Kiểm tra dòng có chứa đánh dấu của ageLANServer không.</summary>
        public bool HasOwnMarking() => Comments.Any(c => c.Contains(OwnMarking));

        /// <summary>Chuyển dòng thành chuỗi comment.</summary>
        public string ToComment()
        {
            var sb = new StringBuilder();
            sb.Append(CommentMarker);
            if (!string.IsNullOrEmpty(Ip))
            {
                sb.Append(' ').Append(Ip);
                foreach (var host in Hosts)
                    sb.Append(' ').Append(host);
            }
            foreach (var comment in Comments)
                sb.Append(' ').Append(CommentMarker).Append(' ').Append(comment);
            return sb.ToString();
        }

        /// <summary>Chuyển dòng thành chuỗi bình thường, có đánh dấu.</summary>
        public string WithOwnMarking()
        {
            var sb = new StringBuilder();
            sb.Append(Ip);
            foreach (var host in Hosts)
                sb.Append('\t').Append(host);
            sb.Append('\t').Append(CommentMarker).Append(' ').Append(OwnMarking);
            foreach (var comment in Comments.Where(c => !c.Contains(OwnMarking)))
                sb.Append(' ').Append(CommentMarker).Append(' ').Append(comment);
            return sb.ToString();
        }

        public override string ToString()
        {
            if (IsCommentOnly)
            {
                return Comments.Count > 0
                    ? string.Join(LineEnding, Comments.Select(c => $"{CommentMarker} {c}"))
                    : string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append(Ip);
            foreach (var host in Hosts)
                sb.Append('\t').Append(host);
            foreach (var comment in Comments)
                sb.Append(' ').Append(CommentMarker).Append(' ').Append(comment);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Đọc tất cả dòng từ file hosts.
    /// </summary>
    public static List<HostsLine> ReadAllLines(string? filePath = null)
    {
        filePath ??= HostsFilePath;
        var lines = new List<HostsLine>();

        if (!File.Exists(filePath))
            return lines;

        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var rawLines = content.Replace("\r\n", "\n").Split('\n');

        foreach (var rawLine in rawLines)
        {
            var trimmed = rawLine.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var line = ParseLine(trimmed);
            if (line != null)
                lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// Phân tích một dòng trong file hosts.
    /// </summary>
    private static HostsLine? ParseLine(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        // Dòng comment
        if (trimmed.StartsWith(CommentMarker))
        {
            return new HostsLine
            {
                Comments = { trimmed.Substring(CommentMarker.Length).Trim() }
            };
        }

        // Dòng ánh xạ IP-host
        var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            // Có thể là dòng không hợp lệ - xem như comment
            return new HostsLine { Comments = { trimmed } };
        }

        var ip = parts[0];
        var hosts = new List<string>();
        var comments = new List<string>();

        for (int i = 1; i < parts.Length; i++)
        {
            if (parts[i] == CommentMarker)
            {
                // Phần còn lại là comment
                comments.Add(string.Join(" ", parts.Skip(i + 1)));
                break;
            }
            hosts.Add(parts[i]);
        }

        return new HostsLine
        {
            Ip = ip,
            Hosts = hosts,
            Comments = comments
        };
    }

    /// <summary>
    /// Tạo file backup của file hosts hiện tại.
    /// </summary>
    public static void CreateBackup(string? filePath = null)
    {
        filePath ??= HostsFilePath;
        var backupPath = filePath + BackupExtension;

        if (File.Exists(filePath) && !File.Exists(backupPath))
        {
            File.Copy(filePath, backupPath);
        }
    }

    /// <summary>
    /// Khôi phục file hosts từ bản backup.
    /// </summary>
    public static void RestoreFromBackup(string? filePath = null)
    {
        filePath ??= HostsFilePath;
        var backupPath = filePath + BackupExtension;

        if (File.Exists(backupPath))
        {
            File.Copy(backupPath, filePath, overwrite: true);
        }
    }

    /// <summary>
    /// Thêm ánh xạ IP-host vào file hosts.
    /// Tự động backup trước khi sửa đổi.
    /// Mỗi domain được ghi trên 1 dòng riêng để dễ đọc và revert.
    /// </summary>
    public static void AddHostMappings(string ip, IEnumerable<string> hosts, string? filePath = null)
    {
        filePath ??= HostsFilePath;
        CreateBackup(filePath);

        var existingLines = ReadAllLines(filePath);
        var hostsSet = new HashSet<string>(hosts);

        // Xóa các dòng cũ có cùng IP và chứa host cần thêm
        var linesToRemove = existingLines
            .Where(l => l.Ip == ip && l.Hosts.Any(h => hostsSet.Contains(h)))
            .ToList();

        foreach (var line in linesToRemove)
        {
            existingLines.Remove(line);
        }

        // Thêm mỗi domain trên 1 dòng riêng
        foreach (var host in hosts)
        {
            var newLine = new HostsLine
            {
                Ip = ip,
                Hosts = new List<string> { host }
            };
            existingLines.Add(newLine);
        }

        WriteAllLines(existingLines, filePath);
    }

    /// <summary>
    /// Xóa các ánh xạ IP-host có đánh dấu của ageLANServer.
    /// </summary>
    public static void RemoveOwnMappings(string? filePath = null)
    {
        filePath ??= HostsFilePath;

        var existingLines = ReadAllLines(filePath);
        var linesToKeep = existingLines.Where(l => !l.HasOwnMarking()).ToList();

        // Nếu không có gì thay đổi, không cần ghi lại
        if (linesToKeep.Count == existingLines.Count)
            return;

        WriteAllLines(linesToKeep, filePath);
    }

    /// <summary>
    /// Kiểm tra xem IP đã được ánh xạ tới các host chưa.
    /// </summary>
    public static bool HasMapping(string ip, IEnumerable<string> hosts, string? filePath = null)
    {
        filePath ??= HostsFilePath;
        var lines = ReadAllLines(filePath);
        var hostSet = new HashSet<string>(hosts);

        foreach (var line in lines)
        {
            if (line.Ip == ip && line.Hosts.Any(h => hostSet.Contains(h)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Ghi tất cả dòng trở lại file hosts.
    /// </summary>
    private static void WriteAllLines(List<HostsLine> lines, string filePath)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            var lineStr = line.ToString();
            if (!string.IsNullOrEmpty(lineStr))
                sb.AppendLine(lineStr);
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Xóa bộ nhớ đệm DNS (Windows: ipconfig /flushdns).
    /// </summary>
    public static void FlushDnsCache()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/flushdns",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit();
            }
            catch
            {
                // Bỏ qua lỗi
            }
        }
        // Trên Linux, không cần flush DNS (handled by systemd-resolved or dnsmasq)
    }
}
