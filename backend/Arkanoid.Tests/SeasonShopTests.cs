using Arkanoid.Core.Meta;
using Xunit;

/// <summary>Season Shop (economy rework §7): Season Tokens exchange for currency bundles (the shop is a
/// faucet into the 3 coins, not a 4th wallet). Bonus-roll offers are applied by the endpoint, not here.</summary>
public class SeasonShopTests
{
    [Fact]
    public void BuyCurrencyBundle_SpendsTokens_GrantsCoin()
    {
        var p = new Profile();
        p.Season.Tokens = 100;
        Assert.True(SeasonShopService.TryBuyCurrency(p, "insight_bundle")); // 40 tokens → +120 Insight
        Assert.Equal(60, p.Season.Tokens);
        Assert.Equal(120, p.Insight);
    }

    [Fact]
    public void BuyCurrencyBundle_FailsWhenShortOnTokens()
    {
        var p = new Profile();
        p.Season.Tokens = 10;
        Assert.False(SeasonShopService.TryBuyCurrency(p, "souls_bundle")); // needs 80
        Assert.Equal(10, p.Season.Tokens);
        Assert.Equal(0, p.Souls);
    }

    [Fact]
    public void TryBuyCurrency_RejectsRollOffers()
    {
        var p = new Profile { Season = { Tokens = 999 } };
        Assert.False(SeasonShopService.TryBuyCurrency(p, "bonus_card_roll")); // not a currency offer
        Assert.Equal(999, p.Season.Tokens);
    }
}
