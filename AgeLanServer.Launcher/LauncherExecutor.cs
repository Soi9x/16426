using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using AgeLanServer.Common;

namespace AgeLanServer.Launcher.Internal.Executor;

/// <summary>
/// Kết quả thực thi lệnh.
/// Tương đương exec.Result trong Go.
/// </summary>
public record ExecResult
{
    public bool Success => ExitCode == ErrorCodes.Success && Error == null;
    public int ExitCode { get; init; } = ErrorCodes.Success;
    public Exception? Error { get; init; }
    public uint? Pid { get; init; }
}

/// <summary>
/// Tùy chọn khi thực thi lệnh.
/// Tương đương exec.Options trong Go.
/// </summary>
public record ExecOptions
{
    public string File { get; init; } = string.Empty;
    public List<string> Args { get; init; } = new();
    public bool Wait { get; init; }
    public bool Pid { get; init; }
    public bool ShowWindow { get; init; } = true;
    public bool GUI { get; init; }
    public bool Shell { get; init; }
    public bool SpecialFile { get; init; }
    public bool AsAdmin { get; init; }
    public bool UseWorkingPath { get; init; }
    public bool ExitCode { get; init; }
    public TextWriter? Stdout { get; init; }
    public TextWriter? Stderr { get; init; }
}

/// <summary>
/// Extension method cho ExecOptions.
/// </summary>
public static class ExecOptionsExtensions
{
    /// <summary>
    /// Thực thi lệnh và trả về kết quả.
    /// </summary>
    public static ExecResult Exec(this ExecOptions options)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = options.File,
                UseShellExecute = options.Shell || options.SpecialFile || options.GUI,
                RedirectStandardOutput = options.Stdout != null || !options.GUI,
                RedirectStandardError = options.Stderr != null || !options.GUI,
                CreateNoWindow = !options.ShowWindow,
                WorkingDirectory = options.UseWorkingPath ? Directory.GetCurrentDirectory() : ""
            };

            if (options.Args.Count > 0)
            {
                psi.Arguments = string.Join(" ", options.Args.Select(EscapeArg));
            }

            if (options.AsAdmin)
            {
                psi.Verb = "runas";
                psi.UseShellExecute = true;
            }

            var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
            {
                return new ExecResult
                {
                    ExitCode = ErrorCodes.General,
                    Error = new InvalidOperationException($"Không thể khởi động process: {options.File}")
                };
            }

            uint? pid = options.Pid ? (uint)process.Id : null;

            if (options.Wait)
            {
                process.WaitForExit();
                var exitCode = options.ExitCode ? process.ExitCode : ErrorCodes.Success;

                return new ExecResult
                {
                    ExitCode = exitCode == 0 ? ErrorCodes.Success : exitCode,
                    Pid = pid,
                    Error = exitCode != 0 ? new Exception($"Process thoát với mã {exitCode}") : null
                };
            }

            return new ExecResult { Pid = pid };
        }
        catch (Exception ex)
        {
            return new ExecResult
            {
                ExitCode = ErrorCodes.General,
                Error = ex
            };
        }
    }

    private static string EscapeArg(string arg)
    {
        if (string.IsNullOrEmpty(arg) || !arg.Contains(' '))
            return arg;
        return $"\"{arg.Replace("\"", "\\\"")}\"";
    }
}

/// <summary>
/// Kiểm tra xem process đang chạy với quyền admin không.
/// Tương đương IsAdmin trong common/executor
/// </summary>
public static class Executor
{
    public static bool IsAdmin()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}

/// <summary>
/// Module thực thi các lệnh thiết lập và khôi phục cấu hình.
/// Tương đương package executor trong Go (config.go, command.go, agent.go).
/// </summary>
public static class ExecutorModule
{
    /// <summary>
    /// Chạy thiết lập cấu hình (setup).
    /// Tương đương RunSetUp trong config.go
    /// </summary>
    public static ExecResult RunSetUp(
        string game,
        HashSet<string>? mapIps,
        byte[]? addUserCertData,
        byte[]? addLocalCertData,
        byte[]? addGameCertData,
        bool backupMetadata,
        bool backupProfiles,
        bool exitAgentOnError,
        string hostFilePath,
        string certFilePath,
        string gamePath,
        TextWriter? outVar,
        Action<ExecOptions> optionsFn)
    {
        var reloadSystemCertificates = false;
        var reloadHostMappings = false;
        var args = new List<string> { "setup" };

        if (!string.IsNullOrEmpty(game))
        {
            args.Add("-e");
            args.Add(game);
        }

        if (!Executor.IsAdmin())
        {
            args.Add("-g");
            if (exitAgentOnError)
            {
                args.Add("-r");
            }
        }

        if (mapIps != null)
        {
            foreach (var ip in mapIps)
            {
                args.Add("-i");
                args.Add(ip);
            }
            reloadHostMappings = true;
        }

        if (addLocalCertData != null)
        {
            reloadSystemCertificates = true;
            args.Add("-l");
            args.Add(Convert.ToBase64String(addLocalCertData));
        }

        if (addUserCertData != null)
        {
            reloadSystemCertificates = true;
            args.Add("-u");
            args.Add(Convert.ToBase64String(addUserCertData));
        }

        if (addGameCertData != null)
        {
            args.Add("-s");
            args.Add(Convert.ToBase64String(addGameCertData));
        }

        if (backupMetadata)
        {
            args.Add("-m");
        }

        if (backupProfiles)
        {
            args.Add("-p");
        }

        if (!string.IsNullOrEmpty(hostFilePath))
        {
            args.Add("-o");
            args.Add(hostFilePath);
        }

        if (!string.IsNullOrEmpty(certFilePath))
        {
            args.Add("-t");
            args.Add(certFilePath);
        }

        if (!string.IsNullOrEmpty(gamePath))
        {
            args.Add("--gamePath");
            args.Add(gamePath);
        }

        var logRoot = AppLogger.LogFolder();
        if (!string.IsNullOrEmpty(logRoot))
        {
            args.Add("--logRoot");
            args.Add(logRoot);
        }

        var options = new ExecOptions
        {
            File = ExecutablePaths.GetFileName(ExecutablePaths.LauncherConfig),
            Wait = true,
            Args = args,
            ExitCode = true
        };

        optionsFn(options);
        var result = options.Exec();

        if (reloadSystemCertificates)
        {
            CertStoreModule.ReloadSystemCertificates();
        }

        if (reloadHostMappings)
        {
            CommonUtilities.ClearCache();
        }

        return result;
    }

