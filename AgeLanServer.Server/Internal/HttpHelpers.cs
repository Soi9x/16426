// Port từ server/internal/http.go
// Tiện ích HTTP: JSON wrapper, bind request, write response.

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace AgeLanServer.Server.Internal;

/// <summary>
/// Wrapper JSON generic - tương đương Json[T] trong Go.
/// Dùng để deserialize JSON vào kiểu Data.
/// </summary>
public class JsonWrapper<T>
{
    [JsonPropertyName("data")]
    public T Data { get; set; } = default!;
}

/// <summary>
/// Tiện ích HTTP để viết JSON và bind dữ liệu từ request.
/// </summary>
public static class HttpHelpers
{
    /// <summary>
    /// Ghi header Content-Type cho JSON.
    /// </summary>
    public static void WriteJsonHeader(HttpResponse response)
    {
        response.ContentType = "application/json;charset=utf-8";
    }

    /// <summary>
    /// Ghi đối tượng dưới dạng JSON vào response.
    /// </summary>
    public static async Task JsonAsync(HttpResponse response, object data)
    {
        WriteJsonHeader(response);
        await JsonSerializer.SerializeAsync(response.Body, data);
    }

    /// <summary>
    /// Ghi raw JSON (byte[]) vào response.
    /// </summary>
    public static async Task RawJsonAsync(HttpResponse response, byte[] data)
    {
        WriteJsonHeader(response);
        await response.Body.WriteAsync(data);
    }

    /// <summary>
    /// Bind dữ liệu từ HTTP request vào đối tượng destination.
    /// - GET: bind từ query string.
    /// - Content-Type chứa "application/json": bind từ body JSON.
    /// - Còn lại: bind từ form POST.
    /// </summary>
    public static async Task<bool> BindAsync<T>(HttpRequest request, T destination)
    {
        try
        {
            if (HttpMethods.IsGet(request.Method))
            {
                // Bind từ query string
                BindFromQuery(request.Query, destination);
            }
            else if (request.ContentType != null && request.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                // Bind từ JSON body
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var obj = await JsonSerializer.DeserializeAsync<T>(request.Body, options);
                if (obj != null)
                {
                    CopyProperties(obj, destination);
                }
            }
            else
            {
                // Bind từ form
                await request.ReadFormAsync();
                BindFromForm(request.Form, destination);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Bind từ collection query/form vào đối tượng bằng reflection.
    /// </summary>
    private static void BindFromQuery(IQueryCollection query, object destination)
    {
        var props = destination.GetType().GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var prop in props)
        {
            if (query.TryGetValue(prop.Name, out var value) && value.Count > 0)
            {
                var strVal = value.ToString();
                if (!string.IsNullOrEmpty(strVal) && prop.CanWrite)
                {
                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    try
                    {
                        var converted = Convert.ChangeType(strVal, targetType);
                        prop.SetValue(destination, converted);
                    }
                    catch
                    {
                        // Bỏ qua nếu không convert được (tương đương ignore unknown keys trong Go)
                    }
                }
            }
        }
    }

    /// <summary>
    /// Bind từ form collection vào đối tượng bằng reflection.
    /// </summary>
    private static void BindFromForm(IFormCollection form, object destination)
    {
        var props = destination.GetType().GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var prop in props)
        {
            if (form.TryGetValue(prop.Name, out var value) && value.Count > 0)
            {
                var strVal = value.ToString();
                if (!string.IsNullOrEmpty(strVal) && prop.CanWrite)
                {
                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    try
                    {
                        var converted = Convert.ChangeType(strVal, targetType);
                        prop.SetValue(destination, converted);
                    }
                    catch
                    {
                        // Bỏ qua nếu không convert được
                    }
                }
            }
        }
    }

    /// <summary>
    /// Copy thuộc tính từ source sang destination.
    /// </summary>
    private static void CopyProperties<T>(T source, T destination)
    {
        var props = typeof(T).GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var prop in props)
        {
            if (prop.CanWrite)
            {
                prop.SetValue(destination, prop.GetValue(source));
            }
        }
    }
}
