using Arkanoid.Core.Meta;
using Xunit;

/// <summary>
/// Mastery economy (economy rework §6): nodes level on Insight; a respec refunds the exact Insight spent
/// (re-allocatable) for a flat Souls fee — the C2 gate against respec-spam.
/// </summary>
public class MasteryEconomyTests
{
    [Fact]
    public void Reset_RefundsExactInsight_CostsSouls()
    {
        var p = new Profile { Insight = 1000, Souls = 100 };
        Assert.True(Upgrades.TryUpgradeMastery(p, StatResolver.Sharpshooter)); // −MasteryCost(0)
        Assert.True(Upgrades.TryUpgradeMastery(p, StatResolver.Sharpshooter)); // −MasteryCost(1)
        int spent = Upgrades.MasteryCost(0) + Upgrades.MasteryCost(1);
        int insightBeforeReset = p.Insight;

        Assert.True(Upgrades.ResetMasteries(p));
        Assert.Empty(p.Masteries);                                    // wiped
        Assert.Equal(insightBeforeReset + spent, p.Insight);          // exact Insight refunded
        Assert.Equal(100 - Upgrades.MasteryRespecCost, p.Souls);      // Souls fee paid
    }

    [Fact]
    public void Reset_FailsWhenShortOnSouls_NoOp()
    {
        var p = new Profile { Insight = 1000, Souls = 0 };
        Upgrades.TryUpgradeMastery(p, StatResolver.Brutality);
        Assert.False(Upgrades.ResetMasteries(p)); // can't pay the Souls fee
        Assert.Single(p.Masteries);               // unchanged
    }
}
