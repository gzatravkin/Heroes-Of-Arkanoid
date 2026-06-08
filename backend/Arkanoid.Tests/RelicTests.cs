using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Net;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Tests for the relic/synergy system. Each test uses a small helper level.</summary>
public class RelicTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Make a GameInstance with one block of the given HP at (col=1, row=0).</summary>
    private static GameInstance MakeWithBlock(int blockHp, SimConfig? cfg = null)
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

    /// <summary>Drive the ball into the block at (col=1, row=0) on the next tick.</summary>
    private static void AimAtBlock(GameInstance g)
    {
        var blk = g.Level.Blocks[0];
        var c   = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        // Place ball just below the block's bottom face, moving straight up.
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(
            c.X,
            c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, -SimConfig.Default.BallSpeed);
    }

    // -------------------------------------------------------------------------
    // 1. RelicCatalog
    // -------------------------------------------------------------------------

    [Fact]
    public void RelicCatalog_FromJson_LoadsAllFourRelics()
    {
        var catalog = RelicCatalog.FromJson("""
          { "relics": [
            { "id": "glass_cannon", "name": "Glass Cannon", "description": "...", "icon": "ItemHummer" },
            { "id": "flint_core",   "name": "Flint Core",   "description": "...", "icon": "ItemDrill"  },
            { "id": "pyroclasm",    "name": "Pyroclasm",    "description": "...", "icon": "ItemTorch"  },
            { "id": "mana_battery", "name": "Mana Battery", "description": "...", "icon": "ItemGem"    }
          ]}
        """);
        Assert.True(catalog.TryGet("glass_cannon", out var gc));
        Assert.Equal("Glass Cannon", gc.Name);
        Assert.Equal("ItemHummer", gc.Icon);
        Assert.True(catalog.TryGet("mana_battery", out _));
    }

    // -------------------------------------------------------------------------
    // 2. glass_cannon
    // -------------------------------------------------------------------------

    [Fact]
    public void GlassCannon_ReducesLivesByOne_AndAddsBallDamage()
    {
        var g = MakeWithBlock(blockHp: 5);
        g.Serve();
        int livesBefore = g.Lives;   // 3 from SimConfig.Default

        g.AddRelic("glass_cannon");
        Assert.Equal(livesBefore - 1, g.Lives);   // immediate -1 life

        // HP-3 block hit: should remove BallDamage + GlassCannonDamageBonus = 1+1 = 2
        var blk = g.Level.Blocks[0];
        int hpBefore = blk.Hp;        // 5
        AimAtBlock(g);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(hpBefore - (SimConfig.Default.BallDamage + SimConfig.Default.GlassCannonDamageBonus), blk.Hp);
    }

    [Fact]
    public void GlassCannon_LivesNeverGoesBelow1()
    {
        var g = MakeWithBlock(blockHp: 1);
        // Force lives to 1 by adding relic twice
        g.AddRelic("glass_cannon");
        g.AddRelic("glass_cannon"); // second add should still clamp at 1
        Assert.True(g.Lives >= 1);
    }

    // -------------------------------------------------------------------------
    // 3. flint_core
    // -------------------------------------------------------------------------

    [Fact]
    public void FlintCore_AddsDamage_OnlyToToughBlocks()
    {
        // Tough block (MaxHp >= FlintToughThreshold = 3)
        var gTough = MakeWithBlock(blockHp: 3);
        gTough.Serve();
        gTough.AddRelic("flint_core");
        var toughBlk = gTough.Level.Blocks[0];
        AimAtBlock(gTough);
        gTough.Tick(SimConfig.Default.FixedDt);
        int expectedTough = 3 - (SimConfig.Default.BallDamage + SimConfig.Default.FlintBonus); // 3 - 2 = 1
        Assert.Equal(expectedTough, toughBlk.Hp);

        // Weak block (MaxHp == 1 < FlintToughThreshold)
        var gWeak = MakeWithBlock(blockHp: 1);
        gWeak.Serve();
        gWeak.AddRelic("flint_core");
        var weakBlk = gWeak.Level.Blocks[0];
        AimAtBlock(gWeak);
        gWeak.Tick(SimConfig.Default.FixedDt);
        // Block dies (hp 1 - BallDamage 1 = 0), NOT 1 - (1+1) = -1.
        // After the tick it should be dead (hp <= 0).
        Assert.True(weakBlk.Dead || weakBlk.Hp <= 0);
        // Specifically BallDamage only was applied (no FlintBonus)
        // so HP was 1 - 1 = 0, i.e. dead but not -1 (which would also be dead; check via Dead flag).
        // We confirm no extra damage by using a 2-hp weak block:
        var gWeak2 = MakeWithBlock(blockHp: 2);
        gWeak2.Serve();
        gWeak2.AddRelic("flint_core");
        var weakBlk2 = gWeak2.Level.Blocks[0]; // MaxHp == 2 < 3 threshold
        AimAtBlock(gWeak2);
        gWeak2.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(2 - SimConfig.Default.BallDamage, weakBlk2.Hp); // 2-1=1, no flint bonus
    }

    // -------------------------------------------------------------------------
    // 4. pyroclasm
    // -------------------------------------------------------------------------

    [Fact]
    public void Pyroclasm_IncreasesFireSpreadChip()
    {
        // Without pyroclasm: chip = 1
        var gNormal = MakeWithBlock(blockHp: 4);
        gNormal.Serve();
        // Give ball ignite, aim at kill block to trigger SpreadFire
        // Level: cols=3, rows=3, block at (1,0). We need a neighbor block.
        // Use a wider level that has two blocks side-by-side.
        var catalogPyro = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"test\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}");
        var levelPyro = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":3," +
            "\"rows_data\":[\"AA.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalogPyro);

        // Without pyroclasm
        var gPlain = new GameInstance(levelPyro, SimConfig.Default, seed: 1);
        gPlain.Serve();
        var originBlock = levelPyro.Blocks[0]; // (col=0,row=0)
        var neighborBlock = levelPyro.Blocks[1]; // (col=1,row=0)
        // Manually kill origin with ignite to trigger SpreadFire
        gPlain.Balls[0].IgniteHitsLeft = 5;
        var oc = gPlain.Level.Grid.CellCenter(originBlock.Col, originBlock.Row);
        gPlain.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(
            oc.X, oc.Y + SimConfig.Default.CellSize / 2 + gPlain.Balls[0].Radius + 1);
        gPlain.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, -SimConfig.Default.BallSpeed);
        int hpNeighborBefore = neighborBlock.Hp;
        gPlain.Tick(SimConfig.Default.FixedDt);
        // neighbor should have been chipped by 1
        int plainChip = hpNeighborBefore - neighborBlock.Hp;
        Assert.Equal(1, plainChip);

        // With pyroclasm — fresh level instance needed (blocks are mutated in place)
        var levelPyro2 = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":3," +
            "\"rows_data\":[\"AA.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalogPyro);
        var gPyro = new GameInstance(levelPyro2, SimConfig.Default, seed: 1);
        gPyro.Serve();
        gPyro.AddRelic("pyroclasm");
        var originPyro   = levelPyro2.Blocks[0];
        var neighborPyro = levelPyro2.Blocks[1];
        gPyro.Balls[0].IgniteHitsLeft = 5;
        var oc2 = gPyro.Level.Grid.CellCenter(originPyro.Col, originPyro.Row);
        gPyro.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(
            oc2.X, oc2.Y + SimConfig.Default.CellSize / 2 + gPyro.Balls[0].Radius + 1);
        gPyro.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, -SimConfig.Default.BallSpeed);
        int hpNB2 = neighborPyro.Hp;
        gPyro.Tick(SimConfig.Default.FixedDt);
        int pyroChip = hpNB2 - neighborPyro.Hp;
        Assert.Equal(SimConfig.Default.PyroclasmChip, pyroChip);
    }

    // -------------------------------------------------------------------------
    // 5. mana_battery
    // -------------------------------------------------------------------------

    [Fact]
    public void ManaBattery_RaisesMaxManaAndRegen()
    {
        var g = MakeWithBlock(blockHp: 1);
        Assert.Equal(SimConfig.Default.ManaMax, g.ManaMaxValue);

        g.AddRelic("mana_battery");
        Assert.Equal(SimConfig.Default.ManaMax + SimConfig.Default.ManaBatteryBonus, g.ManaMaxValue);

        // Verify mana can now exceed the old Config.ManaMax ceiling
        g.Serve();
        g.ManaValue = SimConfig.Default.ManaMax;  // fill to old cap
        // run a few ticks to let regen push above old cap
        for (int i = 0; i < 10; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.ManaValue > SimConfig.Default.ManaMax,
            $"mana {g.ManaValue} should exceed old cap {SimConfig.Default.ManaMax} with mana_battery");
    }

    [Fact]
    public void ManaBattery_RegenFasterThanBaseline()
    {
        // Two identical instances; one has mana_battery
        var gBase    = MakeWithBlock(blockHp: 1);
        var gBattery = MakeWithBlock(blockHp: 1);
        gBase.Serve(); gBattery.Serve();
        gBattery.AddRelic("mana_battery");

        // Start both at 0 mana
        gBase.ManaValue    = 0;
        gBattery.ManaValue = 0;

        const int ticks = 30;
        for (int i = 0; i < ticks; i++)
        {
            gBase.Tick(SimConfig.Default.FixedDt);
            gBattery.Tick(SimConfig.Default.FixedDt);
        }
        Assert.True(gBattery.ManaValue > gBase.ManaValue,
            $"battery {gBattery.ManaValue:F1} should exceed baseline {gBase.ManaValue:F1}");
    }

    // -------------------------------------------------------------------------
    // 6. addRelic cheat
    // -------------------------------------------------------------------------

    [Fact]
    public void AddRelicCheat_ParsesIdAndActivates()
    {
        var g = MakeWithBlock(blockHp: 5);
        Assert.False(g.HasRelic("glass_cannon"));
        g.ApplyCheat("addRelic:glass_cannon", 0);
        Assert.True(g.HasRelic("glass_cannon"));
    }

    [Fact]
    public void AddRelicCheat_MultipleDifferentRelics()
    {
        var g = MakeWithBlock(blockHp: 5);
        g.ApplyCheat("addRelic:flint_core", 0);
        g.ApplyCheat("addRelic:mana_battery", 0);
        Assert.True(g.HasRelic("flint_core"));
        Assert.True(g.HasRelic("mana_battery"));
        Assert.False(g.HasRelic("pyroclasm"));
    }

    // -------------------------------------------------------------------------
    // 7. Snapshot includes active relics
    // -------------------------------------------------------------------------

    [Fact]
    public void Snapshot_IncludesActiveRelics()
    {
        var g = MakeWithBlock(blockHp: 1);
        g.Serve();
        g.AddRelic("glass_cannon");
        var snap = Snapshot.From(g, tick: 1);
        Assert.Single(snap.ActiveRelics);
        Assert.Equal("glass_cannon", snap.ActiveRelics[0].Id);
    }

    [Fact]
    public void Snapshot_ManaMax_ReflectsBatteryBonus()
    {
        var g = MakeWithBlock(blockHp: 1);
        g.Serve();
        g.AddRelic("mana_battery");
        var snap = Snapshot.From(g, tick: 1);
        Assert.Equal(g.ManaMaxValue, snap.ManaMax);
        Assert.True(snap.ManaMax > SimConfig.Default.ManaMax);
    }

    // -------------------------------------------------------------------------
    // 8. No-relic baseline unchanged
    // -------------------------------------------------------------------------

    [Fact]
    public void NoRelics_BehaviorUnchanged()
    {
        var g = MakeWithBlock(blockHp: 3);
        g.Serve();
        // No relics added — expect BallDamage only (1)
        var blk = g.Level.Blocks[0];
        AimAtBlock(g);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(3 - SimConfig.Default.BallDamage, blk.Hp); // 3-1=2
    }
}
