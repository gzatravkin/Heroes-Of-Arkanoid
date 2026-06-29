using Arkanoid.Core.Meta;
using Xunit;

/// <summary>
/// Dungeon shop floors (docs/04 §6.2 "the pool deliberately mixes: ... a shop floor", §7 mix shop as a
/// category, §5 "spent at shops on spells, relics, heals"). A "shop" floor-clear pick opens a shop that
/// sells run-boons for Gold. Buying deducts Gold and adds the boon to the run; picking "shop" is the
/// MECHANISM (advances the floor) — the boons come from buying, not from the pick itself.
/// </summary>
public class DungeonShopTests
{
    private static DungeonDef MakeDef(int floors = 3) => new()
    {
        Id = "test-dungeon", Name = "Test Dungeon",
        Floors = Enumerable.Range(1, floors).Select(i => $"floor-{i}").ToList(),
        RewardRelic = "pyroclasm", RewardCrystals = 50,
    };

    // ── "shop" as a floor-clear pick category (docs/04 §7) ──────────────────

    [Fact]
    public void GenerateChoices_CanOffer_Shop()
    {
        bool sawShop = false;
        for (int seed = 0; seed < 500 && !sawShop; seed++)
        {
            var run = DungeonService.StartRun(MakeDef(), seed);
            DungeonService.OnFloorCleared(run);
            if (run.PendingChoices.Contains("shop")) sawShop = true;
        }
        Assert.True(sawShop, "A shop floor should appear among floor-clear picks at least sometimes");
    }

    [Fact]
    public void PickChoice_Shop_AdvancesFloor_AcquiresNoBoon()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 1);
        run.PendingChoices = new System.Collections.Generic.List<string> { "shop" };

        DungeonService.PickChoice(run, "shop");

        Assert.Equal(1, run.FloorIndex);          // advanced — shop is the mechanism, not the boon
        Assert.Empty(run.PendingChoices);
        Assert.Empty(run.Relics);                 // boons come from buying, not the pick
        Assert.Empty(run.BallCores);
        Assert.Empty(run.PaddleMods);
        Assert.Empty(run.DraftedSpells);
    }

    // ── shop inventory generation ────────────────────────────────────────────

    [Fact]
    public void GenerateShopItems_Returns3DistinctPricedItems()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 42);
        var items = DungeonService.GenerateShopItems(run, 3);

        Assert.Equal(3, items.Count);
        Assert.Equal(3, items.Select(i => i.Id).Distinct().Count()); // no dupes
        Assert.All(items, i => Assert.True(i.Price > 0, "every shop item must have a price"));
        Assert.All(items, i => Assert.False(string.IsNullOrEmpty(i.Kind), "every item must have a kind"));
    }

    [Fact]
    public void GenerateShopItems_Deterministic_SameRun()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 99);
        run.FloorIndex = 1;
        var a = DungeonService.GenerateShopItems(run, 3);
        var b = DungeonService.GenerateShopItems(run, 3);
        Assert.Equal(a.Select(i => i.Id), b.Select(i => i.Id)); // server can re-derive for buy validation
    }

    // ── TryBuy — spend Gold, gain the boon (docs/04 §5) ─────────────────────

    [Fact]
    public void TryBuy_Relic_DeductsGold_AddsRelic()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 1);
        run.Gold = 20;
        var item = new ShopItem { Id = "pyroclasm", Kind = "relic", Price = 15 };

        Assert.True(DungeonService.TryBuy(run, item));
        Assert.Equal(5, run.Gold);
        Assert.Contains("pyroclasm", run.Relics);
    }

    [Fact]
    public void TryBuy_FailsAndUnchanged_WhenInsufficientGold()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 1);
        run.Gold = 5;
        var item = new ShopItem { Id = "pyroclasm", Kind = "relic", Price = 15 };

        Assert.False(DungeonService.TryBuy(run, item));
        Assert.Equal(5, run.Gold);   // no partial spend
        Assert.Empty(run.Relics);
    }

    [Fact]
    public void TryBuy_Heal_RaisesRunHp()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 1);
        run.Hp = 3; run.Gold = 10;
        var item = new ShopItem { Id = "heal", Kind = "heal", Price = 6 };

        Assert.True(DungeonService.TryBuy(run, item));
        Assert.Equal(3 + DungeonService.HealAmount, run.Hp);
        Assert.Equal(4, run.Gold);
    }

    [Fact]
    public void TryBuy_Spell_AddsToDraftedSpells()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 1);
        run.Gold = 20;
        var item = new ShopItem { Id = "turret", Kind = "spell", Price = 18 };

        Assert.True(DungeonService.TryBuy(run, item));
        Assert.Contains("turret", run.DraftedSpells);
        Assert.Equal(2, run.Gold);
    }

    [Fact]
    public void TryBuy_Duplicate_NoSecondCopy()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 1);
        run.Gold = 30;
        run.Relics.Add("pyroclasm");
        var item = new ShopItem { Id = "pyroclasm", Kind = "relic", Price = 15 };

        DungeonService.TryBuy(run, item); // buys but doesn't double-add
        Assert.Single(run.Relics);
    }

    [Fact]
    public void MultiBuy_RemainingDisplayedItems_StayBuyable()
    {
        // Buying one item must not reshuffle the shop — the other displayed items must remain on offer
        // when the server re-derives the inventory to validate a second buy.
        var run = DungeonService.StartRun(MakeDef(), seed: 7);
        run.Gold = 200;
        var shown = DungeonService.GenerateShopItems(run, 3);
        Assert.True(shown.Count >= 2);

        Assert.True(DungeonService.TryBuy(run, shown[0]));

        var rederived = DungeonService.GenerateShopItems(run, 3);
        for (int i = 1; i < shown.Count; i++)
            Assert.Contains(rederived, r => r.Id == shown[i].Id && r.Kind == shown[i].Kind);
    }

    // ── cross-floor Gold persistence (docs/04 §5) ───────────────────────────

    [Fact]
    public void StartRun_Gold_DefaultsToZero()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 1);
        Assert.Equal(0, run.Gold);
    }

    // ── campaign Gold seam (Gap H) ──────────────────────────────────────────

    [Fact]
    public void Profile_CampaignGold_DefaultsToZero()
    {
        var p = new Profile();
        Assert.Equal(0, p.CampaignGold);
    }
}
