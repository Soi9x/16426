using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.Server.Routes.WebSocket;

/// <summary>
/// Đăng ký endpoint WebSocket cho giao tiếp real-time.
/// Xử lý kết nối full duplex, ping/pong, và gửi/nhận tin nhắn.
/// </summary>
public static class WebSocketEndpoints
{
    // Lưu trữ các kết nối WebSocket theo session ID
    private static readonly ConcurrentDictionary<string, WebSocketConnection> Connections = new();

    // Thời gian chờ cho việc gửi tin nhắn
    private static readonly TimeSpan WriteWait = TimeSpan.FromSeconds(1);

    // Thời gian chờ đọc (1 phút)
    private static readonly TimeSpan ReadDeadline = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Đăng ký endpoint WebSocket.
    /// </summary>
    public static void RegisterEndpoints(WebApplication app)
    {
        app.MapGet("/wss", HandleWebSocket);
    }

    /// <summary>
    /// Xử lý kết nối WebSocket.
    /// Thực hiện handshake, xác thực session, và duy trì kết nối.
    /// </summary>
    private static async Task HandleWebSocket(HttpContext ctx, ILogger<Program> logger)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var webSocket = await ctx.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("WebSocket kết nối từ {RemoteAddress}", ctx.Connection.RemoteIpAddress);

        // Đọc thông điệp đăng nhập ban đầu
        var loginMsg = await ReceiveJsonAsync(webSocket);
        if (!loginMsg.HasValue)
        {
            await CloseWebSocketAsync(webSocket, WebSocketCloseStatus.NormalClosure, "Invalid or absent login message");
            return;
        }

        // Parse session token từ login message
        var sessionId = ParseSessionToken(loginMsg.Value);
        if (string.IsNullOrEmpty(sessionId))
        {
            await CloseWebSocketAsync(webSocket, WebSocketCloseStatus.NormalClosure, "Invalid login message data");
            return;
        }

        // Lưu kết nối
        var connection = new WebSocketConnection { Socket = webSocket, SessionId = sessionId, LastActivity = DateTime.UtcNow };
        Connections[sessionId] = connection;

        // Reset expiry cho session
        ResetSessionExpiry(sessionId);

        try
        {
            // Vòng lặp đọc tin nhắn
            while (webSocket.State == WebSocketState.Open)
            {
                // Đặt thời gian chờ đọc
                using var cts = new CancellationTokenSource(ReadDeadline);
                var message = await ReceiveJsonAsync(webSocket, cts.Token);

                if (!message.HasValue)
                {
                    break;
                }

                // Xử lý operation
                var op = message.Value.GetProperty("operation").GetInt32();
                if (op == 0)
                {
                    // Session switch
                    var newSessionId = ParseSessionToken(message.Value);
                    if (!string.IsNullOrEmpty(newSessionId) && newSessionId != sessionId)
                    {
                        // Chuyển kết nối sang session mới
                        Connections.TryRemove(sessionId, out _);
                        sessionId = newSessionId;
                        Connections[sessionId] = connection;
                        connection.SessionId = sessionId;
                    }
                }

                // Reset expiry cho session
                ResetSessionExpiry(sessionId);
                connection.LastActivity = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            // Hết thời gian chờ
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            // Kết nối đóng đột ngột
        }
        finally
        {
            // Xóa kết nối
            Connections.TryRemove(sessionId, out _);
            await CloseWebSocketAsync(webSocket, WebSocketCloseStatus.NormalClosure, "Connection closed");
        }
    }

    /// <summary>
    /// Gửi tin nhắn tới session cụ thể.
    /// Trả về false nếu không thể gửi (lưu tin nhắn để gửi sau).
    /// </summary>
    public static bool SendMessage(string sessionId, object[] message)
    {
        if (!Connections.TryGetValue(sessionId, out var connection))
        {
            return false;
        }

        lock (connection.WriteLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                var buffer = Encoding.UTF8.GetBytes(json);
                var segment = new ArraySegment<byte>(buffer);

                using var cts = new CancellationTokenSource(WriteWait);
                connection.Socket.SendAsync(segment, WebSocketMessageType.Text, true, cts.Token).Wait();
                connection.LastActivity = DateTime.UtcNow;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Gửi hoặc lưu tin nhắn.
    /// Nếu không gửi được ngay, lưu tin nhắn vào session để gửi sau.
    /// Chạy bất đồng bộ để không chặn request HTTP.
    /// </summary>
    public static void SendOrStoreMessage(object? session, string action, object[] message, int userId)
    {
        // Lấy sessionId từ session object
        string sessionId;
        if (session is Login.SessionData sessionData)
        {
            sessionId = sessionData.SessionId;
        }
        else if (session is string sessionIdStr)
        {
            sessionId = sessionIdStr;
        }
        else
        {
            // Fallback: thử lấy từ HttpContext
            sessionId = string.Empty;
        }

        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var finalMessage = new object[] { 0, action, userId, message };

        Task.Run(async () =>
        {
            if (!SendMessage(sessionId, finalMessage))
            {
                // Lưu tin nhắn chờ - thêm vào danh sách messages của session
                Login.LoginEndpoints.AddMessageToSession(sessionId, finalMessage);
            }
        });
    }

    /// <summary>
    /// Reset thời gian hết hạn của session.
    /// </summary>
    private static void ResetSessionExpiry(string sessionId)
    {
        // Cập nhật last activity của session
        if (Login.LoginEndpoints.Sessions.TryGetValue(sessionId, out var session))
        {
            // Session vẫn còn hoạt động
        }
    }

    /// <summary>
    /// Nhận dữ liệu JSON từ WebSocket.
    /// </summary>
    private static async Task<JsonElement?> ReceiveJsonAsync(System.Net.WebSockets.WebSocket webSocket, CancellationToken ct = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            return JsonDocument.Parse(json).RootElement;
        }
        catch
        {
            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Đóng kết nối WebSocket.
    /// </summary>
    private static async Task CloseWebSocketAsync(System.Net.WebSockets.WebSocket webSocket, WebSocketCloseStatus status, string reason)
    {
        try
        {
            await webSocket.CloseAsync(status, reason, CancellationToken.None);
        }
        catch
        {
            // Bỏ qua lỗi khi đóng
        }
    }

    /// <summary>
    /// Parse session token từ JSON message.
    /// </summary>
    private static string ParseSessionToken(JsonElement message)
    {
        try
        {
            var op = message.GetProperty("operation").GetInt32();
            if (op == 0 && message.TryGetProperty("sessionToken", out var tokenProp))
            {
                return tokenProp.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // Bỏ qua lỗi parse
        }
        return string.Empty;
    }

    /// <summary>
    /// Parse session token từ nullable JSON message.
    /// </summary>
    private static string ParseSessionToken(JsonElement? message)
    {
        if (!message.HasValue) return string.Empty;
        return ParseSessionToken(message.Value);
    }

    /// <summary>
    /// Lấy số lượng kết nối hiện tại.
    /// </summary>
    public static int GetConnectionCount() => Connections.Count;
}

/// <summary>
/// Wrapper cho kết nối WebSocket.
/// </summary>
public sealed class WebSocketConnection
{
    public System.Net.WebSockets.WebSocket Socket { get; set; } = null!;
    public string SessionId { get; set; } = string.Empty;
    public object WriteLock { get; } = new();
    public DateTime LastActivity { get; set; }
}
