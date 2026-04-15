using System.Collections.Concurrent;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Login;
using AgeLanServer.Server.Routes.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Account;

/// <summary>
/// Đăng ký các endpoint quản lý account/profile: set language, crossplay, avatar metadata,
/// find profiles, get profile name/property, add/clear property.
/// </summary>
public static class AccountEndpoints
{
    // Kho lưu trữ profile properties theo user ID
    internal static readonly ConcurrentDictionary<int, Dictionary<string, string>> UserProperties = new();
    internal static readonly ConcurrentDictionary<int, string> UserAvatars = new();
    internal static readonly ConcurrentDictionary<int, string> UserLanguages = new();
    internal static readonly ConcurrentDictionary<int, bool> UserCrossplayEnabled = new();

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/account");

        // Đặt ngôn ngữ
        group.MapPost("/setLanguage", HandleSetLanguage);

        // Bật/tắt crossplay
        group.MapPost("/setCrossplayEnabled", HandleSetCrossplayEnabled);

        // Đặt avatar metadata
        group.MapPost("/setAvatarMetadata", HandleSetAvatarMetadata);

        // Tìm profiles theo platform IDs
        group.MapPost("/FindProfilesByPlatformID", HandleFindProfilesByPlatformId);

        // Tìm profiles theo tên
        group.MapGet("/FindProfiles", HandleFindProfiles);

        // Lấy tên profile theo IDs
        group.MapGet("/getProfileName", HandleGetProfileName);

        // Lấy thuộc tính profile
        group.MapGet("/getProfileProperty", HandleGetProfileProperty);

        // Thêm thuộc tính profile
        group.MapPost("/addProfileProperty", HandleAddProfileProperty);

