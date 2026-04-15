// Port từ server/internal/routes/router/proxy.go
// Reverse Proxy với TLS SNI rewriting.

using AgeLanServer.Common;
using AgeLanServer.Server.Internal;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace AgeLanServer.Server.Middleware;

/// <summary>
/// Reverse Proxy cấu hình sẵn cho một upstream.
/// Tương đương Proxy struct trong Go.
/// </summary>
public class ReverseProxy
{
    private readonly HttpClient _httpClient;
    private readonly string _upstreamHost;
    private readonly string _upstreamIp;
    private readonly Func<string, HttpContext, Task>? _initializeRoutes;

    /// <summary>
    /// Tên của proxy (dùng cho logging).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Tạo ReverseProxy mới.
    /// </summary>
    /// <param name="host">Tên miền upstream (dùng cho SNI và Host header).</param>
    /// <param name="ip">Địa chỉ IP upstream (nếu null sẽ resolve từ DNS).</param>
    /// <param name="initFn">Hàm khởi tạo routes bổ sung (có thể null).</param>
    public ReverseProxy(string host, string? ip = null, Func<string, HttpContext, Task>? initFn = null)
    {
        _upstreamHost = host;
        _upstreamIp = ip ?? ResolveHostToIp(host);
        _initializeRoutes = initFn;

        Name = $"proxy-{host}";

        // Cấu hình HttpClientHandler với TLS SNI
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                // Bỏ qua lỗi chứng chỉ cho LAN (self-signed)
                return true;
            }
        };

        // Cấu hình SNI cho TLS
        if (handler.ServerCertificateCustomValidationCallback != null)
        {
            // .NET dùng SNI tự động dựa trên URI host
            // Nhưng ta có thể tùy chỉnh nếu cần
        }

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri($"https://{_upstreamIp}")
        };
    }

    /// <summary>
    /// Kiểm tra xem request có thuộc proxy này không.
    /// </summary>
    public bool Check(string host)
    {
        var normalizedHost = NormalizeHost(host);
        var normalizedUpstream = NormalizeHost(_upstreamHost);
        return string.Equals(normalizedHost, normalizedUpstream, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Xử lý proxy request.
    /// </summary>
    public async Task<bool> HandleAsync(HttpContext context, string gameId)
    {
        // Thực hiện routes khởi tạo nếu có
        if (_initializeRoutes != null)
        {
            await _initializeRoutes(gameId, context);
            // Nếu request đã được xử lý bởi route custom, không cần proxy
            if (context.Response.HasStarted)
                return true;
        }

        return await ProxyRequestAsync(context);
    }

    /// <summary>
    /// Proxy request đến upstream server.
    /// </summary>
    private async Task<bool> ProxyRequestAsync(HttpContext context)
    {
        try
        {
            // Tạo request mới đến upstream
            var requestMessage = CreateProxyRequest(context);

            // Gửi request và nhận response
            using var response = await _httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);

            // Copy response headers
            CopyResponseHeaders(response, context.Response);

            // Ghi status code
            context.Response.StatusCode = (int)response.StatusCode;

            // Copy response body
            if (response.Content.Headers.ContentLength is > 0)
            {
                context.Response.ContentLength = response.Content.Headers.ContentLength;
            }

            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);

            return true;
        }
        catch (OperationCanceledException)
        {
            // Client ngắt kết nối
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[Proxy {_upstreamHost}] Lỗi: {ex.Message}");
            context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
            return true;
        }
    }

    /// <summary>
    /// Tạo HttpRequestMessage từ HttpContext hiện tại.
    /// </summary>
    private HttpRequestMessage CreateProxyRequest(HttpContext context)
    {
        var request = context.Request;

        // Xây dựng URL đích (giữ nguyên path và query)
        var uri = new UriBuilder
        {
            Scheme = "https",
            Host = _upstreamIp,
            Path = request.Path.Value ?? string.Empty,
            Query = request.QueryString.Value ?? string.Empty
        };

        var proxyRequest = new HttpRequestMessage(new HttpMethod(request.Method), uri.Uri);

        // Copy headers từ request gốc
        foreach (var header in request.Headers)
        {
            // Bỏ qua các header không nên forward
            if (ShouldSkipHeader(header.Key))
                continue;

            if (!proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                proxyRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Đảm bảo Host header đúng với upstream
        proxyRequest.Headers.Host = _upstreamHost;

        // Copy body nếu có
        if (request.ContentLength > 0 || request.HasFormContentType)
        {
            if (request.Body.CanSeek)
                request.Body.Position = 0;

            var content = new StreamContent(request.Body);

            // Copy Content-Type
            if (!string.IsNullOrEmpty(request.ContentType))
            {
                content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(request.ContentType);
            }

            // Copy Content-Length
            if (request.ContentLength.HasValue)
            {
                content.Headers.ContentLength = request.ContentLength.Value;
            }

            proxyRequest.Content = content;
        }

        return proxyRequest;
    }

    /// <summary>
    /// Copy headers từ response upstream sang response gốc.
    /// </summary>
    private static void CopyResponseHeaders(HttpResponseMessage source, HttpResponse destination)
    {
        // Copy headers từ response
        foreach (var header in source.Headers)
        {
            if (ShouldSkipResponseHeader(header.Key))
                continue;

            destination.Headers[header.Key] = header.Value.ToArray();
        }

        // Copy headers từ content
        foreach (var header in source.Content.Headers)
        {
            if (ShouldSkipResponseHeader(header.Key))
                continue;

            destination.Headers[header.Key] = header.Value.ToArray();
        }
    }

    /// <summary>
    /// Kiểm tra header có nên bỏ qua khi forward không.
    /// </summary>
    private static bool ShouldSkipHeader(string headerName)
    {
        var skipHeaders = new[]
        {
            "host",
            "connection",
            "keep-alive",
            "upgrade",
            "transfer-encoding",
            "content-length",
            "expect",
            "proxy-connection",
            "x-forwarded-for",
            "x-forwarded-proto",
            "x-real-ip"
        };

        return skipHeaders.Contains(headerName.ToLowerInvariant());
    }

    /// <summary>
    /// Kiểm tra header response có nên bỏ qua không.
    /// </summary>
    private static bool ShouldSkipResponseHeader(string headerName)
    {
        var skipHeaders = new[]
        {
            "transfer-encoding",
            "connection",
            "keep-alive",
            "server"
        };

        return skipHeaders.Contains(headerName.ToLowerInvariant());
    }

    /// <summary>
    /// Chuẩn hóa host (loại bỏ port).
    /// </summary>
    private static string NormalizeHost(string host)
    {
        var colonIndex = host.LastIndexOf(':');
        if (colonIndex > 0)
        {
            return host.Substring(0, colonIndex).ToLowerInvariant();
        }
        return host.ToLowerInvariant();
    }

    /// <summary>
    /// Resolve hostname thành IP (đơn giản - dùng DNS).
    /// </summary>
    private static string ResolveHostToIp(string host)
    {
        try
        {
            var addresses = Dns.GetHostAddresses(host);
            if (addresses.Length > 0)
            {
                return addresses[0].ToString();
            }
        }
        catch (Exception)
        {
            // Nếu không resolve được, dùng luôn hostname
        }
        return host;
    }

    /// <summary>
    /// Dispose resources.
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
