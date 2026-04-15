using AgeLanServer.Common;
using AgeLanServer.Server.Models.Playfab;
using System.Collections.Immutable;

namespace AgeLanServer.Server.Models.Age4;

/// <summary>
/// Tạo game cho Age of Empires 4.
/// Hỗ trợ PlayFab với phiên PlayFab và các tệp có chữ ký bổ sung.
/// </summary>
public static class GameFactory
{
    public static IGame CreateGame()
    {
        var mainGame = GameFactoryHelper.CreateMainGame(
            AppConstants.GameAoE4,
            new CreateMainGameOpts
            {
                Resources = new ResourcesOpts
                {
                    KeyedFilenames = ImmutableHashSet.Create(
                        "itemBundleItems.json",
                        "itemDefinitions.json",
                        "levelRewardsTable.json")
                },
                BattleServer = new BattleServerOpts
                {
                    OobPort = true,
                    Name = "null"
                }
            });

        var game = new BaseGame { Game = mainGame };
        game.PlayfabSessions.Initialize();
        return game;
    }
}

/// <summary>
/// Lớp game cơ sở hỗ trợ PlayFab.
/// Wraps một IGame và thêm phiên PlayFab.
/// </summary>
public class BaseGame : IGame
{
    public IGame Game { get; set; } = null!;
    public MainPlayfabSessions PlayfabSessions { get; } = new();

    public string Title => Game.Title;
    public IResources Resources => Game.Resources;
    public IItems Items => Game.Items;
    public ILeaderboardDefinitions LeaderboardDefinitions => Game.LeaderboardDefinitions;
    public IPresenceDefinitions PresenceDefinitions => Game.PresenceDefinitions;
    public IBattleServers BattleServers => Game.BattleServers;
    public IUsers Users => Game.Users;
    public IAdvertisements Advertisements => Game.Advertisements;
    public IChatChannels ChatChannels => Game.ChatChannels;
    public ISessions Sessions => Game.Sessions;
}
