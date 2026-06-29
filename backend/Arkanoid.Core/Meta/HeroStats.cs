using Arkanoid.Core.Sim;

namespace Arkanoid.Core.Meta;

/// <summary>Resolved hero stat block (design §5.1). Power/Vitality are flat; Multiball is a flat
/// extra-ball count; CritChance is a probability (0..1); CritDamage/Tempo are multipliers.</summary>
public readonly record struct HeroStats(
    double Power, double Vitality, double CritChance, double CritDamage, int Multiball, double Tempo);

/// <summary>The §5 stat engine: composes a hero's final stats from base profile (§5.2), hero level
/// (§5.3), ★ ascension (§5.4), hero perks (§5.5), and account Masteries (§5.6), honoring the §5.10
/// LOCKED caps and the §5.8 composition order. Pure (no I/O); the applier wires the result into a run.</summary>
public static class StatResolver
{
    // §5.6 mastery node ids + max levels.
    public const string Sharpshooter = "sharpshooter";
    public const string Brutality    = "brutality";
    public const string Conditioning = "conditioning";
    public const string Juggler      = "juggler";
    public const string Momentum     = "momentum";

    public static readonly IReadOnlyDictionary<string, int> MasteryMaxLevels =
        new Dictionary<string, int>
        {
            [Sharpshooter] = 5, [Brutality] = 5, [Conditioning] = 3, [Juggler] = 2, [Momentum] = 5,
        };

    // §5.2 per-hero base profile (Lvl 1, ★0). Source of truth: docs/2026-06-13 §5.2.
    private static readonly Dictionary<string, HeroStats> BaseProfiles = new()
    {
        ["fire_mage"]   = new HeroStats(3, 3, 0.12, 1.7, 0, 1.1),
        ["paladin"]     = new HeroStats(3, 6, 0.04, 2.2, 0, 0.9),
        ["engineer"]    = new HeroStats(2, 4, 0.06, 1.5, 1, 1.2),
        ["necromancer"] = new HeroStats(2, 5, 0.08, 2.0, 0, 1.0),
    };

    /// <summary>The Lvl-1/★0 base profile for a hero (§5.2), or a neutral fallback for unknown ids.</summary>
    public static HeroStats BaseProfile(string heroId)
        => BaseProfiles.TryGetValue(heroId, out var s) ? s : new HeroStats(2, 3, 0.05, 1.5, 0, 1.0);

    // §5.3 XP curve: xpToNext = 80 × 1.12^(lvl-1).
    public static int XpToNext(int level)
        => (int)System.Math.Round(80 * System.Math.Pow(1.12, System.Math.Max(0, level - 1)));

    // §5.4 cumulative star multiplier = 1.08^stars (compounding), ★0..★6 (★6 ≈ ×1.59).
    public static double StarMult(int stars)
        => System.Math.Pow(1.08, System.Math.Clamp(stars, 0, 6));

    /// <summary>Max ★ (§5.10 LOCKED).</summary>
    public const int MaxStars = 6;

    // §5.4 Hero-Token cost to reach each star (★1..★6).
    private static readonly int[] StarCosts = { 10, 20, 40, 70, 110, 160 };

    /// <summary>Hero-Token cost to ascend FROM (targetStar-1) TO targetStar (1..6), or int.MaxValue if out of range.</summary>
    public static int StarTokenCost(int targetStar)
        => targetStar is >= 1 and <= MaxStars ? StarCosts[targetStar - 1] : int.MaxValue;

    /// <summary>Final resolved stats for a hero at a given level/★ + account-wide masteries (§5.8 order).</summary>
    public static HeroStats Resolve(string heroId, int level, int stars,
        IReadOnlyDictionary<string, int>? masteries = null)
    {
        var b = BaseProfile(heroId);
        level = System.Math.Clamp(level, 1, 30);
        stars = System.Math.Clamp(stars, 0, 6);
        int lv = level - 1;

        // §5.3 per-level growth, weighted to each hero's highs (low stats grow slower).
        double powerPerLvl = b.Power      >= 3    ? 0.25 : 0.15; // high-power vs low
        double ccPerLvl    = b.CritChance >= 0.10 ? 0.003 : 0.001; // crit hero vs not
        double power      = b.Power      + lv * powerPerLvl;
        double vitality   = b.Vitality   + lv * 0.15;
        double critChance = b.CritChance + lv * ccPerLvl;
        double critDamage = b.CritDamage + lv * 0.01;
        int    multiball  = b.Multiball; // §5.3: Multiball/Tempo do NOT grow with level
        double tempo      = b.Tempo;

        // §5.4 ★ multiplier on the scalable stat block.
        double star = StarMult(stars);
        power *= star; vitality *= star; critChance *= star; critDamage *= star; tempo *= star;

        // §5.5 stat-flat hero perks at ★1/★3/★5 (behavioral perks are sim-side).
        switch (heroId)
        {
            case "fire_mage":   if (stars >= 1) critChance += 0.05; break; // ★1 +5% Crit Chance
            case "paladin":     if (stars >= 1) critDamage += 0.2;  break; // ★1 +0.2 Crit Damage
            case "engineer":
                if (stars >= 1) tempo     += 0.1;                          // ★1 +1 Tempo step
                if (stars >= 3) multiball += 1;                            // ★3 +1 starting ball
                break;
        }

        // §5.6 Masteries — account-wide flats added AFTER ×star (§5.8).
        if (masteries != null)
        {
            critChance += MasteryLvl(masteries, Sharpshooter) * 0.01;
            critDamage += MasteryLvl(masteries, Brutality)    * 0.05;
            vitality   += MasteryLvl(masteries, Conditioning) * 1.0;
            multiball  += MasteryLvl(masteries, Juggler)      * 1;
            tempo      += MasteryLvl(masteries, Momentum)     * 0.02;
        }

        // §5.10 LOCKED caps.
        critChance = System.Math.Clamp(critChance, 0, 0.75);
        critDamage = System.Math.Clamp(critDamage, 1.0, 4.0);
        multiball  = System.Math.Clamp(multiball, 0, 2);

        return new HeroStats(power, vitality, critChance, critDamage, multiball, tempo);
    }

