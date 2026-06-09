using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Unit tests for ItemCatalog, ItemShop, and ItemEffects integration in the sim.</summary>
public class ItemTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private const string CatalogJson = """
        { "items": [
          { "id": "drill",    "name": "Drill",     "icon": "ItemDrill",    "maxTier": 3, "cost": [60, 120, 240], "effect": "ball_damage",  "description": "" },
          { "id": "flask",    "name": "Flask",      "icon": "ItemFlask",    "maxTier": 3, "cost": [50, 100, 200], "effect": "max_mana",     "description": "" },
          { "id": "tome",     "name": "Tome",       "icon": "ItemTom",      "maxTier": 3, "cost": [50, 100, 200], "effect": "mana_regen",   "description": "" },
          { "id": "helm",     "name": "Helm",       "icon": "ItemHelm",     "maxTier": 3, "cost": [50, 100, 200], "effect": "start_life",   "description": "" },
          { "id": "ring",     "name": "Ring",       "icon": "ItemRing",     "maxTier": 3, "cost": [40,  80, 160], "effect": "treasure",     "description": "" },
          { "id": "gem",      "name": "Gem",        "icon": "ItemGem",      "maxTier": 3, "cost": [50, 100, 200], "effect": "kill_mana",    "description": "" },
          { "id": "jadeball", "name": "Jade Ball",  "icon": "ItemJadeBall", "maxTier": 3, "cost": [60, 120, 240], "effect": "paddle_width", "description": "" },
          { "id": "hummer",   "name": "Hummer",     "icon": "ItemHummer",   "maxTier": 3, "cost": [60, 120, 240], "effect": "crit_tough",   "description": "" }
        ]}
        """;

    private static ItemCatalog MakeCatalog() => ItemCatalog.FromJson(CatalogJson);

    private static GameInstance MakeGame(int blockHp = 3, SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var catalog = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"test\",\"hp\":{blockHp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":3," +
            "\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        return new GameInstance(level, cfg, seed: 1);
    }

    private static void AimBallAtBlock(GameInstance g)
    {
        var blk = g.Level.Blocks[0];
        var c   = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(
            c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, -SimConfig.Default.BallSpeed);
    }

    // ── 1. ItemCatalog ───────────────────────────────────────────────────────

    [Fact]
    public void ItemCatalog_FromJson_LoadsAllItems()
    {
        var catalog = MakeCatalog();
        Assert.True(catalog.TryGet("drill", out var drill));
        Assert.Equal("Drill", drill.Name);
        Assert.Equal("ball_damage", drill.Effect);
        Assert.Equal(3, drill.MaxTier);
        Assert.Equal(60, drill.CostForTier(1));
        Assert.Equal(120, drill.CostForTier(2));
        Assert.Equal(240, drill.CostForTier(3));
    }

    [Fact]
    public void ItemCatalog_TryGet_ReturnsFalse_ForUnknownId()
    {
        var catalog = MakeCatalog();
        Assert.False(catalog.TryGet("nonexistent", out _));
    }

    // ── 2. ItemShop.TryBuy ───────────────────────────────────────────────────

    [Fact]
    public void TryBuy_Tier1_SpendsCrystalsAndSetsOwned()
    {
        var catalog = MakeCatalog();
        var profile = Profile.NewDefault();
        profile.Crystals = 100;

        var ok = ItemShop.TryBuy(profile, catalog, "drill");

        Assert.True(ok);
        Assert.Equal(1, profile.OwnedItems["drill"]);
        Assert.Equal(100 - 60, profile.Crystals); // cost[0] = 60
    }

    [Fact]
    public void TryBuy_Tier2_RaisesExistingOwned()
    {
        var catalog = MakeCatalog();
        var profile = Profile.NewDefault();
        profile.Crystals = 300;
        profile.OwnedItems["drill"] = 1; // already owns tier 1

        var ok = ItemShop.TryBuy(profile, catalog, "drill");

        Assert.True(ok);
        Assert.Equal(2, profile.OwnedItems["drill"]);
        Assert.Equal(300 - 120, profile.Crystals); // cost[1] = 120
    }

    [Fact]
    public void TryBuy_FailsWhenInsufficientCrystals()
    {
        var catalog = MakeCatalog();
        var profile = Profile.NewDefault();
        profile.Crystals = 10; // drill costs 60

        var ok = ItemShop.TryBuy(profile, catalog, "drill");

        Assert.False(ok);
        Assert.Equal(10, profile.Crystals); // unchanged
        Assert.False(profile.OwnedItems.ContainsKey("drill"));
    }

    [Fact]
    public void TryBuy_FailsAtMaxTier()
    {
        var catalog = MakeCatalog();
        var profile = Profile.NewDefault();
        profile.Crystals = 9999;
        profile.OwnedItems["drill"] = 3; // already maxed

        var ok = ItemShop.TryBuy(profile, catalog, "drill");

        Assert.False(ok);
        Assert.Equal(3, profile.OwnedItems["drill"]); // unchanged
    }

    // ── 3. ItemShop.Equip / Unequip ─────────────────────────────────────────

    [Fact]
    public void Equip_AddsToEquippedList()
    {
        var profile = Profile.NewDefault();
        profile.OwnedItems["drill"] = 1;

        var ok = ItemShop.Equip(profile, "drill");

        Assert.True(ok);
        Assert.Contains("drill", profile.EquippedItems);
    }

    [Fact]
    public void Equip_FailsWhenNotOwned()
    {
        var profile = Profile.NewDefault();

        var ok = ItemShop.Equip(profile, "drill");

        Assert.False(ok);
        Assert.Empty(profile.EquippedItems);
    }

    [Fact]
    public void Equip_CapsAtThreeSlots()
    {
        var profile = Profile.NewDefault();
        profile.OwnedItems["drill"]  = 1;
        profile.OwnedItems["flask"]  = 1;
        profile.OwnedItems["helm"]   = 1;
        profile.OwnedItems["gem"]    = 1;
        ItemShop.Equip(profile, "drill");
        ItemShop.Equip(profile, "flask");
        ItemShop.Equip(profile, "helm");

        var ok4 = ItemShop.Equip(profile, "gem"); // 4th equip should fail

        Assert.False(ok4);
        Assert.Equal(3, profile.EquippedItems.Count);
    }

    [Fact]
    public void Equip_AlreadyEquipped_ReturnsFalse()
    {
        var profile = Profile.NewDefault();
        profile.OwnedItems["drill"] = 1;
        ItemShop.Equip(profile, "drill");

        var ok2 = ItemShop.Equip(profile, "drill");

        Assert.False(ok2);
        Assert.Single(profile.EquippedItems);
    }

    [Fact]
    public void Unequip_RemovesFromList()
    {
        var profile = Profile.NewDefault();
        profile.OwnedItems["drill"] = 1;
        ItemShop.Equip(profile, "drill");

        var ok = ItemShop.Unequip(profile, "drill");

        Assert.True(ok);
        Assert.Empty(profile.EquippedItems);
    }

    [Fact]
    public void Unequip_NotEquipped_ReturnsFalse()
    {
        var profile = Profile.NewDefault();
        var ok = ItemShop.Unequip(profile, "drill");
        Assert.False(ok);
    }

    // ── 4. ItemEffects — ball_damage ────────────────────────────────────────

    [Fact]
    public void BallDamageItem_IncreasesHitDamage()
    {
        var catalog = MakeCatalog();
        var gBase = MakeGame(blockHp: 5);
        var gItem = MakeGame(blockHp: 5);
        gBase.Serve();
        gItem.Serve();

        // Apply drill tier-1 (ball_damage +1 per tier, so +1 total)
        ItemEffects.Apply(new[] { "drill" }, new Dictionary<string, int> { ["drill"] = 1 }, catalog, gItem);
        ItemEffects.Commit(gItem);

        var blkBase = gBase.Level.Blocks[0];
        var blkItem = gItem.Level.Blocks[0];

        AimBallAtBlock(gBase);
        AimBallAtBlock(gItem);

        gBase.Tick(SimConfig.Default.FixedDt);
        gItem.Tick(SimConfig.Default.FixedDt);

        // gBase: 5 - 1 = 4; gItem: 5 - 2 = 3
        Assert.Equal(5 - SimConfig.Default.BallDamage,                                blkBase.Hp);
        Assert.Equal(5 - SimConfig.Default.BallDamage - SimConfig.Default.ItemBallDamageBonusPerTier, blkItem.Hp);
    }

    // ── 5. ItemEffects — max_mana ────────────────────────────────────────────

    [Fact]
    public void MaxManaItem_IncreasesMaxMana()
    {
        var catalog = MakeCatalog();
        var g = MakeGame();
        var baseMana = g.ManaMaxValue;

        ItemEffects.Apply(new[] { "flask" }, new Dictionary<string, int> { ["flask"] = 2 }, catalog, g);
        ItemEffects.Commit(g);

        // flask is tier 2, bonus = 2 * ItemMaxManaBonusPerTier = 2 * 20 = 40
        Assert.Equal(baseMana + 2 * SimConfig.Default.ItemMaxManaBonusPerTier, g.ManaMaxValue);
    }

    // ── 6. ItemEffects — mana_regen ──────────────────────────────────────────

    [Fact]
    public void ManaRegenItem_IncreasesRegenRate()
    {
        var catalog = MakeCatalog();

        var gBase = MakeGame();
        var gItem = MakeGame();
        gBase.Serve();
        gItem.Serve();

        ItemEffects.Apply(new[] { "tome" }, new Dictionary<string, int> { ["tome"] = 1 }, catalog, gItem);
        ItemEffects.Commit(gItem);

        gBase.ManaValue = 0;
        gItem.ManaValue = 0;

        for (int i = 0; i < 30; i++)
        {
            gBase.Tick(SimConfig.Default.FixedDt);
            gItem.Tick(SimConfig.Default.FixedDt);
        }

        Assert.True(gItem.ManaValue > gBase.ManaValue,
            $"item mana {gItem.ManaValue:F1} should exceed base {gBase.ManaValue:F1}");
    }

    // ── 7. ItemEffects — start_life ──────────────────────────────────────────

    [Fact]
    public void StartLifeItem_IncreasesStartingLives()
    {
        var catalog = MakeCatalog();
        var g = MakeGame();
        var baseLives = g.Lives;

        ItemEffects.Apply(new[] { "helm" }, new Dictionary<string, int> { ["helm"] = 1 }, catalog, g);
        ItemEffects.Commit(g);

        Assert.Equal(baseLives + SimConfig.Default.ItemStartLifeBonusPerTier, g.Lives);
    }

    // ── 8. Profile round-trip (OwnedItems / EquippedItems persisted) ─────────

    [Fact]
    public void Profile_OwnedAndEquipped_DefaultEmpty()
    {
        var p = Profile.NewDefault();
        Assert.Empty(p.OwnedItems);
        Assert.Empty(p.EquippedItems);
    }

    [Fact]
    public void Profile_BuyThenEquip_Persists()
    {
        var catalog = MakeCatalog();
        var profile = Profile.NewDefault();
        profile.Crystals = 200;

        ItemShop.TryBuy(profile, catalog, "drill");
        ItemShop.Equip(profile, "drill");

        Assert.Equal(1, profile.OwnedItems["drill"]);
        Assert.Contains("drill", profile.EquippedItems);
    }
}
