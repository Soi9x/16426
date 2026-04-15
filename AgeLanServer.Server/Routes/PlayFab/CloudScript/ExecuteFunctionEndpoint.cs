// Port từ server/internal/routes/playfab/CloudScript/ExecuteFunction.go
// Endpoint /PlayFab/Client/ExecuteCloudScript (hoặc /CloudScript/ExecuteFunction) - thực thi cloud function.

using System.Text.Json;
using System.Text.Json.Serialization;
using AgeLanServer.Server.Internal;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.CloudScript;

/// <summary>
/// DTO yêu cầu cho ExecuteFunction.
/// </summary>
public sealed class ExecuteFunctionRequest
{
    [JsonPropertyName("CustomTags")]
    public object? CustomTags { get; set; }

    [JsonPropertyName("Entity")]
    public object? Entity { get; set; }

    [JsonPropertyName("FunctionName")]
    public string FunctionName { get; set; } = string.Empty;

    [JsonPropertyName("GeneratePlayStreamEvent")]
    public bool? GeneratePlayStreamEvent { get; set; }

    [JsonPropertyName("FunctionParameter")]
    public JsonElement? FunctionParameter { get; set; }
}

/// <summary>
/// DTO phản hồi cho ExecuteFunction.
/// </summary>
public sealed class ExecuteFunctionResponse
{
    [JsonPropertyName("ExecutionTimeMilliseconds")]
    public long ExecutionTimeMilliseconds { get; set; }

    [JsonPropertyName("FunctionName")]
    public string FunctionName { get; set; } = string.Empty;

    [JsonPropertyName("FunctionResult")]
    public JsonElement? FunctionResult { get; set; }

    [JsonPropertyName("FunctionResultSize")]
    public uint FunctionResultSize { get; set; }
}

/// <summary>
/// Endpoint ExecuteFunction - thực thi cloud script function.
/// Nhận FunctionName và FunctionParameter, gọi hàm tương ứng và trả về kết quả.
/// Trong LAN server, hỗ trợ một tập hợp các cloud script functions đã đăng ký.
/// </summary>
public static class ExecuteFunctionEndpoint
{
    // Registry các cloud script functions đã đăng ký
    private static readonly Dictionary<string, Func<JsonElement?, object?>> CloudScriptFunctions = new()
    {
        // Có thể đăng ký các functions tại đây
        // Example: { "MyFunction", (param) => new { result = "success" } }
    };

    /// <summary>
    /// Xử lý yêu cầu ExecuteFunction.
    /// Tìm và thực thi function theo FunctionName, trả về kết quả và thời gian thực thi.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        var req = new ExecuteFunctionRequest();
        var bound = await HttpHelpers.BindAsync(ctx.Request, req);

        if (!bound || string.IsNullOrEmpty(req.FunctionName))
        {
            await PlayFabResponder.RespondBadRequestAsync(ctx);
            return;
        }

        // Lấy cloud script function từ registry và thực thi
        if (CloudScriptFunctions.TryGetValue(req.FunctionName, out var fn))
        {
            var startTime = DateTime.UtcNow;
            var result = fn(req.FunctionParameter);
            var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            var response = new ExecuteFunctionResponse
            {
                ExecutionTimeMilliseconds = executionTime,
                FunctionName = req.FunctionName,
                FunctionResult = result != null ? JsonDocument.Parse(JsonSerializer.Serialize(result)).RootElement : null,
                FunctionResultSize = result != null ? (uint)JsonSerializer.Serialize(result).Length : 0
            };

            await PlayFabResponder.RespondOkAsync(ctx, response);
            return;
        }

        // Function không tồn tại - trả về BadRequest
        await PlayFabResponder.RespondBadRequestAsync(ctx);
    }

    /// <summary>
    /// Đăng ký một cloud script function.
    /// </summary>
    public static void RegisterFunction(string name, Func<JsonElement?, object?> handler)
    {
        CloudScriptFunctions[name] = handler;
    }

    /// <summary>
    /// Đăng ký endpoint ExecuteFunction.
    /// Route: POST /PlayFab/Client/ExecuteCloudScript
    /// hoặc POST /CloudScript/ExecuteFunction
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/ExecuteCloudScript", Handle);
        app.MapPost("/CloudScript/ExecuteFunction", Handle);
    }
}
