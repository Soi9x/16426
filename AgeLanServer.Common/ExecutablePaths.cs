namespace AgeLanServer.Common;

/// <summary>
/// Quản lý đường dẫn tới các file thực thi của hệ thống (server, launcher, battle-server-manager...).
/// Tương đương common/executables/executables.go trong bản Go gốc.
/// </summary>
public static class ExecutablePaths
{
    // === Tên các file thực thi (không có extension) ===

    /// <summary>Server chính.</summary>
    public const string Server = "server";

    /// <summary>Trình tạo chứng chỉ.</summary>
    public const string ServerGenCert = "genCert";

    /// <summary>Launcher chính.</summary>
    public const string Launcher = "launcher";

    /// <summary>Launcher agent (giám sát tiến trình game).</summary>
    public const string LauncherAgent = "agent";

    /// <summary>Launcher config (cấu hình setup/revert).</summary>
    public const string LauncherConfig = "config";

    /// <summary>Launcher config admin (yêu cầu quyền quản trị).</summary>
    public const string LauncherConfigAdmin = "config-admin";

    /// <summary>Launcher config admin agent (IPC agent cho admin).</summary>
    public const string LauncherConfigAdminAgent = "config-admin-agent";

    /// <summary>Battle server manager.</summary>
    public const string BattleServerManager = "battle-server-manager";

    /// <summary>Các thư mục tìm kiếm tương đối so với thư mục exe hiện tại.</summary>
    private static readonly string[] SearchDirectories =
    {
        Path.DirectorySeparatorChar.ToString(),
        Path.Combine(Path.DirectorySeparatorChar.ToString(), ".."),
        Path.Combine(Path.DirectorySeparatorChar.ToString(), "..", "..")
    };

    /// <summary>
    /// Lấy tên file thực thi có extension phù hợp với hệ điều hành.
    /// </summary>
    public static string GetFileName(string executableName)
    {
        if (OperatingSystem.IsWindows())
            return $"{executableName}.exe";
        return executableName;
    }

    /// <summary>
    /// Lấy tên file thực thi có extension, nằm trong thư mục bin/.
    /// </summary>
    public static string GetBinFileName(string executableName)
    {
        return Path.Combine("bin", GetFileName(executableName));
    }

    /// <summary>
    /// Lấy tên file không có extension.
    /// </summary>
    public static string GetBaseNameWithoutExtension(string fileName)
    {
        return Path.GetFileNameWithoutExtension(fileName);
    }

    /// <summary>
    /// Tìm đường dẫn tuyệt đối tới file thực thi bằng cách duyệt các thư mục lân cận.
    /// Thứ tự tìm: thư mục hiện tại → thư mục cha → thư mục ông.
    /// </summary>
    public static string? FindExecutablePath(string executableName)
    {
        var exePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
        var exeDir = Path.GetDirectoryName(exePath) ?? ".";
        var exeFileName = GetFileName(executableName);

        foreach (var relDir in SearchDirectories)
        {
            var candidate = Path.GetFullPath(Path.Combine(exeDir, relDir, executableName, exeFileName));
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
