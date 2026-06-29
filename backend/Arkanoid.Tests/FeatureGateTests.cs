using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Meta;
using Xunit;

/// <summary>Progressive feature unlocks by campaign progress (clear UI gating).</summary>
public class FeatureGateTests
{
    private static HashSet<string> Done(params string[] ids) => new(ids);

    [Fact]
    public void Daily_AlwaysUnlocked()
    {
        Assert.True(FeatureGates.IsUnlocked(Feature.Daily, Done()));
    }

    [Fact]
    public void Cards_LockedUntilHell2()
    {
        Assert.False(FeatureGates.IsUnlocked(Feature.Cards, Done("hell-1")));
        Assert.True(FeatureGates.IsUnlocked(Feature.Cards, Done("hell-1", "hell-2")));
    }

    [Fact]
    public void Modules_LockedUntilHellBoss()
    {
        Assert.False(FeatureGates.IsUnlocked(Feature.Modules, Done("hell-3")));
        Assert.True(FeatureGates.IsUnlocked(Feature.Modules, Done("hell-boss")));
    }

    [Fact]
    public void League_LockedUntilCavernsBoss_Prestige_UntilHeavenBoss()
    {
        Assert.False(FeatureGates.IsUnlocked(Feature.League, Done("hell-boss")));
        Assert.True(FeatureGates.IsUnlocked(Feature.League, Done("caverns-boss")));
        Assert.False(FeatureGates.IsUnlocked(Feature.Prestige, Done("village-boss")));
        Assert.True(FeatureGates.IsUnlocked(Feature.Prestige, Done("heaven-boss")));
    }

    [Fact]
    public void UnlockedBy_MapsLevelToFeatures()
    {
        Assert.Contains(Feature.Modules, FeatureGates.UnlockedBy("hell-boss"));
        Assert.Contains(Feature.Cards, FeatureGates.UnlockedBy("hell-2"));
        Assert.Empty(FeatureGates.UnlockedBy("caverns-3")); // a non-gate level unlocks nothing
    }

    [Fact]
    public void Reward_AnnouncesUnlockedFeature_OnGateClear()
    {
        var p = Profile.NewDefault();
        var r = Rewards.GrantLevelCompletion(p, "hell-boss", ProgressionConfig.Default);
        Assert.Contains("Modules", r.FeaturesUnlocked);
    }
}
