using System.Text.RegularExpressions;

namespace AgeLanServer.Common;

/// <summary>
/// Phân tích và xử lý tham số dòng lệnh với hỗ trợ thay thế biến trong chuỗi.
/// Tương đương common/parse.go trong bản Go gốc.
/// </summary>
public static class CommandArgsParser
{
    // Regex khớp biến môi trường Windows dạng %VAR_NAME%
    private static readonly Regex WindowsEnvVarRegex = new(@"%(\w+)%", RegexOptions.Compiled);

    /// <summary>
    /// Phân tích chuỗi tham số, thay thế các biến dạng {KEY} bằng giá trị thực tế.
    /// Hỗ trợ chuyển đổi biến Windows %VAR% sang $VAR cho Linux.
    /// </summary>
    /// <param name="args">Mảng tham số gốc.</param>
    /// <param name="values">Bản đồ biến → giá trị.</param>
    /// <param name="separateFields">Nếu true, tách chuỗi thành các trường riêng biệt.</param>
    public static string[] ParseCommandArgs(string[] args, Dictionary<string, string> values, bool separateFields = false)
    {
        var cmdArgs = string.Join(" ", args);

        // Thay thế các biến {KEY} bằng giá trị
        foreach (var (key, val) in values)
        {
            cmdArgs = cmdArgs.Replace($"{{{key}}}", val);
        }

        // Chuyển %VAR% Windows sang $VAR cho Linux
        if (!OperatingSystem.IsWindows())
        {
            cmdArgs = WindowsEnvVarRegex.Replace(cmdArgs, "$$$1");
        }

        if (separateFields)
        {
            return SplitCommandLine(cmdArgs);
        }

        return new[] { cmdArgs };
    }

    /// <summary>
    /// Tách chuỗi lệnh thành các tham số riêng biệt, hỗ trợ trích dẫn.
    /// </summary>
    public static string[] SplitCommandLine(string commandLine)
    {
        var args = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        char quoteChar = '\0';
        bool escaping = false;

        foreach (char c in commandLine)
        {
            if (escaping)
            {
                current.Append(c);
                escaping = false;
                continue;
            }

            if (c == '\\' && inQuotes)
            {
                escaping = true;
                continue;
            }

            if (c is '"' or '\'')
            {
                if (!inQuotes)
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (c == quoteChar)
                {
                    inQuotes = false;
                    quoteChar = '\0';
                }
                else
                {
                    current.Append(c);
                }
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            args.Add(current.ToString());

        return args.ToArray();
    }

    /// <summary>
    /// Phân tích và xác thực đường dẫn file từ tham số.
    /// Trả về FileInfo nếu file tồn tại.
    /// </summary>
    public static FileInfo? ParsePath(string[] args, Dictionary<string, string> values)
    {
        var parsed = ParseCommandArgs(args, values, separateFields: false);
        if (parsed.Length != 1)
            return null;

        var absolutePath = Path.GetFullPath(parsed[0]);
        return File.Exists(absolutePath) ? new FileInfo(absolutePath) : null;
    }
}
