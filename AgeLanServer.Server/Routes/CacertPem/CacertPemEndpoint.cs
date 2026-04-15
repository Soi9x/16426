// Port từ server/internal/routes/cacert.pem/cacertPem.go
// Endpoint /cacert.pem - trả về file chứng chỉ CA.

using AgeLanServer.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.CacertPem;

/// <summary>
/// Endpoint CacertPem - trả về file chứng chỉ CA certificate.
/// Phục vụ cho việc thiết lập kết nối SSL/TLS an toàn giữa client và server.
/// Trong LAN server, trả về file cacert.pem từ thư mục cấu hình.
/// </summary>
public static class CacertPemEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu cacert.pem.
    /// Kiểm tra và trả về file chứng chỉ CA thích hợp.
    /// Nếu không tìm thấy file, trả về 404.
    /// Nếu có lỗi hệ thống, trả về 500.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        try
        {
            // Xác định thư mục chứa certificate
            var certificateFolder = GetCertificateFolder();

            if (string.IsNullOrEmpty(certificateFolder))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            // Xác định file chứng chỉ dựa trên game title
            var certificateFile = GetCertificateFileName(ctx);

            var filePath = Path.Combine(certificateFolder, certificateFile);

            if (!File.Exists(filePath))
            {
                // Thử tìm trong thư mục etc
                var etcPath = Path.Combine(AppConstants.ResourcesDir, "etc", certificateFile);
                if (File.Exists(etcPath))
                {
                    ctx.Response.ContentType = "application/x-pem-file";
                    await ctx.Response.SendFileAsync(etcPath);
                    return;
                }

                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            ctx.Response.ContentType = "application/x-pem-file";
            await ctx.Response.SendFileAsync(filePath);
        }
        catch
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
    }

    /// <summary>
    /// Lấy thư mục chứa chứng chỉ.
    /// Dựa trên đường dẫn executable.
    /// </summary>
    private static string? GetCertificateFolder()
    {
        // Lấy thư mục chứa executable
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            // Fallback: thư mục hiện tại
            return Directory.GetCurrentDirectory();
        }

        var folder = Path.GetDirectoryName(exePath);
        return folder;
    }

    /// <summary>
    /// Lấy tên file chứng chỉ dựa trên game title.
    /// Mặc định: trả về cacert.pem
    /// </summary>
    private static string GetCertificateFileName(HttpContext ctx)
    {
        // Kiểm tra game title từ query string hoặc header
        var gameTitle = ctx.Request.Query["game"].ToString();

        // Nếu có game title cụ thể, có thể dùng self-signed cert
        if (!string.IsNullOrEmpty(gameTitle) && gameTitle != "age4")
        {
            // Ưu tiên dùng cacert.pem
            return AppConstants.CaCert;
        }

        // Mặc định: trả về cacert.pem
        return AppConstants.CaCert;
    }

    /// <summary>
    /// Đăng ký endpoint CacertPem.
    /// Route: GET /cacert.pem
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/cacert.pem", Handle);
    }
}
