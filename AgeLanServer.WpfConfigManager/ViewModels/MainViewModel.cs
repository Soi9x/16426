using System.Collections.ObjectModel;
using System.Windows;
using AgeLanServer.Common;
using AgeLanServer.WpfConfigManager.Models;
using AgeLanServer.WpfConfigManager.Services;
using WpfThemeMode = AgeLanServer.WpfConfigManager.Models.ThemeMode;

namespace AgeLanServer.WpfConfigManager.ViewModels;

/// <summary>
/// ViewModel chính cho bảng quản lý cấu hình WPF.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly ProfileStorageService _profileStorageService;
    private readonly TomlConfigService _tomlConfigService;
    private readonly LauncherRuntimeService _launcherRuntimeService;

    private GameProfile? _selectedProfile;
    private WpfThemeMode _theme = WpfThemeMode.Dark;
    private string _statusMessage = "Sẵn sàng";
    private bool _isBusy;

    public ObservableCollection<GameProfile> Profiles { get; } = new();

    public IReadOnlyList<WpfThemeMode> ThemeModes { get; } = new[] { WpfThemeMode.Dark, WpfThemeMode.Light };

    public IReadOnlyList<string> OnOffAutoModes { get; } = new[] { "auto", "true", "false" };
    public IReadOnlyList<string> RequiredOnOffModes { get; } = new[] { "required", "true", "false" };
    public IReadOnlyList<string> CertTrustModes { get; } = new[] { "local", "user", "false" };

    public GameProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value))
                return;

            StatusMessage = value is null
                ? "Chưa có profile được chọn."
                : $"Đã chọn profile: {value.ProfileName}";

            RefreshCommandState();
        }
    }

    public WpfThemeMode Theme
    {
        get => _theme;
        set
        {
            if (!SetProperty(ref _theme, value))
                return;

            ThemeChanged?.Invoke(value);
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!SetProperty(ref _isBusy, value))
                return;

            RefreshCommandState();
        }
    }

    public RelayCommand LoadTomlCommand { get; }
    public RelayCommand SaveTomlCommand { get; }
    public RelayCommand SaveProfilesCommand { get; }
    public RelayCommand StartLauncherCommand { get; }
    public RelayCommand StopLauncherCommand { get; }

    public event Action<WpfThemeMode>? ThemeChanged;

    public MainViewModel()
    {
        _profileStorageService = new ProfileStorageService();
        _tomlConfigService = new TomlConfigService();
        _launcherRuntimeService = new LauncherRuntimeService();
        _launcherRuntimeService.StatusChanged += message =>
        {
            Application.Current.Dispatcher.Invoke(() => StatusMessage = message);
            RefreshCommandState();
        };

        LoadTomlCommand = new RelayCommand(async () => await LoadTomlAsync(), CanExecuteProfileAction);
        SaveTomlCommand = new RelayCommand(async () => await SaveTomlAsync(), CanExecuteProfileAction);
        SaveProfilesCommand = new RelayCommand(async () => await SaveProfilesAsync(), () => !IsBusy);
        StartLauncherCommand = new RelayCommand(StartLauncher, CanStartLauncher);
        StopLauncherCommand = new RelayCommand(async () => await StopLauncherAsync(), CanStopLauncher);
    }

    /// <summary>
    /// Tự động nạp profile khi khởi động màn hình quản lý cấu hình.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsBusy = true;

        try
        {
            var state = await _profileStorageService.LoadAsync();
            Profiles.Clear();

            foreach (var profile in state.Profiles)
            {
                _tomlConfigService.LoadProfileFromToml(profile);
                Profiles.Add(profile);
            }

            Theme = state.Theme;
            SelectedProfile = Profiles.FirstOrDefault(p => p.GameId == state.SelectedGameId) ?? Profiles.FirstOrDefault();
            StatusMessage = $"Đã nạp {Profiles.Count} profile và tự đồng bộ TOML.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể nạp profile: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshCommandState();
        }
    }

    /// <summary>
    /// Nạp toàn bộ giá trị từ file TOML vào profile đang chọn.
    /// </summary>
    public async Task LoadTomlAsync()
    {
        if (SelectedProfile is null)
            return;

        IsBusy = true;

        try
        {
            _tomlConfigService.LoadProfileFromToml(SelectedProfile);
            StatusMessage = "Đã nạp dữ liệu từ TOML vào profile.";
            await SaveProfilesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nạp TOML thất bại: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshCommandState();
        }
    }

    /// <summary>
    /// Ghi profile đang chọn ra đầy đủ các trường TOML (global + theo game).
    /// </summary>
    public async Task SaveTomlAsync()
    {
        if (SelectedProfile is null)
            return;

        IsBusy = true;

        try
        {
            _tomlConfigService.SaveProfileToToml(SelectedProfile);
            await SaveProfilesAsync();
            StatusMessage = "Đã lưu profile vào tệp TOML.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lưu TOML thất bại: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshCommandState();
        }
    }

    /// <summary>
    /// Lưu trạng thái profile UI để lần mở sau tự nạp lại.
    /// </summary>
    public async Task SaveProfilesAsync()
    {
        var state = new PersistedState
        {
            Theme = Theme,
            SelectedGameId = SelectedProfile?.GameId ?? GameIds.AgeOfEmpires4,
            Profiles = Profiles.ToList()
        };

        await _profileStorageService.SaveAsync(state);
    }

    /// <summary>
    /// Khởi động launcher/game theo profile hiện tại.
    /// </summary>
    private void StartLauncher()
    {
        if (SelectedProfile is null)
            return;

        _ = SaveProfilesAsync();
        _launcherRuntimeService.Start(SelectedProfile);
        RefreshCommandState();
    }

    /// <summary>
    /// Dừng launcher và dọn dẹp tài nguyên.
    /// </summary>
    private async Task StopLauncherAsync()
    {
        if (SelectedProfile is null)
            return;

        IsBusy = true;
        try
        {
            await _launcherRuntimeService.StopAsync(SelectedProfile.GameId, SelectedProfile.AutoStopServer);
        }
        finally
        {
            IsBusy = false;
            RefreshCommandState();
        }
    }

    private bool CanExecuteProfileAction() => !IsBusy && SelectedProfile is not null;

    private bool CanStartLauncher() => !IsBusy && SelectedProfile is not null && !_launcherRuntimeService.IsRunning;

    private bool CanStopLauncher() => !IsBusy && _launcherRuntimeService.IsRunning;

    private void RefreshCommandState()
    {
        LoadTomlCommand.NotifyCanExecuteChanged();
        SaveTomlCommand.NotifyCanExecuteChanged();
        SaveProfilesCommand.NotifyCanExecuteChanged();
        StartLauncherCommand.NotifyCanExecuteChanged();
        StopLauncherCommand.NotifyCanExecuteChanged();
    }
}
