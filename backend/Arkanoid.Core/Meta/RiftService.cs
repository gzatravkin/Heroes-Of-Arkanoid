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
    /// <summary>
    /// Populated for generated rifts; null for catalog lookups. Not serialized — used
    /// server-side to save the def to the per-profile pending slot instead of the global catalog.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public DungeonDef? Def  { get; set; }
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
        DungeonCatalog catalog, CampaignCatalog? campaign = null, int ascension = 0, int riftLevels = 10)
    {
        bool open;
        if (mode == "force") open = true;
        else if (string.IsNullOrEmpty(mode) || mode == "none") open = false;
        else open = new Rng(seed).NextDouble() < riftChance;

        if (!open) return null;

        // With a campaign catalog the rift is GENERATED as a §7 biome gauntlet (10 escalating biome levels,
        // one HP/ball pool, §8 modifier picks); without one (legacy callers/tests) fall back to fixed dungeons.
        var tier = System.Math.Clamp(ascension, 0, MaxAscension);
        var def = campaign != null
            ? GenerateRift(clearedLevelId, seed, catalog, campaign, tier, riftLevels)
            : PickDungeon(clearedLevelId, catalog);
        if (def is null) return null;

        // For generated rifts, attach the full def so the endpoint can store it per-profile
        // instead of registering into the shared catalog (avoids concurrent-player race).
        var isGenerated = campaign != null;
        return new RiftOffer
        {
            Opened    = true,
            DungeonId = def.Id,
            Name      = def.Name,
            Floors    = def.Floors.Count,
            Def       = isGenerated ? def : null,
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
        DungeonCatalog catalog, CampaignCatalog campaign, int tier = 0, int riftLevels = 10)
    {
        var biome = clearedLevelId.Split('-')[0];
        if (!BiomeFlavor.TryGetValue(biome, out var flavor)) return PickDungeon(clearedLevelId, catalog);

        var pool = campaign.Nodes
            .Where(n => n.Biome == biome && !n.Id.EndsWith("-boss"))
            .Select(n => n.Id).ToList();
        if (pool.Count < MinRunFloors) return PickDungeon(clearedLevelId, catalog);

        // §7: a long ESCALATING gauntlet of up to `riftLevels` biome levels, ending at the biome boss. The
        // biome usually has fewer than 10 unique levels, so we cycle the shuffled pool to fill the depth.
        var rng = new Rng(seed);
        int total = System.Math.Max(MinRunFloors, riftLevels);
        var shuffled = pool.OrderBy(_ => rng.Range(int.MaxValue)).ToList();
        var picks = new List<string>();
        for (int i = 0; i < total - 1; i++) picks.Add(shuffled[i % shuffled.Count]);
        picks.Add($"{biome}-boss"); // the final level is always the biome's boss (the depth-10 jackpot)

        return new DungeonDef
        {
            Id             = $"rift-{biome}",
            Name           = tier > 0 ? $"{flavor.Name} +{tier}" : flavor.Name,
            Floors         = picks,
            RewardRelic    = "",            // §7: rifts no longer grant a permanent relic draft
            RewardCrystals = 0,             // §7: reward is DEPTH-scaled (RiftModifierService.DepthCrystals)
            Tier           = tier,
            IsRift         = true,
        };
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
