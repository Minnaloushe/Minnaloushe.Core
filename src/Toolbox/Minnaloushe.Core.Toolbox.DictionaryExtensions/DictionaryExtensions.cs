using System.Text.Json;
using System.Text.Json.Serialization;

namespace Minnaloushe.Core.Toolbox.DictionaryExtensions;

public static class DictionaryExtensions
{
    /// <summary>
    /// Shared JsonSerializerOptions configured for proper deserialization of configuration values.
    /// Includes support for TimeSpan string parsing, enum string conversion, and case-insensitive property matching.
    /// </summary>
    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(),
            new TimeSpanConverter()
        }
    };

    /// <summary>
    /// Custom JsonConverter for TimeSpan that handles string format like "3.00:00:00" or "00:30:00".
    /// </summary>
    private class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (TimeSpan.TryParse(value, out var result))
                {
                    return result;
                }
            }
            return TimeSpan.Zero;
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    extension(IReadOnlyDictionary<string, JsonElement> data)
    {
        public int? GetIntValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value.ValueKind switch
                {
                    JsonValueKind.Number => value.GetInt32(),
                    JsonValueKind.String => int.TryParse(value.GetString(), out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public short? GetShortValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value.ValueKind switch
                {
                    JsonValueKind.Number => value.GetInt16(),
                    JsonValueKind.String => short.TryParse(value.GetString(), out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public bool? GetBoolValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public T? GetValue<T>(string key)
        {
            if (TryGetValueCaseInsensitive(data, key, out var value))
            {
                try
                {
                    return value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined
                        ? default
                        : JsonSerializer.Deserialize<T>(value.GetRawText(), DeserializerOptions);
                }
                catch
                {
                    // If deserialization fails, return null
                    return default;
                }
            }
            return default;
        }
    }

    extension(IDictionary<string, object> data)
    {
        public string? GetStringValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    string str => str,
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement => jsonElement.GetString(),
                    _ => value?.ToString()
                }
                : null;
        }

        public int? GetIntValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    int i => i,
                    long l and >= int.MinValue and <= int.MaxValue => (int)l,
                    JsonElement { ValueKind: JsonValueKind.Number } jsonElement => jsonElement.GetInt32(),
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        int.TryParse(jsonElement.GetString(), out var parsed) ? parsed : null,
                    string str => int.TryParse(str, out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public long? GetLongValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    long l => l,
                    int i => i,
                    JsonElement { ValueKind: JsonValueKind.Number } jsonElement => jsonElement.GetInt64(),
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        long.TryParse(jsonElement.GetString(), out var parsed) ? parsed : null,
                    string str => long.TryParse(str, out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public ushort? GetUshortValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    ushort i => i,
                    long l and >= int.MinValue and <= ushort.MaxValue => (ushort)l,
                    JsonElement { ValueKind: JsonValueKind.Number } jsonElement => jsonElement.GetUInt16(),
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        ushort.TryParse(jsonElement.GetString(), out var parsed) ? parsed : null,
                    string str => ushort.TryParse(str, out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public bool? GetBoolValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    bool b => b,
                    JsonElement { ValueKind: JsonValueKind.True } => true,
                    JsonElement { ValueKind: JsonValueKind.False } => false,
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        bool.TryParse(jsonElement.GetString(), out var parsed) ? parsed : null,
                    string str => bool.TryParse(str, out var parsed) ? parsed : null,
                    int i => i != 0,
                    long l => l != 0,
                    JsonElement { ValueKind: JsonValueKind.Number } jsonElement => jsonElement.GetInt32() != 0,
                    _ => null
                }
                : null;
        }

        public double? GetDoubleValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    double d => d,
                    float f => f,
                    int i => i,
                    long l => l,
                    JsonElement { ValueKind: JsonValueKind.Number } jsonElement => jsonElement.GetDouble(),
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        double.TryParse(jsonElement.GetString(), out var parsed) ? parsed : null,
                    string str => double.TryParse(str, out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public decimal? GetDecimalValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    decimal d => d,
                    double db => (decimal)db,
                    float f => (decimal)f,
                    int i => i,
                    long l => l,
                    JsonElement { ValueKind: JsonValueKind.Number } jsonElement => jsonElement.GetDecimal(),
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        decimal.TryParse(jsonElement.GetString(), out var parsed) ? parsed : null,
                    string str => decimal.TryParse(str, out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public DateTime? GetDateTimeValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    DateTime dt => dt,
                    DateTimeOffset dto => dto.DateTime,
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        DateTime.TryParse(jsonElement.GetString(), out var parsed) ? parsed : null,
                    string str => DateTime.TryParse(str, out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public DateTimeOffset? GetDateTimeOffsetValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    DateTimeOffset dto => dto,
                    DateTime dt => new DateTimeOffset(dt),
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        DateTimeOffset.TryParse(jsonElement.GetString(), out var parsed) ? parsed : null,
                    string str => DateTimeOffset.TryParse(str, out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public Guid? GetGuidValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    Guid g => g,
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        Guid.TryParse(jsonElement.GetString(), out var parsed) ? parsed : null,
                    string str => Guid.TryParse(str, out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public Uri? GetUriValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    Uri uri => uri,
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        Uri.TryCreate(jsonElement.GetString(), UriKind.RelativeOrAbsolute, out var parsed)
                            ? parsed
                            : null,
                    string str => Uri.TryCreate(str, UriKind.RelativeOrAbsolute, out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public TimeSpan? GetTimeSpanValue(string key)
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    TimeSpan ts => ts,
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        TimeSpan.TryParse(jsonElement.GetString(), out var parsed) ? parsed : null,
                    string str => TimeSpan.TryParse(str, out var parsed) ? parsed : null,
                    _ => null
                }
                : null;
        }

        public T? GetEnumValue<T>(string key) where T : struct, Enum
        {
            return TryGetValueCaseInsensitive(data, key, out var value)
                ? value switch
                {
                    T enumValue => enumValue,
                    JsonElement { ValueKind: JsonValueKind.String } jsonElement =>
                        Enum.TryParse<T>(jsonElement.GetString(), true, out var parsed) ? parsed : null,
                    JsonElement { ValueKind: JsonValueKind.Number } jsonElement =>
                        Enum.IsDefined(typeof(T), jsonElement.GetInt32()) ? (T)(object)jsonElement.GetInt32() : null,
                    string str => Enum.TryParse<T>(str, true, out var parsed) ? parsed : null,
                    int i when Enum.IsDefined(typeof(T), i) => (T)(object)i,
                    _ => null
                }
                : null;
        }

        public string[] GetStringArrayValue(string key)
        {
            return !data.TryGetValue(key, out var value)
                ? []
                : value switch
                {
                    string[] strArray => strArray,
                    JsonElement { ValueKind: JsonValueKind.Array } jsonElement =>
                        [..jsonElement.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String? e.GetString() : e.ToString())
                        .Where(s => s != null)!],
                    IEnumerable<string> enumerable => [.. enumerable],
                    _ => []
                };
        }

        public T? Deserialize<T>()
        {
            var json = JsonSerializer.Serialize(data);
            return JsonSerializer.Deserialize<T>(json);
        }
    }

    private static bool TryGetValueCaseInsensitive<T>(IReadOnlyDictionary<string, T>? dict, string key,
        out T? value)
    {
        if (dict == null)
        {
            value = default;
            return false;
        }
        // Fast path (works if the dictionary already happens to be case-insensitive)
        if (dict.TryGetValue(key, out value))
        {
            return true;
        }

        // Slow path: case-insensitive scan
        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetValueCaseInsensitive<T>(IDictionary<string, T>? dict, string key,
        out T? value)
    {
        if (dict == null)
        {
            value = default;
            return false;
        }
        // Fast path (works if the dictionary already happens to be case-insensitive)
        if (dict.TryGetValue(key, out value))
        {
            return true;
        }

        // Slow path: case-insensitive scan
        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}