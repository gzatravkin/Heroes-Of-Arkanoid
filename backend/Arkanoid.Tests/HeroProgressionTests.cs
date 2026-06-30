using Arkanoid.Core.Meta;
using Xunit;

/// <summary>Hero progression — XP/level (§5.3), ★ ascension via Hero Tokens (§5.4), Masteries on the
/// shared Skill-Points pool (§5.6/§5.10). Asserts the DESIGN economy, not just that a number moves.</summary>
public class HeroProgressionTests
{
    // ── §5.3 Hero XP accrues EVERY win (not first-clear), levels on the curve ─────
    [Fact]
    public void HeroXp_AccruesOnEveryWin_AndLevelsOnCurve()
    {
        var p = Profile.NewDefault();
        // A win with 60 blocks = 60 + 25 win bonus = 85 ≥ XpToNext(1)=80 → Lvl 2 (carry 5).
        var r1 = Rewards.GrantHeroXp(p, "fire_mage", blocksDestroyed: 60, won: true);
        Assert.Equal(85, r1.XpGained);
        Assert.Equal(2, r1.NewLevel);
        Assert.True(r1.LeveledUp);
        Assert.Equal(2, p.HeroProgress["fire_mage"].Level);
        Assert.Equal(5, p.HeroProgress["fire_mage"].Exp);

        // A SECOND win with the same hero accrues again (not idempotent like first-clear rewards).
        var r2 = Rewards.GrantHeroXp(p, "fire_mage", blocksDestroyed: 10, won: true);
        Assert.True(r2.XpGained > 0);
        Assert.True(p.HeroProgress["fire_mage"].Exp > 5 || p.HeroProgress["fire_mage"].Level > 2);
    }

    [Fact]
    public void HeroXp_CapsAtLevel30()
    {
        var p = Profile.NewDefault();
        for (int i = 0; i < 200; i++) Rewards.GrantHeroXp(p, "fire_mage", blocksDestroyed: 500, won: true);
        Assert.Equal(30, p.HeroProgress["fire_mage"].Level);
        Assert.Equal(0, p.HeroProgress["fire_mage"].Exp);
    }

    [Fact]
    public void HeroXp_IsPerHero_NotShared()
    {
        var p = Profile.NewDefault();
        Rewards.GrantHeroXp(p, "paladin", blocksDestroyed: 100, won: true);
        Assert.True(p.HeroProgress["paladin"].Level >= 2);
        Assert.False(p.HeroProgress.ContainsKey("fire_mage")); // untouched
    }

    // ── §5.4 ★ ascension: MANUALLY spend banked duplicate pips per the cost table (2026-06-15) ─────────
    [Fact]
    public void Ascend_SpendsPips_PerCostTable()
    {
        var p = Profile.NewDefault();
        p.HeroProgress["fire_mage"] = new HeroProgress { AscendPips = 10 }; // exactly ★1 cost
        Assert.True(Upgrades.TryAscendHero(p, "fire_mage"));
        Assert.Equal(1, p.HeroProgress["fire_mage"].Stars);
        Assert.Equal(0, p.HeroProgress["fire_mage"].AscendPips);
        // Not enough for ★2 (cost 20).
        Assert.False(Upgrades.TryAscendHero(p, "fire_mage"));
        Assert.Equal(1, p.HeroProgress["fire_mage"].Stars);
    }

    [Fact]
    public void Ascend_StopsAtStar6()
    {
        var p = Profile.NewDefault();
        p.HeroProgress["fire_mage"] = new HeroProgress { AscendPips = 10_000 };
        for (int i = 0; i < 6; i++) Assert.True(Upgrades.TryAscendHero(p, "fire_mage"));
        Assert.Equal(6, p.HeroProgress["fire_mage"].Stars);
        Assert.False(Upgrades.TryAscendHero(p, "fire_mage")); // ★6 is the cap
        // Total spent = 10+20+40+70+110+160 = 410.
        Assert.Equal(10_000 - 410, p.HeroProgress["fire_mage"].AscendPips);
    }

    [Fact]
    public void WinDripsSouls_BossPaysMore()
    {
        // Economy rework: a win drips Souls (the hero/spell coin) — HeroTokens are retired (★ via rolls).
        // HeroTokensPerWin = 3, HeroTokensPerBoss = 5.
        var p = Profile.NewDefault();
        Rewards.GrantHeroXp(p, "fire_mage", 10, won: true, isBoss: false);
        Assert.Equal(3, p.Souls);
        Rewards.GrantHeroXp(p, "fire_mage", 10, won: true, isBoss: true);
        Assert.Equal(3 + 5, p.Souls);
        Assert.False(p.HeroTokens.ContainsKey("fire_mage")); // no token leak
    }

    // ── §6 Masteries spend Insight (economy rework) ──────────────────────────────
    [Fact]
    public void Mastery_SpendsInsight_AndClampsAtMax()
    {
        var p = Profile.NewDefault();
        p.Insight = 1000;
        Assert.True(Upgrades.TryUpgradeMastery(p, StatResolver.Sharpshooter));
        Assert.Equal(1, p.Masteries[StatResolver.Sharpshooter]);
        Assert.Equal(1000 - Upgrades.MasteryCost(0), p.Insight); // Insight funds mastery now

        // Conditioning maxes at 3.
        p.Insight = 1000;
        for (int i = 0; i < 3; i++) Assert.True(Upgrades.TryUpgradeMastery(p, StatResolver.Conditioning));
        Assert.False(Upgrades.TryUpgradeMastery(p, StatResolver.Conditioning)); // at max
        Assert.Equal(3, p.Masteries[StatResolver.Conditioning]);
    }

    [Fact]
    public void Mastery_FailsWithoutInsight()
    {
        var p = Profile.NewDefault();
        p.Insight = 0;
        Assert.False(Upgrades.TryUpgradeMastery(p, StatResolver.Brutality));
        Assert.False(p.Masteries.ContainsKey(StatResolver.Brutality));
    }

    // ── End-to-end: progression feeds the resolver ───────────────────────────────
    [Fact]
    public void Progression_FlowsIntoResolvedStats()
    {
        var p = Profile.NewDefault();
        // Level the Fire Mage up a few times, ascend ★1, buy 2 Sharpshooter.
        for (int i = 0; i < 5; i++) Rewards.GrantHeroXp(p, "fire_mage", 200, won: true);
        p.HeroProgress["fire_mage"].AscendPips = 10;   // banked dupes → enough for ★1
        Upgrades.TryAscendHero(p, "fire_mage");
        p.Insight = 1000;
        Upgrades.TryUpgradeMastery(p, StatResolver.Sharpshooter);
        Upgrades.TryUpgradeMastery(p, StatResolver.Sharpshooter);

        var prog = p.HeroProgress["fire_mage"];
        var baseStats = StatResolver.Resolve("fire_mage", 1, 0);
        var grown     = StatResolver.Resolve("fire_mage", prog.Level, prog.Stars, p.Masteries);
        Assert.True(prog.Level > 1, "hero leveled from play");
        Assert.Equal(1, prog.Stars);
        // Crit chance grew from level + ★ + Fire Mage ★1 perk + 2× Sharpshooter.
        Assert.True(grown.CritChance > baseStats.CritChance);
        Assert.True(grown.Power > baseStats.Power);
    }
}
