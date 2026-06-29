using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Prestige campaign loop (plan §B.1): ascend, scaling, mutators, board score.</summary>
public class PrestigeTests
{
    [Fact]
    public void CanAscend_OnlyAfterFinalBoss()
    {
        var p = new Profile();
        Assert.False(PrestigeService.CanAscend(p));
        p.CompletedLevels.Add("heaven-boss");
        Assert.True(PrestigeService.CanAscend(p));
    }

    [Fact]
    public void Ascend_WipesCampaignProgress_KeepsMeta_BumpsTier()
    {
        var p = new Profile
        {
            CompletedLevels = new List<string> { "hell-1", "caverns-boss", "heaven-boss" },
            CardDust = 50, ModuleCores = 3, Crystals = 200,
            OwnedCards = new() { ["molten_core"] = new CardOwn { Level = 4 } },
            PrestigeTier = 1,
        };
        int tier = PrestigeService.Ascend(p);
        Assert.Equal(2, tier);
        Assert.Empty(p.CompletedLevels);          // campaign progress wiped for the new loop
        Assert.Equal(50, p.CardDust);             // meta kept
        Assert.Equal(3, p.ModuleCores);
        Assert.Equal(200, p.Crystals);
        Assert.Equal(4, p.OwnedCards["molten_core"].Level);
    }

    [Fact]
    public void Ascend_NoOp_WhenNotEligible()
    {
        var p = new Profile { CompletedLevels = new() { "hell-1" } };
        Assert.Equal(0, PrestigeService.Ascend(p));
        Assert.Equal(0, p.PrestigeTier);
        Assert.Contains("hell-1", p.CompletedLevels); // untouched
    }

    [Fact]
    public void ScaleReward_Adds50PercentPerTier()
    {
        Assert.Equal(10, PrestigeService.ScaleReward(10, 0));
        Assert.Equal(15, PrestigeService.ScaleReward(10, 1));
        Assert.Equal(20, PrestigeService.ScaleReward(10, 2));
    }

    [Fact]
    public void PrestigeScore_TierDominates()
    {
        Assert.True(PrestigeService.PrestigeScore(2, 0) > PrestigeService.PrestigeScore(1, 999));
    }

    [Fact]
    public void ApplyMutators_HardensBlocks_AddsEnemies_KeepsWinnable()
    {
        var g = K.OneBlock(5);
        int hpBefore = g.Blocks.Where(b => !b.Dead).Sum(b => b.Hp);
        int countBefore = g.Blocks.Count;

        PrestigeService.ApplyMutators(g, tier: 2, seed: 123);

        // Harder: total block HP increased (ApplyTier +2 on the destructible block).
        Assert.True(g.Blocks.Where(b => !b.Dead).Sum(b => b.Hp) > hpBefore);
        // Remixed: at least one enemy added.
        Assert.True(g.Blocks.Count > countBefore);
        // Winnable: no block was turned indestructible by the mutator.
        Assert.DoesNotContain(g.Blocks, b => b.Indestructible);
    }

    [Fact]
    public void ApplyMutators_Tier0_NoOp()
    {
        var g = K.OneBlock(5);
        int before = g.Blocks.Count, hp = g.Blocks.Sum(b => b.Hp);
        PrestigeService.ApplyMutators(g, tier: 0, seed: 1);
        Assert.Equal(before, g.Blocks.Count);
        Assert.Equal(hp, g.Blocks.Sum(b => b.Hp));
    }
}
