// Registrar cho tất cả PlayFab endpoints.
// Đăng ký các endpoint tương đương thư mục playfab/ trong Go server.
// Bao gồm: Client, Catalog, CloudScript, Event, Inventory, MultiplayerServer, Party.

using AgeLanServer.Server.Routes.PlayFab.Catalog;
using AgeLanServer.Server.Routes.PlayFab.Client;
using AgeLanServer.Server.Routes.PlayFab.CloudScript;
using AgeLanServer.Server.Routes.PlayFab.Event;
using AgeLanServer.Server.Routes.PlayFab.Inventory;
using AgeLanServer.Server.Routes.PlayFab.MultiplayerServer;
using AgeLanServer.Server.Routes.PlayFab.Party;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab;

/// <summary>
/// Đăng ký tất cả PlayFab endpoints cho server.
/// Tương đương cấu trúc routes/playfab/ trong Go server.
/// </summary>
public static class PlayFabRegistrar
{
    /// <summary>
    /// Đăng ký tất cả PlayFab endpoints.
    /// Bao gồm các nhóm: Client, Catalog, CloudScript, Event, Inventory, MultiplayerServer, Party.
    /// </summary>
    public static void RegisterEndpoints(IEndpointRouteBuilder app)
    {
        // === PlayFab Client endpoints ===
        // Login, GetTime, GetTitleData, GetUserData, v.v.
        PlayFabClientRegistrar.RegisterEndpoints(app);

        // === PlayFab Catalog endpoints ===
        GetItemsEndpoint.RegisterEndpoint(app);

        // === PlayFab CloudScript endpoints ===
        ExecuteFunctionEndpoint.RegisterEndpoint(app);

        // === PlayFab Event endpoints ===
        WriteTelemetryEventsEndpoint.RegisterEndpoint(app);

        // === PlayFab Inventory endpoints ===
        GetInventoryItemsEndpoint.RegisterEndpoint(app);

        // === PlayFab MultiplayerServer endpoints ===
        ListPartyQosServersEndpoint.RegisterEndpoint(app);
        GetCognitiveServicesTokenEndpoint.RegisterEndpoint(app);

        // === PlayFab Party endpoints ===
        RequestPartyEndpoint.RegisterEndpoint(app);
    }
}
