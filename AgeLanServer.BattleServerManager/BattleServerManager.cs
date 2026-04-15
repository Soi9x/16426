using System.Diagnostics;
using AgeLanServer.BattleServerManager.CmdUtils;
using AgeLanServer.Common;

namespace AgeLanServer.BattleServerManager;

/// <summary>
/// API công khai để các module khác gọi tới Battle Server Manager.
/// </summary>
public static class BattleServerManagerApi
{
    /// <summary>
    /// Dừng battle server theo region và xóa cấu hình.
    /// </summary>
    public static async Task StopAsync(string gameId, string region, CancellationToken ct = default)
    {
        var configs = BattleServerConfigLib.Configs(gameId, onlyValid: false);
        var targetConfig = configs.FirstOrDefault(c => c.Region == region);

        if (targetConfig == null)
        {
            return;
        }

        // Kill process
        if (targetConfig.PID > 0)
        {
            try
            {
                var proc = Process.GetProcessById((int)targetConfig.PID);
                if (proc != null && !proc.HasExited)
                {
                    proc.Kill();
                    await proc.WaitForExitAsync(ct);
                }
                proc?.Dispose();
            }
            catch
            {
                // Process đã thoát hoặc không tồn tại
            }
        }

        // Xóa cấu hình
        Remove.RemoveConfigs(gameId, new List<Config> { targetConfig }, onlyInvalid: false);
    }
}
