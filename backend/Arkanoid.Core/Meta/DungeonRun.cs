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
    [JsonPropertyName("paddleMods")]     public List<string> PaddleMods     { get; set; } = new();
    /// <summary>Spells drafted this run via floor-clear picks (docs/04 §5). Per-run only —
    /// appended to the equipped loadout for this run, reset on death. Distinct from the
    /// persistent owned/equipped set in the profile.</summary>
    [JsonPropertyName("draftedSpells")]  public List<string> DraftedSpells  { get; set; } = new();
    [JsonPropertyName("pendingChoices")] public List<string> PendingChoices { get; set; } = new();
    /// <summary>HP carried across floors (docs/04 §6.2 permadeath): 0 = not yet set (use the level
    /// default on floor 1). Saved at floor-clear, restored at the next floor's start, raised by heals.</summary>
    [JsonPropertyName("hp")]             public int          Hp             { get; set; }
    /// <summary>In-run Gold carried across floors (docs/04 §5). Accumulated from coins pickups, spent at
    /// shop floors. 0 = none yet. Saved at floor-clear, restored when the next floor's instance is built.</summary>
    [JsonPropertyName("gold")]           public int          Gold           { get; set; }
    [JsonPropertyName("active")]         public bool         Active         { get; set; }
    [JsonPropertyName("cleared")]        public bool         Cleared        { get; set; }
    [JsonPropertyName("seed")]           public int          Seed           { get; set; }
    /// <summary>Ascension tier this run was started at (0 = base difficulty).</summary>
    [JsonPropertyName("tier")]           public int          Tier           { get; set; }
    /// <summary>Relic id granted on final-floor clear (copied from DungeonDef at run start).</summary>
    [JsonPropertyName("rewardRelic")]    public string       RewardRelic    { get; set; } = "";
    /// <summary>Crystal bonus granted on final-floor clear (copied from DungeonDef at run start).</summary>
    [JsonPropertyName("rewardCrystals")] public int         RewardCrystals { get; set; }

    // ── §7 Rift gauntlet (supersedes the old rift→dungeon draft) ────────────────────────────────
    /// <summary>True when this run is a §7 Rift (10-level biome gauntlet, one HP/ball pool, §8 modifier picks,
    /// depth rewards) rather than a legacy dungeon. Drives the modifier-pick pool + reward curve.</summary>
    [JsonPropertyName("isRift")]         public bool         IsRift         { get; set; }
    /// <summary>§8 run modifiers chosen between rift levels (applied for the rest of the run).</summary>
    [JsonPropertyName("riftModifiers")]  public List<string> RiftModifiers  { get; set; } = new();
    /// <summary>The rift's single max-HP pool (raised by Ironclad, lowered by Berserker). 0 = use hero default.</summary>
    [JsonPropertyName("riftMaxHp")]      public int          RiftMaxHp      { get; set; }
    /// <summary>Spare balls carried across rift levels (the one shared ball pool — no reset between levels).</summary>
    [JsonPropertyName("spareBalls")]     public int          SpareBalls     { get; set; }
    /// <summary>End-reward multiplier from Prospector/Cursed Bounty (1.0 = base). Stacks per pick.</summary>
    [JsonPropertyName("rewardMult")]     public double       RewardMult     { get; set; } = 1.0;
    /// <summary>Cursed Bounty: extra enemy emitters forced onto each remaining rift level.</summary>
    [JsonPropertyName("extraEmitters")]  public int          ExtraEmitters  { get; set; }

    /// <summary>The level id the player must beat next, or null when the run is not active.</summary>
    [JsonIgnore]
    public string? CurrentFloor =>
        Active && FloorIndex < Floors.Count ? Floors[FloorIndex] : null;
}
