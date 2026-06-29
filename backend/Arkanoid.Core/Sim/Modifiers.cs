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
        var igniteBonus = ignited ? 1 : 0;
        var relicBonus  = (g.HasRelic("glass_cannon") ? (int)g.RelicMagnitude("glass_cannon") : 0)
                        + (g.HasRelic("flint_core") && target.MaxHp >= (int)g.RelicThreshold("flint_core")
                            ? (int)g.RelicMagnitude("flint_core") : 0)
                        + (ghost && g.HasRelic("ghost_lens") ? (int)g.RelicMagnitude("ghost_lens") : 0)
                        + (g.HasRelic("pillar_doctrine")
                            && (target.IsStatue || target.TypeId.StartsWith("heaven_column"))
                            ? (int)g.RelicMagnitude("pillar_doctrine") : 0);
        var ballCoreBonus = g.BallCores.Contains("heavy") ? 1 : 0; // HeavyBallDamageBonus = 1
        // item: ball_damage adds a flat per-hit bonus; crit_tough adds only vs tough blocks (3+ HP)
        var itemBonus = g.ItemBallDamageBonus
                      + (target.MaxHp >= 3 ? g.ItemCritToughBonus : 0);
        // Stat engine (§5.1/§5.8): Power is the hero-resolved base hit damage; 0 ⇒ legacy Config base.
        var basePower = g.StatPower > 0 ? g.StatPower : g.Config.BallDamage;
        // §3 Lich's Gaze: the ball hits cursed blocks harder.
        var curseBonus = target.Cursed ? g.LichCurseBonus : 0;
        return basePower + igniteBonus + relicBonus + ballCoreBonus + itemBonus + curseBonus;
    }

    // -----------------------------------------------------------------------
    // Mana
    // -----------------------------------------------------------------------

    /// <summary>Effective mana-regen multiplier (mana_battery × engineer stacks × item bonuses).</summary>
    internal static double ManaRegenMult(GameInstance g)
    {
        var mult = g.HasRelic("mana_battery") ? g.RelicMagnitude2("mana_battery") : 1.0;
        mult *= (g.Character == "engineer" ? g.Config.EngineerRegenMult : 1.0);
        // Stat engine (§5.1): Tempo multiplies mana-regen (and paddle speed where applicable).
        mult *= g.Tempo;
        // item: mana_regen is an additive bonus to the multiplier (e.g. +0.20 per tier)
        mult *= (1.0 + g.ItemManaRegenMultBonus);
        // Lead Paddle tradeoff: the wide paddle is paid for in regen.
        if (g.HasRelic("lead_paddle")) mult *= g.RelicMagnitude2("lead_paddle");
        return mult;
    }

    /// <summary>Mana granted when a block is killed. killMult: 1.0 = ball kill, 0.5 = spell/minion, 0.25 = chain.</summary>
    internal static double KillManaGain(GameInstance g, double killMult = 1.0)
        => (g.Config.ManaPerKill
           * (g.Character == "necromancer" ? g.Config.NecromancerKillManaMult : 1.0)
           * (1.0 + g.ItemKillManaMultBonus))
           * killMult
           + Systems.SpellSystem.DrainBonusMana(g, killMult);

    // -----------------------------------------------------------------------
    // Fire spread
    // -----------------------------------------------------------------------

    /// <summary>Whether ignited kills should spread fire to neighbours (fire_mage, pyroclasm, or the Molten fusion).</summary>
    internal static bool ShouldSpreadFire(GameInstance g)
        => g.Character == "fire_mage" || g.HasRelic("pyroclasm") || g.HasFusion("heavy", "ember");

    /// <summary>Damage dealt per burn DoT tick — pyroclasm and the Molten fusion deepen it.</summary>
    internal static int BurnDamage(GameInstance g)
        => g.Config.Fire.BurnDamage
           + (g.HasRelic("pyroclasm") ? (int)g.RelicMagnitude("pyroclasm") - 1 : 0) // pyroclasm magnitude 2 → +1
           + (g.HasFusion("heavy", "ember") ? 1 : 0);                                // MoltenChipBonus = 1

    /// <summary>Whether fire spread also hits diagonal neighbours (pyroclasm only).</summary>
    internal static bool SpreadIncludesDiagonals(GameInstance g)
        => g.HasRelic("pyroclasm");

    // -----------------------------------------------------------------------
    // Spells
    // -----------------------------------------------------------------------

    /// <summary>Number of ignite-hits applied to the ball after a deflect.</summary>
    internal static int IgniteHits(GameInstance g)
    {
        var def = g.GetSpellDef("ignite");
        if (def == null) return 4;
        var lvl = g.SpellLevel("ignite");
        return def.Hits + (lvl - 1) * def.HitsPerLevel
            + (g.HasRelic("ember_heart") ? (int)g.RelicMagnitude("ember_heart") : 0);
    }

    // Hook relics — one lookup site per hook point so system files are string-free.
    internal static bool HasLodestone(GameInstance g)      => g.HasRelic("lodestone");
    internal static double LodestoneSpeed(GameInstance g)  => g.RelicMagnitude("lodestone");
    internal static bool HasMidas(GameInstance g)          => g.HasRelic("midas");
    internal static int MidasCrystals(GameInstance g)      => (int)g.RelicMagnitude("midas");
    internal static bool HasSapper(GameInstance g)         => g.HasRelic("sapper");
    internal static int SapperRadiusBonus(GameInstance g)  => (int)g.RelicMagnitude("sapper");
    internal static bool HasSecondWind(GameInstance g)     => g.HasRelic("second_wind");
    internal static bool HasSplitShot(GameInstance g)      => g.HasRelic("split_shot");
    internal static int SplitShotCadence(GameInstance g)   => (int)g.RelicMagnitude("split_shot");
    internal static bool HasSouljar(GameInstance g)        => g.HasRelic("souljar");
    internal static int SouljarCadence(GameInstance g)     => (int)g.RelicMagnitude("souljar");
    internal static bool HasOvercharge(GameInstance g)     => g.HasRelic("overcharge");
    internal static double OverchargeBonus(GameInstance g) => g.RelicMagnitude("overcharge");
    internal static bool HasConductor(GameInstance g)      => g.HasRelic("conductor");
}
