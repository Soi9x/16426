// Port từ common/game/appx/ (2 file: appx_windows.go, appx_other.go)
// Phát hiện game cài đặt từ Microsoft Store (APPX/MSIX).
// Chỉ hoạt động trên Windows, sử dụng API FindPackagesByPackageFamily và registry.

using System.Runtime.InteropServices;
using System.Text;
using AgeLanServer.Common;

namespace AgeLanServer.Common;

/// <summary>
/// Tiện ích phát hiện game cài đặt từ Microsoft Store (APPX/MSIX).
/// Sử dụng API Windows FindPackagesByPackageFamily và registry.
/// Chỉ hoạt động trên Windows.
/// </summary>
public static class AppxDetection
{
    private const string AppNamePrefix = "Microsoft.";
    private const string AppPublisherId = "8wekyb3d8bbwe";

    #region Windows API Imports

    // FindPackagesByPackageFamily từ kernelbase.dll
    // Lưu ý: API này trả về qua các tham số out, cần cấp phát bộ nhớ không an toàn.
    [DllImport("kernelbase.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint FindPackagesByPackageFamily(
        [MarshalAs(UnmanagedType.LPWStr)] string packageFamilyName,
        uint packageFilters,
        out uint count,
        IntPtr packageFullNames,
        out uint bufferLength,
        IntPtr buffer,
        IntPtr packageProperties,
        IntPtr reserved);

    private const uint PackageFilterHead = 0x10;
    private const uint ErrorSuccess = 0;
    private const uint ErrorInsufficientBuffer = 122;

    #endregion

    /// <summary>
    /// Trả về hậu tố tên app cho game title.
    /// </summary>
    private static string AppNameSuffix(string gameTitle)
    {
        return gameTitle switch
        {
            AppConstants.GameAoE1 => "Darwin",
            AppConstants.GameAoE2 => "MSPhoenix",
            AppConstants.GameAoE3 => "MSGPBoston",
            AppConstants.GameAoE4 => "Cardinal",
            // FIXME: Thêm AoM
            _ => string.Empty
        };
    }

    /// <summary>
    /// Tên package không có publisher.
    /// Ví dụ: Microsoft.Cardinal
    /// </summary>
    private static string GetPackageName(string gameTitle)
    {
        var suffix = AppNameSuffix(gameTitle);
        return string.IsNullOrEmpty(suffix) ? string.Empty : $"{AppNamePrefix}{suffix}";
    }

    /// <summary>
    /// Package Family Name (tên ngắn gọn với publisher ID).
    /// Ví dụ: Microsoft.Cardinal_8wekyb3d8bbwe
    /// </summary>
    public static string GetFamilyName(string gameTitle)
    {
        var name = GetPackageName(gameTitle);
        return string.IsNullOrEmpty(name) ? string.Empty : $"{name}_{AppPublisherId}";
    }

    /// <summary>
    /// Kiểm tra xem API APPX có khả dụng không (chỉ Windows 8+).
    /// </summary>
    private static bool IsAppxApiAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            // Thử gọi API để kiểm tra availability
            uint count = 0;
            uint bufferLength = 0;
            var result = FindPackagesByPackageFamily(
                "test",
                PackageFilterHead,
                out count,
                IntPtr.Zero,
                out bufferLength,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);
            // Nếu gọi được (kể cả lỗi) thì API có sẵn
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Chuyển Package Family Name sang Package Full Name.
    /// Ví dụ: Microsoft.Cardinal_8wekyb3d8bbwe => Microsoft.Cardinal_1.0.0.0_x64__8wekyb3d8bbwe
    /// </summary>
    public static (bool Ok, string FullName) PackageFamilyNameToFullName(string packageFamilyName)
    {
        if (!IsAppxApiAvailable())
            return (false, string.Empty);

        if (string.IsNullOrEmpty(packageFamilyName))
            return (false, string.Empty);

        try
        {
            // Lần gọi đầu để lấy kích thước buffer cần thiết
            var result = FindPackagesByPackageFamily(
                packageFamilyName,
                PackageFilterHead,
                out uint count,
                IntPtr.Zero,
                out uint bufferLength,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (result != ErrorInsufficientBuffer || count == 0 || bufferLength == 0)
                return (false, string.Empty);

            // Cấp phát bộ nhớ cho con trỏ và buffer
            var fullNamesSize = (int)(count * (uint)IntPtr.Size);
            var fullNamesPtr = Marshal.AllocHGlobal(fullNamesSize);
            var buffer = Marshal.AllocHGlobal((int)bufferLength * 2); // UTF-16

            try
            {
                result = FindPackagesByPackageFamily(
                    packageFamilyName,
                    PackageFilterHead,
                    out count,
                    fullNamesPtr,
                    out bufferLength,
                    buffer,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (result == ErrorSuccess && count > 0)
                {
                    // Đọc con trỏ đầu tiên từ mảng
                    var firstNamePtr = Marshal.ReadIntPtr(fullNamesPtr);
                    if (firstNamePtr != IntPtr.Zero)
                    {
                        var fullName = Marshal.PtrToStringUni(firstNamePtr);
                        if (!string.IsNullOrEmpty(fullName))
                            return (true, fullName);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(fullNamesPtr);
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            // Bỏ qua lỗi
        }

        return (false, string.Empty);
    }

    /// <summary>
    /// Lấy đường dẫn cài đặt từ Package Full Name thông qua registry.
    /// </summary>
    public static (bool Ok, string InstallLocation) GetInstallLocation(string packageFullName)
    {
        if (string.IsNullOrEmpty(packageFullName))
            return (false, string.Empty);

        try
        {
            var regPath = $@"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages\{packageFullName}";
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regPath);
            if (key == null)
                return (false, string.Empty);

            var location = key.GetValue("PackageRootFolder") as string;
            if (string.IsNullOrEmpty(location))
                return (false, string.Empty);

            return (true, location);
        }
        catch
        {
            return (false, string.Empty);
        }
    }

    /// <summary>
    /// Lấy đường dẫn thư mục Game của game cài từ Microsoft Store.
    /// Trả về (false, "") nếu không tìm thấy hoặc không phải Windows.
    /// </summary>
    public static (bool Ok, string GameLocation) GetGameInstallLocation(string gameTitle)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return (false, string.Empty);

        var familyName = GetFamilyName(gameTitle);
        if (string.IsNullOrEmpty(familyName))
            return (false, string.Empty);

        // Chuyển family name sang full name
        var (ok, fullName) = PackageFamilyNameToFullName(familyName);
        if (!ok)
            return (false, string.Empty);

        // Lấy đường dẫn cài đặt
        var (ok2, installLocation) = GetInstallLocation(fullName);
        if (!ok2)
            return (false, string.Empty);

        var gameLocation = Path.Combine(installLocation, "Game");
        if (!Directory.Exists(gameLocation))
            return (false, string.Empty);

        return (true, gameLocation);
    }
}
