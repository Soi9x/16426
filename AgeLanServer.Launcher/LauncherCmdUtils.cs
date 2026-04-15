using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AgeLanServer.BattleServerManager;
using AgeLanServer.Common;
using AgeLanServer.Launcher.Internal.CmdUtils.Logger;
using AgeLanServer.Launcher.Internal.Executor;
using AgeLanServer.LauncherCommon;
using AgeLanServer.LauncherCommon.ServerKill;

namespace AgeLanServer.Launcher.Internal.CmdUtils;

/// <summary>
/// Lớp quản lý cấu hình launcher với trạng thái runtime.
/// Tương đương Config struct trong root.go (cmdUtils) của Go.
/// </summary>
public class LauncherConfigManager
{
    public string GameId { get; private set; } = string.Empty;
    public string ServerExe { get; set; } = string.Empty;
    public bool SetupCommandRan { get; set; }
    public string HostFilePath { get; set; } = string.Empty;
    public string CertFilePath { get; set; } = string.Empty;
    public string BattleServerRegion { get; set; } = string.Empty;
    public string BattleServerExe { get; set; } = string.Empty;

    public void SetGameId(string gameId) => GameId = gameId;

    /// <summary>
    /// Kiểm tra xem có cần revert cấu hình không.
    /// </summary>
    public bool RequiresConfigRevert()
    {
        var loaded = ConfigRevertManager.LoadRevertArgs();
        return loaded != null;
    }

    /// <summary>
    /// Kiểm tra xem có cần chạy revert command không.
    /// </summary>
    public bool RequiresRunningRevertCommand() => SetupCommandRan && GetRevertCommand().Count > 0;

    /// <summary>
    /// Lấy revert command nếu setup command đã chạy.
    /// </summary>
    public List<string> GetRevertCommand() => SetupCommandRan ? new List<string>() : new List<string>();

