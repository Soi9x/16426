// Port từ server/internal/models/playfab/game.go
/// Giao diện Game mở rộng cho PlayFab, thêm quản lý phiên PlayFab.

using AgeLanServer.Server.Models;

namespace AgeLanServer.Server.Models.Playfab;

/// <summary>
/// Giao diện Game mở rộng từ IGame, bổ sung quản lý phiên PlayFab.
/// </summary>
public interface IPlayfabGame : IGame
{
    /// <summary>
    /// Trả về đối tượng quản lý các phiên PlayFab.
    /// </summary>
    MainPlayfabSessions PlayfabSessions { get; }
}

/// <summary>
/// Lớp cơ sở triển khai IPlayfabGame.
/// Kế thừa từ MainGame và bổ sung thêm MainPlayfabSessions.
/// </summary>
public class PlayfabBaseGame : MainGame, IPlayfabGame
{
    private readonly MainPlayfabSessions _playfabSessions = new();

    public PlayfabBaseGame()
    {
        _playfabSessions.Initialize();
    }

    /// <summary>
    /// Truy cập đối tượng quản lý phiên PlayFab.
    /// </summary>
    public MainPlayfabSessions PlayfabSessions => _playfabSessions;
}
