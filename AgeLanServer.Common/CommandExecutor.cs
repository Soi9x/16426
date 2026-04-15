using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AgeLanServer.Common;

/// <summary>
/// Thực thi lệnh shell hoặc file thực thi với các tùy chọn (admin, ẩn cửa sổ, detached...).
/// Tương đương common/executor/exec/ trong bản Go gốc.
/// </summary>
public class CommandExecutor
{
    /// <summary>
    /// Kết quả thực thi lệnh.
    /// </summary>
    public record ExecutionResult
    {
        public int ExitCode { get; init; }
        public string StdOut { get; init; } = string.Empty;
        public string StdErr { get; init; } = string.Empty;
        public Process? Process { get; init; }
    }

    /// <summary>
    /// Thực thi lệnh và chờ kết quả (đồng bộ).
    /// </summary>
    public static ExecutionResult Execute(string fileName, string arguments, bool hidden = true, bool adminRequired = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = hidden
        };

        // Xử lý yêu cầu quyền admin trên Windows
        if (adminRequired && OperatingSystem.IsWindows())
        {
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas"; // UAC elevation
            startInfo.RedirectStandardOutput = false;
            startInfo.RedirectStandardError = false;
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdout = string.Empty;
        var stderr = string.Empty;

        if (!adminRequired || !OperatingSystem.IsWindows())
        {
            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
        }

        process.WaitForExit();

        return new ExecutionResult
        {
            ExitCode = process.ExitCode,
            StdOut = stdout,
            StdErr = stderr,
            Process = process
        };
    }

    /// <summary>
    /// Thực thi lệnh bất đồng bộ.
    /// </summary>
    public static async Task<ExecutionResult> ExecuteAsync(string fileName, string arguments, bool hidden = true, bool adminRequired = false, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = hidden
        };

        if (adminRequired && OperatingSystem.IsWindows())
        {
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";
            startInfo.RedirectStandardOutput = false;
            startInfo.RedirectStandardError = false;
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdout = string.Empty;
        var stderr = string.Empty;

        if (!adminRequired || !OperatingSystem.IsWindows())
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(ct);

            stdout = await stdoutTask;
            stderr = await stderrTask;
        }
        else
        {
            await process.WaitForExitAsync(ct);
        }

        return new ExecutionResult
        {
            ExitCode = process.ExitCode,
            StdOut = stdout,
            StdErr = stderr
        };
    }

    /// <summary>
    /// Thực thi tiến trình detached (không chờ kết thúc).
    /// </summary>
    public static Process? StartDetached(string fileName, string arguments, bool hidden = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            CreateNoWindow = hidden,
            WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
        };

        try
        {
            return Process.Start(startInfo);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 740 && OperatingSystem.IsWindows())
        {
            // ERROR_ELEVATION_REQUIRED - thử lại với ShellExecute
            startInfo.Verb = "runas";
            return Process.Start(startInfo);
        }
    }

    /// <summary>
    /// Kiểm tra xem tiến trình hiện tại có đang chạy với quyền admin không.
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        if (OperatingSystem.IsWindows())
        {
            return IsWindowsAdmin();
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return Environment.GetEnvironmentVariable("USER") == "root"
                   || Environment.GetEnvironmentVariable("LOGNAME") == "root"
                   || UnixGetEuid() == 0;
        }
        return false;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();

    private static uint UnixGetEuid()
    {
        try
        {
            return geteuid();
        }
        catch
        {
            return uint.MaxValue;
        }
    }

    private static bool IsWindowsAdmin()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Thay đổi thư mục làm việc hiện tại về thư mục chứa file thực thi.
    /// </summary>
    public static void ChangeWorkingDirectoryToExecutable()
    {
        var exePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
        var exeDir = Path.GetDirectoryName(exePath);
        if (!string.IsNullOrEmpty(exeDir))
        {
            Environment.CurrentDirectory = exeDir;
        }
    }
}
