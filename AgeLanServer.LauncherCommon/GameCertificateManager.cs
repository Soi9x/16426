using AgeLanServer.Common;

namespace AgeLanServer.LauncherCommon;

/// <summary>
/// Quản lý chứng chỉ CA trong thư mục game (cacert.pem).
/// Tương đương launcher-common/cert/ca.go và wrapper trong bản Go gốc.
/// </summary>
public static class GameCertificateManager
{
    /// <summary>
    /// Đường dẫn tới file CA cert của game.
    /// </summary>
    public record GameCaCertPath
    {
        public string OriginalPath { get; init; } = string.Empty;
        public string BackupPath { get; init; } = string.Empty;
        public string TmpPath { get; init; } = string.Empty;
    }

    /// <summary>
    /// Lấy đường dẫn CA cert cho game.
    /// Chỉ hỗ trợ AoE2, AoE3, AoM (không phải AoE1, AoE4).
    /// </summary>
    public static GameCaCertPath? GetGameCaCertPath(string gameId)
    {
        // Xác định thư mục game dựa trên platform
        string? gameBasePath = null;

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";

            // Thử tìm trong thư mục cài đặt phổ biến
            var possiblePaths = new[]
            {
                Path.Combine(programFilesX86, "Steam", "steamapps", "common"),
                Path.Combine(programFiles, "WindowsApps"), // Xbox
            };

            foreach (var basePath in possiblePaths)
            {
                if (!Directory.Exists(basePath)) continue;

                var gameFolderName = gameId switch
                {
                    "age2" => "AoE2DE",
                    "age3" => "AoE3DE",
                    "athens" => "Age of Mythology Retold",
                    _ => null
                };

                if (gameFolderName != null)
                {
                    var candidate = Path.Combine(basePath, gameFolderName);
                    if (Directory.Exists(candidate))
                    {
                        gameBasePath = candidate;
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(gameBasePath))
            return null;

        var cacertPath = Path.Combine(gameBasePath, "cacert.pem");
        if (!File.Exists(cacertPath))
            return null;

        return new GameCaCertPath
        {
            OriginalPath = cacertPath,
            BackupPath = cacertPath + ".bak",
            TmpPath = cacertPath + ".tmp"
        };
    }

    /// <summary>
    /// Backup file CA cert gốc thành .bak.
    /// </summary>
    public static void BackupCaCertificate(string gameId)
    {
        var path = GetGameCaCertPath(gameId);
        if (path == null)
            return;

        if (File.Exists(path.OriginalPath) && !File.Exists(path.BackupPath))
        {
            File.Copy(path.OriginalPath, path.BackupPath);
        }
    }

    /// <summary>
    /// Thêm chứng chỉ vào file CA cert của game.
    /// Kiểm tra xem cert đã tồn tại chưa trước khi thêm.
    /// </summary>
    public static async Task AppendCaCertificateAsync(string gameId, string certPem, CancellationToken ct = default)
    {
        var path = GetGameCaCertPath(gameId);
        if (path == null)
            return;

        await Task.Run(() =>
        {
            // Backup trước nếu chưa backup
            if (!File.Exists(path.BackupPath))
                File.Copy(path.OriginalPath, path.BackupPath);

            // Đọc nội dung hiện tại
            var content = File.ReadAllText(path.OriginalPath);

            // Kiểm tra cert đã tồn tại chưa (so sánh nội dung)
            var trimmedCert = certPem.Trim();
            if (content.Contains(trimmedCert))
                return; // Đã tồn tại

            // Thêm cert mới
            content = content.TrimEnd() + Environment.NewLine + trimmedCert + Environment.NewLine;
            File.WriteAllText(path.OriginalPath, content);
        }, ct);
    }

    /// <summary>
    /// Khôi phục CA cert từ bản backup.
    /// </summary>
    public static async Task RestoreCaCertificateAsync(string gameId, CancellationToken ct = default)
    {
        var path = GetGameCaCertPath(gameId);
        if (path == null)
            return;

        await Task.Run(() =>
        {
            if (File.Exists(path.BackupPath))
            {
                File.Copy(path.BackupPath, path.OriginalPath, overwrite: true);
                File.Delete(path.BackupPath);
            }

            if (File.Exists(path.TmpPath))
                File.Delete(path.TmpPath);
        }, ct);
    }

    /// <summary>
    /// Kiểm tra xem game có hỗ trợ tùy chỉnh CA cert không.
    /// </summary>
    public static bool SupportsCaCertModification(string gameId)
    {
        return gameId is "age2" or "age3" or "athens";
    }
}
