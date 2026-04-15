using AgeLanServer.Common;

namespace AgeLanServer.LauncherCommon;

/// <summary>
/// Mã lỗi riêng của module Launcher-Common.
/// </summary>
public static class LauncherErrorCodes
{
    /// <summary>Thành công.</summary>
    public const int Success = ErrorCodes.Success;

    /// <summary>Không có quyền quản trị.</summary>
    public const int NotAdmin = ErrorCodes.Last + 1;

    /// <summary>Game ID không hợp lệ.</summary>
    public const int InvalidGame = ErrorCodes.Last + 2;

    // Các mã lỗi riêng của launcher-agent (tương đương launcher-agent/internal/errors.go).
    /// <summary>Timeout khi chờ game khởi động.</summary>
    public const int GameTimeoutStart = ErrorCodes.Last + 3;

    /// <summary>Timeout khi chờ battle server khởi động.</summary>
    public const int BattleServerTimeoutStart = ErrorCodes.Last + 4;

    /// <summary>Không thể dừng server.</summary>
    public const int FailedStopServer = ErrorCodes.Last + 5;

    /// <summary>Không thể chờ tiến trình game kết thúc.</summary>
    public const int FailedWaitForProcess = ErrorCodes.Last + 6;
}

/// <summary>
/// Lệnh revert cấu hình.
/// </summary>
public static class ConfigRevertCommands
{
    /// <summary>Đảo ngược cấu hình.</summary>
    public const string Revert = "revert";
}
