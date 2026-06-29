using System.Text.Json.Serialization;
namespace Arkanoid.Core.Spells;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpellArchetype { Projectile, Imbue, TimedAura, Placement, Instant }

public sealed class SpellDef
{
    [JsonPropertyName("id")]              public string        Id             { get; init; } = "";
    [JsonPropertyName("archetype")]       public SpellArchetype Archetype     { get; init; }
    /// <summary>Mana cost — mutable so SpellCatalog can overlay per-character costs from CharacterCatalog.</summary>
    [JsonPropertyName("manaCost")]        public double        ManaCost       { get; set;  }

    // Shared
    [JsonPropertyName("damage")]          public int    Damage          { get; init; }
    [JsonPropertyName("damagePerLevel")]  public int    DamagePerLevel  { get; init; }
    [JsonPropertyName("radius")]          public double Radius          { get; init; }

    // Projectile
    [JsonPropertyName("speed")]           public double Speed           { get; init; }
    [JsonPropertyName("radiusMult")]      public double RadiusMult      { get; init; } = 1.0;
    [JsonPropertyName("pierce")]          public int    Pierce          { get; init; }
    [JsonPropertyName("homing")]          public bool   Homing          { get; init; }
    [JsonPropertyName("aoeRadius")]       public double AoeRadius       { get; init; }
    [JsonPropertyName("aoeDamage")]       public int    AoeDamage       { get; init; }
    [JsonPropertyName("homingStrength")]  public double HomingStrength  { get; init; }
    [JsonPropertyName("count")]           public int    Count           { get; init; } = 1;
    [JsonPropertyName("fanHalfAngleDeg")] public double FanHalfAngleDeg { get; init; }
    [JsonPropertyName("kind")]            public string Kind            { get; init; } = "";

    // Imbue
    [JsonPropertyName("imbueSlot")]       public string ImbueSlot       { get; init; } = "";
    [JsonPropertyName("hits")]            public int    Hits            { get; init; }
    [JsonPropertyName("hitsPerLevel")]    public int    HitsPerLevel    { get; init; }

    // TimedAura
    [JsonPropertyName("duration")]        public double Duration         { get; init; }
    [JsonPropertyName("durationPerLevel")]public double DurationPerLevel { get; init; }
    [JsonPropertyName("tickInterval")]    public double TickInterval     { get; init; }
    [JsonPropertyName("bonusManaPerKill")]public double BonusManaPerKill { get; init; }
    [JsonPropertyName("steerDegPerSec")]  public double SteerDegPerSec   { get; init; }
    [JsonPropertyName("cooldown")]        public double Cooldown          { get; init; }

    [JsonPropertyName("aoeRadiusPerLevel")] public double AoeRadiusPerLevel { get; init; }

    // Placement
    [JsonPropertyName("placementKind")]   public string PlacementKind    { get; init; } = "";
    [JsonPropertyName("lifetime")]        public double Lifetime          { get; init; }
    [JsonPropertyName("widthMult")]       public double WidthMult         { get; init; }
    [JsonPropertyName("bandHalfHeight")]  public double BandHalfHeight    { get; init; }
    [JsonPropertyName("riseSpeed")]       public double RiseSpeed         { get; init; }
    [JsonPropertyName("damageInterval")]  public double DamageInterval    { get; init; }
    [JsonPropertyName("placementRow")]    public int    PlacementRow       { get; init; }

    // Instant
    [JsonPropertyName("extraCopies")]         public int ExtraCopies         { get; init; } = 1;
    [JsonPropertyName("extraCopiesPerLevel")] public int ExtraCopiesPerLevel { get; init; }
    [JsonPropertyName("chainJumps")]      public int    ChainJumps        { get; init; }
    [JsonPropertyName("chainRadius")]     public double ChainRadius        { get; init; }
}
