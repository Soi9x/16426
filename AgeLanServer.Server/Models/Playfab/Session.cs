namespace AgeLanServer.Server.Models.Playfab;

/// <summary>
/// Phiên PlayFab.
/// Quản lý phiên đăng nhập qua PlayFab với entity token.
/// Thời gian hết hạn: 24 giờ.
/// </summary>
public class PlayfabSessionData
{
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(24);

    public string PlayfabId { get; set; } = null!;
    public string Token { get; set; } = null!;
    public IUser User { get; set; } = null!;
}

/// <summary>
/// Quản lý phiên PlayFab.
/// </summary>
public class MainPlayfabSessions
{
    private BaseSessions<string, PlayfabSessionData> _baseSessions = null!;

    public void Initialize()
    {
        _baseSessions = new BaseSessions<string, PlayfabSessionData>(TimeSpan.FromHours(24));
    }

    /// <summary>
    /// Tạo phiên với người dùng Steam.
    /// </summary>
    public string? CreateWithSteamUserId(IUsers users, ulong steamUserId)
    {
        if (users.GetUserByPlatformUserId(false, steamUserId) is not { } user)
            return null;
        return Create(user);
    }

    /// <summary>
    /// Tạo phiên với ID người dùng.
    /// </summary>
    public string? CreateWithUserId(IUsers users, int userId)
    {
        if (users.GetUserById(userId) is not { } user)
            return null;
        return Create(user);
    }

    private string Create(IUser user)
    {
        var session = new PlayfabSessionData
        {
            Token = Guid.NewGuid().ToString(),
            User = user
        };
        _baseSessions.CreateSession(GenerateId, session);
        session.PlayfabId = GenerateId();
        return session.PlayfabId;
    }

    public PlayfabSessionData? GetById(string entityToken)
    {
        if (_baseSessions.Get(entityToken, out var entry))
            return entry.Data();
        return null;
    }

    public void ResetExpiry(string entityToken)
    {
        _baseSessions.ResetExpiryTimer(entityToken);
    }

    private static string GenerateId()
    {
        var bytes = new byte[8];
        Random.Shared.NextBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
