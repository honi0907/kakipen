using System.Text.Json.Serialization;

namespace KakiMoni.Core.Models;

public sealed class StrokePoint
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

public sealed class StrokeData
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "pen";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#000000";

    [JsonPropertyName("size")]
    public double Size { get; set; } = 4;

    [JsonPropertyName("points")]
    public List<StrokePoint> Points { get; set; } = new();

    [JsonPropertyName("srcW")]
    public double SrcW { get; set; }

    [JsonPropertyName("srcH")]
    public double SrcH { get; set; }
}
