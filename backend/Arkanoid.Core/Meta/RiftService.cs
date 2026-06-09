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
    public static RiftOffer? Roll(string? mode, double riftChance, string clearedLevelId, int seed, DungeonCatalog catalog)
    {
        bool open;
        if (mode == "force") open = true;
        else if (string.IsNullOrEmpty(mode) || mode == "none") open = false;
        else open = new Rng(seed).NextDouble() < riftChance;

        if (!open) return null;

        var def = PickDungeon(clearedLevelId, catalog);
        if (def is null) return null;

        return new RiftOffer
        {
            Opened    = true,
            DungeonId = def.Id,
            Name      = def.Name,
            Floors    = def.Floors.Count,
        };
    }

    /// <summary>
    /// Biome-appropriate dungeon: Witchland/Heaven clears lead to the Ghost Spire,
    /// Hell/Caverns to the Ember Depths. Falls back to any dungeon if the
    /// preferred one is absent.
    /// </summary>
    private static DungeonDef? PickDungeon(string clearedLevelId, DungeonCatalog catalog)
    {
        var preferred = clearedLevelId.StartsWith("village") || clearedLevelId.StartsWith("heaven")
            ? "ghost-spire"
            : "ember-depths";
        return catalog.All.FirstOrDefault(d => d.Id == preferred) ?? catalog.All.FirstOrDefault();
    }
}
