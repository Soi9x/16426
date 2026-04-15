using AgeLanServer.Common;

namespace AgeLanServer.Server.Models.Age1;

/// <summary>
/// Tạo game cho Age of Empires 1 (Definitive Edition).
/// Cấu hình đơn giản nhất: bỏ qua tên máy chủ battle.
/// </summary>
public static class GameFactory
{
    public static IGame CreateGame()
    {
        return GameFactoryHelper.CreateMainGame(
            AppConstants.GameAoE1,
            new CreateMainGameOpts
            {
                BattleServer = new BattleServerOpts { Name = "omit" }
            });
    }
}
