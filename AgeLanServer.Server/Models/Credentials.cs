using System.Security.Cryptography;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Quản lý thông tin xác thực (credentials).
/// Sử dụng BaseSessions với thời gian hết hạn 5 phút.
/// </summary>
public static class CredentialsManager
{
    private static readonly TimeSpan CredentialsExpiry = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Tạo chữ ký ngẫu nhiên dùng SHA-256.
    /// </summary>
    public static string GenerateSignature()
    {
        var b = new byte[32];
        Random.Shared.NextBytes(b);
        var hash = SHA256.HashData(b);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Tạo mới đối tượng quản lý credentials.
    /// </summary>
    public static BaseSessions<string, string?> NewCredentials()
    {
        return new BaseSessions<string, string?>(CredentialsExpiry);
    }

    /// <summary>
    /// Tạo credential mới với key cho trước.
    /// </summary>
    public static SessionEntry<string, string?> CreateCredential(BaseSessions<string, string?> creds, string? key)
    {
        return creds.CreateSession(GenerateSignature, key);
    }
}
