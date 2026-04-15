// Port từ server/internal/routes/playfab/Event/WriteTelemetryEvents.go
// Endpoint /PlayFab/Client/WriteTelemetryEvents - ghi nhận sự kiện telemetry.

using System.Text.Json.Serialization;
using AgeLanServer.Server.Routes.PlayFab.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgeLanServer.Server.Routes.PlayFab.Event;

/// <summary>
/// DTO phản hồi cho WriteTelemetryEvents.
/// </summary>
public sealed class WriteTelemetryEventsResponse
{
    [JsonPropertyName("AssignedEventIds")]
    public List<object> AssignedEventIds { get; set; } = new();
}

/// <summary>
/// Endpoint WriteTelemetryEvents - nhận và ghi nhận các sự kiện telemetry từ client.
/// Trong LAN server, endpoint này trả về danh sách rỗng vì không lưu trữ telemetry.
/// </summary>
public static class WriteTelemetryEventsEndpoint
{
    /// <summary>
    /// Xử lý yêu cầu WriteTelemetryEvents.
    /// Trả về danh sách AssignedEventIds rỗng.
    /// </summary>
    public static async Task Handle(HttpContext ctx)
    {
        var response = new WriteTelemetryEventsResponse
        {
            AssignedEventIds = new List<object>()
        };
        await PlayFabResponder.RespondOkAsync(ctx, response);
    }

    /// <summary>
    /// Đăng ký endpoint WriteTelemetryEvents.
    /// Route: POST /PlayFab/Client/WriteTelemetryEvents
    /// hoặc POST /Event/WriteTelemetryEvents
    /// </summary>
    public static void RegisterEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/PlayFab/Client/WriteTelemetryEvents", Handle);
        app.MapPost("/Event/WriteTelemetryEvents", Handle);
    }
}
