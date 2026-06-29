using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

public sealed class SpellSlotDef
{
    [JsonPropertyName("id")]       public string Id       { get; set; } = "";
    [JsonPropertyName("name")]     public string Name     { get; set; } = "";
    [JsonPropertyName("icon")]     public string Icon     { get; set; } = "";
    [JsonPropertyName("manaCost")] public int    ManaCost { get; set; }
    /// <summary>Element (economy rework §3): fire | holy | tech | death | neutral. A spell on its matching-
    /// element hero gets a small bonus (a mana discount). Absent ⇒ neutral (no affinity bonus anywhere).</summary>
    [JsonPropertyName("affinity")] public string Affinity { get; set; } = "neutral";
    /// <summary>Player-facing one-line effect, transcribed from actual spell behavior (docs/01/04).</summary>
    [JsonPropertyName("desc")]     public string Desc     { get; set; } = "";
}

public sealed class CharacterDef
{
    [JsonPropertyName("id")]      public string Id      { get; set; } = "";
    [JsonPropertyName("name")]    public string Name    { get; set; } = "";
    [JsonPropertyName("passive")] public string Passive { get; set; } = "";
    [JsonPropertyName("icon")]    public string Icon    { get; set; } = "";

    /// <summary>The character's exclusive signature spell id — locked in hotbar slot 0,
    /// never draftable from the shared pool (docs/04 §3). Falls back to the first spell.</summary>
    [JsonPropertyName("signature")] public string Signature { get; set; } = "";

    /// <summary>The default equipped loadout (ordered, signature first) a fresh save owns
    /// and equips for this character. docs/04 §4.1.</summary>
    [JsonPropertyName("starting")]  public List<string> Starting { get; set; } = new();

    /// <summary>Per-class display catalog for this character's themed spells (name/icon/manaCost).
    /// The union of all characters' spells (minus signatures) forms the shared draftable pool.</summary>
    [JsonPropertyName("spells")]  public List<SpellSlotDef> Spells { get; set; } = new();

    /// <summary>The signature id, falling back to the first spell slot if unset.</summary>
    public string SignatureId => string.IsNullOrEmpty(Signature) ? (Spells.Count > 0 ? Spells[0].Id : "") : Signature;
}

public sealed class CharacterCatalog : Catalog<CharacterDef>
{
    private CharacterCatalog(IEnumerable<CharacterDef> defs, List<SpellSlotDef> neutral)
        : base(defs, d => d.Id) { _neutral = neutral; }

    /// <summary>Class-less "shared ball/paddle tech" spells (e.g. Recall, Slow Time — docs/04 §3):
    /// draftable by anyone, owned by no character, never a signature.</summary>
    private readonly List<SpellSlotDef> _neutral;
    public IReadOnlyList<SpellSlotDef> NeutralSpells => _neutral;

    private Dictionary<string, SpellSlotDef>? _display;
    private HashSet<string>? _signatures;

