using System.Runtime.InteropServices;

namespace AgeLanServer.LauncherCommon;

/// <summary>
/// Quản lý dữ liệu người dùng game (metadata, profiles): backup, restore, isolation.
/// Tương đương launcher-common/userData/ trong bản Go gốc.
/// </summary>
public static class UserDataManager
{
    private const string LanSuffix = ".lan";
    private const string BakSuffix = ".bak";

    /// <summary>
    /// Thông tin đường dẫn dữ liệu user.
    /// </summary>
    public record UserDataPath
    {
        public string GameId { get; init; } = string.Empty;
        public string BasePath { get; init; } = string.Empty;
        public string ActivePath { get; init; } = string.Empty;
        public string LanPath { get; init; } = string.Empty;
        public string BackupPath { get; init; } = string.Empty;

        /// <summary>Loại: metadata hay profile.</summary>
        public string Type { get; init; } = string.Empty;
    }

    /// <summary>
    /// Lấy đường dẫn cơ sở cho dữ liệu game theo hệ điều hành.
    /// </summary>
    public static string GetBasePath(string gameId)
    {
        if (OperatingSystem.IsWindows())
        {
            // AoE IV dùng Documents, các game khác dùng USERPROFILE\Games
            if (gameId == "age4")
            {
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documents, "My Games", "Age of Empires IV");
            }

            var userProfile = Environment.GetEnvironmentVariable("USERPROFILE")
                              ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Games");
        }

        // Linux: dùng thư mục Steam compatdata
        var home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".steam", "steam", "steamapps", "compatdata");
    }

    /// <summary>
    /// Lấy đường dẫn metadata của game.
    /// </summary>
    public static UserDataPath GetMetadataPath(string gameId)
    {
        var basePath = GetBasePath(gameId);
        var gameFolder = Path.Combine(basePath, gameId);

        return new UserDataPath
        {
            GameId = gameId,
            BasePath = gameFolder,
            ActivePath = Path.Combine(gameFolder, "metadata"),
            LanPath = Path.Combine(gameFolder, "metadata" + LanSuffix),
            BackupPath = Path.Combine(gameFolder, "metadata" + BakSuffix),
            Type = "metadata"
        };
    }

    /// <summary>
    /// Lấy đường dẫn profile của game.
    /// </summary>
    public static List<UserDataPath> GetProfilePaths(string gameId)
    {
        var basePath = GetBasePath(gameId);
        var usersPath = Path.Combine(basePath, gameId, "Users");
        var profiles = new List<UserDataPath>();

        if (Directory.Exists(usersPath))
        {
            foreach (var userId in Directory.EnumerateDirectories(usersPath))
            {
                var userIdName = Path.GetFileName(userId);
                profiles.Add(new UserDataPath
                {
                    GameId = gameId,
                    BasePath = userId,
                    ActivePath = userId,
                    LanPath = userId + LanSuffix,
                    BackupPath = userId + BakSuffix,
                    Type = "profile"
                });
            }
        }

        return profiles;
    }

    /// <summary>
    /// Backup dữ liệu: đổi tên active → .bak, .lan → active.
    /// Nếu .lan không tồn tại, tạo thư mục trống.
    /// </summary>
    public static void BackupUserData(UserDataPath dataPath)
    {
        // Backup active → .bak
        if (Directory.Exists(dataPath.ActivePath) && !Directory.Exists(dataPath.BackupPath))
        {
            Directory.Move(dataPath.ActivePath, dataPath.BackupPath);
        }

        // .lan → active
        if (Directory.Exists(dataPath.LanPath))
        {
            if (Directory.Exists(dataPath.ActivePath))
                Directory.Delete(dataPath.ActivePath, true);
            Directory.Move(dataPath.LanPath, dataPath.ActivePath);
        }
        else
        {
            // Tạo thư mục .lan trống nếu chưa có
            Directory.CreateDirectory(dataPath.LanPath);
        }
    }

    /// <summary>
    /// Khôi phục dữ liệu: active → .lan, .bak → active.
    /// </summary>
    public static void RestoreUserData(UserDataPath dataPath)
    {
        // active → .lan
        if (Directory.Exists(dataPath.ActivePath))
        {
            if (Directory.Exists(dataPath.LanPath))
                Directory.Delete(dataPath.LanPath, true);
            Directory.Move(dataPath.ActivePath, dataPath.LanPath);
        }

        // .bak → active
        if (Directory.Exists(dataPath.BackupPath))
        {
            if (Directory.Exists(dataPath.ActivePath))
                Directory.Delete(dataPath.ActivePath, true);
            Directory.Move(dataPath.BackupPath, dataPath.ActivePath);
        }
    }

    /// <summary>
    /// Backup toàn bộ metadata và profiles của game.
    /// </summary>
    public static void BackupAllUserData(string gameId)
    {
        var metadata = GetMetadataPath(gameId);
        if (Directory.Exists(metadata.BasePath))
            BackupUserData(metadata);

        var profiles = GetProfilePaths(gameId);
        foreach (var profile in profiles)
            BackupUserData(profile);
    }

    /// <summary>
    /// Khôi phục toàn bộ metadata và profiles của game.
    /// </summary>
    public static void RestoreAllUserData(string gameId)
    {
        var metadata = GetMetadataPath(gameId);
        if (Directory.Exists(metadata.BasePath))
            RestoreUserData(metadata);

        var profiles = GetProfilePaths(gameId);
        foreach (var profile in profiles)
            RestoreUserData(profile);
    }
}
