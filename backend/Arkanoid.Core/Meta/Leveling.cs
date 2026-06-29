using System.Collections.Generic;

namespace Arkanoid.Core.Meta;

/// <summary>
/// Manual copy-threshold leveling (owner direction 2026-06-15): rolls bank DUPLICATE COPIES; the player
/// spends banked copies to raise an item/module/spell one level. Cost to go from level L to L+1 is
/// <c>2L+1</c> — 3, 5, 7, 9, 11, 13 … copies.
///
/// This is the SINGLE implementation of the grant/level-up algorithm. Modules and spells store level+copies
/// as two parallel <c>Dictionary&lt;string,int&gt;</c>; cards store them on a <see cref="CardOwn"/> object —
/// both shapes are served by the overloads below so the three services never re-implement the rule.
/// </summary>
public static class Leveling
{
    /// <summary>Copies required to advance FROM <paramref name="currentLevel"/> to the next level.</summary>
    public static int CopiesForNextLevel(int currentLevel) => 2 * currentLevel + 1;

    /// <summary>True if an entry at this level/copies can be leveled now (owned, below cap, enough copies).</summary>
    public static bool CanLevelUp(int level, int copies, int maxLevel)
        => level >= 1 && level < maxLevel && copies >= CopiesForNextLevel(level);

    // ── Parallel-dict shape (modules, spells) ────────────────────────────────────
    /// <summary>Grant a copy into parallel level/copies dicts: first copy → owned at level 1; dupe → +1 copy.</summary>
    public static void GrantCopy(Dictionary<string, int> levels, Dictionary<string, int> copies, string id)
    {
        if (levels.ContainsKey(id))
            copies[id] = copies.GetValueOrDefault(id) + 1;
        else { levels[id] = 1; copies[id] = 0; }
    }

    /// <summary>Spend banked copies to raise one level in parallel level/copies dicts. False if unowned/maxed/short.</summary>
    public static bool TryLevelUp(Dictionary<string, int> levels, Dictionary<string, int> copies, string id, int maxLevel)
    {
        if (!levels.TryGetValue(id, out var lvl)) return false;
        int have = copies.GetValueOrDefault(id);
        if (!CanLevelUp(lvl, have, maxLevel)) return false;
        copies[id] = have - CopiesForNextLevel(lvl);
        levels[id] = lvl + 1;
        return true;
    }

    // ── CardOwn object shape (cards) ─────────────────────────────────────────────
    /// <summary>Spend banked copies to raise a <see cref="CardOwn"/> one level. False if maxed/short.</summary>
    public static bool TryLevelUp(CardOwn own, int maxLevel)
    {
        if (!CanLevelUp(own.Level, own.Copies, maxLevel)) return false;
        own.Copies -= CopiesForNextLevel(own.Level);
        own.Level++;
        return true;
    }
}
