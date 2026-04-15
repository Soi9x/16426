using AgeLanServer.Common;

namespace AgeLanServer.LauncherCommon;

/// <summary>
/// Các hằng số và kiểu dữ liệu cho giao tiếp IPC giữa các thành phần launcher.
/// Tương đương launcher-common/ipc/ trong bản Go gốc.
/// </summary>
public static class IpcConstants
{
    /// <summary>Action byte: setup cấu hình.</summary>
    public const byte ActionSetup = 0x01;

    /// <summary>Action byte: revert cấu hình.</summary>
    public const byte ActionRevert = 0x02;

    /// <summary>Action byte: thoát agent.</summary>
    public const byte ActionExit = 0x03;

    /// <summary>
    /// Đường dẫn tới named pipe/Unix socket cho IPC.
    /// </summary>
    public static string IpcPath
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                // Named pipe trên Windows với security descriptor theo user
                var userName = Environment.UserName;
                return $@"\\.\pipe\{AppConstants.Name}-launcher-config-admin-agent-{userName}";
            }

            // Unix socket trên Linux/macOS
            var socketName = $"{AppConstants.Name}-launcher-config-admin-agent";
            return Path.Combine(Path.GetTempPath(), socketName);
        }
    }

    /// <summary>
    /// Dữ liệu gửi khi setup.
    /// </summary>
    public record SetupCommandData
    {
        /// <summary>IP cần ánh xạ.</summary>
        public string Ip { get; init; } = string.Empty;

        /// <summary>Chứng chỉ base64 để thêm vào kho tin cậy.</summary>
        public string CertDataBase64 { get; init; } = string.Empty;

        /// <summary>Game ID.</summary>
        public string GameId { get; init; } = string.Empty;

        /// <summary>Danh sách host cần ánh xạ.</summary>
        public string[] Hosts { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Dữ liệu gửi khi revert.
    /// </summary>
    public record RevertCommandData
    {
        /// <summary>Cờ thao tác revert.</summary>
        public int Flags { get; init; }

        /// <summary>Game ID.</summary>
        public string GameId { get; init; } = string.Empty;

        /// <summary>IP cần xóa ánh xạ.</summary>
        public string Ip { get; init; } = string.Empty;

        /// <summary>Chứng chỉ base64 cần xóa.</summary>
        public string CertDataBase64 { get; init; } = string.Empty;

        /// <summary>Xóa toàn bộ cấu hình.</summary>
        public bool RemoveAll { get; init; }
    }
}
