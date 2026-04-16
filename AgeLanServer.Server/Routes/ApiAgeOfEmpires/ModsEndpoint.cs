using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.ApiAgeOfEmpires;

public static class ModsEndpoint
{
    public static void RegisterEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v4/Mods/My", HandleEmptyMods);
        app.MapPost("/api/v4/Mods/My/", HandleEmptyMods);
        app.MapPost("/api/v4/Mods/Installed", HandleEmptyMods);
        app.MapPost("/api/v4/Mods/Installed/", HandleEmptyMods);
        app.MapPost("/api/v4/Mods/Find", HandleEmptyMods);
        app.MapPost("/api/v4/Mods/Find/", HandleEmptyMods);
    }

    private static IResult HandleEmptyMods()
    {
        return Results.Json(new
        {
            mods = Array.Empty<object>(),
            count = 0
        });
    }
}