    private static int MasteryLvl(IReadOnlyDictionary<string, int> m, string node)
        => m.TryGetValue(node, out var l)
            ? System.Math.Clamp(l, 0, MasteryMaxLevels[node]) : 0;

    // §5.5 behavioral perk ids (the non-stat-flat perks; implemented sim-side, gated by HasPerk).
    public const string FmIgnitedCrit     = "fm_s3_ignited_crit";     // ★3 ignited blocks take +15% from crits
    public const string FmCritKillIgnite  = "fm_s5_critkill_ignite";  // ★5 a crit kill ignites a nearby block
    public const string PalSaveDrain      = "pal_s3_save_drain";      // ★3 first ball-drain each level is saved
    public const string PalLowHpCritDmg   = "pal_s5_lowhp_critdmg";   // ★5 below 50% HP, +25% Crit Damage
    public const string EngExtraFullDmg   = "eng_s5_extraball_fulldmg"; // ★5 extra balls deal full damage
    public const string NecroHeal         = "necro_s1_heal";          // ★1 heal 1 HP / 60 kills
    public const string NecroCritDrain    = "necro_s3_crit_drain";    // ★3 crits drain mana to you
    public const string NecroComboHelper  = "necro_s5_combo_helper";  // ★5 full-combo kill may raise a helper-ball

    /// <summary>The active §5.5 behavioral perks for a hero at a star level (★3/★5 unlock thresholds;
    /// Necromancer ★1 is behavioral too). Stat-flat perks are folded into Resolve, not returned here.</summary>
    public static IReadOnlyList<string> PerksFor(string heroId, int stars)
    {
        var list = new List<string>();
        switch (heroId)
        {
            case "fire_mage":
                if (stars >= 3) list.Add(FmIgnitedCrit);
                if (stars >= 5) list.Add(FmCritKillIgnite);
                break;
            case "paladin":
                if (stars >= 3) list.Add(PalSaveDrain);
                if (stars >= 5) list.Add(PalLowHpCritDmg);
                break;
            case "engineer":
                // ★5 EngExtraFullDmg is DEFERRED: it removes an extra-ball damage penalty that does
                // not exist yet — introducing that penalty is the §5.9 balance pass (a coordinated
                // playtest change to Duplicate/Raise/Multiball), not a stat-engine task. Logged in
                // docs/round-2-recommendations.md. Activating it now would be a no-op stub.
                break;
            case "necromancer":
                if (stars >= 1) list.Add(NecroHeal);
                if (stars >= 3) list.Add(NecroCritDrain);
                if (stars >= 5) list.Add(NecroComboHelper);
                break;
        }
        return list;
    }

    /// <summary>Wire a resolved stat block into a run at start (§5.8). Power→hit damage, Vitality→HP,
    /// Crit→roll, Multiball→extra serves, Tempo→regen. HP is left to dungeon-carry override afterward.</summary>
    public static void Apply(in HeroStats s, GameInstance game)
    {
        // Round half-AWAY-from-zero so the integer HP/Power the sim uses matches the value the
        // Masteries UI shows (JS Math.round is half-up; C# default Math.Round is banker's).
        game.StatPower     = System.Math.Max(1, (int)System.Math.Round(s.Power,    System.MidpointRounding.AwayFromZero));
        game.SetCrit(s.CritChance, s.CritDamage);
        game.StatMultiball = s.Multiball;
        game.Tempo         = s.Tempo;
        int hp = System.Math.Max(1, (int)System.Math.Round(s.Vitality, System.MidpointRounding.AwayFromZero));
        game.StatMaxHp = hp;
        game.SetHp(hp);
        game._log.Log(0, "stats", "hero stats applied",
            $"power={game.StatPower} crit={s.CritChance:0.###} critDmg=x{s.CritDamage:0.##} " +
            $"multiball={s.Multiball} tempo=x{s.Tempo:0.##} hp={game.Hp}");
    }
}
