using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Necromancer kit: decay / skeleton / drain (partial of ClassKitTests).</summary>
public partial class ClassKitTests
{
    // -------------------------------------------------------------------------
    // 8. Necromancer — Decay
    // -------------------------------------------------------------------------

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

        var origin   = level.Blocks[0];
        var neighbor = level.Blocks[1];
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

    // -------------------------------------------------------------------------
    // 9. Necromancer — Skeleton
    // -------------------------------------------------------------------------

    [Fact]
    public void Necromancer_Skeleton_ActivatesAndSpawnsBullets()
    {
        var g = Make("necromancer");
        MaxManaAndServe(g);
        g.CastSlot(1); // skeleton
        Assert.True(g.SkeletonActive);
        // Run enough ticks for at least one bullet
        int spawned = 0;
        int maxTicks = (int)(SimConfig.Default.TickHz * SimConfig.Default.SkeletonFireInterval) + 5;
        for (int i = 0; i < maxTicks; i++)
        {
            g.Tick(SimConfig.Default.FixedDt);
            spawned += g.Projectiles.Count(p => p.Kind == "skeleton_bullet");
        }
        Assert.True(spawned > 0, "Skeleton should have fired at least one bullet");
    }

    [Fact]
    public void Necromancer_Skeleton_NoMana_NoActivation()
    {
        var g = Make("necromancer");
        g.Serve();
        g.ManaValue = 0;
        g.CastSlot(1);
        Assert.False(g.SkeletonActive);
    }

    // -------------------------------------------------------------------------
    // 10. Necromancer — Drain
    // -------------------------------------------------------------------------

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
        Assert.True(g.DrainActive);
        g.ManaValue = 0;

        // Kill the block
        var blk = level.Blocks[0];
        var c   = level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + cfg.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -cfg.BallSpeed);
        g.Tick(cfg.FixedDt);

        // Mana gained should include drain bonus on top of normal kill mana
        double minExpected = cfg.ManaPerKill * cfg.NecromancerKillManaMult + cfg.DrainBonusManaPerKill - cfg.ManaRegenPerSec * cfg.FixedDt;
        Assert.True(g.ManaValue >= minExpected - 0.1,
            $"Drain + necromancer mana={g.ManaValue:F2} should be ≥{minExpected:F2}");
    }

    [Fact]
    public void Necromancer_Drain_NoMana_NoActivation()
    {
        var g = Make("necromancer");
        g.Serve();
        g.ManaValue = 0;
        g.CastSlot(2);
        Assert.False(g.DrainActive);
    }

    [Fact]
    public void Necromancer_Drain_ExpiresAfterDuration()
    {
        var g = Make("necromancer");
        MaxManaAndServe(g);
        g.CastSlot(2);
        Assert.True(g.DrainActive);
        var ball = g.Balls[0];
        double elapsed = 0;
        while (elapsed < SimConfig.Default.DrainDuration + 0.5)
        {
            // Park ball so Phase stays Playing
            ball.Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2 - ball.Radius - 2);
            ball.Vel = new Vec2(0, 0);
            g.Tick(SimConfig.Default.FixedDt);
            elapsed += SimConfig.Default.FixedDt;
        }
        Assert.False(g.DrainActive, "Drain should have expired");
    }
}
