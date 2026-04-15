namespace AgeLanServer.LauncherCommon.Cmd;

/// <summary>
/// Cờ trạng thái cho thao tác Revert (hoàn tác).
/// Chuyển đổi từ Go package: launcher-common/cmd/revert.go
/// </summary>
public class SetupRevertFlags
{
    /// <summary>
    /// Cờ xóa ánh xạ IP khỏi DNS server cục bộ.
    /// Tương đương flag --ip / -i
    /// </summary>
    public bool RemoveIpMappings { get; set; }

    /// <summary>
    /// Cờ xóa chứng chỉ khỏi kho tin cậy của máy cục bộ.
    /// Tương đương flag --localCert / -l
    /// </summary>
    public bool RemoveLocalCert { get; set; }

    /// <summary>
    /// Cờ xóa tất cả cấu hình.
    /// Tương đương với tất cả các flag được đặt mà không fail-fast.
    /// Tương đương flag --all / -a
    /// </summary>
    public bool RemoveAll { get; set; }

    /// <summary>
    /// Kiểm tra xem có cần thực hiện bất kỳ thao tác revert nào không.
    /// </summary>
    public bool HasAnyRevertFlag => RemoveIpMappings || RemoveLocalCert || RemoveAll;

    /// <summary>
    /// Kiểm tra xem có cần revert IP mappings không.
    /// </summary>
    public bool ShouldRevertIp => RemoveAll || RemoveIpMappings;

    /// <summary>
    /// Kiểm tra xem có cần revert certificate không.
    /// </summary>
    public bool ShouldRevertCert => RemoveAll || RemoveLocalCert;
}

/// <summary>
/// Cờ cấu hình cho thao tác Setup.
/// Chuyển đổi từ Go package: launcher-common/cmd/setUp.go
/// </summary>
public class SetupFlags
{
    /// <summary>
    /// Địa chỉ IP cần phân giải trong DNS server cục bộ.
    /// Tương đương flag --ip / -i
    /// </summary>
    public string? MapIp { get; set; }

    /// <summary>
    /// Dữ liệu chứng chỉ đã mã hóa Base64.
    /// Tương đương flag --localCert / -l
    /// </summary>
    public string? AddLocalCertDataB64 { get; set; }

    /// <summary>
    /// Dữ liệu chứng chỉ đã giải mã (từ Base64).
    /// </summary>
    public byte[]? AddLocalCertData { get; private set; }

    /// <summary>
    /// ID của game đang cấu hình.
    /// </summary>
    public string? GameId { get; set; }

    /// <summary>
    /// Giải mã flag AddLocalCertDataB64 sang AddLocalCertData.
    /// </summary>
    /// <returns>True nếu giải mã thành công hoặc không có cert data</returns>
    public bool DecodeSetUpFlags()
    {
        if (!string.IsNullOrEmpty(AddLocalCertDataB64))
        {
            try
            {
                AddLocalCertData = Convert.FromBase64String(AddLocalCertDataB64);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
        return true;
    }
}
