using System.Text.Json.Serialization;
namespace Arkanoid.Core.Net;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InputKind { PaddleX, Serve, CastImbueIgnite, CastFireball, CastFireWall, CastTurret, CastPhoenix, CastSlot, Cheat, RiftPick }

public sealed class InputCommand
{
    [JsonPropertyName("kind")]  public InputKind Kind  { get; set; }
    [JsonPropertyName("x")]     public double    X     { get; set; }     // for PaddleX
    [JsonPropertyName("slot")]  public int       Slot  { get; set; }     // for CastSlot (0-based)
    [JsonPropertyName("cheat")] public string?   Cheat { get; set; }     // cheat op name
    [JsonPropertyName("value")] public double    Value { get; set; }     // cheat arg
    [JsonPropertyName("riftMod")] public string? RiftMod { get; set; }   // for RiftPick (the §8 modifier id)
}
