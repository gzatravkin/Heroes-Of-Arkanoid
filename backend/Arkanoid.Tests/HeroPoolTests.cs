using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Economy rework §4 — hero acquisition is TWO steps: a biome-boss clear makes the next hero ROLLABLE
/// (seeds the pool, hero still locked), then a hero roll unlocks it. Encodes the acquisition design.
/// </summary>
public class HeroPoolTests
{
    [Fact]
    public void BossClear_SeedsPool_ThenRollUnlocks()
    {
        var p = Profile.NewDefault();
        Rewards.GrantLevelCompletion(p, "hell-boss", new ProgressionConfig());

        Assert.Contains("paladin", p.HeroPool);                 // rollable…
        Assert.DoesNotContain("paladin", p.UnlockedCharacters); // …but not playable until rolled

        var r = RollService.RollHero(p, new Rng(1));            // single-entry pool ⇒ deterministic
        Assert.Equal("paladin", r.Id);
        Assert.True(r.WasNew);
        Assert.Contains("paladin", p.UnlockedCharacters);       // first card unlocks the hero
    }
}
