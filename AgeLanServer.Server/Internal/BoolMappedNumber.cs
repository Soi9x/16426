// Port từ server/internal/boolInt.go
/// Chuyển đổi giữa bool và số, dùng cho JSON serialization.

using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgeLanServer.Server.Internal;

/// <summary>
/// Chuyển số thành bool: khác 0 = true, bằng 0 = false.
/// </summary>
public static class BoolIntHelpers
{
    /// <summary>
    /// Chuyển số thành bool.
    /// </summary>
    public static bool NumberToBool<T>(T value) where T : notnull
    {
        return Convert.ToDouble(value) != 0.0;
    }
}

/// <summary>
/// BoolMappedNumber: Kiểu số có thể đánh giá như bool.
/// Tương đương BoolMappedNumber[T] trong Go.
/// JSON serialization vẫn dùng giá trị số.
/// </summary>
[JsonConverter(typeof(BoolMappedNumberJsonConverterFactory))]
public class BoolMappedNumber<T> where T : struct, INumber<T>
{
    /// <summary>Giá trị số nội tại.</summary>
    public T Value { get; set; }

    public BoolMappedNumber(T value)
    {
        Value = value;
    }

    /// <summary>
    /// Kiểm tra dưới dạng bool (khác 0 = true).
    /// </summary>
    public bool BoolValue => Value != T.Zero;

    /// <summary>
    /// Tạo từ giá trị số.
    /// </summary>
    public static BoolMappedNumber<T> FromNumber(T value) => new(value);

    /// <summary>
    /// Tạo từ bool (true = 1, false = 0). Chỉ hỗ trợ kiểu byte.
    /// </summary>
    public static BoolMappedNumber<byte> FromBool(bool value)
    {
        return new BoolMappedNumber<byte>(value ? (byte)1 : (byte)0);
    }
}

/// <summary>
/// JSON converter cho BoolMappedNumber.
/// Serialize/deserialize dưới dạng số.
/// </summary>
public class BoolMappedNumberJsonConverter<T> : JsonConverter<BoolMappedNumber<T>> where T : struct, INumber<T>
{
    public override BoolMappedNumber<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            // Thử đọc dưới dạng số
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (typeof(T) == typeof(byte))
                    return new BoolMappedNumber<T>(T.CreateChecked(reader.GetByte())!);
                if (typeof(T) == typeof(sbyte))
                    return new BoolMappedNumber<T>(T.CreateChecked(reader.GetSByte())!);
                if (typeof(T) == typeof(short))
                    return new BoolMappedNumber<T>(T.CreateChecked(reader.GetInt16())!);
                if (typeof(T) == typeof(ushort))
                    return new BoolMappedNumber<T>(T.CreateChecked(reader.GetUInt16())!);
                if (typeof(T) == typeof(int))
                    return new BoolMappedNumber<T>(T.CreateChecked(reader.GetInt32())!);
                if (typeof(T) == typeof(uint))
                    return new BoolMappedNumber<T>(T.CreateChecked(reader.GetUInt32())!);
                if (typeof(T) == typeof(long))
                    return new BoolMappedNumber<T>(T.CreateChecked(reader.GetInt64())!);
                if (typeof(T) == typeof(ulong))
                    return new BoolMappedNumber<T>(T.CreateChecked(reader.GetUInt64())!);
                if (typeof(T) == typeof(float))
                    return new BoolMappedNumber<T>(T.CreateChecked(reader.GetSingle())!);
                if (typeof(T) == typeof(double))
                    return new BoolMappedNumber<T>(T.CreateChecked(reader.GetDouble())!);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, BoolMappedNumber<T> value, JsonSerializerOptions options)
    {
        // Ghi dưới dạng số
        if (typeof(T) == typeof(byte))
            writer.WriteNumberValue((byte)(object)value.Value);
        else if (typeof(T) == typeof(sbyte))
            writer.WriteNumberValue((sbyte)(object)value.Value);
        else if (typeof(T) == typeof(short))
            writer.WriteNumberValue((short)(object)value.Value);
        else if (typeof(T) == typeof(ushort))
            writer.WriteNumberValue((ushort)(object)value.Value);
        else if (typeof(T) == typeof(int))
            writer.WriteNumberValue((int)(object)value.Value);
        else if (typeof(T) == typeof(uint))
            writer.WriteNumberValue((uint)(object)value.Value);
        else if (typeof(T) == typeof(long))
            writer.WriteNumberValue((long)(object)value.Value);
        else if (typeof(T) == typeof(ulong))
            writer.WriteNumberValue((ulong)(object)value.Value);
        else if (typeof(T) == typeof(float))
            writer.WriteNumberValue((float)(object)value.Value);
        else if (typeof(T) == typeof(double))
            writer.WriteNumberValue((double)(object)value.Value);
        else
            writer.WriteStringValue(value.Value.ToString());
    }
}

/// <summary>
/// Factory cho BoolMappedNumber converter.
/// </summary>
public class BoolMappedNumberJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(BoolMappedNumber<>);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(BoolMappedNumberJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}
