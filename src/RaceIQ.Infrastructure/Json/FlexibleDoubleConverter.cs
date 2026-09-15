using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaceIQ.Infrastructure.Json;

// ZwiftPower sends some numeric fields as JSON strings ("28.29") and others, when zero,
// as a bare number (0). This reads either shape into a nullable double.
public class FlexibleDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return reader.GetDouble();
            case JsonTokenType.String:
                var raw = reader.GetString();
                return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (value is { } v)
            writer.WriteNumberValue(v);
        else
            writer.WriteNullValue();
    }
}
