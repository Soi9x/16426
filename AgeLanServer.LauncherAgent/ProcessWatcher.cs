using System.Diagnostics;
using AgeLanServer.BattleServerBroadcast;
using AgeLanServer.Common;
using AgeLanServer.LauncherCommon;
using global::AgeLanServer.BattleServerManager;

namespace AgeLanServer.LauncherAgent;

/// <summary>
/// Giám sát tiến trình game và xử lý cleanup khi game thoát.
/// Tương đương launcher-agent/internal/watch/ (watch.go, watch_windows.go, watch_unix.go) trong bản Go gốc.
/// </summary>
public static class ProcessWatcher
{
    // Khoảng thời gian kiểm tra tiến trình (1 giây, giống bản Go).
    private static readonly TimeSpan ProcessWaitInterval = TimeSpan.FromSeconds(1);

    // Timeout tối đa để chờ tiến trình game khởi động (1 phút).
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Tên tiến trình game theo platform (Steam/Xbox) cho từng game.
    /// Tương đương commonProcess.GameProcesses trong Go.
    /// </summary>
    private static string[] GetProcessNames(string gameId, bool isSteam, bool isXbox)
    {
        var names = new List<string>();

        if (isSteam)
        {
            names.Add(gameId switch
            {
                GameIds.AgeOfEmpires1 => "AoEDE_s",
                GameIds.AgeOfEmpires2 => "AoE2DE_s",
                GameIds.AgeOfEmpires3 => "AoE3DE_s",
                GameIds.AgeOfEmpires4 => "RelicCardinal",
                GameIds.AgeOfMythology => "AoMRT_s",
                _ => ""
            });
        }

        if (isXbox)
        {
            names.Add(gameId switch
            {
                GameIds.AgeOfEmpires1 => "AoEDE",
                GameIds.AgeOfEmpires2 => "AoE2DE",
                GameIds.AgeOfEmpires3 => "AoE3DE",
                GameIds.AgeOfEmpires4 => "RelicCardinal_ws",
                GameIds.AgeOfMythology => "AoMRT",
                _ => ""
            });
        }

        return names.Where(n => !string.IsNullOrEmpty(n)).ToArray();
    }

    /// <summary>
    /// Chờ đến khi bất kỳ tiến trình game nào xuất hiện (timeout 1 phút).
    /// Trả về dictionary PID -> Process, tương đương waitUntilAnyProcessExist.
    /// </summary>
    private static Dictionary<int, Process> WaitUntilAnyProcessExist(string[] processNames)
    {
        var processes = new Dictionary<int, Process>();
        var maxIterations = (int)(ProcessTimeout.TotalSeconds / ProcessWaitInterval.TotalSeconds);

        for (var i = 0; i < maxIterations; i++)
        {
            foreach (var procName in processNames)
            {
                var procs = Process.GetProcessesByName(procName);
                foreach (var proc in procs)
                {
                    if (!processes.ContainsKey(proc.Id))
                    {
                        processes[proc.Id] = proc;
                    }
                }
            }

            if (processes.Count > 0)
                return processes;

            Thread.Sleep(ProcessWaitInterval);
        }

        return processes;
    }

    /// <summary>
    /// Chờ tiến trình BattleServer.exe xuất hiện (dùng cho rebroadcast trên Windows).
    /// </summary>
    private static bool WaitForBattleServer()
    {
        var procs = Process.GetProcessesByName("BattleServer");
        if (procs.Length > 0)
        {
            foreach (var p in procs) p.Dispose();
            return true;
        }

        // Chờ tối đa 1 phút giống như game process
        var maxIterations = (int)(ProcessTimeout.TotalSeconds / ProcessWaitInterval.TotalSeconds);
        for (var i = 0; i < maxIterations; i++)
        {
            procs = Process.GetProcessesByName("BattleServer");
            if (procs.Length > 0)
            {
                foreach (var p in procs) p.Dispose();
                return true;
            }
            Thread.Sleep(ProcessWaitInterval);
        }

        return false;
    }