        // Xóa thuộc tính profile
        group.MapPost("/clearProfileProperty", HandleClearProfileProperty);
    }

    /// <summary>
    /// Xử lý đặt ngôn ngữ.
    /// </summary>
    private static async Task<IResult> HandleSetLanguage(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var language = ctx.Request.Form["language"].ToString();
        if (string.IsNullOrEmpty(language))
        {
            return Results.Ok(new object[] { 2 });
        }

        var userId = GetUserIdFromSession(ctx);
        if (userId > 0)
        {
            UserLanguages[userId] = language;

            // Cập nhật session nếu có
            var sessionId = ctx.Items["SessionId"] as string;
            if (!string.IsNullOrEmpty(sessionId) &&
                LoginEndpoints.Sessions.TryGetValue(sessionId, out var session))
            {
                session.Language = language;
            }
        }

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xử lý bật/tắt crossplay.
    /// Crossplay luôn phải bật, không chấp nhận tắt.
    /// AoE I dùng "enable" thay vì "crossplayEnabled".
    /// </summary>
    private static async Task<IResult> HandleSetCrossplayEnabled(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var enable = ctx.Request.Form["crossplayEnabled"].ToString();
        if (string.IsNullOrEmpty(enable))
        {
            // AoE I dùng key khác
            enable = ctx.Request.Form["enable"].ToString();
        }

        var userId = GetUserIdFromSession(ctx);
        if (userId > 0)
        {
            UserCrossplayEnabled[userId] = enable == "1";
        }

        if (enable == "1")
        {
            return Results.Ok(new object[] { 0 });
        }

        // Không chấp nhận tắt crossplay
        return Results.Ok(new object[] { 2 });
    }

    /// <summary>
    /// Xử lý đặt avatar metadata.
    /// Cập nhật metadata cho avatar của user.
    /// </summary>
    private static async Task<IResult> HandleSetAvatarMetadata(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new SetAvatarMetadataRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);

        // 2. Cập nhật avatar metadata
        if (userId > 0)
        {
            UserAvatars[userId] = req.Metadata;
        }

        return Results.Ok(new object[] { 0, Array.Empty<object>() });
    }

    /// <summary>
    /// Xử lý tìm profiles theo platform IDs.
    /// Trả về thông tin profile của các users có platformUserID khớp.
    /// </summary>
    private static async Task<IResult> HandleFindProfilesByPlatformId(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new FindProfilesByPlatformIdRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        // Tạo map platform IDs để tìm nhanh
        var platformIdsMap = new HashSet<ulong>(req.PlatformIds.Data);

        // 1. Lọc users theo platformUserID
        // 2. Mã hóa profile info
        var profiles = LoginEndpoints.Sessions.Values
            .Where(s => platformIdsMap.Contains((ulong)s.ProfileId))
            .Select(s => new object[]
            {
                s.ProfileId,
                s.Alias,
                s.Language,
                s.Region
            })
            .ToArray();

        return Results.Ok(new object[] { 0, profiles });
    }

    /// <summary>
    /// Xử lý tìm profiles theo tên.
    /// Tìm kiếm case-insensitive, trả về các users có alias chứa từ khóa.
    /// </summary>
    private static async Task<IResult> HandleFindProfiles(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var name = ctx.Request.Query["name"].ToString().ToLowerInvariant();
        if (string.IsNullOrEmpty(name))
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>() });
        }

        // 1. Lọc users có alias chứa từ khóa (case-insensitive)
        // 2. Mã hóa profile info
        var profiles = LoginEndpoints.Sessions.Values
            .Where(s => s.Alias.ToLowerInvariant().Contains(name))
            .Select(s => new object[]
            {
                s.ProfileId,
                s.Alias,
                s.Language,
                s.Region
            })
            .ToArray();

        return Results.Ok(new object[] { 0, profiles });
    }

    /// <summary>
    /// Xử lý lấy tên profile theo IDs.
    /// Trả về thông tin profile của các users có ID khớp.
    /// </summary>
    private static async Task<IResult> HandleGetProfileName(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new GetProfileNameRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        // Tạo map profile IDs để tìm nhanh
        var profileIdsMap = new HashSet<int>(req.ProfileIds.Data);

        // 1. Lọc users theo profile ID
        // 2. Mã hóa profile info (không có presence)
        var profiles = LoginEndpoints.Sessions.Values
            .Where(s => profileIdsMap.Contains(s.ProfileId))
            .Select(s => new object[]
            {
                s.ProfileId,
                s.Alias
            })
            .ToArray();

        return Results.Ok(new object[] { 0, profiles });
    }

    /// <summary>
    /// Xử lý lấy thuộc tính profile.
    /// Trả về giá trị của thuộc tính được yêu cầu.
    /// </summary>
    private static async Task<IResult> HandleGetProfileProperty(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new GetProfilePropertyRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        var response = new object[] { 0, Array.Empty<object>() };

        if (string.IsNullOrEmpty(req.ProfileId) || string.IsNullOrEmpty(req.PropertyId))
        {
            return Results.Ok(response);
        }

        // 1. Lấy user theo profile ID
        if (int.TryParse(req.ProfileId, out var profileId))
        {
            // 2. Tìm property_value trong profile properties
            var properties = UserProperties.GetOrAdd(profileId, _ => new Dictionary<string, string>());
            if (properties.TryGetValue(req.PropertyId, out var value))
            {
                return Results.Ok(new object[] { 0, new object[] { req.PropertyId, value } });
            }
        }

        return Results.Ok(response);
    }

    /// <summary>
    /// Xử lý thêm thuộc tính profile.
    /// Thêm hoặc cập nhật thuộc tính cho profile của user.
    /// </summary>
    private static async Task<IResult> HandleAddProfileProperty(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new AddProfilePropertyRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        var response = new object[] { 0 };

        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);

        // 2. Thêm/cập nhật property vào profile properties
        if (userId > 0 && !string.IsNullOrEmpty(req.PropertyId))
        {
            var properties = UserProperties.GetOrAdd(userId, _ => new Dictionary<string, string>());
            properties[req.PropertyId] = req.PropertyValue;
        }

        return Results.Ok(response);
    }

    /// <summary>
    /// Xử lý xóa thuộc tính profile.
    /// Xóa thuộc tính khỏi profile của user.
    /// </summary>
    private static async Task<IResult> HandleClearProfileProperty(HttpContext ctx,
        [FromServices] ILogger<Program> logger)
    {
        var req = new ProfilePropertiesRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        var response = new object[] { 0 };

        // 1. Lấy user từ session
        var userId = GetUserIdFromSession(ctx);

        // 2. Xóa property khỏi profile properties
        if (userId > 0 && !string.IsNullOrEmpty(req.PropertyId))
        {
            var properties = UserProperties.GetOrAdd(userId, _ => new Dictionary<string, string>());
            properties.Remove(req.PropertyId);
        }

        return Results.Ok(response);
    }

    /// <summary>
    /// Helper: Lấy userId từ session hiện tại.
    /// </summary>
    private static int GetUserIdFromSession(HttpContext ctx)
    {
        if (ctx.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
        {
            return userId;
        }
        return 0;
    }
}
