namespace AgeLanServer.Server.Models;

/// <summary>
/// Dữ liệu mặc định cho thông tin xác thực (auth).
/// </summary>
public class AuthUpgradableDefaultData : IUpgradableDefaultData<DateTime?>
{
    public DateTime? Default() => null;
}
