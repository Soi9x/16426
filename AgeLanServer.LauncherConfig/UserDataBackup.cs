namespace AgeLanServer.LauncherConfig;

/// <summary>
/// Cau truc du lieu cho metadata va profile cua nguoi dung.
/// Luu tru duong dan den du lieu active va backup.
/// Tuong ung voi struct Data trong backup.go.
/// </summary>
public record UserDataPath
{
    /// <summary>
    /// Duong dan goc den du lieu active (dang su dung).
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Duong dan den thu muc/file da biet doi (server/isolated).
    /// Kieu: duong_dan_goc_server
    /// </summary>
    public string IsolatedPath => TransformPath(Path, "_active", "_server");

    /// <summary>
    /// Duong dan den thu muc/file sao luu.
    /// Kieu: duong_dan_goc_backup
    /// </summary>
    public string OriginalPath => TransformPath(Path, "_active", "_backup");

    /// <summary>
    /// Chuyen doi duong dan bang cach thay the suffix.
    /// Vi du: "profiles_active" -> "profiles_server" hoac "profiles_backup".
    /// </summary>
    private static string TransformPath(string path, string fromSuffix, string toSuffix)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        // Tim vi tri cua suffix cuoi cung
        int idx = path.LastIndexOf(fromSuffix, StringComparison.Ordinal);
        if (idx == -1)
            return string.Empty;

        return path[..idx] + toSuffix + path[(idx + fromSuffix.Length)..];
    }
}

/// <summary>
/// Lop tien ich sao luu va khoi phuc du lieu nguoi dung (metadata va profiles).
/// Tuong ung voi cac ham trong backup.go, metadataBackup.go, profileBackup.go.
/// </summary>
public static class UserDataBackup
{
    /// <summary>
    /// Hoan doi vi tri giua duong dan hien tai va duong dan backup.
    /// Di chuyen: currentPath -> backupPath, va currentPath <- backup (neu ton tai).
    /// Tuong tu switchPaths trong backup.go.
    /// </summary>
    /// <param name="data">Doi tuong du lieu nguoi dung.</param>
    /// <param name="backupPath">Duong dan backup đích.</param>
    /// <param name="currentPath">Duong dan hien tai đích.</param>
    /// <returns>True neu thanh cong, False neu that bai.</returns>
    public static bool SwitchPaths(UserDataPath data, string backupPath, string currentPath)
    {
        Console.WriteLine($"\tDang hoan doi: {currentPath} <-> {backupPath}");

        // Neu backup da ton tai, khong lam gi
        if (File.Exists(backupPath) || Directory.Exists(backupPath))
            return false;

        string absolutePath = data.Path;
        FileMode mode = FileMode.Open;

        // Neu duong dan goc khong ton tai, tao thu muc cha
        if (!Exists(absolutePath))
        {
            string oldParent = absolutePath;
            string newParent = Path.GetDirectoryName(oldParent)!;
            DirectoryInfo? info = null;

            while (true)
            {
                // Kiem tra den root
                if (Path.GetDirectoryName(newParent) == newParent || string.IsNullOrEmpty(newParent))
                    return false;

                if (Exists(newParent))
                {
                    info = new DirectoryInfo(newParent);
                    Console.WriteLine($"\t\tTao cay thu muc: {absolutePath}");

                    try
                    {
                        if (Directory.Exists(absolutePath) || !Path.HasExtension(absolutePath))
                            Directory.CreateDirectory(absolutePath);
                        else
                            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
                    }
                    catch
                    {
                        return false;
                    }
                    break;
                }

                oldParent = newParent;
                newParent = Path.GetDirectoryName(newParent)!;
            }

            if (info != null)
                mode = info.Attributes.HasFlag(FileAttributes.Directory) ? FileMode.Open : FileMode.Open;
        }
        else if (!Exists(absolutePath))
        {
            return false;
        }

        // Di chuyen absolutePath -> backupPath
        Console.WriteLine($"\t\tDi chuyen {absolutePath} -> {backupPath}");
        try
        {
            Move(absolutePath, backupPath);
        }
        catch
        {
            return false;
        }

        // Danh sach ham dao nguoc de rollback neu loi
        var revertActions = new List<Action>();
        revertActions.Add(() =>
        {
            Console.WriteLine($"\t\tDao nguoc: di chuyen {backupPath} -> {absolutePath}");
            try { Move(backupPath, absolutePath); } catch { /* bo qua */ }
        });

        try
        {
            // Neu currentPath khong ton tai, tao thu muc
            if (!Exists(currentPath))
            {
                try
                {
                    if (!Path.HasExtension(currentPath))
                        Directory.CreateDirectory(currentPath);
                    else
                        Directory.CreateDirectory(Path.GetDirectoryName(currentPath)!);
                }
                catch
                {
                    ExecuteRevert(revertActions);
                    return false;
                }
            }

            // Di chuyen currentPath -> absolutePath
            Console.WriteLine($"\t\tDi chuyen {currentPath} -> {absolutePath}");
            Move(currentPath, absolutePath);

            // Thanh cong - huy revert
            return true;
        }
        catch
        {
            ExecuteRevert(revertActions);
            return false;
        }
    }

    /// <summary>
    /// Thuc thi danh sach dao nguoc theo thu tu nguoc.
    /// </summary>
    private static void ExecuteRevert(List<Action> revertActions)
    {
        for (int i = revertActions.Count - 1; i >= 0; i--)
        {
            revertActions[i]();
        }
    }

    /// <summary>
    /// Sao luu du lieu: di chuyen active -> backup.
    /// </summary>
    public static bool Backup(UserDataPath data)
    {
        return SwitchPaths(data, data.OriginalPath, data.IsolatedPath);
    }

