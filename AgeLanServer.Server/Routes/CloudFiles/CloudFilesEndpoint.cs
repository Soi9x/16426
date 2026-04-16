// Port từ server/internal/routes/cloudfiles/cloudfiles.go
// Endpoint /cloudfiles/* - phục vụ file từ cloud storage nội bộ.
// Mô phỏng Azure Blob Storage HTTP API để client tải file.

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.Cloud;

namespace AgeLanServer.Server.Routes.CloudFiles;

/// <summary>
/// Thông tin file trong cloud storage.
/// </summary>
public sealed class CloudFileInfo
{
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Length { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Thông tin xác thực truy cập cloud storage.
/// </summary>
public sealed class CloudFileCredentials
{
    public string Sig { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Endpoint Cloudfiles - phục vụ file từ cloud storage nội bộ.
/// Mô phỏng Azure Blob Storage HTTP API với các headers tương thích.
/// Client truy cập file thông qua URL có chữ ký (sig) và key file.
/// </summary>
public static class CloudFilesEndpoint
{
    /// <summary>
    /// Bộ đệm cloud file - lưu trữ tạm thời các file có sẵn.
    /// Key: đường dẫn file, Value: nội dung file.
    /// </summary>
    private static readonly Dictionary<string, CloudFileInfo> FileMetadata = new();

    /// <summary>
    /// Bộ đệm credentials - lưu trữ chữ ký truy cập hợp lệ.
    /// Key: chữ ký (sig), Value: thông tin credentials.
    /// </summary>
    private static readonly Dictionary<string, CloudFileCredentials> CredentialsStore = new();

    /// <summary>
    /// Thư mục lưu trữ file cloud storage.
    /// </summary>
    private static string CloudFilesDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Thiết lập thư mục và dữ liệu cloud files.
    /// Gọi khi khởi tạo server.
    /// </summary>
    public static void Initialize(string directory, Dictionary<string, CloudFileInfo> metadata, Dictionary<string, CloudFileCredentials> credentials)
    {
        CloudFilesDirectory = directory;
        FileMetadata.Clear();
        foreach (var kvp in metadata)
        {
            FileMetadata[kvp.Key] = kvp.Value;
        }
        CredentialsStore.Clear();
        foreach (var kvp in credentials)
        {
            CredentialsStore[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Xử lý yêu cầu Cloudfiles.
    /// - Kiểm tra chữ ký (sig) từ query string để xác thực.
    /// - Tìm file theo key từ URL path.
    /// - Kiểm tra chữ ký file khớp với credentials.
    /// - Đọc và trả về file với các headers tương thích Azure Blob Storage.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        // Lấy chữ ký từ query string
        var sig = ctx.Request.Query["sig"].ToString();

        // Kiểm tra credentials
        if (!CredentialsStore.TryGetValue(sig, out var credentials))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsync("Unauthorized");
            return;
        }

        // Lấy file key từ URL path
        // URL format: /cloudfiles/key1/key2/key3 -> key = "key1/key2/key3"
        var path = ctx.Request.Path.Value ?? "/cloudfiles/";
        var key = path.StartsWith("/cloudfiles/") ? path.Substring("/cloudfiles/".Length) : path.TrimStart('/');

        // Tìm file metadata
        if (!FileMetadata.TryGetValue(key, out var fileInfo))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsync("Not Found");
            return;
        }

        // Kiểm tra chữ ký file khớp với credentials
        if (fileInfo.Key != credentials.Data)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsync("Incorrect signature");
            return;
        }

        // Đọc file
        var filePath = Path.Combine(CloudFilesDirectory, key);
        if (!File.Exists(filePath))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsync("Not Found");
            return;
        }

        // Thiết lập headers tương thích Azure Blob Storage
        var lengthStr = fileInfo.Length.ToString();
        ctx.Response.Headers["Content-Length"] = lengthStr;
        ctx.Response.Headers["Content-Type"] = fileInfo.Type;
        ctx.Response.Headers["Content-MD5"] = fileInfo.Checksum;
        ctx.Response.Headers["Last-Modified"] = fileInfo.Created;
        ctx.Response.Headers["Accept-Range"] = "bytes";
        ctx.Response.Headers["ETag"] = fileInfo.ETag;
        ctx.Response.Headers["Server"] = "Windows-Azure-Blob/1.0 Microsoft-HTTPAPI/2.0";
        ctx.Response.Headers["x-ms-request-id"] = GenerateRequestId();
        ctx.Response.Headers["x-ms-version"] = fileInfo.Version;

        // Với game age3 và athens, bỏ qua 2 headers x-ms-meta-filename và x-ms-meta-ContentLength
        // Tương đương logic trong Go: if models.G(r).Title() != common.GameAoE3 && models.G(r).Title() != common.GameAoM
        var gameTitle = ExtractGameTitle(ctx);
        if (!CloudEndpoints.ShouldSkipMetaHeaders(gameTitle))
        {
            ctx.Response.Headers["x-ms-meta-filename"] = key;
            ctx.Response.Headers["x-ms-meta-ContentLength"] = lengthStr;
        }

        ctx.Response.Headers["x-ms-creation-time"] = fileInfo.Created;
        ctx.Response.Headers["x-ms-lease-status"] = "unlocked";
        ctx.Response.Headers["x-ms-lease-state"] = "available";
        ctx.Response.Headers["x-ms-blob-type"] = "BlockBlob";
        ctx.Response.Headers["x-ms-server-encrypted"] = "true";
        ctx.Response.Headers["Date"] = DateTime.UtcNow.ToString("R");

        // Gửi file
        await ctx.Response.SendFileAsync(filePath);
    }

    /// <summary>
    /// Sinh request ID theo định dạng GUID.
    /// Tương đương generateRequestId trong Go.
    /// </summary>
    private static string GenerateRequestId()
    {
        // Tạo GUID với version 4 (random)
        var guid = Guid.NewGuid();
        return guid.ToString();
    }

    /// <summary>
    /// Trích xuất game title từ HTTP context.
    /// Ưu tiên header X-Game-Title, fallback về runtime game hiện tại.
    /// </summary>
    private static string ExtractGameTitle(HttpContext ctx)
    {
        if (ctx.Request.Headers.TryGetValue("X-Game-Title", out var headerTitle) &&
            !string.IsNullOrEmpty(headerTitle))
        {
            return headerTitle.ToString()!;
        }

        return string.IsNullOrWhiteSpace(ServerRuntime.CurrentGameId)
            ? GameIds.AgeOfEmpires4
            : ServerRuntime.CurrentGameId;
    }

    /// <summary>
    /// Đăng ký endpoint Cloudfiles.
    /// Route: GET /cloudfiles/{*path}
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/cloudfiles/{*path}", Handle);
    }
}
