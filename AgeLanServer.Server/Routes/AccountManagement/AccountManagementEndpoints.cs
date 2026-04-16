using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace AgeLanServer.Server.Routes.AccountManagement;

public class SyncAppIdsRequest
{
    public JsonArray<int> AppIds { get; set; } = new();
    [BindAlias("appids")]
    public JsonArray<int> Appids { get; set; } = new();
}

public static class AccountManagementEndpoints
{
    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/accountmanagement");
        group.MapPost("/syncAppIds", async (HttpContext ctx) =>
        {
            var req = new SyncAppIdsRequest();
            await HttpHelpers.BindAsync(ctx.Request, req);
            
            var list = req.AppIds.Data.Count > 0 ? req.AppIds.Data : req.Appids.Data;
            
            // Age of Empires 4, 3, 2 DLCs to unlock
            var safeAppIds = new int[] 
            { 
                1466860, 1959430, 1775950, 1582800, 2692220, 2715010, 2826860, // AoE4
                813780, 1389240, 1559590, 1869820, 2139650, 2555450, // AoE2
                933110, 1581450, 1817360, 1581451, 1817361 // AoE3
            };
            
            var finalAppIds = list.Union(safeAppIds).Distinct().ToList();

            return Results.Ok(new object[] { 0, finalAppIds.ToArray() });
        });
    }
}
