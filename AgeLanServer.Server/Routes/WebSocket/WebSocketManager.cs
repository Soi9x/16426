using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AgeLanServer.Server.Routes.Login;
using WsSocket = System.Net.WebSockets.WebSocket;

namespace AgeLanServer.Server.Routes.WebSocket;

/// <summary>
/// Quản lý WebSocket connections và gửi/nhận tin nhắn real-time.
/// Tương đương server/internal/routes/wss/wss.go trong Go gốc.
/// </summary>
public static class WsMessageSender
{
    /// <summary>
    /// Lưu trữ WebSocket connections theo sessionId.
    /// </summary>
    private static readonly ConcurrentDictionary<string, WsConn> Connections = new();

    /// <summary>
    /// Thời gian chờ write (1 giây như Go gốc).
    /// </summary>
    private static readonly TimeSpan WriteWait = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Thời gian timeout read (1 phút như Go gốc).
    /// </summary>
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Wrapper cho WebSocket connection với thread-safe lock.
    /// </summary>
    private sealed record WsConn(WsSocket Ws, string Sid, DateTime LastActivity)
    {
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
    }

    /// <summary>
    /// Thêm connection mới vào danh sách.
    /// </summary>
    public static void AddConnection(string sessionId, WsSocket ws)
    {
        Connections[sessionId] = new WsConn(ws, sessionId, DateTime.UtcNow);
    }

    /// <summary>
    /// Xóa connection khỏi danh sách.
    /// </summary>
    public static void RemoveConnection(string sessionId)
    {
        Connections.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// Cập nhật thời gian hoạt động cuối của connection.
    /// </summary>
    public static void ResetSessionExpiry(string sessionId)
    {
        if (Connections.TryGetValue(sessionId, out var conn))
        {
            Connections[sessionId] = conn with { LastActivity = DateTime.UtcNow };
        }
    }

    /// <summary>
    /// Gửi tin nhắn qua WebSocket. Nếu connection không tồn tại hoặc lỗi, lưu vào session queue.
    /// Tương đương SendOrStoreMessage() trong Go.
    /// </summary>
    public static async Task SendOrStoreMessageAsync(string sessionId, string action, object message)
    {
        var userId = LoginEndpoints.GetUserIdFromSession(sessionId) ?? 0;
        var finalMessage = new object[] { 0, action, userId, message };

        if (await TrySendMessageAsync(sessionId, finalMessage))
            return;

        LoginEndpoints.AddMessageToSession(sessionId, finalMessage);
    }

    /// <summary>
    /// Thử gửi tin nhắn qua WebSocket connection.
    /// </summary>
    private static async Task<bool> TrySendMessageAsync(string sessionId, object message)
    {
        if (!Connections.TryGetValue(sessionId, out var conn))
            return false;

        await conn.WriteLock.WaitAsync();
        try
        {
            if (conn.Ws.State != WebSocketState.Open)
                return false;

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            await conn.Ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            ResetSessionExpiry(sessionId);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            conn.WriteLock.Release();
        }
    }

    /// <summary>
    /// Xử lý vòng đời WebSocket connection: login, ping/pong, đọc messages.
    /// </summary>
    public static async Task HandleConnectionAsync(WsSocket ws, HttpContext httpContext, CancellationToken ct)
    {
        var sessionId = httpContext.Request.Query["sessionToken"].ToString();

        var loginMsg = await ReceiveJsonAsync(ws, ct);
        if (loginMsg == null)
        {
            await CloseAsync(ws, WebSocketCloseStatus.NormalClosure, "Invalid login message");
            return;
        }

        if (loginMsg.Value.TryGetProperty("sessionToken", out var tokenElem))
            sessionId = tokenElem.GetString() ?? sessionId;

        if (string.IsNullOrEmpty(sessionId) || !LoginEndpoints.Sessions.ContainsKey(sessionId))
        {
            await CloseAsync(ws, WebSocketCloseStatus.NormalClosure, "Invalid session");
            return;
        }

        AddConnection(sessionId, ws);

        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(ReadTimeout);

                try
                {
                    var msg = await ReceiveJsonAsync(ws, cts.Token);
                    if (msg == null) break;

                    if (msg.Value.TryGetProperty("operation", out var opElem))
                    {
                        var op = opElem.GetUInt32();
                        if (op == 0 && msg.Value.TryGetProperty("sessionToken", out var newTokenElem))
                        {
                            var newSid = newTokenElem.GetString();
                            if (!string.IsNullOrEmpty(newSid) && newSid != sessionId)
                            {
                                Connections.TryRemove(sessionId, out _);
                                sessionId = newSid;
                                AddConnection(sessionId, ws);
                            }
                        }
                    }
                    ResetSessionExpiry(sessionId);
                }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            RemoveConnection(sessionId);
            await CloseAsync(ws, WebSocketCloseStatus.NormalClosure, "Connection closed");
        }
    }

    /// <summary>
    /// Nhận JSON message từ WebSocket.
    /// </summary>
    private static async Task<JsonElement?> ReceiveJsonAsync(WsSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
        if (result.MessageType == WebSocketMessageType.Close) return null;

        using var ms = new MemoryStream();
        ms.Write(buffer, 0, result.Count);
        while (!result.EndOfMessage)
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            ms.Write(buffer, 0, result.Count);
        }
        return JsonDocument.Parse(ms.ToArray()).RootElement;
    }

    /// <summary>
    /// Đóng WebSocket connection.
    /// </summary>
    private static async Task CloseAsync(WsSocket ws, WebSocketCloseStatus status, string reason)
    {
        try
        {
            if (ws.State == WebSocketState.Open)
                await ws.CloseOutputAsync(status, reason, CancellationToken.None);
        }
        catch { }
        finally { ws.Dispose(); }
    }
}
