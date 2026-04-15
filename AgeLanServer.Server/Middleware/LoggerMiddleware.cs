// Port từ server/internal/routes/router/loggerMiddleware.go
// Middleware ghi log request/response, tính SHA512 hash, và log vào comm buffer.

using AgeLanServer.Common;
using AgeLanServer.Common.ServerCommunication;
using AgeLanServer.Server.Internal.Logger;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;

namespace AgeLanServer.Server.Middleware;

/// <summary>
/// Wrapper cho HttpResponse để ghi lại response body.
/// Tương đương ResponseWriterWrapper trong Go.
/// </summary>
public class ResponseWriterWrapper : Stream
{
    private readonly Stream _innerStream;
    private readonly MemoryStream _bodyBuffer;
    private bool _headersSent;

    /// <summary>
    /// Status code của response.
    /// </summary>
    public int StatusCode { get; private set; } = StatusCodes.Status200OK;

    /// <summary>
    /// Body của response (đã buffer).
    /// </summary>
    public byte[] ResponseBody => _bodyBuffer.ToArray();

    /// <summary>
    /// Headers của response.
    /// </summary>
    public IHeaderDictionary Headers { get; }

    public ResponseWriterWrapper(HttpResponse response)
    {
        _innerStream = response.Body;
        _bodyBuffer = new MemoryStream();
        Headers = response.Headers;

        // Thay thế body stream bằng buffer
        response.Body = this;
    }

    /// <summary>
    /// Ghi status code.
    /// </summary>
    public void OnStarting(int statusCode)
    {
        if (StatusCode == StatusCodes.Status200OK)
        {
            StatusCode = statusCode;
        }
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        _innerStream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return _innerStream.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        // Ghi vào buffer để lưu lại
        _bodyBuffer.Write(buffer, offset, count);

        // Ghi vào stream gốc
        _innerStream.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        // Ghi vào buffer
        _bodyBuffer.Write(buffer, offset, count);

        // Ghi vào stream gốc
        return _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Ghi vào buffer
        _bodyBuffer.Write(buffer.Span);

        // Ghi vào stream gốc
        return _innerStream.WriteAsync(buffer, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        // Không dispose inner stream
        _bodyBuffer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// Middleware ghi log request/response với SHA512 hash.
/// Tương đương NewLoggingMiddleware trong Go.
/// </summary>
public class LoggerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly CommLogBuffer? _commBuffer;

    /// <summary>
    /// Tạo LoggerMiddleware.
    /// </summary>
    /// <param name="next">Middleware tiếp theo trong pipeline.</param>
    /// <param name="commBuffer">Buffer log giao tiếp (có thể null).</param>
    public LoggerMiddleware(RequestDelegate next, CommLogBuffer? commBuffer = null)
    {
        _next = next;
        _commBuffer = commBuffer ?? CommLogBuffer.Instance;
    }

    /// <summary>
    /// Xử lý logging cho request/response.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var requestStart = DateTime.UtcNow;

        // Đọc request body
        var requestBody = await ReadRequestBody(context.Request);

        // Reset body stream để middleware sau đọc được
        if (context.Request.Body.CanSeek)
        {
            context.Request.Body.Position = 0;
        }

        // Wrapper response để buffer body
        var responseWrapper = new ResponseWriterWrapper(context.Response);

        // Gọi middleware tiếp theo
        await _next(context);

        var requestLatency = DateTime.UtcNow - requestStart;

        // Lấy response body từ wrapper
        var responseBody = responseWrapper.ResponseBody;

        // Tính SHA512 hash của response body (nếu có)
        byte[]? bodyHash = null;
        if (context.Request.Method != HttpMethods.Head && responseBody.Length > 0)
        {
            bodyHash = SHA512.HashData(responseBody);
        }

        // Nếu response body quá lớn (> 4KB), không ghi body vào log
        var loggedResponseBody = responseBody.Length > 4096 ? Array.Empty<byte>() : responseBody;

        // Xây dựng URL đầy đủ
        var url = BuildFullUrl(context.Request);

        // Tạo log entry
        var logEntry = new
        {
            type = MessageTypes.MessageRequest,
            @in = new
            {
                body = new { body = requestBody },
                headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                uptime = UptimeHelper.GetUptime(),
                sender = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                url = url,
                method = context.Request.Method
            },
            @out = new
            {
                body = new { body = loggedResponseBody },
                headers = responseWrapper.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                bodyHash = bodyHash != null ? Convert.ToBase64String(bodyHash) : null,
                statusCode = responseWrapper.StatusCode,
                latency = requestLatency.TotalMilliseconds
            },
            timestamp = DateTime.UtcNow
        };

        // Ghi vào comm buffer
        _commBuffer?.Log(logEntry);
    }

    /// <summary>
    /// Đọc toàn bộ request body.
    /// </summary>
    private static async Task<byte[]> ReadRequestBody(HttpRequest request)
    {
        if (request.ContentLength == 0 || request.Body == Stream.Null)
        {
            return Array.Empty<byte>();
        }

        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Xây dựng URL đầy đủ từ request.
    /// </summary>
    private static string BuildFullUrl(HttpRequest request)
    {
        var scheme = request.Scheme;
        var host = request.Host.Value ?? "localhost";
        var path = request.Path.Value ?? string.Empty;
        var query = request.QueryString.Value ?? string.Empty;

        return $"{scheme}://{host}{path}{query}";
    }
}

/// <summary>
/// Extension methods để đăng ký LoggerMiddleware.
/// </summary>
public static class LoggerMiddlewareExtensions
{
    /// <summary>
    /// Sử dụng request/response logging middleware.
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(
        this IApplicationBuilder app,
        CommLogBuffer? commBuffer = null)
    {
        return app.Use(async (context, next) =>
        {
            var middleware = new LoggerMiddleware(next, commBuffer);
            await middleware.InvokeAsync(context);
        });
    }
}

/// <summary>
/// Struct đại diện cho log entry (tương đương request.Read trong Go).
/// </summary>
public struct RequestLogEntry
{
    /// <summary>
    /// Loại thông điệp.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Thông tin request (inbound).
    /// </summary>
    public RequestIn In { get; set; }

    /// <summary>
    /// Thông tin response (outbound).
    /// </summary>
    public RequestOut Out { get; set; }

    /// <summary>
    /// Thời gian ghi log.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Thông tin request inbound.
/// </summary>
public struct RequestIn
{
    public RequestBase Base { get; set; }
    public TimeSpan Uptime { get; set; }
    public string Sender { get; set; }
    public string Url { get; set; }
    public string Method { get; set; }
}

/// <summary>
/// Thông tin request outbound.
/// </summary>
public struct RequestOut
{
    public RequestBase Base { get; set; }
    public string? BodyHash { get; set; }
    public int StatusCode { get; set; }
    public double Latency { get; set; }
}

/// <summary>
/// Thông tin cơ bản của request/response.
/// </summary>
public struct RequestBase
{
    public RequestBody Body { get; set; }
    public Dictionary<string, string> Headers { get; set; }
}

/// <summary>
/// Nội dung body.
/// </summary>
public struct RequestBody
{
    public byte[] Body { get; set; }
}
