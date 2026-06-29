using System.Collections.Generic;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>The §5 stat engine. These assert the DESIGN numbers (base profiles, level growth, ★
/// compounding, perks, masteries, LOCKED caps) and the §5.8 composition order — not mechanics.</summary>
public class StatResolverTests
{
    // ── §5.2 base profiles (Lvl 1, ★0) ──────────────────────────────────────────
    [Theory]
    [InlineData("fire_mage",   3, 3, 0.12, 1.7, 0, 1.1)]
    [InlineData("paladin",     3, 6, 0.04, 2.2, 0, 0.9)]
    [InlineData("engineer",    2, 4, 0.06, 1.5, 1, 1.2)]
    [InlineData("necromancer", 2, 5, 0.08, 2.0, 0, 1.0)]
    public void BaseProfiles_MatchDesign_5_2(string hero, double pow, double vit,
        double cc, double cd, int mb, double tempo)
    {
        var s = StatResolver.Resolve(hero, 1, 0);
        Assert.Equal(pow,   s.Power,      3);
        Assert.Equal(vit,   s.Vitality,   3);
        Assert.Equal(cc,    s.CritChance, 3);
        Assert.Equal(cd,    s.CritDamage, 3);
        Assert.Equal(mb,    s.Multiball);
        Assert.Equal(tempo, s.Tempo,      3);
    }

    // ── §5.3 per-level growth (weighted to the hero's highs) ─────────────────────
    [Fact]
    public void Level_GrowsHighsFasterThanLows_5_3()
    {
        var fm30 = StatResolver.Resolve("fire_mage", 30, 0);  // high power + crit hero
        Assert.Equal(3 + 29 * 0.25, fm30.Power, 3);           // +0.25/lvl
        Assert.Equal(0.12 + 29 * 0.003, fm30.CritChance, 3);  // +0.3%/lvl
        Assert.Equal(3 + 29 * 0.15, fm30.Vitality, 3);        // +0.15/lvl
        Assert.Equal(1.7 + 29 * 0.01, fm30.CritDamage, 3);    // +0.01/lvl

        var eng30 = StatResolver.Resolve("engineer", 30, 0);  // low power + non-crit hero
        Assert.Equal(2 + 29 * 0.15, eng30.Power, 3);          // +0.15/lvl (slower)
        Assert.Equal(0.06 + 29 * 0.001, eng30.CritChance, 3); // +0.1%/lvl (slower)

        // Multiball/Tempo do NOT grow with level (§5.3).
        Assert.Equal(0, fm30.Multiball);
        Assert.Equal(1.1, fm30.Tempo, 3);
    }

    // ── §5.4 ★ compounds +8% per star (★6 ≈ ×1.59) ──────────────────────────────
    [Fact]
    public void Stars_Compound8PctPerStar_5_4()
    {
        Assert.Equal(1.0,  StatResolver.StarMult(0), 3);
        Assert.Equal(1.08, StatResolver.StarMult(1), 3);
        Assert.Equal(1.587, StatResolver.StarMult(6), 3);
        // ★6 multiplies the base power block (fire_mage has no power perk to confound it).
        var fm = StatResolver.Resolve("fire_mage", 1, 6);
        Assert.Equal(3 * 1.587, fm.Power, 2);
    }

    // ── §5.5 stat-flat hero perks at ★1/★3/★5 ───────────────────────────────────
    [Fact]
    public void Perks_StatFlats_AtStars_5_5()
    {
        // Fire Mage ★1: +5% crit chance (on top of the ×1.08 star-scaled base).
        var fm = StatResolver.Resolve("fire_mage", 1, 1);
        Assert.Equal(0.12 * 1.08 + 0.05, fm.CritChance, 3);
        // Paladin ★1: +0.2 crit damage.
        var pal = StatResolver.Resolve("paladin", 1, 1);
        Assert.Equal(2.2 * 1.08 + 0.2, pal.CritDamage, 3);
        // Engineer ★1: +0.1 tempo; ★3: +1 starting ball.
        var eng1 = StatResolver.Resolve("engineer", 1, 1);
        Assert.Equal(1.2 * 1.08 + 0.1, eng1.Tempo, 3);
        var eng3 = StatResolver.Resolve("engineer", 1, 3);
        Assert.Equal(2, eng3.Multiball); // base 1 + ★3 perk 1 = 2 (at cap)
    }

    // ── §5.6 masteries add account-wide flats AFTER ×star ────────────────────────
    [Fact]
    public void Masteries_AddAccountFlats_5_6()
    {
        var m = new Dictionary<string, int>
        {
            [StatResolver.Sharpshooter] = 5, // +5% crit
            [StatResolver.Brutality]    = 5, // +0.25 crit dmg
            [StatResolver.Conditioning] = 3, // +3 HP
            [StatResolver.Momentum]     = 5, // +0.10 tempo
        };
        var s = StatResolver.Resolve("fire_mage", 1, 0, m);
        Assert.Equal(0.12 + 0.05, s.CritChance, 3);
        Assert.Equal(1.7 + 0.25, s.CritDamage, 3);
        Assert.Equal(3 + 3, s.Vitality, 3);
        Assert.Equal(1.1 + 0.10, s.Tempo, 3);
    }

    [Fact]
    public void Masteries_RespectMaxLevels_5_6()
    {
        // Over-cap mastery levels clamp to their max (Sharpshooter max 5 ⇒ +5% not +10%).
        var m = new Dictionary<string, int> { [StatResolver.Sharpshooter] = 99 };
        var s = StatResolver.Resolve("fire_mage", 1, 0, m);
        Assert.Equal(0.12 + 0.05, s.CritChance, 3);
    }

    // ── §5.10 LOCKED caps ───────────────────────────────────────────────────────
    [Fact]
    public void Caps_AreLocked_5_10()
    {
        // Multiball: engineer base 1 + ★3 perk 1 + Juggler 2 = 4 → clamp to +2.
        var m = new Dictionary<string, int> { [StatResolver.Juggler] = 2 };
        var eng = StatResolver.Resolve("engineer", 1, 3, m);
        Assert.Equal(2, eng.Multiball);

        // Crit chance ≤ 75%, crit damage ≤ ×4 even when stacked sky-high.
        var maxed = new Dictionary<string, int>
            { [StatResolver.Sharpshooter] = 5, [StatResolver.Brutality] = 5 };
        var fm = StatResolver.Resolve("fire_mage", 30, 6, maxed);
        Assert.True(fm.CritChance <= 0.75 + 1e-9);
        Assert.True(fm.CritDamage <= 4.0 + 1e-9);
    }

    // ── §5.3 XP curve ───────────────────────────────────────────────────────────
    [Fact]
    public void XpCurve_MatchesDesign_5_3()
    {
        Assert.Equal(80, StatResolver.XpToNext(1));
        Assert.Equal((int)System.Math.Round(80 * 1.12), StatResolver.XpToNext(2));
    }

    // ── §5.8 the resolved block is wired into a real run ─────────────────────────
    [Fact]
    public void Apply_WiresStatsIntoRun_5_8()
    {
        var g = K.OneBlock(20);
        StatResolver.Apply(StatResolver.Resolve("fire_mage", 1, 0), g);
        Assert.Equal(3, g.StatPower);
        Assert.Equal(0.12, g.CritChance, 3);
        Assert.Equal(1.7, g.CritDamage, 3);
        Assert.Equal(1.1, g.Tempo, 3);
        Assert.Equal(3, g.Hp);
        Assert.Equal(0, g.StatMultiball);
    }

    [Fact]
    public void Apply_Power_RaisesBallDamage_5_1()
    {
        // Fire Mage Power 3 ⇒ a normal (non-crit) hit deals 3, not the Config base.
        var g = K.OneBlock(20);
        StatResolver.Apply(StatResolver.Resolve("fire_mage", 1, 0), g);
        g.CritChance = 0; // isolate Power from crit
        g.Serve();
        var blk = g.Blocks[0];
        int hp0 = blk.Hp;
        K.AimAt(g, blk);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(hp0 - 3, blk.Hp);
    }

    [Fact]
    public void Apply_Multiball_LaunchesExtraBalls_5_1()
    {
        // Engineer base Multiball +1 ⇒ serving puts 2 balls in play.
        var g = K.OneBlock(20);
        StatResolver.Apply(StatResolver.Resolve("engineer", 1, 0), g);
        Assert.Equal(1, g.StatMultiball);
        g.Serve();
        Assert.Equal(2, g.Balls.Count);
    }
}
