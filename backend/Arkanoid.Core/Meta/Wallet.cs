namespace Arkanoid.Core.Meta;

/// <summary>The meta (Profile-held) currencies. In-run Gold lives on <c>GameInstance</c>, not here.
/// Economy rework (docs/2026-06-14): the 3 spend currencies are <c>Sparks</c> (gear: cards/modules),
/// <c>Souls</c> (loadout/roster: spells/heroes/slots/respec) and <c>Insight</c> (mastery). The pre-rework
/// coins are kept ONLY so a one-time save migration can fold them into the three (see Profile.MigrateCurrencies);
/// they have no sources and are removed once nothing references them.</summary>
public enum Currency
{
    // ── The 3 live spend currencies ──
    Sparks, Souls, Insight,
    // ── Live-ops track currencies (kept) ──
    Medals, EventTokens, SeasonTokens,
    // ── Legacy (migration-only; no sources) ──
    Crystals, Shards, Points, CampaignGold, CardDust, ModuleCores,
}

/// <summary>
/// Pure multi-currency wallet over a <see cref="Profile"/>. Centralises read/grant/spend so the
/// social/economy systems (cards, modules, league/season shops) never hand-roll <c>profile.X -= n</c>.
/// Spends are atomic: a multi-currency cost either fully applies or not at all.
/// </summary>
public static class Wallet
{
    public static int Get(Profile p, Currency c) => c switch
    {
        Currency.Sparks       => p.Sparks,
        Currency.Souls        => p.Souls,
        Currency.Insight      => p.Insight,
        Currency.Crystals     => p.Crystals,
        Currency.Shards       => p.Shards,
        Currency.Points       => p.Points,
        Currency.CampaignGold => p.CampaignGold,
        Currency.CardDust     => p.CardDust,
        Currency.ModuleCores  => p.ModuleCores,
        Currency.Medals       => p.Medals,
        Currency.EventTokens  => p.EventTokens,
        Currency.SeasonTokens => p.SeasonTokens,
        _ => 0,
    };

    public static void Add(Profile p, Currency c, int amount)
    {
        switch (c)
        {
            case Currency.Sparks:       p.Sparks       = System.Math.Max(0, p.Sparks + amount);       break;
            case Currency.Souls:        p.Souls        = System.Math.Max(0, p.Souls + amount);        break;
            case Currency.Insight:      p.Insight      = System.Math.Max(0, p.Insight + amount);      break;
            case Currency.Crystals:     p.Crystals     = System.Math.Max(0, p.Crystals + amount);     break;
            case Currency.Shards:       p.Shards       = System.Math.Max(0, p.Shards + amount);       break;
            case Currency.Points:       p.Points       = System.Math.Max(0, p.Points + amount);       break;
            case Currency.CampaignGold: p.CampaignGold = System.Math.Max(0, p.CampaignGold + amount); break;
            case Currency.CardDust:     p.CardDust     = System.Math.Max(0, p.CardDust + amount);     break;
            case Currency.ModuleCores:  p.ModuleCores  = System.Math.Max(0, p.ModuleCores + amount);  break;
            case Currency.Medals:       p.Medals       = System.Math.Max(0, p.Medals + amount);       break;
            case Currency.EventTokens:  p.EventTokens  = System.Math.Max(0, p.EventTokens + amount);  break;
            case Currency.SeasonTokens: p.SeasonTokens = System.Math.Max(0, p.SeasonTokens + amount); break;
        }
    }

    public static bool CanAfford(Profile p, IReadOnlyDictionary<Currency, int> cost)
    {
        foreach (var kv in cost) if (Get(p, kv.Key) < kv.Value) return false;
        return true;
    }

    /// <summary>Spend a (possibly multi-currency) cost atomically. Returns false and changes nothing
    /// when any component is short.</summary>
    public static bool TrySpend(Profile p, IReadOnlyDictionary<Currency, int> cost)
    {
        if (!CanAfford(p, cost)) return false;
        foreach (var kv in cost) Add(p, kv.Key, -kv.Value);
        return true;
    }

    /// <summary>Convenience single-currency spend.</summary>
    public static bool TrySpend(Profile p, Currency c, int amount)
        => TrySpend(p, new Dictionary<Currency, int> { [c] = amount });
}
