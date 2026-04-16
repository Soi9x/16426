namespace AgeLanServer.Common;

/// <summary>
/// Chứa các hằng số chung cho toàn bộ hệ thống Age LAN Server.
/// </summary>
public static class AppConstants
{
    /// <summary>Tên ứng dụng.</summary>
    public const string Name = "ageLANServer";

    /// <summary>Tên file chứng chỉ mặc định.</summary>
    public const string Cert = "cert.pem";

    /// <summary>Tên file khóa riêng mặc định.</summary>
    public const string Key = "key.pem";

    /// <summary>Tên file chứng chỉ CA.</summary>
    public const string CaCert = "cacert.pem";

    /// <summary>Tên file chứng chỉ tự ký.</summary>
    public const string SelfSignedCert = "selfsigned_cert.pem";

    /// <summary>Tên file khóa riêng tự ký.</summary>
    public const string SelfSignedKey = "selfsigned_key.pem";

    /// <summary>Tổ chức chủ thể chứng chỉ.</summary>
    public const string CertSubjectOrganization = "github.com/luskaner/" + Name;

    /// <summary>Thông số kỹ thuật cho yêu cầu PID file (8 bytes PID + 8 bytes StartTime).</summary>
    public const int PidFileSize = 16;

    /// <summary>Cổng thông báo UDP mặc định.</summary>
    public const int AnnouncePort = 31978;

    /// <summary>Địa chỉ multicast cho khám phá server.</summary>
    public const string AnnounceMulticastGroup = "239.31.97.8";

    /// <summary>Header thông báo.</summary>
    public const string AnnounceHeader = Name;

    /// <summary>Header chứa ID server.</summary>
    public const string IdHeader = "X-Id";

    /// <summary>Header chứa phiên bản.</summary>
    public const string VersionHeader = "X-Version";

    /// <summary>Game ID: Age of Empires 1.</summary>
    public const string GameAoE1 = GameIds.AgeOfEmpires1;

    /// <summary>Game ID: Age of Empires 2.</summary>
    public const string GameAoE2 = GameIds.AgeOfEmpires2;

    /// <summary>Game ID: Age of Empires 3.</summary>
    public const string GameAoE3 = GameIds.AgeOfEmpires3;

    /// <summary>Game ID: Age of Empires 4.</summary>
    public const string GameAoE4 = GameIds.AgeOfEmpires4;

    /// <summary>Game ID: Age of Mythology.</summary>
    public const string GameAoM = GameIds.AgeOfMythology;

    /// <summary>Thư mục resources.</summary>
    public const string ResourcesDir = "resources";

    /// <summary>Thư mục configs.</summary>
    public const string ConfigsPath = "configs";

    /// <summary>Có sinh Platform User ID tự động.</summary>
    public const bool GeneratePlatformUserId = false;
}
