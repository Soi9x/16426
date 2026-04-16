using Microsoft.AspNetCore.Builder;

namespace AgeLanServer.Server.Routes.AccountManagement;

public static class AccountManagementEndpoints
{
    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/accountmanagement");
        group.MapPost("/syncAppIds", () => Results.Ok(new object[] { 0 }));
    }
}
