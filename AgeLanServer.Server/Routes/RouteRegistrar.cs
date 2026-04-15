using AgeLanServer.Server.Routes.Account;
using AgeLanServer.Server.Routes.Achievement;
using AgeLanServer.Server.Routes.Advertisement;
using AgeLanServer.Server.Routes.Automatch;
using AgeLanServer.Server.Routes.CacertPem;
using AgeLanServer.Server.Routes.Challenge;
using AgeLanServer.Server.Routes.Chat;
using AgeLanServer.Server.Routes.Clan;
using AgeLanServer.Server.Routes.Cloud;
using AgeLanServer.Server.Routes.CommunityEvent;
using AgeLanServer.Server.Routes.Invitation;
using AgeLanServer.Server.Routes.Item;
using AgeLanServer.Server.Routes.Leaderboard;
using AgeLanServer.Server.Routes.Login;
using AgeLanServer.Server.Routes.MsStore;
using AgeLanServer.Server.Routes.News;
using AgeLanServer.Server.Routes.Party;
using AgeLanServer.Server.Routes.PlayerReport;
using AgeLanServer.Server.Routes.Relationship;
using AgeLanServer.Server.Routes.Test;
using AgeLanServer.Server.Routes.WebSocket;
using Microsoft.AspNetCore.Builder;

namespace AgeLanServer.Server.Routes;

/// <summary>
/// Đăng ký tất cả các route endpoints cho server.
/// Tương đương InitializeRoutes trong router/game.go của bản Go.
/// </summary>
public static class RouteRegistrar
{
    /// <summary>
    /// Đăng ký tất cả API endpoints cho game server.
    /// </summary>
    /// <param name="app">Ứng dụng web ASP.NET Core</param>
    /// <param name="gameId">ID game (age1, age2, age3, age4, athens)</param>
    public static void RegisterGameRoutes(WebApplication app, string gameId)
    {
        // === Item endpoints ===
        ItemEndpoints.RegisterEndpoints(app);

        // Item endpoints chỉ có thêm trong AoE1
        // (các game khác có thêm endpoints đã được đăng ký trong ItemEndpoints)

        // === Clan endpoints ===
        ClanEndpoints.RegisterEndpoints(app);

        // === Community Event endpoints ===
        CommunityEventEndpoints.RegisterEndpoints(app);

        // Community event stats/leaderboard chỉ có trong AoE4/AoM
        if (gameId is "age4" or "athens")
        {
            // Đã đăng ký trong CommunityEventEndpoints
        }

        // === Challenge endpoints ===
        ChallengeEndpoints.RegisterEndpoints(app);

        // Challenge/updateProgress chỉ có trong AoE3
        // Challenge/updateProgressBatched chỉ có trong AoE4/AoM

        // === News endpoints ===
        NewsEndpoints.RegisterEndpoints(app);

        // === Login endpoints ===
        LoginEndpoints.RegisterEndpoints(app);
        // AuthMiddleware và LoginUserMiddleware được thêm riêng nếu authentication enabled

        // === Account endpoints ===
        AccountEndpoints.RegisterEndpoints(app);

        // Account property endpoints chỉ có trong AoE3/AoE4/AoM

        // === Leaderboard endpoints ===
        LeaderboardEndpoints.RegisterEndpoints(app);

        // === Achievement endpoints ===
        AchievementEndpoints.RegisterEndpoints(app);

        // === Advertisement endpoints ===
        AdvertisementEndpoints.RegisterEndpoints(app);

        // Advertisement/updatePlatformSessionID chỉ có trong AoE2/AoE4/AoM
        // Advertisement/updateTags chỉ có trong AoE2/AoE4/AoM
        // Advertisement/getLanAdvertisements: POST cho AoE1/AoE3, GET cho AoE2
        // Advertisement/updatePlatformLobbyID chỉ có trong AoE1/AoE3
        // Advertisement/findObservableAdvertisements: POST cho AoE3, GET cho AoE2/AoE4/AoM
        // Advertisement/findAdvertisements: POST cho AoE1/AoE3, GET cho AoE2/AoE4/AoM
        // Advertisement/startObserving/stopObserving chỉ có trong AoE2/AoE3/AoE4/AoM

        // === Chat endpoints ===
        ChatEndpoints.RegisterEndpoints(app);

        // Chat/getChatChannels: POST cho AoE1/AoE3, GET cho AoE2/AoE4/AoM
        // Chat/joinChannel/leaveChannel/sendText chỉ có trong AoE3
        // Chat/sendWhisper: POST cho AoE3, sendWhispers cho AoE4/AoM
        // Chat/deleteOfflineMessage chỉ có trong AoM

        // === Relationship endpoints ===
        RelationshipEndpoints.RegisterEndpoints(app);

        // Relationship/getRelationships: POST cho AoE1/AoE3, GET cho AoE2/AoE4/AoM
        // Relationship/setPresenceProperty chỉ có trong AoE3/AoE4/AoM
        // Relationship/addfriend chỉ có trong AoE3/AoE4/AoM

        // === Party endpoints ===
        PartyEndpoints.RegisterEndpoints(app);

        // Party/createOrReportSinglePlayer chỉ có trong AoE4/AoM

        // === Player Report endpoints ===
        // Chỉ có trong AoE2/AoE4/AoM
        if (gameId is "age2" or "age4" or "athens")
        {
            PlayerReportEndpoints.RegisterEndpoints(app);
        }

        // === Invitation endpoints ===
        InvitationEndpoints.RegisterEndpoints(app);

        // === Cloud endpoints ===
        CloudEndpoints.RegisterEndpoints(app);

        // Cloud/getFileURL: POST cho AoE3, GET cho AoE2/AoE4

        // === MS Store endpoints ===
        MsStoreEndpoints.RegisterEndpoints(app);

        // === Automatch endpoints ===
        AutomatchEndpoints.RegisterEndpoints(app);

        // === WebSocket endpoint ===
        WebSocketEndpoints.RegisterEndpoints(app);
    }

    /// <summary>
    /// Đăng ký các endpoint chung (không theo game).
    /// Tương đương InitializeRoutes trong router/general.go.
    /// </summary>
    public static void RegisterGeneralRoutes(WebApplication app)
    {
        // Test endpoint
        app.MapGet("/test", TestEndpoint.HandleTest);

        // CA cert endpoint - trả về file cacert.pem từ Resources/etc/
        CacertPemEndpoint.RegisterEndpoint(app);

        // Shutdown endpoint (chỉ Windows)
        if (OperatingSystem.IsWindows())
        {
            app.MapPost("/shutdown", (HttpContext ctx) =>
            {
                // Kiểm tra IP remote là localhost
                var remoteIp = ctx.Connection.RemoteIpAddress;
                if (remoteIp != null && !remoteIp.IsIPv4MappedToIPv6 &&
                    remoteIp.ToString() != "127.0.0.1" &&
                    remoteIp.ToString() != "::1")
                {
                    return Results.Forbid();
                }

                // Gửi signal dừng server
                Environment.Exit(0);
                return Results.Ok();
            });
        }
    }
}
