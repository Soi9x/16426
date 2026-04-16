using System.Text.Json;
using AgeLanServer.Common;
using AgeLanServer.WpfConfigManager.Models;

namespace AgeLanServer.WpfConfigManager.Services;

/// <summary>
/// Dịch vụ đọc/ghi profile WPF để lưu trạng thái UI và cấu hình theo từng game.
/// </summary>
public sealed class ProfileStorageService
{
    private const string FileName = "wpf-launcher-profiles.json";

    public string StorageFilePath { get; }

    public ProfileStorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, AppConstants.Name, "wpf-config-manager");
        Directory.CreateDirectory(folder);
        StorageFilePath = Path.Combine(folder, FileName);
    }

    public async Task<PersistedState> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(StorageFilePath))
        {
            return CreateDefaultState();
        }

        await using var stream = File.OpenRead(StorageFilePath);
        var state = await JsonSerializer.DeserializeAsync<PersistedState>(stream, cancellationToken: ct);
        if (state is null || state.Profiles.Count == 0)
        {
            return CreateDefaultState();
        }

        return state;
    }

    public async Task SaveAsync(PersistedState state, CancellationToken ct = default)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        await using var stream = File.Create(StorageFilePath);
        await JsonSerializer.SerializeAsync(stream, state, options, ct);
    }

    private static PersistedState CreateDefaultState()
    {
        var profiles = new List<GameProfile>
        {
            CreateDefaultProfile(GameIds.AgeOfEmpires1, "Age of Empires I"),
            CreateDefaultProfile(GameIds.AgeOfEmpires2, "Age of Empires II"),
            CreateDefaultProfile(GameIds.AgeOfEmpires3, "Age of Empires III"),
            CreateDefaultProfile(GameIds.AgeOfEmpires4, "Age of Empires IV"),
            CreateDefaultProfile(GameIds.AgeOfMythology, "Age of Mythology")
        };

        return new PersistedState
        {
            SelectedGameId = GameIds.AgeOfEmpires4,
            Theme = ThemeMode.Dark,
            Profiles = profiles
        };
    }

    private static GameProfile CreateDefaultProfile(string gameId, string name)
    {
        return new GameProfile
        {
            ProfileName = name,
            GameId = gameId,
            GlobalTomlPath = ResolveDefaultGlobalTomlPath(),
            GameTomlPath = ResolveDefaultGameTomlPath(gameId),
            ClientExecutable = gameId == GameIds.AgeOfMythology ? "steam" : "auto"
        };
    }

    private static string ResolveDefaultGlobalTomlPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "resources", "config.toml"),
            Path.Combine(AppContext.BaseDirectory, "AgeL", "config.toml"),
            Path.Combine(Environment.CurrentDirectory, "resources", "config.toml"),
            Path.Combine(Environment.CurrentDirectory, "AgeL", "config.toml"),
            Path.Combine(Environment.CurrentDirectory, "config.toml")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[^1];
    }

    private static string ResolveDefaultGameTomlPath(string gameId)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "resources", $"config.{gameId}.toml"),
            Path.Combine(AppContext.BaseDirectory, "resources", "config.game.toml"),
            Path.Combine(AppContext.BaseDirectory, "AgeL", "config.game.toml"),
            Path.Combine(Environment.CurrentDirectory, "resources", $"config.{gameId}.toml"),
            Path.Combine(Environment.CurrentDirectory, "resources", "config.game.toml"),
            Path.Combine(Environment.CurrentDirectory, "AgeL", "config.game.toml"),
            Path.Combine(Environment.CurrentDirectory, $"config.{gameId}.toml")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[^1];
    }
}

public sealed class PersistedState
{
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public string SelectedGameId { get; set; } = GameIds.AgeOfEmpires4;
    public List<GameProfile> Profiles { get; set; } = new();
}