    /// <summary>
    /// Chạy khôi phục cấu hình (revert).
    /// Tương đương RunRevert trong config.go
    /// </summary>
    public static ExecResult RunRevert(
        List<string> flags,
        bool bin,
        TextWriter? outVar,
        Action<ExecOptions> optionFn)
    {
        var args = new List<string> { "revert" };
        args.AddRange(flags);

        var logRoot = AppLogger.LogFolder();
        if (!string.IsNullOrEmpty(logRoot))
        {
            args.Add("--logRoot");
            args.Add(logRoot);
        }

        var options = new ExecOptions
        {
            File = ExecutablePaths.GetFileName(ExecutablePaths.LauncherConfig),
            Wait = true,
            Args = args,
            ExitCode = true
        };

        optionFn(options);
        var result = options.Exec();

        if (flags.Contains("-a") || flags.Contains("-u") || flags.Contains("-l"))
        {
            CertStoreModule.ReloadSystemCertificates();
        }

        if (flags.Contains("-a") || flags.Contains("-i") || flags.Contains("-c"))
        {
            CommonUtilities.ClearCache();
        }

        return result;
    }

    /// <summary>
    /// Chạy revert command.
    /// Tương đương RunRevertCommand trong command.go
    /// </summary>
    public static void RunRevertCommand(TextWriter? outVar, Action<ExecOptions> optionsFn)
    {
        var options = new ExecOptions
        {
            File = ExecutablePaths.GetFileName(ExecutablePaths.LauncherConfig),
            Wait = true,
            Args = new List<string> { "revert-command" },
            ExitCode = true
        };

        optionsFn(options);
        options.Exec();

        CertStoreModule.ReloadSystemCertificates();
        CommonUtilities.ClearCache();
    }

    /// <summary>
    /// Khởi động agent launcher.
    /// Tương đương StartAgent trong agent.go
    /// </summary>
    public static ExecResult StartAgent(
        string game,
        bool steamProcess,
        bool xboxProcess,
        string serverExe,
        bool broadcastBattleServer,
        string battleServerExe,
        string battleServerRegion,
        string logRoot,
        TextWriter? outVar,
        Action<ExecOptions> optionsFn)
    {
        if (string.IsNullOrEmpty(serverExe))
            serverExe = "-";
        if (string.IsNullOrEmpty(battleServerExe))
            battleServerExe = "-";
        if (string.IsNullOrEmpty(battleServerRegion))
            battleServerRegion = "-";

        var args = new List<string>
        {
            steamProcess.ToString().ToLower(),
            xboxProcess.ToString().ToLower(),
            serverExe,
            broadcastBattleServer.ToString().ToLower(),
            game,
            battleServerExe,
            battleServerRegion,
            logRoot
        };

        var options = new ExecOptions
        {
            File = ExecutablePaths.GetFileName(ExecutablePaths.LauncherAgent),
            Pid = true,
            Args = args
        };

        optionsFn(options);
        return options.Exec();
    }
}

/// <summary>
/// Module quản lý certificate store hệ thống.
/// Tương đương package certStore trong Go.
/// </summary>
public static class CertStoreModule
{
    /// <summary>
    /// Tải lại certificate hệ thống.
    /// Trên Windows không cần làm gì vì OS tự kiểm tra.
    /// Trên Unix, cần reload system roots.
    /// </summary>
    public static void ReloadSystemCertificates()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Trên Unix, certificate được load từ các file cert directories
            // Khi cần, ta clear cache để lần truy cập sau sẽ đọc lại
            ClearCertificateCache();
        }
    }

    private static void ClearCertificateCache()
    {
        // Trên Unix, X509Certificate2 cache được load từ hệ thống file cert directories.
        // .NET không có API public để reload system roots, nhưng ta có thể tạo một
        // X509Store mới để buộc reload từ disk trong các lần truy cập sau.
        // Workaround: tạo instance mới của X509Store để clear internal cache
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                // Việc mở lại store buộc .NET đọc lại certificates từ disk
                _ = store.Certificates.Count;
                store.Close();
            }
            catch
            {
                // Bỏ qua lỗi - cache sẽ được làm mới tự động ở lần truy cập sau
            }
        }
    }
}
