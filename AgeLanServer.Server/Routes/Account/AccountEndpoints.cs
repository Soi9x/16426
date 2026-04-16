using System.Collections.Concurrent;
using System.Linq;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Login;
using AgeLanServer.Server.Routes.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Account;

public static class AccountEndpoints
{
    internal static readonly ConcurrentDictionary<int, Dictionary<string, string>> UserProperties = new();

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/account");
        var gameId = GetCurrentGameId();

        group.MapPost("/setLanguage", HandleSetLanguage);
        group.MapPost("/setCrossplayEnabled", HandleSetCrossplayEnabled);
        group.MapPost("/setAvatarMetadata", HandleSetAvatarMetadata);

        group.MapPost("/FindProfilesByPlatformID", HandleFindProfilesByPlatformId);
        group.MapGet("/FindProfiles", HandleFindProfiles);
        group.MapGet("/getProfileName", HandleGetProfileName);

        if (gameId is GameIds.AgeOfEmpires3 or GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology)
        {
            group.MapGet("/getProfileProperty", HandleGetProfileProperty);
            group.MapPost("/addProfileProperty", HandleAddProfileProperty);
            group.MapPost("/clearProfileProperty", HandleClearProfileProperty);
        }
    }

    private static Task<IResult> HandleSetLanguage([FromServices] ILogger<Program> logger)
    {
        return Task.FromResult<IResult>(Results.Ok(new object[] { 2 }));
    }

    private static async Task<IResult> HandleSetCrossplayEnabled(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        if (!ctx.Request.HasFormContentType)
        {
            return Results.Ok(new object[] { 2 });
        }

        var form = await ctx.Request.ReadFormAsync();
        var enable = form["crossplayEnabled"].ToString();
        if (string.IsNullOrEmpty(enable))
        {
            enable = form["enable"].ToString();
        }

        return Results.Ok(enable == "1" ? new object[] { 0 } : new object[] { 2 });
    }

    private static async Task<IResult> HandleSetAvatarMetadata(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new SetAvatarMetadataRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || !LoginEndpoints.TryGetSession(ctx, out var session))
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>() });
        }

        session.AvatarMetadata = req.Metadata;
        var profileInfo = LoginEndpoints.EncodeProfileInfo(session, session.ClientLibVersion);
        return Results.Ok(new object[] { 0, profileInfo });
    }

    private static async Task<IResult> HandleFindProfilesByPlatformId(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new FindProfilesByPlatformIdRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || !LoginEndpoints.TryGetSession(ctx, out var session))
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>() });
        }

        var platformIds = new HashSet<ulong>(req.PlatformIds.Data);
        var profiles = LoginEndpoints.Sessions.Values
            .Where(s => platformIds.Contains(s.PlatformUserId))
            .Select(s => LoginEndpoints.EncodeProfileInfoWithPresence(s, session.ClientLibVersion))
            .Cast<object>()
            .ToArray();

        return Results.Ok(new object[] { 0, profiles });
    }

    private static Task<IResult> HandleFindProfiles(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var name = ctx.Request.Query["name"].ToString().ToLowerInvariant();
        if (string.IsNullOrEmpty(name) || !LoginEndpoints.TryGetSession(ctx, out var session))
        {
            return Task.FromResult<IResult>(Results.Ok(new object[] { 2, Array.Empty<object>() }));
        }

        var profiles = LoginEndpoints.Sessions.Values
            .Where(s => s.Alias.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(s => LoginEndpoints.EncodeProfileInfoWithPresence(s, session.ClientLibVersion))
            .Cast<object>()
            .ToArray();

        return Task.FromResult<IResult>(Results.Ok(new object[] { 0, profiles }));
    }

    private static async Task<IResult> HandleGetProfileName(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new GetProfileNameRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || !LoginEndpoints.TryGetSession(ctx, out var session))
        {
            return Results.Ok(new object[] { 2, Array.Empty<object>() });
        }

        var userIds = new HashSet<int>(req.ProfileIds.Data);
        var profiles = LoginEndpoints.Sessions.Values
            .Where(s => userIds.Contains(s.UserId))
            .Select(s => LoginEndpoints.EncodeProfileInfo(s, session.ClientLibVersion))
            .Cast<object>()
            .ToArray();

        return Results.Ok(new object[] { 0, profiles });
    }

    private static async Task<IResult> HandleGetProfileProperty(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new GetProfilePropertyRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        var response = new object[] { 0, Array.Empty<object>() };

        if (!int.TryParse(req.ProfileId, out var userId) || string.IsNullOrEmpty(req.PropertyId))
        {
            return Results.Ok(response);
        }

        if (!UserProperties.TryGetValue(userId, out var properties) || !properties.TryGetValue(req.PropertyId, out var value))
        {
            return Results.Ok(response);
        }

        return Results.Ok(new object[] { 0, new object[] { value } });
    }

    private static async Task<IResult> HandleAddProfileProperty(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new AddProfilePropertyRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (LoginEndpoints.TryGetSession(ctx, out var session) && !string.IsNullOrEmpty(req.PropertyId))
        {
            var properties = UserProperties.GetOrAdd(session.UserId, _ => new Dictionary<string, string>());
            properties[req.PropertyId] = req.PropertyValue;
        }

        return Results.Ok(new object[] { 0 });
    }

    private static async Task<IResult> HandleClearProfileProperty(HttpContext ctx, [FromServices] ILogger<Program> logger)
    {
        var req = new ProfilePropertiesRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);

        if (LoginEndpoints.TryGetSession(ctx, out var session) && !string.IsNullOrEmpty(req.PropertyId))
        {
            var properties = UserProperties.GetOrAdd(session.UserId, _ => new Dictionary<string, string>());
            properties.Remove(req.PropertyId);
        }

        return Results.Ok(new object[] { 0 });
    }

    private static string GetCurrentGameId()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }
}
