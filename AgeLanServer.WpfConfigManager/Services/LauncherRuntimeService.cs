using AgeLanServer.Common;
using AgeLanServer.Launcher;
using AgeLanServer.WpfConfigManager.Models;
using LauncherConfigFacade = AgeLanServer.LauncherConfig.LauncherConfig;

namespace AgeLanServer.WpfConfigManager.Services;

/// <summary>
/// Dịch vụ chạy launcher nền cho WPF và hỗ trợ stop/cleanup tài nguyên.
/// </summary>
public sealed class LauncherRuntimeService
{
    private CancellationTokenSource? _cts;
    private Task? _runningTask;

    public bool IsRunning => _runningTask is { IsCompleted: false };

    public event Action<string>? StatusChanged;

    /// <summary>
    /// Chạy launcher với profile hiện tại. Hàm trả về ngay để không khóa UI.
    /// </summary>
    public void Start(GameProfile profile)
    {
        if (IsRunning)
        {
            StatusChanged?.Invoke("Launcher đang chạy, không thể chạy profile mới.");
            return;
        }

        _cts = new CancellationTokenSource();
        var config = BuildLauncherConfig(profile);

        _runningTask = Task.Run(async () =>
        {
            try
            {
                StatusChanged?.Invoke($"Đang chạy profile {profile.ProfileName}...");
                var exitCode = await LauncherApp.RunAsync(config, _cts.Token);
                StatusChanged?.Invoke($"Launcher đã kết thúc (exit code: {exitCode}).");
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke("Đã nhận lệnh dừng launcher.");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Launcher lỗi: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Dừng launcher và dọn dẹp tài nguyên liên quan.
    /// </summary>
    public async Task StopAsync(string gameId, bool forceStopServer)
    {
        if (_cts is not null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        if (_runningTask is not null)
        {
            await Task.WhenAny(_runningTask, Task.Delay(TimeSpan.FromSeconds(10)));
        }

        try
        {
            await LauncherConfigFacade.RevertAsync(gameId, removeAll: false);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Cleanup revert cảnh báo: {ex.Message}");
        }

        if (forceStopServer)
        {
            await ProcessManager.KillProcessesByNameAsync(new[] { "server", "battle-server-manager", "BattleServer" });
        }

        StatusChanged?.Invoke("Đã dừng và dọn dẹp tài nguyên launcher.");
    }

    private static LauncherApp.LauncherConfig BuildLauncherConfig(GameProfile profile)
    {
        return new LauncherApp.LauncherConfig
        {
            GameId = profile.GameId,
            Server = new LauncherApp.ServerConfig
            {
                ExecutablePath = profile.ServerExecutablePath,
                AutoStart = profile.AutoStartServer,
                AutoStop = profile.AutoStopServer,
                AnnouncePort = profile.AnnouncePort
            },
            Client = new LauncherApp.ClientConfig
            {
                Executable = profile.ClientExecutable,
                GamePath = profile.ClientGamePath,
                ExtraArgs = profile.ClientExtraArgs
            },
            TrustCertificate = profile.TrustCertificate,
            MapHosts = profile.MapHosts,
            IsolateMetadata = profile.IsolateMetadata,
            IsolateProfiles = profile.IsolateProfiles,
            LogToFile = profile.LogToFile
        };
    }
}
