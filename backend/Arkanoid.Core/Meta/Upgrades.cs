namespace Arkanoid.Core.Meta;

public static class Upgrades
{
    /// <summary>Insight cost to raise a Mastery node FROM <paramref name="curLevel"/> to the next
    /// (economy rework §6) — scales with the level being bought.</summary>
    public static int MasteryCost(int curLevel) => 25 * (curLevel + 1);

    /// <summary>Souls cost to respec all masteries (economy rework §6) — the C2 gate against respec-spam.</summary>
    public const int MasteryRespecCost = 60;

    /// <summary>Spend Insight to raise a Mastery node by one level (economy rework §6). Returns false if the
    /// node is unknown, already maxed, or Insight is short. Mutates <paramref name="p"/> in-place.</summary>
    public static bool TryUpgradeMastery(Profile p, string node)
    {
        if (!StatResolver.MasteryMaxLevels.TryGetValue(node, out var max))
            return false;
        int cur = p.Masteries.TryGetValue(node, out var l) ? l : 0;
        if (cur >= max) return false;
        if (!Wallet.TrySpend(p, Currency.Insight, MasteryCost(cur))) return false;

        p.Masteries[node] = cur + 1;
        return true;
    }

    /// <summary>Respec: clear all masteries, REFUND the exact Insight spent (re-allocatable), for a flat
    /// Souls fee (economy rework §6). Returns false (no-op) when Souls is short. Mutates <paramref name="p"/>.</summary>
    public static bool ResetMasteries(Profile p)
    {
        int refund = 0;
        foreach (var kv in p.Masteries)
            for (int i = 0; i < kv.Value; i++) refund += MasteryCost(i);
        if (!Wallet.TrySpend(p, Currency.Souls, MasteryRespecCost)) return false;
        p.Masteries.Clear();
        Wallet.Add(p, Currency.Insight, refund);
        return true;
    }

    /// <summary>Souls cost to unlock the next flex spell slot (economy rework §3) — escalates with slots held.</summary>
    public static int SlotUnlockCost(int currentSlots) => currentSlots * 40;

    /// <summary>Spend Souls to unlock one more flex spell slot, up to the hotbar cap (signature + 3).
    /// Returns false at the cap or when short on Souls. Mutates <paramref name="p"/> in-place.</summary>
    public static bool TryUnlockSpellSlot(Profile p)
    {
        if (p.UnlockedSpellSlots >= Loadouts.MaxSlots) return false;
        if (!Wallet.TrySpend(p, Currency.Souls, SlotUnlockCost(p.UnlockedSpellSlots))) return false;
        p.UnlockedSpellSlots++;
        return true;
    }

    /// <summary>MANUALLY raise a hero's ★ by one (owner direction 2026-06-15): spends banked duplicate pips
    /// (<see cref="HeroProgress.AscendPips"/>) per the §5.4 cost table. Returns false if already ★6 or short
    /// on pips. Rolling a duplicate hero only BANKS a pip (RollService); the player ascends here on demand.</summary>
    public static bool TryAscendHero(Profile p, string heroId)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return false;
        if (!p.HeroProgress.TryGetValue(heroId, out var hp))
        {
            hp = new HeroProgress();
            p.HeroProgress[heroId] = hp;
        }
        if (hp.Stars >= StatResolver.MaxStars) return false;

        int cost = StatResolver.StarTokenCost(hp.Stars + 1);
        if (hp.AscendPips < cost) return false;

        hp.AscendPips -= cost;
        hp.Stars++;
        return true;
    }
}
