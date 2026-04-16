using AgeLanServer.Server.Routes.Shared;
using AgeLanServer.Server.Routes.Login;
using System.Collections.Concurrent;
using System.Linq;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Invitation;

/// <summary>
/// ÄÄƒng kÃ½ cÃ¡c endpoint quáº£n lÃ½ lá»i má»i: extend, cancel, reply.
/// </summary>
public static class InvitationEndpoints
{
    // Kho lÆ°u trá»¯ lá»i má»i trong bá»™ nhá»›
    internal static readonly ConcurrentDictionary<string, InvitationData> Invitations = new();

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/invitation");

        // Má»Ÿ rá»™ng lá»i má»i
        group.MapPost("/extendInvitation", HandleExtendInvitation);

        // Há»§y lá»i má»i
        group.MapPost("/cancelInvitation", HandleCancelInvitation);

        // Pháº£n há»“i lá»i má»i
        group.MapPost("/replyToInvitation", HandleReplyInvitation);
    }

    /// <summary>
    /// Xá»­ lÃ½ má»Ÿ rá»™ng lá»i má»i vÃ o lobby.
    /// Gá»­i ExtendInvitationMessage qua WebSocket tá»›i ngÆ°á»i Ä‘Æ°á»£c má»i.
    /// </summary>
    private static async Task<IResult> HandleExtendInvitation(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new ExtendInvitationRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || req.AdvertisementId == 0 || req.UserId == 0 || !LoginEndpoints.TryGetSession(ctx, out var inviterSession))
        {
            return Results.Ok(new object[] { 2 });
        }

        // 1. Kiá»ƒm tra advertisement tá»“n táº¡i
        // 2. Kiá»ƒm tra password khá»›p
        // 3. Kiá»ƒm tra user lÃ  peer trong advertisement
        // 4. Kiá»ƒm tra invitee tá»“n táº¡i
        // 5. Gá»­i ExtendInvitationMessage qua WebSocket cho invitee

        var invitationId = Guid.NewGuid().ToString("N");
        var invitation = new InvitationData
        {
            Id = invitationId,
            AdvertisementId = req.AdvertisementId,
            InviterId = req.UserId,
            InviteeId = req.UserId, // Láº¥y tá»« request body (req.UserId)
            Password = req.AdvertisementPassword,
            CreatedAt = DateTime.UtcNow
        };

        Invitations[invitationId] = invitation;

        // Gá»­i qua WebSocket cho invitee
        var inviteeSession = LoginEndpoints.Sessions.Values.FirstOrDefault(s => s.UserId == req.UserId);
        if (inviteeSession != null)
        {
            var inviteMessage = new object[]
            {
                LoginEndpoints.EncodeProfileInfo(inviterSession, inviteeSession.ClientLibVersion),
                req.AdvertisementId,
                req.AdvertisementPassword
            };
            await WsMessageSender.SendOrStoreMessageAsync(inviteeSession.SessionId, "ExtendInvitationMessage", inviteMessage);
        }

        logger.LogInformation("Invitation extended: ID={InvId}, Adv={AdvId}", invitationId, req.AdvertisementId);

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ há»§y lá»i má»i.
    /// Gá»­i CancelInvitationMessage qua WebSocket tá»›i ngÆ°á»i Ä‘Æ°á»£c má»i.
    /// </summary>
    private static async Task<IResult> HandleCancelInvitation(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new CancelInvitationRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || req.AdvertisementId == 0 || req.UserId == 0 || !LoginEndpoints.TryGetSession(ctx, out var inviterSession))
        {
            return Results.Ok(new object[] { 2 });
        }

        // 1. Kiá»ƒm tra advertisement tá»“n táº¡i
        // 2. Kiá»ƒm tra user lÃ  peer trong advertisement
        // 3. Kiá»ƒm tra invitee tá»“n táº¡i
        // 4. Gá»­i CancelInvitationMessage qua WebSocket cho invitee

        // TÃ¬m vÃ  xÃ³a invitation
        var toRemove = Invitations.FirstOrDefault(x =>
            x.Value.AdvertisementId == req.AdvertisementId && x.Value.InviterId == req.UserId);

        if (!toRemove.Equals(default(KeyValuePair<string, InvitationData>)))
        {
            Invitations.TryRemove(toRemove.Key, out _);
        }

        // Gá»­i qua WebSocket cho invitee
        var inviteeSession = LoginEndpoints.Sessions.Values.FirstOrDefault(s => s.UserId == toRemove.Value.InviteeId);
        if (inviteeSession != null)
        {
            var cancelMessage = new object[]
            {
                LoginEndpoints.EncodeProfileInfo(inviterSession, inviteeSession.ClientLibVersion),
                req.AdvertisementId
            };
            await WsMessageSender.SendOrStoreMessageAsync(inviteeSession.SessionId, "CancelInvitationMessage", cancelMessage);
        }

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ pháº£n há»“i lá»i má»i (cháº¥p nháº­n hoáº·c tá»« chá»‘i).
    /// Gá»­i ReplyInvitationMessage qua WebSocket tá»›i ngÆ°á»i má»i.
    /// </summary>
    private static async Task<IResult> HandleReplyInvitation(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new ReplyInvitationRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);
        if (!bound || req.AdvertisementId == 0 || req.InviterId == 0 || !LoginEndpoints.TryGetSession(ctx, out var currentSession))
        {
            return Results.Ok(new object[] { 2 });
        }

        // 1. Kiá»ƒm tra advertisement tá»“n táº¡i
        // 2. Kiá»ƒm tra inviter tá»“n táº¡i
        // 3. Kiá»ƒm tra inviter lÃ  peer trong advertisement
        // 4. Gá»­i ReplyInvitationMessage qua WebSocket cho inviter (vá»›i "1" hoáº·c "0")

        var responseCode = req.Accept ? "1" : "0";

        // Gá»­i qua WebSocket cho inviter
        var inviterSession = LoginEndpoints.Sessions.Values.FirstOrDefault(s => s.UserId == req.InviterId);
        if (inviterSession != null)
        {
            var replyMessage = new object[]
            {
                LoginEndpoints.EncodeProfileInfo(currentSession, inviterSession.ClientLibVersion),
                req.AdvertisementId,
                responseCode
            };
            await WsMessageSender.SendOrStoreMessageAsync(inviterSession.SessionId, "ReplyInvitationMessage", replyMessage);
        }

        return Results.Ok(new object[] { 0 });
    }
}

/// <summary>
/// Dá»¯ liá»‡u lá»i má»i trong bá»™ nhá»›.
/// </summary>
internal sealed class InvitationData
{
    public string Id { get; set; } = string.Empty;
    public int AdvertisementId { get; set; }
    public int InviterId { get; set; }
    public int InviteeId { get; set; }
    public string Password { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
