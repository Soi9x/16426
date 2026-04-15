// Port từ server/internal/routes/playfab/Client/shared/response.go
// Các hàm trợ giúp phản hồi theo định dạng PlayFab API wrapper.

using System.Text.Json;
using System.Text.Json.Serialization;
using AgeLanServer.Server.Internal;
using Microsoft.AspNetCore.Http;

namespace AgeLanServer.Server.Routes.PlayFab.Shared;

/// <summary>
/// Phản hồi cơ bản từ PlayFab API - chứa mã và trạng thái.
/// </summary>
public class PlayFabResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Phản hồi thành công từ PlayFab API - chứa dữ liệu bên trong.
/// </summary>
public class PlayFabOkResponse<T> : PlayFabResponse
{
    [JsonPropertyName("data")]
    public T Data { get; set; } = default!;
}

/// <summary>
/// Phản hồi lỗi từ PlayFab API.
/// </summary>
public class PlayFabErrorResponse : PlayFabResponse
{
    [JsonPropertyName("errorCode")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Các phương thức trợ giúp để gửi phản hồi PlayFab API.
/// Tương đương các hàm Respond, RespondOK, RespondBadRequest, RespondNotAvailable trong Go.
/// </summary>
public static class PlayFabResponder
{
    /// <summary>
    /// Gửi phản hồi OK với dữ liệu.
    /// </summary>
    public static async Task RespondOkAsync<T>(HttpContext ctx, T data)
    {
        var response = new PlayFabOkResponse<T>
        {
            Code = 200,
            Status = "OK",
            Data = data
        };
        await HttpHelpers.JsonAsync(ctx.Response, response);
    }

    /// <summary>
    /// Gửi phản hồi lỗi BadRequest.
    /// </summary>
    public static async Task RespondBadRequestAsync(HttpContext ctx)
    {
        var response = new PlayFabErrorResponse
        {
            Code = 400,
            Status = "BadRequest",
            ErrorCode = -1,
            Error = "Could not parse request body.",
            ErrorMessage = ""
        };
        ctx.Response.StatusCode = StatusCodes.Status200OK; // PlayFab luôn trả về 200
        await HttpHelpers.JsonAsync(ctx.Response, response);
    }

    /// <summary>
    /// Gửi phản hồi lỗi ServiceUnavailable.
    /// </summary>
    public static async Task RespondNotAvailableAsync(HttpContext ctx)
    {
        var response = new PlayFabErrorResponse
        {
            Code = 503,
            Status = "ServiceUnavailable",
            ErrorCode = -2,
            Error = "Service is currently not available.",
            ErrorMessage = ""
        };
        ctx.Response.StatusCode = StatusCodes.Status200OK; // PlayFab luôn trả về 200
        await HttpHelpers.JsonAsync(ctx.Response, response);
    }
}
