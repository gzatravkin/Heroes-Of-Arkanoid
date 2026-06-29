using System;
using System.Collections.Generic;
using Arkanoid.Core.Meta;
using Xunit;

public class WalletTests
{
    [Fact]
    public void Get_Add_RoundTrips_AllCurrencies()
    {
        var p = new Profile();
        foreach (Currency c in Enum.GetValues(typeof(Currency)))
        {
            Wallet.Add(p, c, 5);
            Assert.Equal(5, Wallet.Get(p, c));
        }
    }

    [Fact]
    public void Add_NeverGoesNegative()
    {
        var p = new Profile();
        Wallet.Add(p, Currency.CardDust, -10);
        Assert.Equal(0, Wallet.Get(p, Currency.CardDust));
    }

    [Fact]
    public void TrySpend_MultiCurrency_IsAtomic()
    {
        var p = new Profile { CardDust = 30, ModuleCores = 1 };
        var cost = new Dictionary<Currency, int> { [Currency.CardDust] = 20, [Currency.ModuleCores] = 5 };
        // Module cores short → whole spend must fail, nothing deducted.
        Assert.False(Wallet.TrySpend(p, cost));
        Assert.Equal(30, p.CardDust);
        Assert.Equal(1, p.ModuleCores);
    }

    [Fact]
    public void TrySpend_Succeeds_WhenAffordable()
    {
        var p = new Profile { CardDust = 30, Medals = 10 };
        var cost = new Dictionary<Currency, int> { [Currency.CardDust] = 20, [Currency.Medals] = 7 };
        Assert.True(Wallet.TrySpend(p, cost));
        Assert.Equal(10, p.CardDust);
        Assert.Equal(3, p.Medals);
    }
}

public class SeasonClockTests
{
    private static readonly SeasonClock C = new();
    private static DateTimeOffset Day(int d) => C.Epoch.AddDays(d).AddHours(3); // mid-day on day d

    [Fact]
    public void Day0_IsWeek0_Season0()
    {
        var t = Day(0);
        Assert.Equal(0, C.DayId(t));
        Assert.Equal(0, C.WeekId(t));
        Assert.Equal(0, C.SeasonId(t));
    }

    [Fact]
    public void Week_Advances_Every7Days()
    {
        Assert.Equal(0, C.WeekId(Day(6)));
        Assert.Equal(1, C.WeekId(Day(7)));
        Assert.Equal(2, C.WeekId(Day(14)));
    }

    [Fact]
    public void Season_Advances_Every4Weeks()
    {
        Assert.Equal(0, C.SeasonId(Day(27))); // week 3
        Assert.Equal(1, C.SeasonId(Day(28))); // week 4 → season 1
    }

    [Fact]
    public void WeekEndsAt_IsNextWeekStart()
    {
        var t = Day(2); // week 0
        Assert.Equal(C.Epoch.AddDays(7), C.WeekEndsAt(t));
    }

    [Fact]
    public void DayId_ClampsBeforeEpoch()
    {
        Assert.Equal(0, C.DayId(C.Epoch.AddDays(-5)));
    }
}
