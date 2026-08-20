using System.Text.Json.Serialization;

namespace DailyPosterGenerator.Models;

/// <summary>
/// A rectangle the user draws over an uploaded poster while building a template.
/// "erase" regions have their content removed (blurred background) and "keep"
/// regions protect logos so they stay on the template background.
/// Coordinates are normalized 0..1 fractions of the canvas.
/// </summary>
public class ImportBox
{
    /// <summary>"erase" or "keep".</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "erase";

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("w")]
    public float W { get; set; }

    [JsonPropertyName("h")]
    public float H { get; set; }

    public bool IsKeep => string.Equals(Type, "keep", StringComparison.OrdinalIgnoreCase);
}