// Registrar cho tất cả PlayFab Client endpoints.
// Đăng ký các endpoint tương đương thư mục playfab/Client/ trong Go server.

using AgeLanServer.Server.Routes.PlayFab.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Client;

/// <summary>
/// Đăng ký tất cả PlayFab Client endpoints.
/// Bao gồm: GetTime, GetTitleData, LoginWithCustomID, LoginWithSteam,
/// GetUserData, GetUserReadOnlyData, GetPlayerCombinedInfo, UpdateUserTitleDisplayName.
/// </summary>
public static class PlayFabClientRegistrar
{
    /// <summary>
    /// Đăng ký tất cả PlayFab Client endpoints.
    /// </summary>
    public static void RegisterEndpoints(IEndpointRouteBuilder app)
    {
        // Thời gian server
        GetTimeEndpoint.RegisterEndpoint(app);

        // Dữ liệu cấu hình title (CDN URL, path)
        GetTitleDataEndpoint.RegisterEndpoint(app);

        // Đăng nhập bằng Custom ID
        LoginWithCustomIdEndpoint.RegisterEndpoint(app);

        // Đăng nhập bằng Steam ticket
        LoginWithSteamEndpoint.RegisterEndpoint(app);

        // Dữ liệu người dùng
        GetUserDataEndpoint.RegisterEndpoint(app);

        // Dữ liệu chỉ-đọc của người dùng
        GetUserReadOnlyDataEndpoint.RegisterEndpoint(app);

        // Thông tin kết hợp người chơi
        GetPlayerCombinedInfoEndpoint.RegisterEndpoint(app);

        // Cập nhật tên hiển thị
        UpdateUserTitleDisplayNameEndpoint.RegisterEndpoint(app);
    }
}
