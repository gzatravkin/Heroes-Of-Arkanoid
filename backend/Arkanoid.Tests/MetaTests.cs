using Arkanoid.Core.Meta;
using Xunit;

public class MetaTests
{
    // Linear chain (economy rework): list ORDER is the sequence; unlock = previous level cleared.
    private static CampaignCatalog MakeCatalog() => CampaignCatalog.FromJson("""
        { "nodes": [
          { "id": "hell-1",        "label": "Hell I",           "biome": "hell" },
          { "id": "hell-teleport", "label": "Hell Teleporters", "biome": "hell" },
          { "id": "caverns-1",     "label": "Caverns I",        "biome": "caverns" }
        ]}
        """);

    private static ProgressionConfig Cfg => ProgressionConfig.Default;

    [Theory]
    [InlineData("hell-1",        new string[]{},                            true)]
    [InlineData("hell-teleport", new string[]{},                            false)]
    [InlineData("hell-teleport", new string[]{"hell-1"},                    true)]
    [InlineData("caverns-1",     new string[]{"hell-1"},                    false)]
    [InlineData("caverns-1",     new string[]{"hell-1", "hell-teleport"},   true)]
    public void Campaign_IsUnlocked(string nodeId, string[] done, bool expected)
    {
        var cat  = MakeCatalog();
        var node = cat.Nodes.Single(n => n.Id == nodeId);
        Assert.Equal(expected, cat.IsUnlocked(node, new HashSet<string>(done)));
    }

    [Fact]
    public void Campaign_UnlockChainProgresses()
    {
        var cat  = MakeCatalog();
        var done = new HashSet<string>();
        Assert.True (cat.IsUnlocked(cat.Nodes.Single(n => n.Id == "hell-1"),        done));
        Assert.False(cat.IsUnlocked(cat.Nodes.Single(n => n.Id == "hell-teleport"), done));
        done.Add("hell-1");
        Assert.True (cat.IsUnlocked(cat.Nodes.Single(n => n.Id == "hell-teleport"), done));
        Assert.False(cat.IsUnlocked(cat.Nodes.Single(n => n.Id == "caverns-1"),     done));
        done.Add("hell-teleport");
        Assert.True (cat.IsUnlocked(cat.Nodes.Single(n => n.Id == "caverns-1"),     done));
    }

    [Fact]
    public void Rewards_FirstCompletion_GrantsExpAndCoins()
    {
        var p      = Profile.NewDefault();
        var result = Rewards.GrantLevelCompletion(p, "hell-1", Cfg);

        Assert.True(result.FirstClear);
        Assert.Equal(Cfg.ExpRewardPerLevel, result.ExpGained);
        Assert.True(result.SparksGained > 0 && result.InsightGained > 0); // 3-coin faucet (economy rework)
        Assert.True(p.Sparks > 0 && p.Insight > 0);
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
        var expAfterFirst     = p.Exp;
        var sparksAfterFirst  = p.Sparks;
        var insightAfterFirst = p.Insight;

        var second = Rewards.GrantLevelCompletion(p, "hell-1", Cfg);

        Assert.False(second.FirstClear);
        Assert.Equal(0, second.ExpGained);
        Assert.Equal(0, second.SparksGained);
        Assert.Equal(0, second.InsightGained);

        // Profile coins must not change on a repeat clear.
        Assert.Equal(expAfterFirst,     p.Exp);
        Assert.Equal(sparksAfterFirst,  p.Sparks);
        Assert.Equal(insightAfterFirst, p.Insight);
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

    // Spell skill-point upgrades were removed in the 2026-06-15 economy rework: spells level only via
    // duplicate rolls (RollService). The old Upgrades.TryUpgradeSpell tests went with that feature.

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
