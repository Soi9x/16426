using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AgeLanServer.Common;

/// <summary>
/// Quản lý tiến trình: tìm kiếm, theo dõi, dừng tiến trình theo tên hoặc PID.
/// Tương đương common/process/ trong bản Go gốc.
/// </summary>
public static class ProcessManager
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Lấy tên tiến trình game tương ứng với Steam.
    /// </summary>
    public static string GetSteamProcessName(string gameId) => gameId switch
    {
        GameIds.AgeOfEmpires1 => "AoEDE_s.exe",
        GameIds.AgeOfEmpires2 => "AoE2DE_s.exe",
        GameIds.AgeOfEmpires3 => "AoE3DE_s.exe",
        GameIds.AgeOfEmpires4 => "RelicCardinal.exe",
        GameIds.AgeOfMythology => "AoMRT_s.exe",
        _ => string.Empty
    };

    /// <summary>
    /// Lấy tên tiến trình game tương ứng với Xbox/Microsoft Store.
    /// </summary>
    public static string GetXboxProcessName(string gameId) => gameId switch
    {
        GameIds.AgeOfEmpires1 => "AoEDE.exe",
        GameIds.AgeOfEmpires2 => "AoE2DE.exe",
        GameIds.AgeOfEmpires3 => "AoE3DE.exe",
        GameIds.AgeOfEmpires4 => "RelicCardinal_ws.exe",
        GameIds.AgeOfMythology => "AoMRT.exe",
        _ => string.Empty
    };

    /// <summary>
    /// Lấy danh sách tên tiến trình game theo platform (Steam/Xbox).
    /// </summary>
    public static string[] GetGameProcesses(string gameId, bool steam, bool xbox)
    {
        var processes = new HashSet<string>();
        if (steam)
        {
            var steamProc = GetSteamProcessName(gameId);
            if (!string.IsNullOrEmpty(steamProc))
                processes.Add(steamProc);
        }
        if (xbox)
        {
            var xboxProc = GetXboxProcessName(gameId);
            if (!string.IsNullOrEmpty(xboxProc))
                processes.Add(xboxProc);
        }
        return processes.ToArray();
    }

    /// <summary>
    /// Tìm tiến trình đang chạy theo tên (hỗ trợ wildcard trên Windows).
    /// </summary>
    public static Process? FindProcessByName(string processName)
    {
        var nameNoExt = Path.GetFileNameWithoutExtension(processName);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: không cần .exe khi tìm
            return Process.GetProcessesByName(nameNoExt).FirstOrDefault();
        }

        // Unix: tìm theo tên chính xác
        var processes = Process.GetProcessesByName(nameNoExt);
        return processes.FirstOrDefault();
    }

    /// <summary>
    /// Tìm tiến trình theo PID.
    /// </summary>
    public static Process? FindProcessByPid(int pid)
    {
        try
        {
            return Process.GetProcessById(pid);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Chờ tiến trình kết thúc trong khoảng thời gian timeout.
    /// Trả về true nếu tiến trình đã thoát, false nếu timeout.
    /// </summary>
    public static bool WaitForExit(Process process, TimeSpan? timeout = null)
    {
        timeout ??= WaitTimeout;
        try
        {
            return process.WaitForExit((int)timeout.Value.TotalMilliseconds);
        }
        catch
        {
            return true; // Process đã thoát hoặc không hợp lệ
        }
    }

    /// <summary>
    /// Dừng tiến trình một cách graceful: gửi SIGINT trước, sau đó Kill nếu cần.
    /// </summary>
    public static async Task<bool> KillProcessGracefullyAsync(Process process, CancellationToken ct = default)
    {
        try
        {
            if (!process.HasExited)
            {
                // Thử đóng gracefully trước
                process.CloseMainWindow();
                if (await Task.Run(() => process.WaitForExit((int)WaitTimeout.TotalMilliseconds), ct))
                    return true;
            }
        }
        catch
        {
            // Không thể đóng gracefully, thử Kill
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                return await Task.Run(() => process.WaitForExit((int)WaitTimeout.TotalMilliseconds), ct);
            }
        }
        catch
        {
            // Không thể kill
        }

        return process.HasExited;
    }

    /// <summary>
    /// Dừng tất cả tiến trình theo danh sách tên.
    /// </summary>
    public static async Task KillProcessesByNameAsync(IEnumerable<string> processNames, CancellationToken ct = default)
    {
        foreach (var name in processNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? Path.GetFileNameWithoutExtension(name)
                        : name);

                foreach (var proc in processes)
                {
                    await KillProcessGracefullyAsync(proc, ct);
                    proc.Dispose();
                }
            }
            catch
            {
                // Bỏ qua lỗi cho từng tiến trình
            }
        }
    }

    /// <summary>
    /// Lấy thời điểm bắt đầu của tiến trình (dùng để xác thực PID file).
    /// Trả về epoch milliseconds, hoặc 0 nếu không thể xác định.
    /// </summary>
    public static long GetProcessStartTimeMs(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            return proc.StartTime.ToUniversalTime().Ticks / TimeSpan.TicksPerMillisecond;
        }
        catch
        {
            return 0;
        }
    }
}
