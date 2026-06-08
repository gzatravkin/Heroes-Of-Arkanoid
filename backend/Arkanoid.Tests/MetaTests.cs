using Arkanoid.Core.Meta;
using Xunit;

/// <summary>Unit tests for campaign graph, rewards, upgrades, and progression config. All target Core only.</summary>
public class MetaTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static CampaignCatalog MakeCatalog() => CampaignCatalog.FromJson("""
        { "nodes": [
          { "id": "hell-1",        "label": "Hell I",             "biome": "hell",    "x": 0, "y": 0, "requires": [] },
          { "id": "hell-teleport", "label": "Hell Teleporters",   "biome": "hell",    "x": 1, "y": 0, "requires": ["hell-1"] },
          { "id": "caverns-1",     "label": "Caverns I",          "biome": "caverns", "x": 2, "y": 0, "requires": ["hell-teleport"] }
        ]}
        """);

    private static ProgressionConfig Cfg => ProgressionConfig.Default;

    // ── Campaign unlock tests ────────────────────────────────────────────────

    [Fact]
    public void Campaign_Hell1_UnlockedWithEmptyCompleted()
    {
        var catalog   = MakeCatalog();
        var completed = new HashSet<string>();
        var hell1     = catalog.Nodes.Single(n => n.Id == "hell-1");
        Assert.True(catalog.IsUnlocked(hell1, completed));
    }

    [Fact]
    public void Campaign_HellTeleport_LockedWithEmptyCompleted()
    {
        var catalog   = MakeCatalog();
        var completed = new HashSet<string>();
        var node      = catalog.Nodes.Single(n => n.Id == "hell-teleport");
        Assert.False(catalog.IsUnlocked(node, completed));
    }

    [Fact]
    public void Campaign_HellTeleport_UnlockedAfterHell1Completed()
    {
        var catalog   = MakeCatalog();
        var completed = new HashSet<string> { "hell-1" };
        var node      = catalog.Nodes.Single(n => n.Id == "hell-teleport");
        Assert.True(catalog.IsUnlocked(node, completed));
    }

    [Fact]
    public void Campaign_Caverns1_LockedUntilHellTeleportCompleted()
    {
        var catalog   = MakeCatalog();
        var node      = catalog.Nodes.Single(n => n.Id == "caverns-1");

        // Only hell-1 done — caverns still locked.
        Assert.False(catalog.IsUnlocked(node, new HashSet<string> { "hell-1" }));

        // hell-teleport done too — caverns now unlocked.
        Assert.True(catalog.IsUnlocked(node, new HashSet<string> { "hell-1", "hell-teleport" }));
    }

    [Fact]
    public void Campaign_UnlockChainProgresses()
    {
        var catalog   = MakeCatalog();
        var completed = new HashSet<string>();

        // Start: only hell-1 accessible.
        Assert.True(catalog.IsUnlocked(catalog.Nodes.Single(n => n.Id == "hell-1"),        completed));
        Assert.False(catalog.IsUnlocked(catalog.Nodes.Single(n => n.Id == "hell-teleport"), completed));
        Assert.False(catalog.IsUnlocked(catalog.Nodes.Single(n => n.Id == "caverns-1"),     completed));

        completed.Add("hell-1");
        Assert.True(catalog.IsUnlocked(catalog.Nodes.Single(n => n.Id == "hell-teleport"), completed));
        Assert.False(catalog.IsUnlocked(catalog.Nodes.Single(n => n.Id == "caverns-1"),     completed));

        completed.Add("hell-teleport");
        Assert.True(catalog.IsUnlocked(catalog.Nodes.Single(n => n.Id == "caverns-1"), completed));
    }

    // ── Rewards tests ────────────────────────────────────────────────────────

    [Fact]
    public void Rewards_FirstCompletion_GrantsExpPointsCrystals()
    {
        var p      = Profile.NewDefault();
        var result = Rewards.GrantLevelCompletion(p, "hell-1", Cfg);

        Assert.True(result.FirstClear);
        Assert.Equal(Cfg.ExpRewardPerLevel,      result.ExpGained);
        Assert.True(result.CrystalsGained > 0);
        Assert.Contains("hell-1", p.CompletedLevels);
    }

    [Fact]
    public void Rewards_FirstCompletion_AddsLevelIdToCompletedLevels()
    {
        var p = Profile.NewDefault();
        Rewards.GrantLevelCompletion(p, "hell-1", Cfg);
        Assert.Single(p.CompletedLevels);
        Assert.Equal("hell-1", p.CompletedLevels[0]);
    }

    [Fact]
    public void Rewards_SameLevelAgain_IsIdempotent()
    {
        var p = Profile.NewDefault();
        Rewards.GrantLevelCompletion(p, "hell-1", Cfg);
        var expAfterFirst   = p.Exp;
        var pointsAfterFirst = p.Points;
        var crystalsAfterFirst = p.Crystals;

        var second = Rewards.GrantLevelCompletion(p, "hell-1", Cfg);

        Assert.False(second.FirstClear);
        Assert.Equal(0, second.ExpGained);
        Assert.Equal(0, second.PointsGained);
        Assert.Equal(0, second.CrystalsGained);

        // Profile stats must not change.
        Assert.Equal(expAfterFirst,      p.Exp);
        Assert.Equal(pointsAfterFirst,   p.Points);
        Assert.Equal(crystalsAfterFirst, p.Crystals);
        Assert.Single(p.CompletedLevels); // not duplicated
    }

    [Fact]
    public void Rewards_EnoughExpTriggerLevelUp()
    {
        var cfg = new ProgressionConfig
        {
            ExpBase             = 100,
            ExpGrowth           = 1.1,
            ExpRewardPerLevel   = 150, // enough to push past level 1 threshold (100)
            PointsRewardPerLevel = 2,
            CrystalsRewardPerLevel = 10,
            MaxSpellLevel       = 10,
        };

        var p = Profile.NewDefault();
        Assert.Equal(1, p.Level);

        var result = Rewards.GrantLevelCompletion(p, "hell-1", cfg);

        Assert.True(result.LeveledUp);
        Assert.True(p.Level > 1, $"Level should be > 1 but was {p.Level}");
        Assert.Equal(p.Level, result.NewLevel);
        // Remaining EXP must be less than next threshold.
        Assert.True(p.Exp < cfg.ExpToLevel(p.Level));
    }

    [Fact]
    public void Rewards_NoLevelUp_WhenExpBelowThreshold()
    {
        // Very low reward — won't trigger a level-up.
        var cfg = new ProgressionConfig
        {
            ExpBase             = 1000,
            ExpGrowth           = 1.1,
            ExpRewardPerLevel   = 10,
            PointsRewardPerLevel = 2,
            CrystalsRewardPerLevel = 10,
            MaxSpellLevel       = 10,
        };

        var p      = Profile.NewDefault();
        var result = Rewards.GrantLevelCompletion(p, "hell-1", cfg);

        Assert.False(result.LeveledUp);
        Assert.Equal(1, p.Level);
    }

    // ── Upgrades tests ───────────────────────────────────────────────────────

    [Fact]
    public void Upgrades_DecrementsPointsAndRaisesSpellLevel()
    {
        var p = Profile.NewDefault();
        p.Points = 3;
        var before = p.SpellLevels["ignite"];

        var ok = Upgrades.TryUpgradeSpell(p, "ignite", Cfg);

        Assert.True(ok);
        Assert.Equal(before + 1, p.SpellLevels["ignite"]);
        Assert.Equal(2, p.Points);
    }

    [Fact]
    public void Upgrades_FailsAtZeroPoints()
    {
        var p = Profile.NewDefault();
        p.Points = 0;

        var ok = Upgrades.TryUpgradeSpell(p, "ignite", Cfg);

        Assert.False(ok);
        Assert.Equal(0, p.Points); // unchanged
    }

    [Fact]
    public void Upgrades_FailsAtMaxSpellLevel()
    {
        var cfg = new ProgressionConfig { MaxSpellLevel = 3 };
        var p   = Profile.NewDefault();
        p.Points = 10;
        p.SpellLevels["ignite"] = cfg.MaxSpellLevel; // already at cap

        var ok = Upgrades.TryUpgradeSpell(p, "ignite", cfg);

        Assert.False(ok);
        Assert.Equal(cfg.MaxSpellLevel, p.SpellLevels["ignite"]);
        Assert.Equal(10, p.Points); // unchanged
    }

    [Fact]
    public void Upgrades_MissingSpellDefaultsToLevel1_ThenIncrements()
    {
        var p = Profile.NewDefault();
        p.Points = 5;
        p.SpellLevels.Remove("fireball"); // simulate missing key

        var ok = Upgrades.TryUpgradeSpell(p, "fireball", Cfg);

        Assert.True(ok);
        Assert.Equal(2, p.SpellLevels["fireball"]); // default 1 + 1
        Assert.Equal(4, p.Points);
    }

    // ── ProgressionConfig math tests ─────────────────────────────────────────

    [Fact]
    public void ProgressionConfig_ExpToLevel_Level1_Returns100()
    {
        // round(100 * 1.1^0) = round(100) = 100
        Assert.Equal(100, Cfg.ExpToLevel(1));
    }

    [Fact]
    public void ProgressionConfig_ExpToLevel_Level2_Returns110()
    {
        // round(100 * 1.1^1) = round(110) = 110
        Assert.Equal(110, Cfg.ExpToLevel(2));
    }

    [Fact]
    public void ProgressionConfig_ExpToLevel_Level5_MatchesFormula()
    {
        // round(100 * 1.1^4) = round(146.41) = 146
        var expected = (int)Math.Round(100 * Math.Pow(1.1, 4));
        Assert.Equal(expected, Cfg.ExpToLevel(5));
    }

    [Fact]
    public void ProgressionConfig_ExpToLevel_Level10_MatchesFormula()
    {
        // round(100 * 1.1^9)
        var expected = (int)Math.Round(100 * Math.Pow(1.1, 9));
        Assert.Equal(expected, Cfg.ExpToLevel(10));
    }
}
