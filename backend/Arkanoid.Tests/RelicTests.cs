using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Net;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim;
using Xunit;

public class RelicTests
{
    private static GameInstance MakeWithBlock(int blockHp, SimConfig? cfg = null) => K.OneBlock(blockHp, cfg);
    private static void AimAtBlock(GameInstance g) => K.AimAt(g, g.Blocks[0]);

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


    [Fact]
    public void GlassCannon_ReducesLivesByOne_AndAddsBallDamage()
    {
        var g = MakeWithBlock(blockHp: 5);
        g.Serve();
        int livesBefore = g.Hp;   // 3 from SimConfig.Default

        g.AddRelic("glass_cannon");
        Assert.Equal(livesBefore - 1, g.Hp);   // immediate -1 life

        // HP-3 block hit: should remove BallDamage + GlassCannonDamageBonus = 1+1 = 2
        var blk = g.Blocks[0];
        int hpBefore = blk.Hp;        // 5
        AimAtBlock(g);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(hpBefore - (SimConfig.Default.BallDamage + 1 /* GlassCannonDamageBonus */), blk.Hp);
    }

    [Fact]
    public void GlassCannon_LivesNeverGoesBelow1()
    {
        var g = MakeWithBlock(blockHp: 1);
        // Force lives to 1 by adding relic twice
        g.AddRelic("glass_cannon");
        g.AddRelic("glass_cannon"); // second add should still clamp at 1
        Assert.True(g.Hp >= 1);
    }


    [Fact]
    public void FlintCore_AddsDamage_OnlyToToughBlocks()
    {
        // Tough block (MaxHp >= FlintToughThreshold = 3)
        var gTough = MakeWithBlock(blockHp: 3);
        gTough.Serve();
        gTough.AddRelic("flint_core");
        var toughBlk = gTough.Blocks[0];
        AimAtBlock(gTough);
        gTough.Tick(SimConfig.Default.FixedDt);
        int expectedTough = 3 - (SimConfig.Default.BallDamage + 1 /* FlintBonus */); // 3 - 2 = 1
        Assert.Equal(expectedTough, toughBlk.Hp);

        // Weak block (MaxHp == 1 < FlintToughThreshold)
        var gWeak = MakeWithBlock(blockHp: 1);
        gWeak.Serve();
        gWeak.AddRelic("flint_core");
        var weakBlk = gWeak.Blocks[0];
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
        var weakBlk2 = gWeak2.Blocks[0]; // MaxHp == 2 < 3 threshold
        AimAtBlock(gWeak2);
        gWeak2.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(2 - SimConfig.Default.BallDamage, weakBlk2.Hp); // 2-1=1, no flint bonus
    }


    [Fact]
    public void Pyroclasm_SpreadsFireToDiagonalNeighbours()
    {
        // Design: pyroclasm deepens fire — it spreads to DIAGONAL neighbours, which base
        // ignite (cardinal-only) never reaches. Origin at (0,0); the only other block is the
        // diagonal at (1,1), with no cardinal block between them.
        const string blocks = "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}";
        const string level  = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3," +
                             "\"rows_data\":[\"A..\",\".B.\",\"...\"],\"legend\":{\"A\":\"b\",\"B\":\"b\"}}";

        int DiagDamage(bool pyroclasm)
        {
            var cat = BlockCatalog.FromJson(blocks);
            var lvl = LevelLoader.FromJson(level, cat);
            var g = new GameInstance(lvl, SimConfig.Default, seed: 1);
            g.SetCharacter("fire_mage");
            g.Serve();
            if (pyroclasm) g.AddRelic("pyroclasm");
            var origin = g.Blocks.First(b => b.Col == 0 && b.Row == 0);
            var diag   = g.Blocks.First(b => b.Col == 1 && b.Row == 1);

            // Ignite + kill the origin block.
            g.Balls[0].IgniteHitsLeft = 5;
            var oc = g.Level.Grid.CellCenter(origin.Col, origin.Row);
            g.Balls[0].Pos = new Vec2(oc.X, oc.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
            g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
            g.Tick(SimConfig.Default.FixedDt);

            // Park the ball just above the paddle (clear of blocks) and let the fire burn.
            g.Balls[0].Pos = new Vec2(g.Paddle.Center.X,
                g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 2);
            g.Balls[0].Vel = new Vec2(0, 0);
            // Slow fire creep (2026-06-16): run past the spread interval so the chain can reach the diagonal.
            for (int i = 0; i < (int)(4.0 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
            return (diag.BurnRemaining > 0 || diag.Dead) ? 1 : 0; // did the diagonal catch fire?
        }

        Assert.Equal(0, DiagDamage(pyroclasm: false));     // base ignite never spreads diagonally
        Assert.True(DiagDamage(pyroclasm: true) > 0,       // pyroclasm does
            "pyroclasm should spread fire to diagonal neighbours");
    }


    [Fact]
    public void ManaBattery_RaisesMaxManaAndRegen()
    {
        var g = MakeWithBlock(blockHp: 1);
        Assert.Equal(SimConfig.Default.ManaMax, g.ManaMaxValue);

        g.AddRelic("mana_battery");
        Assert.Equal(SimConfig.Default.ManaMax + 50 /* ManaBatteryBonus */, g.ManaMaxValue);

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


    [Fact]
    public void NoRelics_BehaviorUnchanged()
    {
        var g = MakeWithBlock(blockHp: 3);
        g.Serve();
        // No relics added — expect BallDamage only (1)
        var blk = g.Blocks[0];
        AimAtBlock(g);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(3 - SimConfig.Default.BallDamage, blk.Hp); // 3-1=2
    }
}
