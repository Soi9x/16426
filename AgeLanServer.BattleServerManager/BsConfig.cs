namespace AgeLanServer.BattleServerManager;

/// <summary>
/// Cấu hình đường dẫn thực thi cho Battle Server.
/// </summary>
public sealed class Executable
{
    /// <summary>
    /// Đường dẫn tới file thực thi Battle Server.
    /// Giá trị "auto" sẽ tự động tìm trong Steam hoặc Xbox.
    /// </summary>
    public string Path { get; set; } = "auto";

    /// <summary>
    /// Các tham số bổ sung khi khởi chạy Battle Server.
    /// </summary>
    public string[] ExtraArgs { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Cấu hình cổng mạng cho Battle Server.
/// </summary>
public sealed class Ports
{
    /// <summary>
    /// Cổng Battle Server chính. 0 = tự động sinh.
    /// </summary>
    public int Bs { get; set; }

    /// <summary>
    /// Cổng WebSocket. 0 = tự động sinh.
    /// </summary>
    public int WebSocket { get; set; }

    /// <summary>
    /// Cổng Out-of-Band (không dùng cho AoE1). 0 = tự động sinh.
    /// </summary>
    public int OutOfBand { get; set; }
}

/// <summary>
/// Cấu hình SSL cho Battle Server.
/// </summary>
public sealed class Ssl
{
    /// <summary>
    /// Nếu true, tự động tìm chứng chỉ SSL từ game.
    /// </summary>
    public bool Auto { get; set; } = true;

    /// <summary>
    /// Đường dẫn tới file chứng chỉ SSL (khi Auto = false).
    /// </summary>
    public string CertFile { get; set; } = string.Empty;

    /// <summary>
    /// Đường dẫn tới file khóa SSL (khi Auto = false).
    /// </summary>
    public string KeyFile { get; set; } = string.Empty;
}

/// <summary>
/// Cấu hình đầy đủ cho một Battle Server.
/// Tương ứng với internal.Configuration trong Go.
/// </summary>
public sealed class Configuration
{
    /// <summary>
    /// Khu vực máy chủ. Giá trị "auto" sẽ tự động sinh.
    /// </summary>
    public string Region { get; set; } = "auto";

    /// <summary>
    /// Tên máy chủ. Giá trị "auto" sẽ tự động sinh.
    /// </summary>
    public string Name { get; set; } = "auto";

    /// <summary>
    /// Địa chỉ host. Giá trị "auto" sẽ tự động chọn IP phù hợp.
    /// </summary>
    public string Host { get; set; } = "auto";

    /// <summary>
    /// Cấu hình đường dẫn thực thi.
    /// </summary>
    public Executable Executable { get; set; } = new();

    /// <summary>
    /// Cấu hình cổng mạng.
    /// </summary>
    public Ports Ports { get; set; } = new();

    /// <summary>
    /// Cấu hình SSL.
    /// </summary>
    public Ssl SSL { get; set; } = new();
}
