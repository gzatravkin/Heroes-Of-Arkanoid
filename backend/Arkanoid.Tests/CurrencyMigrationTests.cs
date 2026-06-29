using Arkanoid.Core.Meta;
using Xunit;

/// <summary>
/// Economy rework (docs/2026-06-14 §9): the legacy 11-currency soup folds ONE TIME into the 3 coins —
/// CardDust+ModuleCores+CampaignGold → Sparks; Crystals+Shards+unspent HeroTokens → Souls; Points → Insight.
/// </summary>
public class CurrencyMigrationTests
{
    [Fact]
    public void Migrate_FoldsLegacyIntoThreeCoins_AndIsIdempotent()
    {
        var p = new Profile
        {
            CardDust = 5, ModuleCores = 3, CampaignGold = 2,  // → Sparks 10
            Crystals = 7, Shards = 4,                          // → Souls 11 (+ tokens below)
            Points = 9,                                        // → Insight 9
        };
        p.HeroTokens["fire_mage"] = 6; // unspent hero tokens → Souls (+6 → 17)

        p.MigrateCurrencies();

        Assert.Equal(10, p.Sparks);
        Assert.Equal(17, p.Souls);
        Assert.Equal(9,  p.Insight);
        Assert.True(p.CurrencyMigrated);
        // Legacy balances are drained so nothing double-counts later.
        Assert.Equal(0, p.CardDust); Assert.Equal(0, p.ModuleCores); Assert.Equal(0, p.CampaignGold);
        Assert.Equal(0, p.Crystals); Assert.Equal(0, p.Shards); Assert.Equal(0, p.Points);
        Assert.Empty(p.HeroTokens);

        // Idempotent: a second call (e.g. next load) must not re-fold.
        p.Sparks = 99;
        p.MigrateCurrencies();
        Assert.Equal(99, p.Sparks);
    }

    [Fact]
    public void Migrate_KeepsLiveOpsTokens_Untouched()
    {
        var p = new Profile { Medals = 3, EventTokens = 4, SeasonTokens = 5, Crystals = 2 };
        p.MigrateCurrencies();
        Assert.Equal(3, p.Medals);
        Assert.Equal(4, p.EventTokens);
        Assert.Equal(5, p.SeasonTokens);
        Assert.Equal(2, p.Souls); // crystals still folded
    }

    [Fact]
    public void Wallet_ReadsAndSpends_TheThreeCoins()
    {
        var p = new Profile { Sparks = 50, Souls = 30, Insight = 20 };
        Assert.Equal(50, Wallet.Get(p, Currency.Sparks));
        Assert.True(Wallet.TrySpend(p, Currency.Souls, 25));
        Assert.Equal(5, p.Souls);
        Assert.False(Wallet.TrySpend(p, Currency.Insight, 999)); // can't overspend
        Assert.Equal(20, p.Insight);
    }
}
