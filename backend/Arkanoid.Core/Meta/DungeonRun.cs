using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

/// <summary>
/// Mutable state of a single dungeon run (permadeath; one run per player at a time).
/// All mutation is done by <see cref="DungeonService"/>; this class is a plain data bag.
/// </summary>
public sealed class DungeonRun
{
    [JsonPropertyName("dungeonId")]      public string       DungeonId      { get; set; } = "";
    [JsonPropertyName("floors")]         public List<string> Floors         { get; set; } = new();
    [JsonPropertyName("floorIndex")]     public int          FloorIndex     { get; set; } = 0;
    [JsonPropertyName("relics")]         public List<string> Relics         { get; set; } = new();
    [JsonPropertyName("ballCores")]      public List<string> BallCores      { get; set; } = new();
    [JsonPropertyName("pendingChoices")] public List<string> PendingChoices { get; set; } = new();
    [JsonPropertyName("active")]         public bool         Active         { get; set; }
    [JsonPropertyName("cleared")]        public bool         Cleared        { get; set; }
    [JsonPropertyName("seed")]           public int          Seed           { get; set; }

    /// <summary>The level id the player must beat next, or null when the run is not active.</summary>
    [JsonIgnore]
    public string? CurrentFloor =>
        Active && FloorIndex < Floors.Count ? Floors[FloorIndex] : null;
}
