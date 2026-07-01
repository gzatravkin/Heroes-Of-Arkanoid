using System.Text.Json;
using System.Text.Json.Serialization;
using Arkanoid.Core.Meta;
namespace Arkanoid.Core.Spells;

public sealed class SpellCatalog : Catalog<SpellDef>
{
    private SpellCatalog(IEnumerable<SpellDef> defs) : base(defs, d => d.Id) { }

    private sealed class Dto
    {
        [JsonPropertyName("spells")] public List<SpellDef> Spells { get; set; } = new();
    }

    private static readonly JsonSerializerOptions _opts = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static SpellCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json, _opts)
            ?? throw new InvalidOperationException("Invalid spells JSON");
        return new SpellCatalog(dto.Spells);
    }

    /// <summary>Build from JSON then overlay mana costs from a CharacterCatalog.</summary>
    public static SpellCatalog FromJson(string json, CharacterCatalog chars)
    {
        var catalog = FromJson(json);
        foreach (var charDef in chars.All)
            foreach (var slot in charDef.Spells)
                if (catalog.TryGet(slot.Id, out var def))
                    def.ManaCost = slot.ManaCost;
        foreach (var slot in chars.NeutralSpells)   // class-less pool spells (Recall, Slow Time)
            if (catalog.TryGet(slot.Id, out var def))
                def.ManaCost = slot.ManaCost;
        return catalog;
    }

    public static SpellCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
    public static SpellCatalog FromFile(string path, CharacterCatalog chars)
        => FromJson(File.ReadAllText(path), chars);

    private static SpellCatalog? _default;
    public static SpellCatalog Default => _default ??= FromJson(DefaultJson, CharacterCatalog.Default);

    // Behavioral parameters matching SimConfig.Default values; mana costs are overlaid from CharacterCatalog.Default.
    private const string DefaultJson = """
    { "spells": [
      { "id":"ignite",      "archetype":"Imbue",      "imbueSlot":"ignite",      "hits":4,  "hitsPerLevel":1 },
      { "id":"fireball",    "archetype":"Instant",     "kind":"conflagration", "damage":9, "damagePerLevel":2 },
      { "id":"firewall",    "archetype":"Placement",   "placementKind":"firewall", "lifetime":6.0, "durationPerLevel":1.0, "widthMult":2.2 },
      { "id":"turret",      "archetype":"TimedAura",   "duration":7.0, "durationPerLevel":1.0, "tickInterval":0,
        "damage":2, "speed":460, "radiusMult":0.6 },
      { "id":"phoenix",     "archetype":"TimedAura",   "duration":6.0, "durationPerLevel":1.0, "tickInterval":0.45, "radius":56, "damage":2 },
      { "id":"ashfall",     "archetype":"TimedAura",   "duration":6.0, "durationPerLevel":1.0 },
      { "id":"shield",      "archetype":"Placement",   "placementKind":"barrier", "lifetime":4.0, "durationPerLevel":0.5, "widthMult":1.2 },
      { "id":"spear",       "archetype":"Projectile",  "kind":"spear", "damage":2, "damagePerLevel":1, "pierce":8, "speed":620, "radiusMult":1.4 },
      { "id":"reckoning",   "archetype":"Instant",     "hits":3, "damage":3, "damagePerLevel":1 },
      { "id":"duplicate",   "archetype":"Instant",     "extraCopies":1, "extraCopiesPerLevel":1 },
      { "id":"holy_echo",   "archetype":"Instant",     "extraCopies":1, "extraCopiesPerLevel":0, "duration":8.0 },
      { "id":"penetration", "archetype":"Imbue",       "imbueSlot":"penetration", "hits":3, "hitsPerLevel":1 },
      { "id":"lastday",     "archetype":"TimedAura",   "duration":8.0, "durationPerLevel":1.0, "damage":2, "cooldown":0.5 },
      { "id":"lightning",   "archetype":"Instant",     "damage":2,  "damagePerLevel":1, "chainJumps":6, "chainRadius":110 },
      { "id":"rocket",      "archetype":"Projectile",  "kind":"rocket", "damage":2, "damagePerLevel":1, "homing":true, "homingStrength":320, "speed":280, "aoeRadius":72, "aoeDamage":2, "radiusMult":1.2 },
      { "id":"radiation",   "archetype":"Placement",   "placementKind":"zone",
        "lifetime":4.0, "radius":140, "damage":1, "damagePerLevel":1, "damageInterval":0.5 },
      { "id":"tesla",       "archetype":"Projectile",  "damage":3, "damagePerLevel":1 },
      { "id":"magnet",      "archetype":"TimedAura",   "duration":4.0, "durationPerLevel":1.0, "steerDegPerSec":120 },
      { "id":"overload",    "archetype":"Placement",   "placementKind":"bomb",    "aoeRadius":1, "aoeRadiusPerLevel":1 },
      { "id":"decay",       "archetype":"Imbue",       "imbueSlot":"decay",       "hits":4, "hitsPerLevel":1 },
      { "id":"skeleton",    "archetype":"TimedAura",   "duration":5.0, "durationPerLevel":1.0 },
      { "id":"drain",       "archetype":"TimedAura",   "duration":6.0, "durationPerLevel":1.0, "bonusManaPerKill":4.0 },
      { "id":"golem",       "archetype":"TimedAura",   "duration":0,   "durationPerLevel":0 },
      { "id":"mage",        "archetype":"TimedAura",   "duration":4.0, "durationPerLevel":1.0, "damage":2, "damagePerLevel":1 },
      { "id":"raise",       "archetype":"Instant",     "extraCopies":1, "extraCopiesPerLevel":1, "radiusMult":0.85 },
      { "id":"recall",      "archetype":"TimedAura",   "duration":2.5, "durationPerLevel":0.5, "steerDegPerSec":240, "manaCost":15 },
      { "id":"slowtime",    "archetype":"TimedAura",   "duration":4.0, "durationPerLevel":0.5, "manaCost":20 }
    ]}
    """;
}
