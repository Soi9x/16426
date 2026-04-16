// Port từ server/internal/http.go
// Tiện ích HTTP: JSON wrapper, bind request, write response.

using System.Collections;
using System.Globalization;
using System.Net;
using System.Reflection;
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

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class BindAliasAttribute : Attribute
{
    public BindAliasAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
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
                using var doc = await JsonDocument.ParseAsync(request.Body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    BindFromJsonObject(doc.RootElement, destination!);
                }
                else
                {
                    var obj = JsonSerializer.Deserialize<T>(doc.RootElement.GetRawText(), JsonOptions);
                    if (obj != null)
                    {
                        CopyProperties(obj, destination);
                    }
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
        BindFromValues(destination, key =>
        {
            if (query.TryGetValue(key, out var value) && value.Count > 0)
            {
                return value.ToString();
            }

            return null;
        });
    }

    /// <summary>
    /// Bind từ form collection vào đối tượng bằng reflection.
    /// </summary>
    private static void BindFromForm(IFormCollection form, object destination)
    {
        BindFromValues(destination, key =>
        {
            if (form.TryGetValue(key, out var value) && value.Count > 0)
            {
                return value.ToString();
            }

            return null;
        });
    }

    private static void BindFromJsonObject(JsonElement root, object destination)
    {
        var props = destination.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            if (!prop.CanWrite)
            {
                continue;
            }

            foreach (var key in GetBindingKeys(prop))
            {
                if (!TryGetJsonPropertyIgnoreCase(root, key, out var valueElement))
                {
                    continue;
                }

                if (TryConvertJsonElement(valueElement, prop.PropertyType, out var converted))
                {
                    prop.SetValue(destination, converted);
                }

                break;
            }
        }
    }

    private static bool TryGetJsonPropertyIgnoreCase(JsonElement root, string key, out JsonElement value)
    {
        if (root.TryGetProperty(key, out value))
        {
            return true;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static void BindFromValues(object destination, Func<string, string?> valueProvider)
    {
        var props = destination.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            if (!prop.CanWrite)
            {
                continue;
            }

            foreach (var key in GetBindingKeys(prop))
            {
                var strVal = valueProvider(key);
                if (string.IsNullOrEmpty(strVal))
                {
                    continue;
                }

                if (TryConvertValue(strVal, prop.PropertyType, out var converted))
                {
                    prop.SetValue(destination, converted);
                }

                break;
            }
        }
    }

    private static IEnumerable<string> GetBindingKeys(PropertyInfo prop)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            prop.Name,
            ToCamelCase(prop.Name),
            ToSnakeCase(prop.Name),
        };

        var idName = ConvertIdSuffix(prop.Name);
        keys.Add(idName);
        keys.Add(ToCamelCase(idName));

        foreach (var alias in prop.GetCustomAttributes<BindAliasAttribute>())
        {
            if (!string.IsNullOrWhiteSpace(alias.Name))
            {
                keys.Add(alias.Name);
            }
        }

        return keys;
    }

    private static bool TryConvertValue(string value, Type propertyType, out object? converted)
    {
        converted = null;
        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (TryConvertStructuredValue(value, targetType, out converted))
        {
            return true;
        }

        try
        {
            if (targetType == typeof(string))
            {
                converted = value;
                return true;
            }

            if (targetType == typeof(bool))
            {
                if (value == "1")
                {
                    converted = true;
                    return true;
                }

                if (value == "0")
                {
                    converted = false;
                    return true;
                }

                if (bool.TryParse(value, out var boolValue))
                {
                    converted = boolValue;
                    return true;
                }

                return false;
            }

            if (targetType.IsEnum)
            {
                converted = Enum.Parse(targetType, value, ignoreCase: true);
                return true;
            }

            converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryConvertStructuredValue(string value, Type targetType, out object? converted)
    {
        converted = null;

        if (TryConvertJsonArrayWrapper(value, targetType, out converted))
        {
            return true;
        }

        if (TryConvertGenericList(value, targetType, out converted))
        {
            return true;
        }

        if (TryConvertStringDictionary(value, targetType, out converted))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 0 && (trimmed[0] == '[' || trimmed[0] == '{'))
        {
            try
            {
                converted = JsonSerializer.Deserialize(trimmed, targetType, JsonOptions);
                return converted is not null;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryConvertJsonArrayWrapper(string value, Type targetType, out object? converted)
    {
        converted = null;
        if (!targetType.IsGenericType || targetType.Name != "JsonArray`1")
        {
            return false;
        }

        var dataProperty = targetType.GetProperty("Data", BindingFlags.Public | BindingFlags.Instance);
        if (dataProperty is null ||
            !dataProperty.CanWrite ||
            !dataProperty.PropertyType.IsGenericType ||
            dataProperty.PropertyType.GetGenericTypeDefinition() != typeof(List<>))
        {
            return false;
        }

        var elementType = targetType.GetGenericArguments()[0];
        if (!TryConvertToList(value, elementType, out var listObject))
        {
            return false;
        }

        var wrapper = Activator.CreateInstance(targetType);
        if (wrapper is null)
        {
            return false;
        }

        dataProperty.SetValue(wrapper, listObject);
        converted = wrapper;
        return true;
    }

    private static bool TryConvertGenericList(string value, Type targetType, out object? converted)
    {
        converted = null;
        if (!targetType.IsGenericType || targetType.GetGenericTypeDefinition() != typeof(List<>))
        {
            return false;
        }

        var elementType = targetType.GetGenericArguments()[0];
        if (!TryConvertToList(value, elementType, out var listObject))
        {
            return false;
        }

        converted = listObject;
        return true;
    }

    private static bool TryConvertStringDictionary(string value, Type targetType, out object? converted)
    {
        converted = null;
        if (targetType != typeof(Dictionary<string, string>))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            try
            {
                converted = JsonSerializer.Deserialize<Dictionary<string, string>>(trimmed, JsonOptions)
                            ?? new Dictionary<string, string>();
                return true;
            }
            catch
            {
                return false;
            }
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (keyValue.Length == 2 && !string.IsNullOrEmpty(keyValue[0]))
            {
                dict[keyValue[0]] = keyValue[1];
            }
        }

        converted = dict;
        return dict.Count > 0;
    }

    private static bool TryConvertToList(string value, Type elementType, out object? listObject)
    {
        listObject = null;

        var listType = typeof(List<>).MakeGenericType(elementType);
        if (Activator.CreateInstance(listType) is not IList list)
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            listObject = list;
            return true;
        }

        if (trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            if (!TryPopulateListFromJson(trimmed, elementType, list))
            {
                return false;
            }

            listObject = list;
            return true;
        }

        var values = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var item in values)
        {
            if (!TryConvertValue(item, elementType, out var converted) || converted is null)
            {
                return false;
            }

            list.Add(converted);
        }

        listObject = list;
        return true;
    }

    private static bool TryPopulateListFromJson(string json, Type elementType, IList targetList)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            JsonElement source;

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                source = doc.RootElement;
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                     doc.RootElement.TryGetProperty("data", out var dataElement) &&
                     dataElement.ValueKind == JsonValueKind.Array)
            {
                source = dataElement;
            }
            else
            {
                return false;
            }

            foreach (var item in source.EnumerateArray())
            {
                if (!TryConvertJsonElement(item, elementType, out var converted) || converted is null)
                {
                    return false;
                }

                targetList.Add(converted);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryConvertJsonElement(JsonElement element, Type targetType, out object? converted)
    {
        converted = null;

        var raw = element.GetRawText();
        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString() ?? string.Empty;
            return TryConvertValue(text, targetType, out converted);
        }

        if (TryConvertStructuredValue(raw, targetType, out converted))
        {
            return true;
        }

        try
        {
            converted = JsonSerializer.Deserialize(raw, targetType, JsonOptions);
            return converted is not null;
        }
        catch
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
            {
                result.Append('_');
            }

            result.Append(char.ToLowerInvariant(c));
        }

        return result.ToString();
    }

    private static string ConvertIdSuffix(string value)
    {
        if (value.EndsWith("Ids", StringComparison.Ordinal))
        {
            return value[..^3] + "IDs";
        }

        if (value.EndsWith("Id", StringComparison.Ordinal))
        {
            return value[..^2] + "ID";
        }

        return value;
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
