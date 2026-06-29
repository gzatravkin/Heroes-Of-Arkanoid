using Arkanoid.Core.Meta;
using Xunit;

/// <summary>Weekly Trial scoring + seed (plan §A.3).</summary>
public class TrialTests
{
    [Fact]
    public void Score_RewardsBlocks_AndWinBonus()
    {
        Assert.Equal(10 * 10 + 5000, TrialConfig.Score(10, won: true));
        Assert.Equal(10 * 10,         TrialConfig.Score(10, won: false));
        Assert.Equal(0,               TrialConfig.Score(0, won: false));
    }

    [Fact]
    public void SeedFor_IsDeterministic_PerWeek_AndNonNegative()
    {
        Assert.Equal(TrialConfig.SeedFor(7), TrialConfig.SeedFor(7));
        Assert.NotEqual(TrialConfig.SeedFor(7), TrialConfig.SeedFor(8));
        Assert.True(TrialConfig.SeedFor(123456) >= 0);
    }

    [Fact]
    public void Score_StaysWithinPlausibleBound()
    {
        // Even a huge clear must stay under the board's anti-cheat ceiling (server-authoritative).
        Assert.True(TrialConfig.Score(500, won: true) <= LeaderboardService.MaxPlausibleScore("trial"));
    }
}
