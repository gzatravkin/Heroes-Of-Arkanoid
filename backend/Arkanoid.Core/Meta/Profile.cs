using System.Linq;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

public sealed class Profile
{
    [JsonPropertyName("level")]    public int Level    { get; set; } = 1;
    [JsonPropertyName("exp")]      public int Exp      { get; set; } = 0;

    // ── The 3 spend currencies (economy rework, docs/2026-06-14) ────────────────
    /// <summary>Sparks — gear coin: rolls Cards + Modules (dupes level them).</summary>
    [JsonPropertyName("sparks")]  public int Sparks  { get; set; } = 0;
    /// <summary>Souls — loadout/roster coin: rolls Spells + Heroes, buys +spell-slots, pays mastery respec.</summary>
    [JsonPropertyName("souls")]   public int Souls   { get; set; } = 0;
    /// <summary>Insight — mastery coin: levels Mastery nodes (the bottomless grind sink).</summary>
    [JsonPropertyName("insight")] public int Insight { get; set; } = 0;
    /// <summary>Set once the legacy 11-currency soup has been folded into the 3 coins.</summary>
    [JsonPropertyName("currencyMigrated")] public bool CurrencyMigrated { get; set; } = false;

    // ── Legacy currencies (migration-only; no sources — folded by MigrateCurrencies) ──
    [JsonPropertyName("points")]   public int Points   { get; set; } = 0;
    [JsonPropertyName("crystals")] public int Crystals { get; set; } = 0;
    /// <summary>Meta-currency (docs/04 §5): drips even on dungeon death so failed runs still progress
    /// you; spent on permanent unlocks (e.g. characters).</summary>
    [JsonPropertyName("shards")]   public int Shards   { get; set; } = 0;
    /// <summary>Persistent campaign Gold (docs/04 §5, §6.1): earned at level clear, spent at campaign
    /// rest/shop nodes (Gap H). Separate from the per-run <c>DungeonRun.Gold</c>.</summary>
    [JsonPropertyName("campaignGold")] public int CampaignGold { get; set; } = 0;

    // ── Live-ops chase currencies (social/economy plan, 2026-06-13) ──────────────
    /// <summary>Card Dust — levels up Cards (the Tower-style passive layer).</summary>
    [JsonPropertyName("cardDust")]     public int CardDust     { get; set; } = 0;
    /// <summary>Module Cores — craft / level / reroll Modules.</summary>
    [JsonPropertyName("moduleCores")]  public int ModuleCores  { get; set; } = 0;
    /// <summary>Medals — league-placement currency, spent in the league shop.</summary>
    [JsonPropertyName("medals")]       public int Medals       { get; set; } = 0;
    /// <summary>Event Tokens — per-event reward-track currency.</summary>
    [JsonPropertyName("eventTokens")]  public int EventTokens  { get; set; } = 0;
    /// <summary>Season Tokens — advances the season reward track.</summary>
    [JsonPropertyName("seasonTokens")] public int SeasonTokens { get; set; } = 0;

    // ── Cards (Tower-style passive layer, plan §A.1) ────────────────────────────
    /// <summary>Owned cards: id → ownership (level + spare copies).</summary>
    [JsonPropertyName("ownedCards")]    public Dictionary<string, CardOwn> OwnedCards { get; set; } = new();
    /// <summary>Equipped card ids (capped at <see cref="CardSlots"/>).</summary>
    [JsonPropertyName("equippedCards")] public List<string> EquippedCards { get; set; } = new();
    /// <summary>Unlocked card slots (grows with progression). Starts at 3.</summary>
    [JsonPropertyName("cardSlots")]     public int CardSlots { get; set; } = 3;

    // ── Daily missions (plan §A.2) ──────────────────────────────────────────────
    [JsonPropertyName("daily")] public DailyState Daily { get; set; } = new();

    /// <summary>Prestige tier (plan §B.1): each campaign clear lets you Ascend into a harder, remixed
    /// New Game+ loop. 0 = base campaign. Drives difficulty scaling, mutators, and the prestige board.</summary>
    [JsonPropertyName("prestigeTier")] public int PrestigeTier { get; set; } = 0;

    // ── Modules (economy rework: collected like cards — no sub-stats/instances) ──────────────────
    /// <summary>Owned modules: def id → level (dupes raise the level). One strong slot-bound passive each.
    /// Converter tolerates the pre-rework array shape so old saves load (economy rework migration).</summary>
    [JsonPropertyName("ownedModules")]
    [JsonConverter(typeof(OwnedModulesConverter))]
    public Dictionary<string, int> OwnedModules { get; set; } = new();
    /// <summary>Banked duplicate copies per module (manual copy-threshold leveling, 2026-06-15).</summary>
    [JsonPropertyName("moduleCopies")] public Dictionary<string, int> ModuleCopies { get; set; } = new();
    /// <summary>Equipped module def id per slot (core/paddle/ball/field).</summary>
    [JsonPropertyName("equippedModules")] public Dictionary<string, string> EquippedModules { get; set; } = new();

    // ── Season Festival (plan §C) ───────────────────────────────────────────────
    [JsonPropertyName("season")] public SeasonState Season { get; set; } = new();

    [JsonPropertyName("completedLevels")]
    public List<string> CompletedLevels { get; set; } = new();

    [JsonPropertyName("unlockedRelics")]
    public List<string> UnlockedRelics { get; set; } = new();

    /// <summary>Per-spell upgrade levels. A key's presence also marks the spell as OWNED
    /// (own ⇒ level ≥ 1), so this doubles as the unlocked-spell set (docs/04 §4.1).</summary>
    [JsonPropertyName("spellLevels")]
    public Dictionary<string, int> SpellLevels { get; set; } = new();

