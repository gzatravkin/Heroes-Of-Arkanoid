using System.Text.Json.Serialization;
namespace Arkanoid.Core.Net;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InputKind { PaddleX, Serve, CastImbueIgnite, CastFireball, CastFireWall, CastTurret, Cheat }

public sealed class InputCommand
{
    [JsonPropertyName("kind")] public InputKind Kind { get; set; }
    [JsonPropertyName("x")] public double X { get; set; }            // for PaddleX
    [JsonPropertyName("cheat")] public string? Cheat { get; set; }  // cheat op name
    [JsonPropertyName("value")] public double Value { get; set; }   // cheat arg
}
