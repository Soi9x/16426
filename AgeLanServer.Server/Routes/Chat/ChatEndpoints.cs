using System.Collections.Concurrent;
using System.Text.Json;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Login;
using AgeLanServer.Server.Routes.Shared;
using AgeLanServer.Server.Routes.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.Chat;

/// <summary>
/// ÄÄƒng kÃ½ cÃ¡c endpoint quáº£n lÃ½ chat: channels, offline messages, join/leave, send text, whisper.
/// </summary>
public static class ChatEndpoints
{
    // Kho lÆ°u trá»¯ chat channels trong bá»™ nhá»›
    private static readonly ConcurrentDictionary<int, ChatChannelData> Channels = new();

    // ÄÆ°á»ng dáº«n tá»›i file chatChannels.json
    private static string ChatChannelsPath => Path.Combine("configs", GetCurrentGameTitleStatic(), "chatChannels.json");

    static ChatEndpoints()
    {
        // Táº£i channels tá»« file JSON khi khá»Ÿi Ä‘á»™ng
        LoadChatChannels();
    }

    public static void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/game/chat");

        // Láº¥y danh sÃ¡ch chat channels
        group.MapGet("/getChatChannels", HandleGetChatChannels);
        group.MapPost("/getChatChannels", HandleGetChatChannels);

        // Láº¥y tin nháº¯n offline
        group.MapGet("/getOfflineMessages", HandleGetOfflineMessages);

        // Tham gia channel
        group.MapPost("/joinChannel", HandleJoinChannel);

        // Rá»i channel
        group.MapPost("/leaveChannel", HandleLeaveChannel);

        // Gá»­i tin nháº¯n text trong channel
        group.MapPost("/sendText", HandleSendText);

        // Gá»­i whisper (tin nháº¯n riÃªng)
        group.MapPost("/sendWhisper", HandleSendWhisper);
        group.MapPost("/sendWhispers", HandleSendWhisper);

