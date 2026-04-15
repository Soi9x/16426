using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AgeLanServer.Server.Models.Playfab;

namespace AgeLanServer.Server.Routes.PlayFab;

/// <summary>
/// Endpoint phục vụ các file tĩnh từ thư mục resources/responses/athens/playfab/.
/// Tương đương http.FileServer(http.Dir(playfab.BaseDir)) trong Go (playfabapi.go).
/// Xử lý các request GET /playfab/static/* và trả về file với Content-Type chính xác.
/// </summary>
public static class StaticFileEndpoint
{
    // Ánh xạ extension sang Content-Type
    private static readonly Dictionary<string, string> ContentTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".json", "application/json" },
        { ".html", "text/html" },
        { ".css", "text/css" },
        { ".js", "application/javascript" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".svg", "image/svg+xml" },
        { ".webp", "image/webp" },
        { ".txt", "text/plain" },
        { ".xml", "application/xml" },
        { ".ico", "image/x-icon" },
        { ".woff", "font/woff" },
        { ".woff2", "font/woff2" },
        { ".ttf", "font/ttf" },
        { ".eot", "application/vnd.ms-fontobject" }
    };

    /// <summary>
    /// Đăng ký endpoint static file cho PlayFab.
    /// Route: GET /playfab/static/{*path}
    /// </summary>
    public static void RegisterEndpoints(WebApplication app)
    {
        app.MapGet("/playfab/static/{**path}", HandleStaticFile);
    }

    /// <summary>
    /// Xử lý yêu cầu file tĩnh.
    /// Đọc file từ thư mục PlayfabStaticConfig.BaseDir, trả về với Content-Type đúng.
    /// </summary>
    private static async Task<IResult> HandleStaticFile(string path, HttpContext ctx, ILogger<Program> logger)
    {
        // Ghép đường dẫn file từ base directory
        var baseDir = PlayfabStaticConfig.BaseDir;
        var relativePath = path.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(baseDir, relativePath);

        // Bảo mật: đảm bảo đường dẫn không vượt ra ngoài baseDir (tránh directory traversal)
        var fullNormalized = Path.GetFullPath(fullPath);
        var baseNormalized = Path.GetFullPath(baseDir);
        if (!fullNormalized.StartsWith(baseNormalized, StringComparison.Ordinal))
        {
            return Results.StatusCode(403); // Forbidden
        }

        // Kiểm tra file tồn tại
        if (!File.Exists(fullPath))
        {
            return Results.NotFound();
        }

        // Xác định Content-Type dựa trên extension
        var extension = Path.GetExtension(fullPath);
        var contentType = "application/octet-stream"; // Mặc định
        if (!string.IsNullOrEmpty(extension) && ContentTypeMap.TryGetValue(extension, out var mappedType))
        {
            contentType = mappedType;
        }

        // Đọc file và trả về
        try
        {
            var bytes = await File.ReadAllBytesAsync(fullPath);
            return Results.File(bytes, contentType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể đọc file tĩnh: {Path}", fullPath);
            return Results.StatusCode(500);
        }
    }
}
