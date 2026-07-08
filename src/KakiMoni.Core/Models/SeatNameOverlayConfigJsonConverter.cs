using System.Text.Json;
using System.Text.Json.Serialization;

namespace KakiMoni.Core.Models;

public sealed class SeatNameOverlayConfigJsonConverter : JsonConverter<SeatNameOverlayConfig>
{
    public override SeatNameOverlayConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return new SeatNameOverlayConfig();

        if (root.TryGetProperty("base", out var baseElement))
        {
            var config = new SeatNameOverlayConfig
            {
                Base = JsonSerializer.Deserialize<SeatNameOverlayStyle>(baseElement.GetRawText(), options)
                    ?? new SeatNameOverlayStyle()
            };

            if (root.TryGetProperty("usePerSeatColors", out var usePerSeatElement))
                config.UsePerSeatColors = usePerSeatElement.GetBoolean();

            if (root.TryGetProperty("perSeatColors", out var perSeatElement))
            {
                config.PerSeatColors = JsonSerializer.Deserialize<Dictionary<int, SeatNameOverlayColorOverride>>(
                    perSeatElement.GetRawText(),
                    options) ?? new Dictionary<int, SeatNameOverlayColorOverride>();
            }

            return config;
        }

        var legacy = JsonSerializer.Deserialize<SeatNameOverlayStyle>(root.GetRawText(), options)
            ?? new SeatNameOverlayStyle();
        return new SeatNameOverlayConfig { Base = legacy };
    }

    public override void Write(Utf8JsonWriter writer, SeatNameOverlayConfig value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("base");
        JsonSerializer.Serialize(writer, value.Base, options);
        writer.WriteBoolean("usePerSeatColors", value.UsePerSeatColors);
        writer.WritePropertyName("perSeatColors");
        JsonSerializer.Serialize(writer, value.PerSeatColors, options);
        writer.WriteEndObject();
    }
}
