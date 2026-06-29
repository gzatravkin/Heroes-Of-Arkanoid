using System.Linq;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Economy rework (docs/2026-06-14 §2): fixed-price PURE-RANDOM pulls. Asserts the design — deterministic
/// for a seed, duplicates level the item / ascend the hero, a pull onto a maxed entry is Wasted, signatures
/// are never in the spell pool, and the maxed-pool guard disables a fully-maxed pool.
/// </summary>
public class RollServiceTests
{
    private static CardCatalog Cards(params string[] ids) =>
        CardCatalog.FromJson("{ \"cards\": [" +
            string.Join(",", ids.Select(i => $"{{\"id\":\"{i}\",\"name\":\"{i}\",\"effect\":\"ball_damage\",\"magnitude\":1}}")) +
            "] }");

    private static ModuleCatalog Modules(params string[] ids) =>
        ModuleCatalog.FromJson("{ \"modules\": [" +
            string.Join(",", ids.Select(i => $"{{\"id\":\"{i}\",\"name\":\"{i}\",\"slot\":\"ball\",\"effect\":\"module\"}}")) +
            "] }");

    [Fact]
    public void RollCard_IsDeterministicForSeed()
    {
        var cat = Cards("a", "b", "c", "d", "e");
        var r1 = RollService.RollCard(new Profile(), cat, new Rng(7));
        var r2 = RollService.RollCard(new Profile(), cat, new Rng(7));
        Assert.Equal(r1.Id, r2.Id); // same seed ⇒ same pull
    }

    [Fact]
    public void RollCard_FirstCopyUnlocks_DuplicateBanksCopy()
    {
        var cat = Cards("solo");           // single-card pool ⇒ deterministic id
        var p = new Profile();
        var r1 = RollService.RollCard(p, cat, new Rng(1));
        Assert.True(r1.WasNew); Assert.Equal(1, r1.Level); Assert.False(r1.Wasted);
        var r2 = RollService.RollCard(p, cat, new Rng(2));
        // Owner direction 2026-06-15: a dupe BANKS a copy (no auto-level). Level stays 1; copies = 1.
        Assert.False(r2.WasNew); Assert.Equal(1, r2.Level); Assert.Equal(1, r2.Copies); Assert.False(r2.Wasted);
        Assert.Equal(1, p.OwnedCards["solo"].Copies);
    }

    [Fact]
    public void RollCard_Maxed_ReturnsWasted_NoChange()
    {
        var cat = Cards("solo");
        var p = new Profile { OwnedCards = new() { ["solo"] = new CardOwn { Level = CardService.MaxCardLevel } } };
        var r = RollService.RollCard(p, cat, new Rng(5));
        Assert.True(r.Wasted);
        Assert.Equal(CardService.MaxCardLevel, p.OwnedCards["solo"].Level); // untouched (pure-random, no protection)
        Assert.False(RollService.CanRollCard(p, cat)); // terminal: nothing left to gain
    }

    [Fact]
    public void RollModule_FirstCopyUnlocks_DuplicateBanksCopy()
    {
        var cat = Modules("solo");
        var p = new Profile();
        Assert.True(RollService.RollModule(p, cat, new Rng(1)).WasNew);
        Assert.Equal(1, p.OwnedModules["solo"]);
        var r2 = RollService.RollModule(p, cat, new Rng(2));
        Assert.Equal(1, r2.Level);                          // dupe banks a copy; level unchanged
        Assert.Equal(1, r2.Copies);
        Assert.Equal(1, p.OwnedModules["solo"]);
        Assert.Equal(1, p.ModuleCopies["solo"]);
    }

    [Fact]
    public void RollSpell_DrawsFromGlobalPool_DupeBanksCopy()
    {
        var cat = CharacterCatalog.Default;
        var p = new Profile();
        var r1 = RollService.RollSpell(p, cat, new Rng(3));
        Assert.True(r1.WasNew); Assert.Equal(1, r1.Level);
        var r2 = RollService.RollSpell(p, cat, new Rng(3)); // same seed ⇒ same id ⇒ duplicate
        Assert.Equal(r1.Id, r2.Id);
        Assert.False(r2.WasNew); Assert.Equal(1, r2.Level); Assert.Equal(1, r2.Copies);
        Assert.Equal(1, p.SpellCopies[r2.Id]);
    }

    [Fact]
    public void SpellPool_ExcludesSignatures()
    {
        var pool = CharacterCatalog.Default.Pool();
        // signatures (ignite, shield, overload, raise) are hero-locked and never rollable.
        foreach (var sig in new[] { "ignite", "shield", "overload", "raise" })
            Assert.DoesNotContain(sig, pool);
    }

    [Fact]
    public void RollHero_DuplicateBanksPips_DoesNotAutoAscend()
    {
        // Owner direction 2026-06-15: duplicate heroes BANK pips; ascension is MANUAL (Upgrades.TryAscendHero).
        var p = new Profile { HeroPool = new() { "fire_mage" }, UnlockedCharacters = new() { "fire_mage" } };
        p.HeroProgress["fire_mage"] = new HeroProgress { Stars = 0 };
        int cost = StatResolver.StarTokenCost(1); // pips needed for ★1
        for (int i = 0; i < cost; i++) RollService.RollHero(p, new Rng(i + 1));
        Assert.Equal(0, p.HeroProgress["fire_mage"].Stars);          // did NOT auto-ascend
        Assert.Equal(cost, p.HeroProgress["fire_mage"].AscendPips);  // pips banked instead
        Assert.True(Upgrades.TryAscendHero(p, "fire_mage"));         // manual ascend now works
        Assert.Equal(1, p.HeroProgress["fire_mage"].Stars);
        Assert.Equal(0, p.HeroProgress["fire_mage"].AscendPips);
    }

    [Fact]
    public void RollHero_FirstCardUnlocks_ButDoesNotAscend()
    {
        var p = new Profile { HeroPool = new() { "necromancer" } }; // not yet unlocked
        var r = RollService.RollHero(p, new Rng(1));
        Assert.True(r.WasNew);
        Assert.Contains("necromancer", p.UnlockedCharacters);
        Assert.Equal(0, p.HeroProgress["necromancer"].Stars);
    }

    [Fact]
    public void CanRollHero_FalseWhenAllOwnedAndMaxed()
    {
        var p = new Profile { HeroPool = new() { "fire_mage" }, UnlockedCharacters = new() { "fire_mage" } };
        p.HeroProgress["fire_mage"] = new HeroProgress { Stars = StatResolver.MaxStars };
        Assert.False(RollService.CanRollHero(p));
    }
}
