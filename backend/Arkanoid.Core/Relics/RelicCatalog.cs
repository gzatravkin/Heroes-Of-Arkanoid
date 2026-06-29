using System.Text.Json;
using System.Text.Json.Serialization;
using Arkanoid.Core.Meta;
namespace Arkanoid.Core.Relics;

public sealed class RelicDef
{
    [JsonPropertyName("id")]          public string Id          { get; set; } = "";
    [JsonPropertyName("name")]        public string Name        { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("icon")]        public string Icon        { get; set; } = "";
    /// <summary>Primary numeric magnitude (additive bonuses, interval counters, etc.).</summary>
    [JsonPropertyName("magnitude")]   public double Magnitude   { get; init; } = 0;
    /// <summary>Secondary magnitude (e.g. mana_battery regen mult, lead_paddle regen penalty).</summary>
    [JsonPropertyName("magnitude2")]  public double Magnitude2  { get; init; } = 0;
    /// <summary>Threshold value (e.g. flint_core: blocks with MaxHp >= Threshold qualify).</summary>
    [JsonPropertyName("threshold")]   public double Threshold   { get; init; } = 0;
    /// <summary>Equip-time effect id ("cost_hp", "mana_max", "width_mult", or "" for none).</summary>
    [JsonPropertyName("effect")]      public string Effect      { get; init; } = "";
}

public sealed class RelicCatalog : Catalog<RelicDef>
{
    private RelicCatalog(IEnumerable<RelicDef> defs) : base(defs, d => d.Id) { }

    private sealed class Dto { [JsonPropertyName("relics")] public List<RelicDef> Relics { get; set; } = new(); }

    public static RelicCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidOperationException("bad relics json");
        return new RelicCatalog(dto.Relics);
    }

    public static RelicCatalog FromFile(string path) => FromJson(File.ReadAllText(path));

    private static RelicCatalog? _default;
    public static RelicCatalog Default => _default ??= FromJson(DefaultJson);

    private const string DefaultJson = """
    { "relics": [
      { "id": "glass_cannon",    "name": "Glass Cannon",    "icon": "ItemHummer",          "effect": "cost_hp",    "magnitude": 1 },
      { "id": "flint_core",      "name": "Flint Core",      "icon": "ItemDrill",            "effect": "",           "magnitude": 1,    "threshold": 3 },
      { "id": "pyroclasm",       "name": "Pyroclasm",       "icon": "ItemTorch",            "effect": "",           "magnitude": 2 },
      { "id": "mana_battery",    "name": "Mana Battery",    "icon": "ItemGem",              "effect": "mana_max",   "magnitude": 50,   "magnitude2": 1.6 },
      { "id": "conductor",       "name": "Conductor",       "icon": "ItemMotor",            "effect": "",           "magnitude": 1 },
      { "id": "overcharge",      "name": "Overcharge",      "icon": "ItemOrb",              "effect": "",           "magnitude": 8 },
      { "id": "split_shot",      "name": "Split Shot",      "icon": "ItemJadeBall",         "effect": "",           "magnitude": 6 },
      { "id": "souljar",         "name": "Souljar",         "icon": "ItemMark",             "effect": "",           "magnitude": 5 },
      { "id": "lodestone",       "name": "Lodestone",       "icon": "ItemForceRing",        "effect": "",           "magnitude": 60 },
      { "id": "ember_heart",     "name": "Ember Heart",     "icon": "ItemPhoenix",          "effect": "",           "magnitude": 2 },
      { "id": "second_wind",     "name": "Second Wind",     "icon": "ItemHelm",             "effect": "",           "magnitude": 0 },
      { "id": "midas",           "name": "Midas Touch",     "icon": "ItemMagicCrown",       "effect": "",           "magnitude": 2 },
      { "id": "lead_paddle",     "name": "Lead Paddle",     "icon": "ItemStaff",            "effect": "width_mult", "magnitude": 1.25, "magnitude2": 0.75 },
      { "id": "sapper",          "name": "Sapper's Charge", "icon": "ItemSun",              "effect": "",           "magnitude": 1 },
      { "id": "hellwalker",      "name": "Hellwalker",      "icon": "ItemFlask",            "effect": "",           "magnitude": 0 },
      { "id": "ghost_lens",      "name": "Ghost Lens",      "icon": "ItemRing",             "effect": "",           "magnitude": 1 },
      { "id": "pillar_doctrine", "name": "Pillar Doctrine", "icon": "ItemTomOfKnowladge",   "effect": "",           "magnitude": 1 }
    ]}
    """;
}
