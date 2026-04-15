// Port từ server/internal/domain.go
/// Tiện ích phân tích tên miền (subdomain, main domain, TLD).

namespace AgeLanServer.Server.Internal;

/// <summary>
/// Kết quả phân tích tên miền.
/// </summary>
public record DomainParts
{
    /// <summary>Subdomain (có thể rỗng).</summary>
    public string Subdomain { get; init; } = string.Empty;

    /// <summary>Tên miền chính (không có TLD).</summary>
    public string MainDomain { get; init; } = string.Empty;

    /// <summary>TLD (Top-Level Domain).</summary>
    public string Tld { get; init; } = string.Empty;
}

/// <summary>
/// Tiện ích phân tích tên miền.
/// Lưu ý: Bản Go dùng publicsuffix.EffectiveTLDPlusOne để xử lý TLD phức tạp
/// (như .co.uk). Bản C# này dùng cách đơn giản hơn vì không có thư viện tương đương built-in.
/// </summary>
public static class DomainHelpers
{
    /// <summary>
    /// Phân tích tên miền thành subdomain, main domain, TLD.
    /// Ví dụ: "sub.example.com" -> ("sub", "example", "com")
    /// </summary>
    /// <param name="domain">Tên miền cần phân tích.</param>
    /// <returns>Kết quả phân tích.</returns>
    /// <exception cref="ArgumentException">Nếu tên miền không hợp lệ.</exception>
    public static DomainParts SplitDomain(string domain)
    {
        var lowerDomain = domain.ToLowerInvariant();

        // Tách tên miền thành các phần
        var parts = lowerDomain.Split('.');
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Tên miền không hợp lệ: {domain}");
        }

        // Xử lý các TLD phức tạp đơn giản (có thể mở rộng)
        // Ví dụ: .co.uk, .com.vn -> lấy 2 phần cuối
        string tld;
        string mainDomain;
        string subdomain;

        // Danh sách TLD phức tạp phổ biến
        var complexTlds = new HashSet<string>
        {
            "co.uk", "com.uk", "org.uk",
            "co.jp", "com.au", "com.br",
            "com.vn", "co.in", "co.th",
            "co.id", "com.ph", "com.sg",
            "com.my", "com.tw", "co.nz"
        };

        // Kiểm tra TLD phức tạp
        if (parts.Length >= 3)
        {
            var lastTwo = $"{parts[^2]}.{parts[^1]}";
            if (complexTlds.Contains(lastTwo))
            {
                tld = lastTwo;
                mainDomain = parts[^3];
                subdomain = parts.Length > 3
                    ? string.Join(".", parts[..^3])
                    : string.Empty;
                return new DomainParts
                {
                    Subdomain = subdomain,
                    MainDomain = mainDomain,
                    Tld = tld
                };
            }
        }

        // Trường hợp thông thường: lấy phần cuối làm TLD
        tld = parts[^1];
        mainDomain = parts[^2];
        subdomain = parts.Length > 2
            ? string.Join(".", parts[..^2])
            : string.Empty;

        return new DomainParts
        {
            Subdomain = subdomain,
            MainDomain = mainDomain,
            Tld = tld
        };
    }
}