    /// <summary>Banked duplicate copies per spell (manual copy-threshold leveling, 2026-06-15).</summary>
    [JsonPropertyName("spellCopies")]
    public Dictionary<string, int> SpellCopies { get; set; } = new();

    /// <summary>Per-character equipped loadout, ordered (slot 0 = signature). Empty for a character
    /// ⇒ fall back to its default starting loadout. The drafted half of the kit (docs/04 §3).</summary>
    [JsonPropertyName("equippedSpells")]
    public Dictionary<string, List<string>> EquippedSpells { get; set; } = new();

    /// <summary>How many hotbar slots are unlocked (signature + drafted). Grows with progression
    /// (docs/04 §4.1): starts at 3 (signature + 2), +1 per biome boss cleared, capped at 5.</summary>
    [JsonPropertyName("unlockedSpellSlots")]
    public int UnlockedSpellSlots { get; set; } = 3;

    // ── Stat engine (design §5: Heroes-are-your-stats) ──────────────────────────
    /// <summary>Per-hero progression: level (1→30, play-driven §5.3), XP toward next, and ★ stars
    /// (0→6, collection-driven §5.4). Missing hero ⇒ Lvl 1 / ★0.</summary>
    [JsonPropertyName("heroProgress")] public Dictionary<string, HeroProgress> HeroProgress { get; set; } = new();
    /// <summary>Hero Tokens per hero (legacy; folded into Souls on migration — no longer earned/spent).</summary>
    [JsonPropertyName("heroTokens")]   public Dictionary<string, int> HeroTokens { get; set; } = new();
    /// <summary>Heroes a biome-boss clear has made ROLLABLE (economy rework §4). A hero stays locked until
    /// its first card is rolled from this pool; further rolls raise ★ via <see cref="HeroProgress.AscendPips"/>.</summary>
    [JsonPropertyName("heroPool")]     public List<string> HeroPool { get; set; } = new();
    /// <summary>Mastery node → purchased level (§5.6). Funded by the shared Skill-Points pool
    /// (<see cref="Points"/>, §5.10) — the same pool that levels spells.</summary>
    [JsonPropertyName("masteries")]    public Dictionary<string, int> Masteries { get; set; } = new();

    [JsonPropertyName("selectedCharacter")]
    public string SelectedCharacter { get; set; } = "fire_mage";

    [JsonPropertyName("unlockedCharacters")]
    public List<string> UnlockedCharacters { get; set; } = new();

    /// <summary>Unlocked achievement ids. Client-driven; backend only persists what it receives.</summary>
    [JsonPropertyName("achievements")]
    public List<string> Achievements { get; set; } = new();

    /// <summary>Rift ascension: the tier the NEXT generated rift opens at (rises by clearing rifts; docs/04 §10).</summary>
    [JsonPropertyName("riftAscension")]
    public int RiftAscension { get; set; }

    /// <summary>True after the first battle has been seen; suppresses tutorial on subsequent sessions.</summary>
    [JsonPropertyName("tutorialSeen")]
    public bool TutorialSeen { get; set; } = false;

    /// <summary>
    /// Per-profile generated rift def awaiting /dungeon/start.
    /// Stored here (not the global DungeonCatalog) so concurrent players never share or overwrite each other's offers.
    /// Cleared once the run is started.
    /// </summary>
    [JsonPropertyName("pendingRift")]
    public DungeonDef? PendingRift { get; set; }

    /// <summary>One-time fold of the legacy 11-currency soup into the 3 coins (proposal §9). Idempotent —
    /// guarded by <see cref="CurrencyMigrated"/>. CardDust/ModuleCores/CampaignGold → Sparks; Crystals/Shards
    /// + all unspent Hero-Tokens → Souls; Points → Insight. Medals/Event/Season tokens are untouched.</summary>
    public void MigrateCurrencies()
    {
        if (CurrencyMigrated) return;
        Sparks  += CardDust + ModuleCores + CampaignGold;
        Souls   += Crystals + Shards + HeroTokens.Values.Sum();
        Insight += Points;
        CardDust = ModuleCores = CampaignGold = Crystals = Shards = Points = 0;
        HeroTokens.Clear();
        CurrencyMigrated = true;
    }

    public static Profile NewDefault()
    {
        return new Profile
        {
            SpellLevels = new Dictionary<string, int>
            {
                ["ignite"]   = 1,
                ["fireball"] = 1,
                ["firewall"] = 1,
                ["turret"]   = 1,
            },
            SelectedCharacter  = "fire_mage",
            // Characters are EARNED (docs/04 §3): fresh saves start with the Fire Mage;
            // boss clears unlock the rest (see Rewards.CharacterUnlocks). Existing
            // saves persisted the full list and are untouched.
            UnlockedCharacters = new List<string> { "fire_mage" },
            // Default loadout: signature + 2 (docs/04 §4.1). Other characters fall back to
            // their starting loadout on demand (CharacterCatalog.DefaultLoadout).
            EquippedSpells     = new Dictionary<string, List<string>>
            {
                ["fire_mage"] = new List<string> { "ignite", "fireball", "firewall" },
            },
            UnlockedSpellSlots = 3,
            // A couple of starter cards so the Cards screen has something to equip (plan §A.1);
            // the rest are earned from dailies/league rewards.
            OwnedCards = new Dictionary<string, CardOwn>
            {
                ["headhunter"]     = new CardOwn { Level = 1 },
                ["opening_gambit"] = new CardOwn { Level = 1 },
            },
            EquippedCards = new List<string> { "headhunter" },
        };
    }
}
