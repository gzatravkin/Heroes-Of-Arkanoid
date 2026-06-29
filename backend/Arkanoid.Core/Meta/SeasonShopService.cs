using System.Collections.Generic;
using System.Linq;

namespace Arkanoid.Core.Meta;

/// <summary>
/// Season Shop (economy rework §7): spend the Season Tokens you earn from play on CURRENCY bundles or BONUS
/// rolls. No skins. Season Tokens stay a track meter, not a fourth wallet — the shop converts them into the
/// three real coins (and the occasional free pull).
/// </summary>
public static class SeasonShopService
{
    public sealed record Offer(string Id, string Label, int Cost, string Kind, int Amount);

    public static readonly IReadOnlyList<Offer> Offers = new List<Offer>
    {
        new("sparks_bundle",   "Sparks ×100",     50,  "sparks",     100),
        new("souls_bundle",    "Souls ×60",       80,  "souls",      60),
        new("insight_bundle",  "Insight ×120",    40,  "insight",    120),
        new("bonus_card_roll", "Free Card Roll",  60,  "roll_card",  1),
        new("bonus_spell_roll","Free Spell Roll", 100, "roll_spell", 1),
    };

    public static Offer? Get(string id) => Offers.FirstOrDefault(o => o.Id == id);

    /// <summary>Spend Season Tokens on a CURRENCY offer (pure). Returns false for unknown / non-currency /
    /// unaffordable offers. Roll offers are applied by the endpoint (they need catalogs + an Rng).</summary>
    public static bool TryBuyCurrency(Profile p, string offerId)
    {
        var o = Get(offerId);
        if (o == null || p.Season.Tokens < o.Cost) return false;
        Currency coin;
        switch (o.Kind)
        {
            case "sparks":  coin = Currency.Sparks;  break;
            case "souls":   coin = Currency.Souls;   break;
            case "insight": coin = Currency.Insight; break;
            default: return false; // roll offers handled elsewhere
        }
        p.Season.Tokens -= o.Cost;
        Wallet.Add(p, coin, o.Amount);
        return true;
    }
}
