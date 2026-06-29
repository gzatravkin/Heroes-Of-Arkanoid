namespace Arkanoid.Core.Meta;

/// <summary>
/// Pure spell ownership/leveling. Manual copy-threshold leveling (owner direction 2026-06-15): the first roll
/// owns a spell at level 1; duplicates BANK copies (<see cref="Profile.SpellCopies"/>); the player spends
/// copies to level up (<see cref="Leveling"/>). Spell LEVEL lives in <see cref="Profile.SpellLevels"/>.
/// </summary>
public static class SpellService
{
    public const int MaxSpellLevel = 10;

    /// <summary>Grant a spell copy: first copy → owned at level 1; a duplicate → +1 banked copy (shared rule).</summary>
    public static void Grant(Profile p, string spellId)
        => Leveling.GrantCopy(p.SpellLevels, p.SpellCopies, spellId);

    /// <summary>Spend banked copies to raise a spell one level (shared rule in <see cref="Leveling"/>).</summary>
    public static bool TryLevelUp(Profile p, string spellId)
        => Leveling.TryLevelUp(p.SpellLevels, p.SpellCopies, spellId, MaxSpellLevel);
}
