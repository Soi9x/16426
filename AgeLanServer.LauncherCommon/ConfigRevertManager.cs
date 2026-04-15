using AgeLanServer.Common;

namespace AgeLanServer.LauncherCommon;

/// <summary>
/// Quản lý lưu trữ và thực thi các thao tác revert (đảo ngược) cấu hình.
/// Tương đương launcher-common/configRevert.go và argsStore.go trong bản Go gốc.
/// </summary>
public static class ConfigRevertManager
{
    /// <summary>
    /// Các cờ revert để theo dõi những gì đã thay đổi và cần đảo ngược.
    /// </summary>
    [Flags]
    public enum RevertFlags
    {
        None = 0,

        /// <summary>Đã thêm chứng chỉ vào máy.</summary>
        AddLocalCert = 1 << 0,

        /// <summary>Đã ánh xạ IP trong file hosts.</summary>
        MapIP = 1 << 1,

        /// <summary>Đã backup/restore metadata.</summary>
        MetadataBackup = 1 << 2,

        /// <summary>Đã backup/restore profile.</summary>
        ProfileBackup = 1 << 3,

        /// <summary>Đã sửa đổi chứng chỉ CA trong game.</summary>
        GameCaCert = 1 << 4,

        /// <summary>Đã xóa toàn bộ cấu hình revert.</summary>
        RemoveAll = 1 << 5
    }

    /// <summary>
    /// Lưu trữ các tham số revert vào file tạm.
    /// File được dùng để khôi phục trạng thái khi launcher thoát bất ngờ.
    /// </summary>
    public record RevertArgs
    {
        public RevertFlags Flags { get; init; }
        public string GameId { get; init; } = string.Empty;
        public string ServerIp { get; init; } = string.Empty;
        public string CertData { get; init; } = string.Empty; // Base64-encoded cert
        public string BattleServerExe { get; init; } = string.Empty;
        public string BattleServerRegion { get; init; } = string.Empty;
    }

    /// <summary>
    /// Đường dẫn file lưu trữ tham số revert.
    /// </summary>
    private static string RevertFilePath
    {
        get
        {
            var name = $"{AppConstants.Name}_config_revert.txt";
            return Path.Combine(Path.GetTempPath(), name);
        }
    }

    /// <summary>
    /// Lưu tham số revert vào file.
    /// Định dạng: pipe-separated values.
    /// </summary>
    public static void StoreRevertArgs(RevertArgs args)
    {
        var lines = new[]
        {
            $"flags={(int)args.Flags}",
            $"gameId={args.GameId}",
            $"serverIp={args.ServerIp}",
            $"certData={args.CertData}",
            $"battleServerExe={args.BattleServerExe}",
            $"battleServerRegion={args.BattleServerRegion}"
        };

        File.WriteAllLines(RevertFilePath, lines);
    }

    /// <summary>
    /// Đọc tham số revert từ file.
    /// </summary>
    public static RevertArgs? LoadRevertArgs()
    {
        if (!File.Exists(RevertFilePath))
            return null;

        try
        {
            var lines = File.ReadAllLines(RevertFilePath);
            var dict = new Dictionary<string, string>();

            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                    dict[parts[0]] = parts[1];
            }

            return new RevertArgs
            {
                Flags = Enum.TryParse(dict.GetValueOrDefault("flags"), out int f) ? (RevertFlags)f : RevertFlags.None,
                GameId = dict.GetValueOrDefault("gameId", ""),
                ServerIp = dict.GetValueOrDefault("serverIp", ""),
                CertData = dict.GetValueOrDefault("certData", ""),
                BattleServerExe = dict.GetValueOrDefault("battleServerExe", ""),
                BattleServerRegion = dict.GetValueOrDefault("battleServerRegion", "")
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Xóa file lưu trữ tham số revert.
    /// </summary>
    public static void ClearRevertArgs()
    {
        if (File.Exists(RevertFilePath))
            File.Delete(RevertFilePath);
    }

    /// <summary>
    /// Thực thi revert dựa trên các cờ đã lưu.
    /// </summary>
    public static async Task ExecuteRevertAsync(CancellationToken ct = default)
    {
        var args = LoadRevertArgs();
        if (args == null)
            return;

        try
        {
            // Xóa chứng chỉ khỏi máy
            if (args.Flags.HasFlag(RevertFlags.AddLocalCert) && !string.IsNullOrEmpty(args.CertData))
            {
                await CertificateUtilities.UntrustLocalCertificateAsync(args.CertData, ct);
            }

            // Xóa ánh xạ IP khỏi hosts file
            if (args.Flags.HasFlag(RevertFlags.MapIP) && !string.IsNullOrEmpty(args.ServerIp))
            {
                var hosts = GameDomains.GetAllHosts(args.GameId);
                HostsManager.RemoveOwnMappings();
                HostsManager.FlushDnsCache();
            }

            // Khôi phục metadata
            if (args.Flags.HasFlag(RevertFlags.MetadataBackup))
            {
                var metadataPath = UserDataManager.GetMetadataPath(args.GameId);
                if (Directory.Exists(metadataPath.BasePath))
                    UserDataManager.RestoreUserData(metadataPath);
            }

            // Khôi phục profiles
            if (args.Flags.HasFlag(RevertFlags.ProfileBackup))
            {
                var profilePaths = UserDataManager.GetProfilePaths(args.GameId);
                foreach (var profile in profilePaths)
                    UserDataManager.RestoreUserData(profile);
            }

            // Khôi phục CA cert trong game
            if (args.Flags.HasFlag(RevertFlags.GameCaCert))
            {
                await GameCertificateManager.RestoreCaCertificateAsync(args.GameId, ct);
            }

            // Dừng battle server
            if (!string.IsNullOrEmpty(args.BattleServerExe) && !string.IsNullOrEmpty(args.BattleServerRegion))
            {
                RemoveBattleServerRegion(args.BattleServerExe, args.GameId, args.BattleServerRegion);
            }
        }
        finally
        {
            ClearRevertArgs();
        }
    }

    /// <summary>
    /// Xóa cấu hình battle server cho region cụ thể.
    /// </summary>
    public static void RemoveBattleServerRegion(string battleServerExe, string gameId, string region)
    {
        try
        {
            // Dừng tiến trình battle server
            var processName = Path.GetFileNameWithoutExtension(battleServerExe);
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            foreach (var proc in processes)
            {
                proc.Kill();
                proc.WaitForExit(3000);
                proc.Dispose();
            }

            // Xóa file cấu hình
            BattleServerConfigManager.RemoveConfig(gameId, 0); // index 0 là region đầu tiên
        }
        catch
        {
            // Bỏ qua lỗi
        }
    }
}

/// <summary>
/// Lưu trữ tham số dòng lệnh vào file để dùng lại.
/// Tương đương launcher-common/argsStore.go trong bản Go gốc.
/// </summary>
public static class ArgsStore
{
    /// <summary>
    /// Lưu danh sách cờ vào file với định dạng pipe-separated.
    /// </summary>
    public static void Store(string filePath, params string[] args)
    {
        File.WriteAllLines(filePath, args);
    }

    /// <summary>
    /// Đọc danh sách cờ từ file.
    /// </summary>
    public static string[] Load(string filePath)
    {
        if (!File.Exists(filePath))
            return Array.Empty<string>();

        return File.ReadAllLines(filePath);
    }

    /// <summary>
    /// Xóa file lưu trữ.
    /// </summary>
    public static void Clear(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}