    /// <summary>
    /// Đảo ngược toàn bộ thay đổi đã thực hiện.
    /// Tương đương Revert() trong root.go (cmdUtils)
    /// </summary>
    public void Revert()
    {
        LauncherLogger.WriteFileLog(GameId, "pre-revert");
        KillAgent();

        if (!string.IsNullOrEmpty(ServerExe))
        {
            LauncherLogger.Info("Đang dừng 'server'...");
            try
            {
                ServerKiller.DoAsync(ServerExe).Wait(TimeSpan.FromSeconds(5));
                LauncherLogger.Info("'Server' đã dừng.");
            }
            catch (Exception ex)
            {
                LauncherLogger.Error("Không thể dừng 'server'.");
                LauncherLogger.Error($"Lỗi: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(BattleServerRegion) && !string.IsNullOrEmpty(BattleServerExe))
        {
            LauncherLogger.Info("Đang dừng battle server qua 'battle-server-manager'...");
            try
            {
                // Gọi BattleServerManagerApi.StopAsync để dừng battle server
                BattleServerManagerApi.StopAsync(GameId, BattleServerRegion).Wait(TimeSpan.FromSeconds(10));
                LauncherLogger.Info("Battle-server đã dừng (hoặc đã dừng trước đó).");
            }
            catch (Exception ex)
            {
                LauncherLogger.Error("Không thể dừng battle-server.");
                LauncherLogger.Error($"Lỗi: {ex.Message}");
                LauncherLogger.Info("Bạn có thể thử kill process 'BattleServer.exe' trong task manager.");
            }
        }

        if (RequiresConfigRevert())
        {
            LauncherLogger.Info("Đang dọn dẹp...");
            try
            {
                // Gọi ConfigRevertManager.ExecuteRevertAsync để đảo ngược cấu hình
                ConfigRevertManager.ExecuteRevertAsync().Wait(TimeSpan.FromSeconds(30));
            }
            catch
            {
                LauncherLogger.Error("Không thể dọn dẹp.");
            }
        }

        if (RequiresRunningRevertCommand())
        {
            try
            {
                // Chạy revert command qua ExecutorModule.RunRevertCommand
                ExecutorModule.RunRevertCommand(null, opts => { });
                LauncherLogger.Info("Đã chạy Revert command.");
            }
            catch (Exception ex)
            {
                LauncherLogger.Error("Không thể chạy revert command.");
                LauncherLogger.Error($"Lỗi: {ex.Message}");
            }
        }

        LauncherLogger.WriteFileLog(GameId, "post-revert");
    }

    /// <summary>
    /// Kill agent nếu đang chạy.
    /// </summary>
    public void KillAgent()
    {
        var agentName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "agent.exe"
            : "agent";
        LauncherLogger.Info("Đang kill 'agent' nếu cần...");
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(
                Path.GetFileNameWithoutExtension(agentName));
            foreach (var proc in processes)
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
                catch (Exception ex)
                {
                    LauncherLogger.Error($"Không thể kill agent: {ex.Message}, thử dùng task manager.");
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch
        {
            // Không tìm thấy process
        }
    }
}

/// <summary>
/// Lớp tiện ích chứa các hàm chung cho lệnh launcher.
/// Tương đương package cmdUtils trong Go.
/// </summary>
public static class LauncherCmdUtils
{
    /// <summary>
    /// Phân tích tham số lệnh, thay thế các biến giá trị.
    /// Tương đương ParseCommandArgs trong parse.go
    /// </summary>
    public static List<string> ParseCommandArgs(List<string> cmdSlice, Dictionary<string, string>? values)
    {
        var result = new List<string>();
        foreach (var arg in cmdSlice)
        {
            var resolved = arg;
            if (values != null)
            {
                foreach (var (key, value) in values)
                {
                    resolved = resolved.Replace($"{{{key}}}", value);
                }
            }
            result.Add(resolved);
        }
        return result;
    }

    /// <summary>
    /// Giải quyết giá trị cô lập (isolate) từ chuỗi cấu hình.
    /// Tương đương ResolveIsolateValue trong isolateUserData.go
    /// </summary>
    public static bool ResolveIsolateValue(string value, bool officialLauncher)
    {
        return value switch
        {
            "true" => true,
            "false" => false,
            "required" => officialLauncher,
            _ => false
        };
    }

    /// <summary>
    /// Kiểm tra xem có lỗi cần quyền admin không.
    /// Tương đương adminError trong game_windows.go / game_other.go
    /// </summary>
    public static bool IsAdminError(Exception? error)
    {
        if (error == null)
            return false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // ERROR_ELEVATION_REQUIRED = 740 (0x2E4)
            return error.HResult == unchecked((int)0x800702E4)
                || error.Message.Contains("requires elevation", System.StringComparison.OrdinalIgnoreCase);
        }

        return error is UnauthorizedAccessException;
    }

    /// <summary>
    /// Kiểm tra xem game đang chạy không, chờ tối đa 1 phút.
    /// Tương đương GameRunning trong root.go
    /// </summary>
    public static bool IsGameRunning()
    {
        // Danh sách các process game phổ biến
        var gameProcesses = new List<string>
        {
            "RelicCardinal",       // AoE4
            "AoE4",                // AoE4 alternative
            "AGE3_xS_P",           // AoE3
            "AoM.R",               // AoM Retold
            "Age of Empires",      // Generic
            "age2_x1"              // AoE2
        };

        bool AnyProcessRunning()
        {
            foreach (var procName in gameProcesses)
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(procName);
                if (processes.Length > 0)
                {
                    foreach (var p in processes) p.Dispose();
                    return true;
                }
            }
            return false;
        }

        if (!AnyProcessRunning())
            return false;

        LauncherLogger.Info("Một game Age đang chạy, chờ tối đa 1 phút để game thoát.");

        var timeout = DateTime.UtcNow.AddMinutes(1);
        while (DateTime.UtcNow < timeout)
        {
            if (!AnyProcessRunning())
                return false;
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }

        LauncherLogger.Info("Game không thoát đúng lúc.");
        return true;
    }
}
