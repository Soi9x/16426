using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgeLanServer.Server.Models.Playfab.Data;

/// <summary>
/// Định dạng thời gian tùy chỉnh theo chuẩn ISO 8601.
/// Sử dụng định dạng: "2006-01-02T15:04:05.000Z"
/// </summary>
public class CustomTime
{
    public const string CustomTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    [JsonIgnore]
    public DateTime Time { get; set; }

    [JsonIgnore]
    public string Format { get; set; } = CustomTimeFormat;

    /// <summary>Cập nhật thời gian hiện tại</summary>
    public void Update() => Time = DateTime.UtcNow;

    public CustomTime Clone() => new() { Time = Time, Format = Format };
}

/// <summary>
/// Bộ chuyển đổi JSON cho CustomTime.
/// </summary>
public class CustomTimeJsonConverter : JsonConverter<CustomTime>
{
    public override CustomTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        return new CustomTime
        {
            Time = DateTime.Parse(str!),
            Format = CustomTime.CustomTimeFormat
        };
    }

    public override void Write(Utf8JsonWriter writer, CustomTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Time.ToString(CustomTime.CustomTimeFormat));
    }
}

/// <summary>
/// Giá trị có kèm thời gian cập nhật và quyền truy cập.
/// Dùng để lưu trữ dữ liệu PlayFab với metadata.
/// </summary>
public class BaseValue<T>
{
    public CustomTime LastUpdated { get; set; } = new();
    public string Permission { get; set; } = null!;
    public T? Value { get; set; }

    /// <summary>Cập nhật thời gian LastUpdated</summary>
    public void UpdateLastUpdated() => LastUpdated.Update();

    /// <summary>Cập nhật giá trị và thời gian</summary>
    public void Update(Action<T?> updateFn)
    {
        updateFn(Value);
        UpdateLastUpdated();
    }
}

/// <summary>
/// Factory tạo BaseValue.
/// </summary>
public static class BaseValueFactory
{
    /// <summary>Tạo BaseValue với quyền chỉ định</summary>
    public static BaseValue<T> NewBaseValue<T>(string permission, T value)
    {
        return new BaseValue<T>
        {
            LastUpdated = new CustomTime { Time = DateTime.UtcNow, Format = CustomTime.CustomTimeFormat },
            Permission = permission,
            Value = value
        };
    }

    /// <summary>Tạo BaseValue với quyền Private</summary>
    public static BaseValue<T> NewPrivateBaseValue<T>(T value)
    {
        return NewBaseValue("Private", value);
    }
}