    /// <summary>
    /// Khoi phuc du lieu: di chuyen isolated -> active.
    /// </summary>
    public static bool Restore(UserDataPath data)
    {
        return SwitchPaths(data, data.IsolatedPath, data.OriginalPath);
    }

    /// <summary>
    /// Kiem tra file hoac thu muc co ton tai khong.
    /// </summary>
    private static bool Exists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    /// <summary>
    /// Di chuyen file hoac thu muc.
    /// </summary>
    private static void Move(string source, string destination)
    {
        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
        }
        else if (File.Exists(source))
        {
            File.Move(source, destination);
        }
        else
        {
            throw new FileNotFoundException($"Khong tim thay: {source}");
        }
    }
}

/// <summary>
/// Quan ly sao luu va khoi phuc profiles cua game.
/// Tuong tu profileBackup.go.
/// </summary>
public static class ProfileBackupManager
{
    private static List<UserDataPath> _profiles = new();

    /// <summary>
    /// Thiet lap danh sach profiles cho game chi dinh.
    /// Chi lay cac profiles co kieu TypeActive.
    /// </summary>
    /// <param name="gameId">Dinh danh game.</param>
    /// <returns>True neu thanh cong.</returns>
    public static bool SetProfileData(string gameId)
    {
        _profiles = GetProfilesForGame(gameId);
        return _profiles.Count > 0;
    }

    /// <summary>
    /// Lay danh sach profiles cho game (gia lap - trong thuc te can truy van tu commonUserData).
    /// </summary>
    private static List<UserDataPath> GetProfilesForGame(string gameId)
    {
        // Trong implement day du, can goi commonUserData.Profiles(gameId)
        // O day tra ve danh sach rong - can duoc cap nhat tuy theo game
        return gameId switch
        {
            "aoe2de" => new List<UserDataPath>
            {
                new() { Path = GetAoE2DEProfilePath() }
            },
            "aoe3de" => new List<UserDataPath>
            {
                new() { Path = GetAoE3DEProfilePath() }
            },
            "aoe4" => new List<UserDataPath>
            {
                new() { Path = GetAoE4ProfilePath() }
            },
            _ => new List<UserDataPath>()
        };
    }

    // Cac duong dan profile mac dinh cho tung game
    private static string GetAoE2DEProfilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Age2DE", "profiles");
    }

    private static string GetAoE3DEProfilePath()
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "Games", "Age of Empires 3 DE", "users", "profile");
    }

    private static string GetAoE4ProfilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Relic", "AgeOfEmpires4");
    }

    /// <summary>
    /// Chay phuong thuc chinh tren tat ca profiles, neu that bai thi chay phuong thuc don tren cac profiles da thanh cong.
    /// </summary>
    /// <param name="gameId">Dinh danh game.</param>
    /// <param name="mainMethod">Phuong thuc chinh (backup hoac restore).</param>
    /// <param name="cleanMethod">Phuong thuc don (dao nguoc).</param>
    /// <param name="stopOnFailed">Dung ngay khi that bai hay tiep tuc.</param>
    private static bool RunProfileMethod(
        string gameId,
        Func<UserDataPath, bool> mainMethod,
        Func<UserDataPath, bool> cleanMethod,
        bool stopOnFailed)
    {
        if (!SetProfileData(gameId))
            return false;

        for (int i = 0; i < _profiles.Count; i++)
        {
            if (!mainMethod(_profiles[i]))
            {
                if (!stopOnFailed)
                    continue;

                // Dao nguoc cac profiles da thanh cong tru do
                for (int j = i - 1; j >= 0; j--)
                {
                    cleanMethod(_profiles[j]);
                }
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Sao luu tat ca profiles cua game.
    /// Dung ngay khi that bai (stopOnFailed = true).
    /// </summary>
    public static bool BackupProfiles(string gameId)
    {
        return RunProfileMethod(gameId, BackupSingleProfile, RestoreSingleProfile, true);
    }

    /// <summary>
    /// Khoi phuc tat ca profiles cua game.
    /// Neu reverseFailed = true, se dao nguoc khi that bai.
    /// </summary>
    public static bool RestoreProfiles(string gameId, bool reverseFailed)
    {
        return RunProfileMethod(gameId, RestoreSingleProfile, BackupSingleProfile, reverseFailed);
    }

    private static bool BackupSingleProfile(UserDataPath data) => data.Path.Length > 0 && UserDataBackup.Backup(data);
    private static bool RestoreSingleProfile(UserDataPath data) => data.Path.Length > 0 && UserDataBackup.Restore(data);
}

/// <summary>
/// Quan ly sao luu va khoi phuc metadata cua game.
/// Tuong tu metadataBackup.go.
/// </summary>
public static class MetadataBackupManager
{
    /// <summary>
    /// Lay du lieu metadata cho game chi dinh.
    /// Chi lay metadata co kieu TypeActive.
    /// </summary>
    /// <param name="gameId">Dinh danh game.</param>
    /// <returns>Doi tuong UserDataPath cho metadata.</returns>
    public static UserDataPath GetMetadata(string gameId)
    {
        return gameId switch
        {
            "aoe2de" => new UserDataPath { Path = GetAoE2DEMetadataPath() },
            "aoe3de" => new UserDataPath { Path = GetAoE3DEMetadataPath() },
            "aoe4" => new UserDataPath { Path = GetAoE4MetadataPath() },
            _ => new UserDataPath { Path = string.Empty }
        };
    }

    private static string GetAoE2DEMetadataPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Age2DE");
    }

    private static string GetAoE3DEMetadataPath()
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "Games", "Age of Empires 3 DE");
    }

    private static string GetAoE4MetadataPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Relic", "AgeOfEmpires4");
    }
}
