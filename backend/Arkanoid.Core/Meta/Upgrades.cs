namespace Arkanoid.Core.Meta;

public static class Upgrades
{
    /// <summary>
    /// Spends one upgrade point to raise <paramref name="spellId"/> by one level.
    /// Returns true on success. Fails (returns false) if Points == 0 or spell is already at MaxSpellLevel.
    /// Mutates <paramref name="p"/> in-place.
    /// </summary>
    public static bool TryUpgradeSpell(Profile p, string spellId, ProgressionConfig cfg)
    {
        // Default missing spell to level 1 before checking cap.
        if (!p.SpellLevels.ContainsKey(spellId))
            p.SpellLevels[spellId] = 1;

        if (p.Points <= 0 || p.SpellLevels[spellId] >= cfg.MaxSpellLevel)
            return false;

        p.Points--;
        p.SpellLevels[spellId]++;
        return true;
    }
}
