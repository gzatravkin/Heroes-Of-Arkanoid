using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Necromancer kit: decay / skeleton / drain (partial of ClassKitTests).</summary>
public partial class ClassKitTests
{

    [Fact]
    public void Necromancer_Decay_ImbuesBallOnDeflect()
    {
        var g = Make("necromancer");
        MaxManaAndServe(g);
        g.CastSlot(0); // arm decay
        // Drive a paddle hit
        var ball = g.Balls[0];
        ball.Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2 - ball.Radius - 1);
        ball.Vel = new Vec2(0, 200);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Balls[0].DecayHitsLeft > 0, "Ball should be imbued with decay after deflect");
    }

    [Fact]
    public void Necromancer_Decay_SpreadsDamageOnKill()
    {
        // Two adjacent blocks, kill the first with a decay-imbued ball → neighbor should take chip
        var cfg = new SimConfig { DecaySpreadRange = 2, DecaySpreadChip = 2 };
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"test\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":3," +
            "\"rows_data\":[\"AA.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        var g = new GameInstance(level, cfg, seed: 1);
        g.SetCharacter("necromancer");
        g.Serve();

        var origin   = g.Blocks[0];
        var neighbor = g.Blocks[1];
        int hpBefore = neighbor.Hp; // = 1

        // Imbue the ball with decay and aim at origin
        g.Balls[0].DecayHitsLeft = 5;
        var c = level.Grid.CellCenter(origin.Col, origin.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + cfg.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -cfg.BallSpeed);
        g.Tick(cfg.FixedDt);

        Assert.True(origin.Dead, "Origin block should be dead");
        Assert.True(neighbor.Hp < hpBefore || neighbor.Dead,
            $"Decay spread should chip the neighbor; hp {neighbor.Hp} vs before {hpBefore}");
    }

    [Fact]
    public void Necromancer_Decay_DoesNotLeakToNextLife()
    {
        // Use enough spare balls that we don't run out
        var g = Make("necromancer");
        MaxManaAndServe(g);
        g.CastSlot(0); // arm decay but do NOT deflect — drain instead
        g.Balls[0].Pos = new Vec2(50, g.Level.Grid.Height + 999);
        g.Tick(SimConfig.Default.FixedDt); // drain ball → re-serve
        Assert.Equal(GamePhase.Serving, g.Phase);
        g.Serve(); // next life
        var p = g.Paddle;
        g.Balls[0].Pos = new Vec2(p.Center.X, p.Center.Y - p.Height / 2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Vec2(0, 200);
        g.Tick(SimConfig.Default.FixedDt); // deflect — must NOT apply decay
        Assert.Equal(0, g.Balls[0].DecayHitsLeft);
    }


    [Fact]
    public void Necromancer_Bonewalker_SpawnsRooftopMinion_NotBullets()
    {
        // §3: "Skeleton" is now Bonewalker — a rooftop-walking minion, NOT a paddle turret. Casting it
        // must spawn a minion entity and fire ZERO bone bolts over its lifetime.
        var g = Make("necromancer");
        MaxManaAndServe(g);
        g.CastSlot(1); // Bonewalker
        Assert.True(g.SkeletonActive);
        var m = Assert.Single(g.Minions);
        Assert.Equal("bonewalker", m.Kind);

        int bullets = 0;
        int maxTicks = (int)(SimConfig.Default.TickHz * 1.0) + 5;
        for (int i = 0; i < maxTicks; i++)
        {
            g.Tick(SimConfig.Default.FixedDt);
            bullets += g.Projectiles.Count(p => p.Kind == "skeleton_bullet");
        }
        Assert.Equal(0, bullets); // it walks + melees; it never sprays bullets like the old turret
    }

    [Fact]
    public void Necromancer_Bonewalker_NoMana_NoActivation()
    {
        var g = Make("necromancer");
        g.Serve();
        g.ManaValue = 0;
        g.CastSlot(1);
        Assert.False(g.SkeletonActive);
    }


    [Fact]
    public void Necromancer_Drain_ActivatesAndBoostsManaOnKill()
    {
        var cfg = SimConfig.Default;
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"test\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":4," +
            "\"rows_data\":[\".A.\",\"...\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        var g = new GameInstance(level, cfg, seed: 1);
        g.SetCharacter("necromancer");
        g.ManaValue = g.ManaMaxValue;
        g.Serve();

        // Activate drain, then reset mana to 0 to measure gain precisely
        g.CastSlot(2); // drain
        Assert.True(g.SpellDrainActive);
        g.ManaValue = 0;

        // Kill the block
        var blk = g.Blocks[0];
        var c   = level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + cfg.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -cfg.BallSpeed);
        g.Tick(cfg.FixedDt);

        // Mana gained should include drain bonus on top of normal kill mana (4.0 per kill since cap rework)
        double minExpected = cfg.ManaPerKill * cfg.NecromancerKillManaMult + 4.0 /* drain bonus per kill */ - cfg.ManaRegenPerSec * cfg.FixedDt;
        Assert.True(g.ManaValue >= minExpected - 0.1,
            $"Drain + necromancer mana={g.ManaValue:F2} should be ≥{minExpected:F2}");
    }

    [Fact]
    public void Necromancer_Drain_BonusMana_CappedAt40PerCast()
    {
        // Design (tasks list.md): Drain grants 4 bonus mana per kill, capped at 40 total per cast.
        // Cast once → kill 11 blocks → budget exhausted after 10 kills (10×4=40), 11th gives nothing.
        var cfg = SimConfig.Default;
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"test\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":11,\"rows\":2," +
            "\"rows_data\":[\"AAAAAAAAAAA\",\"...........\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        var g = new GameInstance(level, cfg, seed: 1);
        g.SetCharacter("necromancer");
        g.ManaValue = g.ManaMaxValue;
        g.Serve();

        g.CastSlot(2); // drain
        Assert.True(g.SpellDrainActive);
        Assert.Equal(40.0, g._drainBonusLeft, 1); // budget reset to 40 on cast

        // Kill 10 blocks → 10 × 4 = 40 bonus mana drawn, budget should hit 0
        var ball = g.Balls[0];
        for (int i = 0; i < 10; i++)
        {
            var blk = g.Blocks.FirstOrDefault(b => !b.Dead);
            if (blk == null) break;
            var c = level.Grid.CellCenter(blk.Col, blk.Row);
            ball.Pos = new Vec2(c.X, c.Y + cfg.CellSize / 2 + ball.Radius + 1);
            ball.Vel = new Vec2(0, -cfg.BallSpeed);
            g.Tick(cfg.FixedDt);
        }
        Assert.True(g._drainBonusLeft <= 0,
            $"budget exhausted after 10 kills (4×10=40); left={g._drainBonusLeft:F1}");

        // Kill the 11th — drain is still active by duration but budget is gone, so no extra mana.
        var blk11 = g.Blocks.FirstOrDefault(b => !b.Dead);
        Assert.NotNull(blk11);
        g.ManaValue = 0;
        var c11 = level.Grid.CellCenter(blk11!.Col, blk11.Row);
        ball.Pos = new Vec2(c11.X, c11.Y + cfg.CellSize / 2 + ball.Radius + 1);
        ball.Vel = new Vec2(0, -cfg.BallSpeed);
        g.Tick(cfg.FixedDt);
        // Mana should be only base kill mana (no drain bonus left)
        double baseKillMana = cfg.ManaPerKill * cfg.NecromancerKillManaMult + cfg.ManaRegenPerSec * cfg.FixedDt;
        Assert.True(g.ManaValue <= baseKillMana + 0.5,
            $"11th kill should give no drain bonus; mana={g.ManaValue:F2}, base={baseKillMana:F2}");
    }

    [Fact]
    public void Necromancer_Drain_NoMana_NoActivation()
    {
        var g = Make("necromancer");
        g.Serve();
        g.ManaValue = 0;
        g.CastSlot(2);
        Assert.False(g.SpellDrainActive);
    }

    [Fact]
    public void Necromancer_Drain_ExpiresAfterDuration()
    {
        var g = Make("necromancer");
        MaxManaAndServe(g);
        g.CastSlot(2);
        Assert.True(g.SpellDrainActive);
        var ball = g.Balls[0];
        double elapsed = 0;
        while (elapsed < 6.0 /* drain duration */ + 0.5)
        {
            // Park ball so Phase stays Playing
            ball.Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2 - ball.Radius - 2);
            ball.Vel = new Vec2(0, 0);
            g.Tick(SimConfig.Default.FixedDt);
            elapsed += SimConfig.Default.FixedDt;
        }
        Assert.False(g.SpellDrainActive, "Drain should have expired");
    }
}
