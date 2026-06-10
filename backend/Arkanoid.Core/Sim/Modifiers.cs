using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Sim;

/// <summary>
/// Pure functions centralising every relic / ball-core / character modifier so
/// the rest of the sim has a single source of truth instead of scattered
/// HasRelic(…)?x:y ternaries.
/// </summary>
internal static class Modifiers
{
    // -----------------------------------------------------------------------
    // Ball / block damage
    // -----------------------------------------------------------------------

    /// <summary>Total damage dealt by a ball hit (base + ignite bonus + relics + ball-core + items).</summary>
    internal static int BallDamage(GameInstance g, Block target, bool ignited, bool ghost = false)
    {
        var igniteBonus   = ignited ? 1 : 0;
        var relicBonus    = (g.HasRelic("glass_cannon") ? g.Config.GlassCannonDamageBonus : 0)
                          + (g.HasRelic("flint_core") && target.MaxHp >= g.Config.FlintToughThreshold
                              ? g.Config.FlintBonus : 0)
                          // Ghost Lens (village-keyed): a phased ball cuts deeper.
                          + (ghost && g.HasRelic("ghost_lens") ? g.Config.GhostLensBonus : 0)
                          // Pillar Doctrine (heaven-keyed): statues and columns crumble faster.
                          + (g.HasRelic("pillar_doctrine")
                              && (target.IsStatue || target.TypeId.StartsWith("heaven_column"))
                              ? g.Config.PillarDoctrineBonus : 0);
        var ballCoreBonus = g.BallCores.Contains("heavy") ? g.Config.HeavyBallDamageBonus : 0;
        // item: ball_damage adds a flat per-hit bonus; crit_tough adds only vs tough blocks
        var itemBonus     = g.ItemBallDamageBonus
                          + (target.MaxHp >= g.Config.FlintToughThreshold ? g.ItemCritToughBonus : 0);
        return g.Config.BallDamage + igniteBonus + relicBonus + ballCoreBonus + itemBonus;
    }

    // -----------------------------------------------------------------------
    // Mana
    // -----------------------------------------------------------------------

    /// <summary>Effective mana-regen multiplier (mana_battery × engineer stacks × item bonuses).</summary>
    internal static double ManaRegenMult(GameInstance g)
    {
        var mult = g.HasRelic("mana_battery") ? g.Config.ManaBatteryRegenMult : 1.0;
        mult *= (g.Character == "engineer" ? g.Config.EngineerRegenMult : 1.0);
        // item: mana_regen is an additive bonus to the multiplier (e.g. +0.20 per tier)
        mult *= (1.0 + g.ItemManaRegenMultBonus);
        // Lead Paddle tradeoff: the wide paddle is paid for in regen.
        if (g.HasRelic("lead_paddle")) mult *= g.Config.LeadPaddleRegenMult;
        return mult;
    }

    /// <summary>Mana granted when a block is killed (base × necromancer multiplier × item kill_mana bonus + drain).</summary>
    internal static double KillManaGain(GameInstance g)
        => g.Config.ManaPerKill
           * (g.Character == "necromancer" ? g.Config.NecromancerKillManaMult : 1.0)
           * (1.0 + g.ItemKillManaMultBonus)
           + Systems.SpellSystem.DrainBonusMana(g);

    // -----------------------------------------------------------------------
    // Fire spread
    // -----------------------------------------------------------------------

    /// <summary>Whether ignited kills should spread fire to neighbours (fire_mage, pyroclasm, or the Molten fusion).</summary>
    internal static bool ShouldSpreadFire(GameInstance g)
        => g.Character == "fire_mage" || g.HasRelic("pyroclasm") || g.HasFusion("heavy", "ember");

    /// <summary>Chip damage dealt to each neighbour during fire spread.</summary>
    internal static int SpreadChip(GameInstance g)
        => (g.HasRelic("pyroclasm") ? g.Config.PyroclasmChip : 1)
           // Molten fusion (heavy+ember): the wrecking ball's fire bites deeper.
           + (g.HasFusion("heavy", "ember") ? g.Config.MoltenChipBonus : 0);

    /// <summary>Whether fire spread also hits diagonal neighbours (pyroclasm only).</summary>
    internal static bool SpreadIncludesDiagonals(GameInstance g)
        => g.HasRelic("pyroclasm");

    // -----------------------------------------------------------------------
    // Spells
    // -----------------------------------------------------------------------

    /// <summary>Number of ignite-hits applied to the ball after a deflect.</summary>
    internal static int IgniteHits(GameInstance g)
    {
        var lvl = g.SpellLevels.TryGetValue("ignite", out var l) ? l : 1;
        return g.Config.IgniteHits + (lvl - 1) * g.Config.IgniteHitsPerLevel
            + (g.HasRelic("ember_heart") ? g.Config.EmberHeartBonusHits : 0);
    }

    /// <summary>Damage dealt by a fireball projectile.</summary>
    internal static int FireballDamage(GameInstance g)
    {
        var lvl = g.SpellLevels.TryGetValue("fireball", out var l) ? l : 1;
        return g.Config.FireballDamage + (lvl - 1) * g.Config.FireballDamagePerLevel;
    }

    /// <summary>Damage per firewall tick.</summary>
    internal static int FireWallDamage(GameInstance g)
    {
        var lvl = g.SpellLevels.TryGetValue("firewall", out var l) ? l : 1;
        return g.Config.FireWallDamage + (lvl - 1) * g.Config.FireWallDamagePerLevel;
    }

    /// <summary>Total turret active duration in seconds.</summary>
    internal static double TurretDuration(GameInstance g)
    {
        var lvl = g.SpellLevels.TryGetValue("turret", out var l) ? l : 1;
        return g.Config.TurretDuration + (lvl - 1) * g.Config.TurretDurationPerLevel;
    }
}
