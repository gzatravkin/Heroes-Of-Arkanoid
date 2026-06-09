using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Net;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Unit tests for the falling bonus pickup system.
/// </summary>
public class BonusTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static BonusCatalog MakeCatalog() => BonusCatalog.FromJson("""
    { "bonuses": [
      { "id": "extra_ball",  "name": "Extra Ball",  "icon": "ui/bonus/BonusSplit",      "effect": "extra_ball"  },
      { "id": "mana_surge",  "name": "Mana Surge",  "icon": "ui/bonus/BonusMana",       "effect": "mana_surge"  },
      { "id": "wide_paddle", "name": "Wide Paddle", "icon": "ui/bonus/BonusLargerBita", "effect": "wide_paddle" },
      { "id": "slow_ball",   "name": "Slow Ball",   "icon": "ui/bonus/BonusProtection", "effect": "slow_ball"   },
      { "id": "heal",        "name": "Heal",        "icon": "ui/bonus/BonusHP",         "effect": "heal"        },
      { "id": "coins",       "name": "Treasure",    "icon": "ui/bonus/BonusGem",        "effect": "coins"       }
    ]}
    """);

    private static GameInstance MakeGame(SimConfig? cfg = null)
    {
        cfg ??= new SimConfig { BonusDropChance = 1.0 };
        var catalog = BlockCatalog.FromJson("""
          {"types":[{"id":"b","biome":"hell","hp":1,"sprite":"HellStandart","needToKill":true}]}
        """);
        var level = LevelLoader.FromJson("""
          {"id":"t","biome":"hell","cols":6,"rows":3,"rows_data":["BBBBBB","......","......"],"legend":{"B":"b"}}
        """, catalog, cfg);
        return new GameInstance(level, cfg, seed: 42, bonuses: MakeCatalog());
    }

    // -------------------------------------------------------------------------
    // 1. Destroyed block spawns a Bonus when drop chance = 1
    //    Drive the ball through a 1-HP block to trigger the real code path.
    // -------------------------------------------------------------------------

    [Fact]
    public void DestroyedBlock_SpawnsBonus_WhenDropChance1()
    {
        var cfg = new SimConfig { BonusDropChance = 1.0 };
        var g   = MakeGame(cfg);
        g.Serve();

        Assert.Empty(g.Bonuses);

        // Place the ball directly on a block so it hits it on the first tick.
        var blk = g.Blocks.First(b => !b.Dead);
        var blkCenter = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Vec2(blkCenter.X, blkCenter.Y + g.Config.BallRadius + 1);
        g.Balls[0].Vel = new Vec2(0, -g.Config.BallSpeed); // moving into the block

        g.Tick(cfg.FixedDt);

        // Block should be dead and a bonus should have been spawned.
        Assert.True(blk.Dead, "Block should be dead after direct ball hit");
        Assert.True(g.Bonuses.Count > 0,
            $"Expected a bonus to be spawned after block death (BonusDropChance=1); count={g.Bonuses.Count}");
    }

    // -------------------------------------------------------------------------
    // 2. Bonus falls past drain line → removed without effect
    // -------------------------------------------------------------------------

    [Fact]
    public void Bonus_PastDrainLine_RemovedWithoutEffect()
    {
        var g = MakeGame();
        g.Serve();

        var drainY = g.Level.Grid.Height + g.Config.CellSize * 2 + 50;
        g.Bonuses.Add(new Arkanoid.Core.Entities.Bonus
        {
            Id    = 999,
            Pos   = new Vec2(g.Paddle.Center.X, drainY),
            Vel   = new Vec2(0, g.Config.BonusFallSpeed),
            Type  = "heal",
            Icon  = "ui/bonus/BonusHP",
            Alive = true,
        });

        int livesBefore = g.Lives;
        g.Tick(g.Config.FixedDt);

        Assert.Empty(g.Bonuses);           // removed
        Assert.Equal(livesBefore, g.Lives); // no heal applied
    }

    // -------------------------------------------------------------------------
    // 3. extra_ball bonus: ball count increases on catch
    // -------------------------------------------------------------------------

    [Fact]
    public void ExtraBall_Caught_IncreasesLiveBallCount()
    {
        // BonusFallSpeed=0 so the bonus stays exactly at paddle center for catch detection.
        var cfg = new SimConfig { BonusDropChance = 1.0, BonusFallSpeed = 0 };
        var g   = MakeGame(cfg);
        g.Serve();

        int ballsBefore = g.Balls.Count(b => b.Alive);

        // Place bonus right at paddle center so the AABB overlaps on the next tick.
        g.Bonuses.Add(new Arkanoid.Core.Entities.Bonus
        {
            Id    = 1,
            Pos   = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y),
            Vel   = new Vec2(0, 0),
            Type  = "extra_ball",
            Icon  = "ui/bonus/BonusSplit",
            Alive = true,
        });

        g.Tick(cfg.FixedDt);

        int ballsAfter = g.Balls.Count(b => b.Alive);
        Assert.Empty(g.Bonuses);
        Assert.True(ballsAfter > ballsBefore,
            $"Expected more live balls after extra_ball catch; before={ballsBefore} after={ballsAfter}");
    }

    // -------------------------------------------------------------------------
    // 4. heal bonus: lives increase on catch
    // -------------------------------------------------------------------------

    [Fact]
    public void Heal_Caught_IncreasesLives()
    {
        var cfg = new SimConfig { BonusDropChance = 1.0, BonusFallSpeed = 0 };
        var g   = MakeGame(cfg);
        g.Serve();

        int livesBefore = g.Lives;

        // Place bonus at paddle center so AABB overlaps on next tick.
        g.Bonuses.Add(new Arkanoid.Core.Entities.Bonus
        {
            Id    = 2,
            Pos   = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y),
            Vel   = new Vec2(0, 0),
            Type  = "heal",
            Icon  = "ui/bonus/BonusHP",
            Alive = true,
        });

        g.Tick(cfg.FixedDt);

        Assert.Empty(g.Bonuses);
        Assert.Equal(livesBefore + 1, g.Lives);
    }

    // -------------------------------------------------------------------------
    // 5. mana_surge bonus: mana increases on catch
    // -------------------------------------------------------------------------

    [Fact]
    public void ManaSurge_Caught_IncreasesMana()
    {
        var cfg = new SimConfig { BonusDropChance = 1.0, BonusFallSpeed = 0, ManaSurgeAmount = 30 };
        var g   = MakeGame(cfg);
        g.Serve();

        double manaBefore = g.ManaValue;

        g.Bonuses.Add(new Arkanoid.Core.Entities.Bonus
        {
            Id    = 3,
            Pos   = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y),
            Vel   = new Vec2(0, 0),
            Type  = "mana_surge",
            Icon  = "ui/bonus/BonusMana",
            Alive = true,
        });

        g.Tick(cfg.FixedDt);

        Assert.Empty(g.Bonuses);
        Assert.True(g.ManaValue > manaBefore,
            $"Expected mana to increase after mana_surge; before={manaBefore} after={g.ManaValue}");
    }

    // -------------------------------------------------------------------------
    // 6. wide_paddle bonus: paddle widens on catch, restores after duration
    // -------------------------------------------------------------------------

    [Fact]
    public void WidePaddle_CaughtAndExpires_RestoresPaddleWidth()
    {
        var cfg = new SimConfig
        {
            BonusDropChance     = 1.0,
            BonusFallSpeed      = 0,
            WidePaddleBonus     = 48,
            BonusEffectDuration = 0.05, // expire in ~3 ticks at 60 Hz
        };
        var g = MakeGame(cfg);
        g.Serve();

        double widthBefore = g.Paddle.Width;

        g.Bonuses.Add(new Arkanoid.Core.Entities.Bonus
        {
            Id    = 4,
            Pos   = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y),
            Vel   = new Vec2(0, 0),
            Type  = "wide_paddle",
            Icon  = "ui/bonus/BonusLargerBita",
            Alive = true,
        });

        // Catch tick — paddle widens.
        g.Tick(cfg.FixedDt);
        Assert.Empty(g.Bonuses);
        Assert.True(g.Paddle.Width > widthBefore,
            $"Paddle should be wider after wide_paddle catch; before={widthBefore} after={g.Paddle.Width}");

        // Run enough ticks to expire the effect.
        for (int i = 0; i < 6; i++) g.Tick(cfg.FixedDt);

        Assert.Equal(widthBefore, g.Paddle.Width, precision: 1);
    }

    // -------------------------------------------------------------------------
    // 7. Snapshot includes bonus array and temp-effect flags
    // -------------------------------------------------------------------------

    [Fact]
    public void Snapshot_IncludesBonusesAndTempFlags()
    {
        var g = MakeGame();

        g.Bonuses.Add(new Arkanoid.Core.Entities.Bonus
        {
            Id    = 5,
            Pos   = new Vec2(100, 50),
            Vel   = new Vec2(0, 130),
            Type  = "coins",
            Icon  = "ui/bonus/BonusGem",
            Alive = true,
        });

        var snap = Snapshot.From(g, tick: 1);
        Assert.Single(snap.Bonuses);
        Assert.Equal("coins",             snap.Bonuses[0].Type);
        Assert.Equal("ui/bonus/BonusGem", snap.Bonuses[0].Icon);
        Assert.Equal(100,                 snap.Bonuses[0].X);

        // Temp-effect flags default to false.
        Assert.False(snap.WidePaddleActive);
        Assert.False(snap.SlowBallActive);
    }

    // -------------------------------------------------------------------------
    // 8. spawnBonus cheat works with catalog index
    // -------------------------------------------------------------------------

    [Fact]
    public void SpawnBonusCheat_AddsBonusToList()
    {
        var g = MakeGame();
        Assert.Empty(g.Bonuses);

        g.ApplyCheat("spawnBonus", 0); // index 0 = extra_ball

        Assert.Single(g.Bonuses);
        Assert.Equal("extra_ball", g.Bonuses[0].Type);
        Assert.Equal("ui/bonus/BonusSplit", g.Bonuses[0].Icon);
    }
}