    /// <summary>Flattened id → display (name/icon/manaCost) across every character's themed spells
    /// plus the neutral pool. Since the pool is shared, a spell's display lives wherever it was authored.</summary>
    private Dictionary<string, SpellSlotDef> Display =>
        _display ??= All
            .SelectMany(c => c.Spells)
            .Concat(_neutral)
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.First());

    /// <summary>All signature spell ids — excluded from the shared draftable pool.</summary>
    public HashSet<string> Signatures =>
        _signatures ??= All.Select(c => c.SignatureId).Where(s => s.Length > 0).ToHashSet();

    /// <summary>Display def (name/icon/manaCost) for a spell id, or null if unknown.</summary>
    public SpellSlotDef? DisplayOf(string id) => Display.TryGetValue(id, out var d) ? d : null;

    /// <summary>Every non-signature spell id — the shared pool any character may draft (docs/04 §3).</summary>
    public IReadOnlyList<string> Pool() =>
        Display.Keys.Where(id => !Signatures.Contains(id)).OrderBy(id => id).ToList();

    /// <summary>The default equipped loadout for a character (signature first), truncated to slotCount.
    /// Falls back to the character's first spells if no explicit starting list.</summary>
    public List<string> DefaultLoadout(string charId, int slotCount)
    {
        if (!TryGet(charId, out var c)) return new();
        var sig = c.SignatureId;
        var ordered = new List<string>();
        if (sig.Length > 0) ordered.Add(sig);
        var rest = (c.Starting.Count > 0 ? c.Starting : c.Spells.Select(s => s.Id))
            .Where(id => id != sig);
        ordered.AddRange(rest);
        return ordered.Take(System.Math.Max(1, slotCount)).ToList();
    }

    private sealed class Dto
    {
        [JsonPropertyName("characters")]   public List<CharacterDef>  Characters    { get; set; } = new();
        [JsonPropertyName("neutralSpells")] public List<SpellSlotDef> NeutralSpells { get; set; } = new();
    }

    public static CharacterCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidOperationException("Invalid characters JSON");
        return new CharacterCatalog(dto.Characters, dto.NeutralSpells);
    }

    public static CharacterCatalog FromFile(string path) => FromJson(File.ReadAllText(path));

    private static CharacterCatalog? _default;
    public static CharacterCatalog Default => _default ??= FromJson(DefaultJson);

    private const string DefaultJson = """
    { "characters": [
      { "id":"fire_mage",   "name":"Fire Mage",   "passive":"", "icon":"FireHeroBall",
        "signature":"ignite", "starting":["ignite","fireball","phoenix"],
        "spells":[
          {"id":"ignite",      "name":"Ignite",      "icon":"", "manaCost":0  },
          {"id":"fireball",    "name":"Conflagration","icon":"", "manaCost":25 },
          {"id":"firewall",    "name":"Fire Wall",   "icon":"", "manaCost":35 },
          {"id":"turret",      "name":"Turret",      "icon":"", "manaCost":25 },
          {"id":"phoenix",     "name":"Phoenix",     "icon":"", "manaCost":30 },
          {"id":"ashfall",     "name":"Ashfall",     "icon":"", "manaCost":30 }
        ]},
      { "id":"paladin",     "name":"Paladin",     "passive":"", "icon":"HPFull",
        "signature":"shield", "starting":["shield","spear","holy_echo"],
        "spells":[
          {"id":"shield",      "name":"Shield",      "icon":"", "manaCost":20 },
          {"id":"spear",       "name":"Spear",       "icon":"", "manaCost":15 },
          {"id":"holy_echo",   "name":"Holy Echo",   "icon":"", "manaCost":25 },
          {"id":"duplicate",   "name":"Duplicate",   "icon":"", "manaCost":25 },
          {"id":"penetration", "name":"Penetration", "icon":"", "manaCost":20 },
          {"id":"lastday",     "name":"Last Day",    "icon":"", "manaCost":35 },
          {"id":"reckoning",   "name":"Reckoning",   "icon":"", "manaCost":25 }
        ]},
      { "id":"engineer",    "name":"Engineer",    "passive":"", "icon":"FireTurretIco",
        "signature":"overload", "starting":["overload","magnet","radiation"],
        "spells":[
          {"id":"lightning",   "name":"Lightning",   "icon":"", "manaCost":20 },
          {"id":"rocket",      "name":"Rocket",      "icon":"", "manaCost":25 },
          {"id":"radiation",   "name":"Containment Field","icon":"", "manaCost":30 },
          {"id":"magnet",      "name":"Magnet",      "icon":"", "manaCost":20 },
          {"id":"overload",    "name":"Overload",    "icon":"", "manaCost":25 },
          {"id":"tesla",       "name":"Tesla Grid",  "icon":"", "manaCost":30 }
        ]},
      { "id":"necromancer", "name":"Necromancer", "passive":"", "icon":"MPFull",
        "signature":"raise", "starting":["raise","decay","golem"],
        "spells":[
          {"id":"decay",       "name":"Rot & Collapse","icon":"", "manaCost":0  },
          {"id":"skeleton",    "name":"Bonewalker",  "icon":"", "manaCost":25 },
          {"id":"drain",       "name":"Drain",       "icon":"", "manaCost":20 },
          {"id":"golem",       "name":"Bone Golem",  "icon":"", "manaCost":30 },
          {"id":"mage",        "name":"Lich's Gaze", "icon":"", "manaCost":25 },
          {"id":"raise",       "name":"Raise",       "icon":"", "manaCost":25 }
        ]}
    ],
      "neutralSpells":[
        {"id":"recall",   "name":"Recall",    "icon":"", "manaCost":15 },
        {"id":"slowtime", "name":"Slow Time", "icon":"", "manaCost":20 }
      ]}
    """;
}
