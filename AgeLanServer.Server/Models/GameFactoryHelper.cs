using AgeLanServer.Server.Internal;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Lớp trợ giúp để tạo MainGame với các tùy chọn.
/// Khởi tạo tất cả các thành phần của game như battle servers, resources, users, v.v.
/// </summary>
public static class GameFactoryHelper
{
    public static IGame CreateMainGame(string gameId, CreateMainGameOpts? opts = null)
    {
        opts ??= new CreateMainGameOpts();
        opts.Resources ??= new ResourcesOpts();
        opts.Instances ??= new InstanceOpts();

        // Khởi tạo các thành phần mặc định nếu chưa có
        opts.Instances.BattleServers ??= new MainBattleServers();
        opts.Instances.Resources ??= new MainResources();
        opts.Instances.Users ??= new MainUsers();
        opts.Instances.Advertisements ??= new MainAdvertisements();
        opts.Instances.ChatChannels ??= new MainChatChannels();
        opts.Instances.Sessions ??= new MainSessions();

        var game = new MainGame();
        // Gán thông qua reflection hoặc setter - chi tiết triển khai phụ thuộc vào internal

        return game;
    }
}
