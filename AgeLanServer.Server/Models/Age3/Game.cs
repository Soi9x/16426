using AgeLanServer.Common;

namespace AgeLanServer.Server.Models.Age3;

/// <summary>
/// Tạo game cho Age of Empires 3.
/// Cấu hình máy chủ battle với tên "null" và bật cổng OOB.
/// </summary>
public static class GameFactory
{
    public static IGame CreateGame()
    {
        return GameFactoryHelper.CreateMainGame(
            AppConstants.GameAoE3,
            new CreateMainGameOpts
            {
                BattleServer = new BattleServerOpts
                {
                    Name = "null",
                    OobPort = true
                }
            });
    }
}
