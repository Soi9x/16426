using System.Globalization;
using System.Net;
using System.Text;

namespace AgeLanServer.LauncherCommon.Hosts;

/// <summary>
/// Trình phân tích cú pháp file hosts.
/// Chuyển đổi từ Go package: launcher-common/hosts/parser.go, parser_windows.go, parser_unix.go, host.go, line.go, common.go
/// </summary>
public static class HostsParser
{
    // Dấu comment trong file hosts
    public const string CommentMarker = "#";

    // Ký tự xuống dòng mặc định trên Windows
    public const string WindowsLineEnding = "\r\n";

    // Ký tự xuống dòng mặc định trên Unix
    public const string UnixLineEnding = "\n";

    // Số lượng host tối đa trên mỗi dòng (Windows)
    public const int MaxHostsPerLine = 9;

    // Tên dùng cho marking (đánh dấu dòng do chương trình tạo)
    private const string Marking = "ageLANServer";

    /// <summary>
    /// Phân tích một host string sang đối tượng Host.
    /// Sử dụng IDNA để chuyển đổi tên miền quốc tế.
    /// </summary>
    /// <param name="host">Tên host cần phân tích</param>
    /// <param name="parsed">Kết quả host đã phân tích</param>
    /// <returns>True nếu host hợp lệ (không phải IP)</returns>
    public static bool ParseHost(string host, out string? parsed)
    {
        parsed = null;
        try
        {
            // Chuyển đổi IDNA sang dạng Unicode
            var idn = new IdnMapping();
            string decoded;
            try
            {
                decoded = idn.GetUnicode(host);
            }
            catch
            {
                // Nếu không thể decode IDNA, dùng nguyên bản
                decoded = host;
            }

            // Nếu là IP thì không hợp lệ làm host
            if (IPAddress.TryParse(decoded, out _))
            {
                return false;
            }

            parsed = decoded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Phân tích một dòng trong file hosts.
    /// Trả về thông tin dòng bao gồm IP, danh sách hosts, và comments.
    /// </summary>
    /// <param name="line">Dòng cần phân tích</param>
    /// <param name="ignoreLimit">Bỏ qua giới hạn số host và ký tự trên dòng</param>
    /// <param name="parsedLine">Kết quả phân tích</param>
    /// <returns>True nếu dòng hợp lệ, overLimit nếu vượt quá giới hạn</returns>
    public static bool ParseLine(string line, bool ignoreLimit, out bool overLimit, out HostsLine? parsedLine)
    {
        overLimit = false;
        parsedLine = null;

        int maxChars = ignoreLimit ? line.Length : int.MaxValue; // Trên Windows không giới hạn thực sự
        int usableLength = line.Length > maxChars ? maxChars : line.Length;

        if (line.Length > maxChars)
        {
            overLimit = true;
        }

        // Tách phần comment
        string lineToParse = line.Substring(0, usableLength);
        string lineWithoutComment = lineToParse;
        List<string> comments = new List<string>();

        int commentIndex = lineToParse.IndexOf(CommentMarker, StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            lineWithoutComment = lineToParse.Substring(0, commentIndex);
            string commentPart = lineToParse.Substring(commentIndex + 1);
            comments = commentPart.Split(new[] { CommentMarker }, StringSplitOptions.None).ToList();
        }

        // Dòng trống hoặc chỉ có comment
        if (string.IsNullOrWhiteSpace(lineWithoutComment))
        {
            parsedLine = new HostsLine
            {
                Ip = null,
                Hosts = new List<string>(),
                Comments = comments
            };
            return true;
        }

        // Tách các trường bằng khoảng trắng
        string[] parts = lineWithoutComment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        // Phân tích IP
        if (!IPAddress.TryParse(parts[0], out IPAddress? ip))
        {
            return false;
        }

        // Phân tích danh sách hosts
        var hosts = new List<string>();
        int maxHosts = ignoreLimit ? parts.Length - 1 : MaxHostsPerLine;
        int hostCount = parts.Length - 1;

        if (hostCount > maxHosts)
        {
            overLimit = true;
        }

        int hostsToParse = Math.Min(hostCount, maxHosts);
        for (int i = 0; i < hostsToParse; i++)
        {
            if (ParseHost(parts[i + 1], out string? parsedHost))
            {
                hosts.Add(parsedHost!);
            }
        }

        if (hosts.Count == 0)
        {
            return false;
        }

        parsedLine = new HostsLine
        {
            Ip = ip,
            Hosts = hosts,
            Comments = comments
        };
        return true;
    }

    /// <summary>
    /// Kiểm tra xem dòng có chỉ chứa comment hay không.
    /// </summary>
    public static bool IsOnlyComments(HostsLine line)
    {
        return line.Ip == null && line.Hosts.Count == 0;
    }

    /// <summary>
    /// Kiểm tra xem dòng có đánh dấu của chương trình hay không.
    /// </summary>
    public static bool HasOwnMarking(HostsLine line)
    {
        if (line.Comments.Count < 1)
            return false;
        return line.Comments[line.Comments.Count - 1] == Marking;
    }

    /// <summary>
    /// Thêm đánh dấu của chương trình vào cuối danh sách comment.
    /// </summary>
    public static HostsLine WithOwnMarking(HostsLine line)
    {
        if (HasOwnMarking(line))
            return line;

        var newComments = new List<string>(line.Comments) { Marking };
        return new HostsLine
        {
            Ip = line.Ip,
            Hosts = new List<string>(line.Hosts),
            Comments = newComments
        };
    }

    /// <summary>
    /// Xóa đánh dấu của chương trình khỏi danh sách comment.
    /// </summary>
    public static HostsLine WithoutOwnMarking(HostsLine line)
    {
        if (!HasOwnMarking(line))
            return line;

        var newComments = new List<string>(line.Comments);
        newComments.RemoveAt(newComments.Count - 1);
        return new HostsLine
        {
            Ip = line.Ip,
            Hosts = new List<string>(line.Hosts),
            Comments = newComments
        };
    }

    /// <summary>
    /// Chuyển dòng thành dạng comment (thêm dấu # vào đầu).
    /// </summary>
    public static bool CommentLine(string line, out HostsLine? commentedLine)
    {
        commentedLine = null;
        string markedLine = CommentMarker + line;
        bool ok = ParseLine(markedLine, true, out _, out commentedLine);
        return ok;
    }

    /// <summary>
    /// Chuyển dòng đã comment thành dạng chưa comment (bỏ dấu # đầu tiên).
    /// </summary>
    public static string UncommentedLine(HostsLine line)
    {
        if (!IsOnlyComments(line))
        {
            return line.ToString();
        }

        var sb = new StringBuilder();
        if (line.Comments.Count > 0)
        {
            sb.Append(line.Comments[0]);
            for (int i = 1; i < line.Comments.Count; i++)
            {
                sb.Append(CommentMarker);
                sb.Append(line.Comments[i]);
            }
        }
        return sb.ToString();
    }
}

/// <summary>
/// Biểu diễn một dòng trong file hosts.
/// </summary>
public class HostsLine
{
    /// <summary>
    /// Địa chỉ IP của dòng.
    /// </summary>
    public IPAddress? Ip { get; set; }

    /// <summary>
    /// Danh sách các host trỏ đến IP.
    /// </summary>
    public List<string> Hosts { get; set; } = new List<string>();

    /// <summary>
    /// Danh sách các comment trong dòng.
    /// </summary>
    public List<string> Comments { get; set; } = new List<string>();

    /// <summary>
    /// Chuyển dòng thành chuỗi để ghi vào file hosts.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();

        if (Ip != null)
        {
            sb.Append(Ip.ToString());
            sb.Append('\t');

            // Chuyển host về dạng ASCII (Punycode)
            var idn = new IdnMapping();
            var hostStrings = new List<string>();
            foreach (var host in Hosts)
            {
                try
                {
                    hostStrings.Add(idn.GetAscii(host));
                }
                catch
                {
                    hostStrings.Add(host);
                }
            }

            sb.Append(string.Join(" ", hostStrings));
        }

        if (Comments.Count > 0)
        {
            sb.Append(HostsParser.CommentMarker);
            sb.Append(string.Join(HostsParser.CommentMarker, Comments));
        }

        return sb.ToString();
    }
}
