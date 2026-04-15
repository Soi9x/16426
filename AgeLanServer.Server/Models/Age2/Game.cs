using AgeLanServer.Common;
using System.Collections.Immutable;

namespace AgeLanServer.Server.Models.Age2;

/// <summary>
/// Tạo game cho Age of Empires 2.
/// Sử dụng các tệp itemBundleItems.json và itemDefinitions.json có chữ ký.
/// </summary>
public static class GameFactory
{
    public static IGame CreateGame()
    {
        return GameFactoryHelper.CreateMainGame(
            AppConstants.GameAoE2,
            new CreateMainGameOpts
            {
                Resources = new ResourcesOpts
                {
                    KeyedFilenames = ImmutableHashSet.Create("itemBundleItems.json", "itemDefinitions.json")
                }
            });
    }
}