    /// <summary>
    /// Phát lại (rebroadcast) thông báo battle server trên Windows.
    /// Chỉ hoạt động trên Windows; trên Unix là no-op (tương đương watch_unix.go).
    /// Lấy interface addresses, chờ BattleServer.exe, rồi clone announcements.
    /// </summary>
    private static async Task RebroadcastBattleServerAsync(int port, CancellationToken ct)
    {
        // Chỉ rebroadcast trên Windows (trên Unix hàm này không được gọi)
        try
        {
            var (mostPriority, restInterfaces) =
                await BattleServerBroadcaster.RetrieveBsInterfaceAddressesAsync(ct);

            if (mostPriority == null || restInterfaces.Count == 0)
                return;

            // Chờ BattleServer.exe khởi động
            if (!WaitForBattleServer())
            {
                AppLogger.Warn("Timeout: Không tìm thấy BattleServer.exe để rebroadcast");
                return;
            }

            AppLogger.Info($"Bắt đầu rebroadcast BattleServer trên cổng {port}...");

            // Chạy rebroadcast bất đồng bộ (không chặn tiến trình chính)
            _ = Task.Run(async () =>
            {
                try
                {
                    await BattleServerBroadcaster.CloneAnnouncementsAsync(
                        mostPriority, restInterfaces, port, ct);
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"Lỗi rebroadcast: {ex.Message}");
                }
            }, ct);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Không thể thiết lập rebroadcast: {ex.Message}");
        }
    }

    /// <summary>
    /// Kill server process theo đường dẫn executable.
    /// Tương đương serverKill.Do trong Go.
    /// </summary>
    private static void KillServer(string serverExe)
    {
        if (string.IsNullOrEmpty(serverExe) || serverExe == "-")
            return;

        AppLogger.Info("Đang kill server...");
        try
        {
            // Tìm tiến trình có đường dẫn khớp với serverExe
            var exeName = Path.GetFileNameWithoutExtension(serverExe);
            var procs = Process.GetProcessesByName(exeName);
            foreach (var proc in procs)
            {
                try
                {
                    var procPath = proc.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(procPath) &&
                        procPath.Equals(serverExe, StringComparison.OrdinalIgnoreCase))
                    {
                        proc.Kill();
                        proc.WaitForExit(5000);
                    }
                }
                catch
                {
                    // Bỏ qua nếu không thể kill
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Lỗi khi kill server: {ex.Message}");
        }
    }

    /// <summary>
    /// Chờ và giám sát tiến trình game.
    /// Khi game thoát: kill server, shutdown battle-server, revert config, revert command, sao chép log, rebroadcast.
    /// </summary>
    /// <param name="gameId">ID game (age1, age2, age3, age4, athens)</param>
    /// <param name="isSteam">Game chạy từ Steam</param>
    /// <param name="isXbox">Game chạy từ Xbox/Microsoft Store</param>
    /// <param name="logDestination">Thư mục đích cho log game</param>
    /// <param name="serverExe">Đường dẫn server executable (để kill khi game thoát)</param>
    /// <param name="broadcastBattleServer">Cờ bật rebroadcast battle server</param>
    /// <param name="battleServerExe">Đường dẫn battle-server-manager executable</param>
    /// <param name="battleServerRegion">Tên vùng battle server</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Mã thoát (0 = thành công)</returns>
    public static async Task<int> WatchGameProcessAsync(
        string gameId,
        bool isSteam,
        bool isXbox,
        string? logDestination = null,
        string? serverExe = null,
        bool broadcastBattleServer = false,
        string? battleServerExe = null,
        string? battleServerRegion = null,
        CancellationToken ct = default)
    {
        var exitCode = LauncherErrorCodes.Success;
        var processNames = GetProcessNames(gameId, isSteam, isXbox);

        if (processNames.Length == 0)
        {
            AppLogger.Error($"Không có tên tiến trình cho game: {gameId}");
            return LauncherErrorCodes.InvalidGame;
        }

        AppLogger.Info($"Đang chờ tiến trình game khởi động... (timeout 1 phút)");

        // Chờ tiến trình game xuất hiện (timeout 1 phút)
        var processes = WaitUntilAnyProcessExist(processNames);

        if (processes.Count == 0)
        {
            AppLogger.Error("Timeout: Không tìm thấy tiến trình game");
            return LauncherErrorCodes.GameTimeoutStart;
        }

        // Lấy tiến trình đầu tiên
        Process? gameProcess = null;
        foreach (var kvp in processes)
        {
            gameProcess = kvp.Value;
            break;
        }

        if (gameProcess == null)
        {
            AppLogger.Error("Không thể lấy thông tin tiến trình game");
            return LauncherErrorCodes.FailedWaitForProcess;
        }

        AppLogger.Info($"Đã tìm thấy tiến trình game: {gameProcess.ProcessName} (PID: {gameProcess.Id})");

        // Cấu hình rebroadcast battle server nếu được bật
        if (broadcastBattleServer)
        {
            // AoE1 dùng cổng 8888, các game khác dùng 9999
            var port = gameId == GameIds.AgeOfEmpires1 ? 8888 : 9999;
            AppLogger.Info($"Phát sóng BattleServer trên cổng {port}...");
            await RebroadcastBattleServerAsync(port, ct);
        }

        // Chờ game thoát
        AppLogger.Info($"Đang chờ PID {gameProcess.Id} kết thúc...");
        try
        {
            await gameProcess.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            AppLogger.Info("Đã hủy giám sát tiến trình");
            gameProcess.Dispose();
            return exitCode;
        }

        AppLogger.Info("Tiến trình game đã thoát. Đang xử lý cleanup...");

        // Cleanup trong defer (thứ tự quan trọng - giống Go defer):
        // 1. Kill server
        if (!string.IsNullOrEmpty(serverExe) && serverExe != "-")
        {
            KillServer(serverExe);
        }

        // 2. Shutdown battle-server region
        if (!string.IsNullOrEmpty(battleServerExe) && battleServerExe != "-" &&
            !string.IsNullOrEmpty(battleServerRegion) && battleServerRegion != "-")
        {
            AppLogger.Info("Đang tắt battle-server...");
            try
            {
                await BattleServerManagerApi.StopAsync(gameId, battleServerRegion, ct);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Lỗi khi tắt battle-server: {ex.Message}");
                if (exitCode == LauncherErrorCodes.Success)
                {
                    exitCode = LauncherErrorCodes.FailedStopServer;
                }
            }
        }

        // 3. Revert cấu hình game (chứng chỉ, hosts, metadata, profiles)
        // Tương đương ConfigRevert trong Go - đọc revert flags từ file và thực thi đảo ngược
        try
        {
            await ConfigRevertManager.ExecuteRevertAsync(ct);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Lỗi revert cấu hình: {ex.Message}");
        }

        // 4. Sao chép log game
        if (!string.IsNullOrEmpty(logDestination) && logDestination != "-")
        {
            await GameLogCopier.CopyGameLogsAsync(gameId, logDestination, ct);
        }

        gameProcess.Dispose();

        return exitCode;
    }
}
