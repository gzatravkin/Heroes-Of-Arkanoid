using System.Linq;
using Arkanoid.Core.Sim;

namespace Arkanoid.Core.Meta;

/// <summary>The dungeon-run offer surfaced when a Rift opens after a campaign clear.</summary>
public sealed class RiftOffer
{
    public bool   Opened    { get; set; } = true;
    public string DungeonId { get; set; } = "";
    public string Name      { get; set; } = "";
    public int    Floors    { get; set; }
}

/// <summary>
/// Pure logic deciding whether clearing a campaign node opens a Rift (the only
/// entry point into dungeon runs) and which dungeon it leads to.
/// No file I/O, no HTTP — deterministic given the seed.
/// </summary>
public static class RiftService
{
    /// <summary>
    /// Decide whether a Rift opens.
    /// <paramref name="mode"/>: "force" → always; "none"/null/empty → never;
    /// anything else (e.g. "roll") → probabilistic using <paramref name="riftChance"/>.
    /// Returns the dungeon offer, or null when no rift opens.
    /// </summary>
    /// <summary>Highest ascension tier a rift can reach (docs/04 §10, answered: 5 tiers).</summary>
    public const int MaxAscension = 5;

    public static RiftOffer? Roll(string? mode, double riftChance, string clearedLevelId, int seed,
        DungeonCatalog catalog, CampaignCatalog? campaign = null, int ascension = 0)
    {
        bool open;
        if (mode == "force") open = true;
        else if (string.IsNullOrEmpty(mode) || mode == "none") open = false;
        else open = new Rng(seed).NextDouble() < riftChance;

        if (!open) return null;

        // With a campaign catalog the rift is GENERATED from the biome's matrix levels
        // (docs/12 inheritance: every floor carries its biome's identity row);
        // without one (legacy callers/tests) fall back to the fixed dungeons.
        var tier = System.Math.Clamp(ascension, 0, MaxAscension);
        var def = campaign != null
            ? GenerateRift(clearedLevelId, seed, catalog, campaign, tier)
            : PickDungeon(clearedLevelId, catalog);
        if (def is null) return null;

        return new RiftOffer
        {
            Opened    = true,
            DungeonId = def.Id,
            Name      = def.Name,
            Floors    = def.Floors.Count,
        };
    }

    // Per-biome rift flavor: name + the biome-keyed relic it can pay out (docs/11).
    private static readonly Dictionary<string, (string Name, string[] Relics)> BiomeFlavor = new()
    {
        ["hell"]    = ("Ember Depths",  new[] { "hellwalker", "pyroclasm", "ember_heart" }),
        ["caverns"] = ("Stone Throat",  new[] { "sapper", "flint_core", "split_shot" }),
        ["village"] = ("Ghost Spire",   new[] { "ghost_lens", "souljar", "second_wind" }),
        ["heaven"]  = ("The Light Trial", new[] { "pillar_doctrine", "overcharge", "mana_battery" }),
    };

    /// <summary>Minimum non-boss floors in a generated rift (docs/04: runs are 2-5 levels).</summary>
    private const int MinRunFloors = 2;
    /// <summary>Random extra floors on top of the minimum (0-2 → 2-4 + boss = 3-5 total).</summary>
    private const int ExtraRunFloorRange = 3;
    /// <summary>Crystals paid for clearing a generated rift (matches the fixed dungeons).</summary>
    private const int RiftRewardCrystals = 50;

    /// <summary>
    /// Curated-shuffle rift generation (docs/12 §3): floors are a seeded pick from the
    /// cleared biome's campaign levels (so each inherits the identity matrix), ending
    /// at the biome's boss. Registered into the catalog under a per-biome id.
    /// </summary>
    public static DungeonDef? GenerateRift(string clearedLevelId, int seed,
        DungeonCatalog catalog, CampaignCatalog campaign, int tier = 0)
    {
        var biome = clearedLevelId.Split('-')[0];
        if (!BiomeFlavor.TryGetValue(biome, out var flavor)) return PickDungeon(clearedLevelId, catalog);

        var pool = campaign.Nodes
            .Where(n => n.Biome == biome && !n.Id.EndsWith("-boss"))
            .Select(n => n.Id).ToList();
        if (pool.Count < MinRunFloors) return PickDungeon(clearedLevelId, catalog);

        var rng    = new Rng(seed);
        int floors = MinRunFloors + rng.Range(ExtraRunFloorRange);
        floors     = System.Math.Min(floors, pool.Count);

        var picks = new List<string>();
        var avail = new List<string>(pool);
        for (int i = 0; i < floors; i++)
        {
            int idx = rng.Range(avail.Count);
            picks.Add(avail[idx]);
            avail.RemoveAt(idx);
        }
        picks.Add($"{biome}-boss"); // every rift ends at the biome's boss

        var def = new DungeonDef
        {
            Id             = $"rift-{biome}",
            // Ascended rifts wear their tier on the banner: "Ember Depths +2".
            Name           = tier > 0 ? $"{flavor.Name} +{tier}" : flavor.Name,
            Floors         = picks,
            RewardRelic    = flavor.Relics[rng.Range(flavor.Relics.Length)],
            RewardCrystals = RiftRewardCrystals + RiftRewardCrystals * tier,
            Tier           = tier,
        };
        catalog.Register(def);
        return def;
    }

    /// <summary>
    /// Legacy fixed-dungeon pick (used when no campaign catalog is supplied):
    /// Witchland/Heaven clears lead to the Ghost Spire, Hell/Caverns to the Ember Depths.
    /// </summary>
    private static DungeonDef? PickDungeon(string clearedLevelId, DungeonCatalog catalog)
    {
        var preferred = clearedLevelId.StartsWith("village") || clearedLevelId.StartsWith("heaven")
            ? "ghost-spire"
            : "ember-depths";
        return catalog.All.FirstOrDefault(d => d.Id == preferred) ?? catalog.All.FirstOrDefault();
    }
}