        // XÃ³a tin nháº¯n offline (AoM only)
        group.MapPost("/deleteOfflineMessage", HandleDeleteOfflineMessage);
    }

    /// <summary>
    /// Táº£i danh sÃ¡ch channels tá»« file chatChannels.json.
    /// </summary>
    private static void LoadChatChannels()
    {
        try
        {
            var path = ChatChannelsPath;
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var channels = JsonSerializer.Deserialize<Dictionary<string, ChatChannelFileData>>(json);
            if (channels == null) return;

            foreach (var kvp in channels)
            {
                if (int.TryParse(kvp.Key, out var channelId))
                {
                    Channels[channelId] = new ChatChannelData
                    {
                        Id = channelId,
                        Name = kvp.Value.Name,
                        Users = new ConcurrentDictionary<int, string>()
                    };
                }
            }
        }
        catch
        {
            // Bá» qua lá»—i khi load
        }
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y danh sÃ¡ch chat channels.
    /// Tráº£ vá» danh sÃ¡ch cÃ¡c kÃªnh chat cÃ³ sáºµn vá»›i sá»‘ lÆ°á»£ng ngÆ°á»i dÃ¹ng tá»‘i Ä‘a.
    /// </summary>
    private static async Task<IResult> HandleGetChatChannels(ILogger<Program> logger)
    {
        var channelsList = Channels.Values.Select(c => new object[]
        {
            c.Id,
            c.Name,
            c.Users.Count
        }).ToArray();

        return Results.Ok(new object[] { 0, channelsList, 100 });
    }

    /// <summary>
    /// Xá»­ lÃ½ láº¥y tin nháº¯n offline.
    /// Chá»‰ AoE3 cÃ³ offline messages nhÆ°ng chÆ°a Ä‘Æ°á»£c triá»ƒn khai Ä‘áº§y Ä‘á»§.
    /// </summary>
    private static async Task<IResult> HandleGetOfflineMessages(HttpContext ctx, ILogger<Program> logger)
    {
        // Trong báº£n Go: chá»‰ tráº£ vá» cáº¥u trÃºc rá»—ng vÃ¬ chÆ°a lÆ°u trá»¯ tin nháº¯n offline
        var userId = GetUserIdFromSession(ctx);
        return Results.Ok(new object[]
        {
            0,
            Array.Empty<object>(),
            new object[] { new object[] { userId.ToString(), Array.Empty<object>() } },
            Array.Empty<object>(),
            Array.Empty<object>(),
            Array.Empty<object>()
        });
    }

    /// <summary>
    /// Xá»­ lÃ½ tham gia chat channel.
    /// ThÃªm user vÃ o channel vÃ  thÃ´ng bÃ¡o cho cÃ¡c user khÃ¡c qua WebSocket.
    /// </summary>
    private static async Task<IResult> HandleJoinChannel(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new ChatroomRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Láº¥y channel theo ID
        if (!Channels.TryGetValue(req.ChatroomId, out var channel))
        {
            return Results.Ok(new object[] { 1, req.ChatroomId.ToString(), 0, Array.Empty<object>() });
        }

        // 2. ThÃªm user vÃ o channel
        var userId = GetUserIdFromSession(ctx);
        var userName = ctx.Items["UserName"] as string ?? "Player";
        channel.Users[userId] = userName;

        // 3. Gá»­i ChannelJoinMessage qua WebSocket cho táº¥t cáº£ users
        var joinMessage = new { chatroomId = req.ChatroomId, userId = userId, userName = userName };
        foreach (var memberUserId in channel.Users.Keys)
        {
            var memberSession = LoginEndpoints.Sessions.Values.FirstOrDefault(s => s.ProfileId == memberUserId);
            if (memberSession != null)
            {
                await WsMessageSender.SendOrStoreMessageAsync(memberSession.SessionId, "ChannelJoinMessage", joinMessage);
            }
        }

        return Results.Ok(new object[] { 0, req.ChatroomId.ToString(), 0, Array.Empty<object>() });
    }

    /// <summary>
    /// Xá»­ lÃ½ rá»i chat channel.
    /// XÃ³a user khá»i channel vÃ  thÃ´ng bÃ¡o cho cÃ¡c user khÃ¡c qua WebSocket.
    /// </summary>
    private static async Task<IResult> HandleLeaveChannel(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new ChatroomRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Láº¥y channel theo ID
        if (!Channels.TryGetValue(req.ChatroomId, out var channel))
        {
            return Results.Ok(new object[] { 0 });
        }

        // 2. XÃ³a user khá»i channel
        var userId = GetUserIdFromSession(ctx);
        channel.Users.TryRemove(userId, out _);

        // 3. Gá»­i ChannelLeaveMessage qua WebSocket cho cÃ¡c users khÃ¡c
        var leaveMessage = new { chatroomId = req.ChatroomId, userId = userId };
        foreach (var memberUserId in channel.Users.Keys)
        {
            var memberSession = LoginEndpoints.Sessions.Values.FirstOrDefault(s => s.ProfileId == memberUserId);
            if (memberSession != null)
            {
                await WsMessageSender.SendOrStoreMessageAsync(memberSession.SessionId, "ChannelLeaveMessage", leaveMessage);
            }
        }

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ gá»­i tin nháº¯n text trong channel.
    /// PhÃ¢n phá»‘i tin nháº¯n tá»›i táº¥t cáº£ members trong channel qua WebSocket.
    /// </summary>
    private static async Task<IResult> HandleSendText(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new SendTextRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Kiá»ƒm tra user cÃ³ trong channel khÃ´ng
        if (!Channels.TryGetValue(req.ChatroomId, out var channel))
        {
            return Results.Ok(new object[] { 1 });
        }

        var userId = GetUserIdFromSession(ctx);
        if (!channel.Users.ContainsKey(userId))
        {
            return Results.Ok(new object[] { 2 });
        }

        // 2. Gá»­i ChannelChatMessage qua WebSocket cho táº¥t cáº£ members
        var chatMessage = new { chatroomId = req.ChatroomId, userId = userId, message = req.Message };
        foreach (var memberUserId in channel.Users.Keys)
        {
            if (memberUserId == userId) continue; // KhÃ´ng gá»­i láº¡i cho ngÆ°á»i gá»­i
            var memberSession = LoginEndpoints.Sessions.Values.FirstOrDefault(s => s.ProfileId == memberUserId);
            if (memberSession != null)
            {
                await WsMessageSender.SendOrStoreMessageAsync(memberSession.SessionId, "ChannelChatMessage", chatMessage);
            }
        }

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ gá»­i whisper (tin nháº¯n riÃªng tÆ°).
    /// AoE4/AoM dÃ¹ng recipientIDs (máº£ng), cÃ¡c game khÃ¡c dÃ¹ng recipientID (single).
    /// PhÃ¢n phá»‘i tin nháº¯n tá»›i ngÆ°á»i nháº­n qua WebSocket.
    /// </summary>
    private static async Task<IResult> HandleSendWhisper(HttpContext ctx,
        ILogger<Program> logger)
    {
        var req = new WhisperRequest();
        await HttpHelpers.BindAsync(ctx.Request, req);
        // 1. Láº¥y danh sÃ¡ch ngÆ°á»i nháº­n (tá»« RecipientIds hoáº·c RecipientId)
        var recipients = req.RecipientIds.Data.Count > 0
            ? req.RecipientIds.Data
            : new List<int> { req.RecipientId };

        var userId = GetUserIdFromSession(ctx);

        // 2. Kiá»ƒm tra ngÆ°á»i nháº­n tá»“n táº¡i
        // 3. Gá»­i PersonalChatMessage qua WebSocket cho tá»«ng ngÆ°á»i nháº­n
        var whisperMessage = new { senderId = userId, message = req.Message };
        foreach (var recipientId in recipients)
        {
            var recipientSession = LoginEndpoints.Sessions.Values.FirstOrDefault(s => s.ProfileId == recipientId);
            if (recipientSession != null)
            {
                await WsMessageSender.SendOrStoreMessageAsync(recipientSession.SessionId, "PersonalChatMessage", whisperMessage);
            }
        }

        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Xá»­ lÃ½ xÃ³a tin nháº¯n offline.
    /// Hiá»‡n táº¡i chÆ°a cÃ³ dá»¯ liá»‡u offline messages nÃªn luÃ´n tráº£ vá» thÃ nh cÃ´ng.
    /// </summary>
    private static async Task<IResult> HandleDeleteOfflineMessage(ILogger<Program> logger)
    {
        return Results.Ok(new object[] { 0 });
    }

    /// <summary>
    /// Helper: Láº¥y userId tá»« session hiá»‡n táº¡i.
    /// </summary>
    private static int GetUserIdFromSession(HttpContext ctx)
    {
        // Láº¥y session tá»« context - Æ°u tiÃªn tá»« Items (Ä‘Æ°á»£c set bá»Ÿi middleware)
        if (ctx.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
        {
            return userId;
        }

        // Fallback: láº¥y tá»« cookie hoáº·c query string
        if (ctx.Request.Cookies.TryGetValue("session_id", out var sessionId))
        {
            // Lookup session tá»« LoginEndpoints.Sessions
            if (LoginEndpoints.Sessions.TryGetValue(sessionId, out var session))
            {
                return session.UserId;
            }
        }

        return 0;
    }

    /// <summary>
    /// Helper: Láº¥y game title tÄ©nh.
    /// </summary>
    private static string GetCurrentGameTitleStatic()
    {
        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId) ? GameIds.AgeOfEmpires4 : ServerRuntime.CurrentGameId;
    }
}

/// <summary>
/// Dá»¯ liá»‡u chat channel trong bá»™ nhá»›.
/// </summary>
internal sealed class ChatChannelData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ConcurrentDictionary<int, string> Users { get; set; } = new();
}

/// <summary>
/// DTO cho dá»¯ liá»‡u channel tá»« file JSON.
/// </summary>
internal sealed class ChatChannelFileData
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// DTO cho yÃªu cáº§u gá»­i tin nháº¯n text.
/// </summary>
public sealed class SendTextRequest : ChatroomRequest
{
    public string Message { get; set; } = string.Empty;
}
