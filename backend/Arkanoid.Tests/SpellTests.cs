using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Sim;
using Xunit;

public class SpellTests
{
    private static GameInstance Make()
    {
        var catalog = BlockCatalog.FromJson(
          "{\"types\":[{\"id\":\"b\",\"biome\":\"hell\",\"hp\":3,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
          "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":4,\"rows_data\":[\".A.\",\"...\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
          catalog);
        return new GameInstance(level, SimConfig.Default, 1);
    }

    [Fact]
    public void Mana_RegeneratesOverTime()
    {
        var g = Make(); g.Serve();
        var before = g.ManaValue;
        for (int i = 0; i < 60; i++) g.Tick(SimConfig.Default.FixedDt); // 1 second
        Assert.True(g.ManaValue > before);
    }

    [Fact]
    public void Fireball_RequiresMana_AndConsumesIt()
    {
        // §3: Fireball is now Conflagration — detonates burning blocks (no projectile), spending
        // mana only when there's fire to detonate.
        var g = Make(); g.Serve();
        g.Blocks.First(b => !b.Dead).BurnRemaining = 5.0; // ignite first
        g.ManaValue = 25; // conflagration mana cost
        g.CastFireball();
        Assert.True(g.ManaValue < 25 + 1e-9);
        Assert.Empty(g.Projectiles);
    }

    [Fact]
    public void Fireball_TooLittleMana_DoesNothing()
    {
        // Even with fire on the board, no mana ⇒ Conflagration must NOT detonate (mana gate, not fizzle).
        var g = Make(); g.Serve();
        var blk = g.Blocks.First(b => !b.Dead); blk.BurnRemaining = 5.0;
        int hp0 = blk.Hp;
        g.ManaValue = 0;
        g.CastFireball();
        Assert.Empty(g.Projectiles);
        Assert.Equal(hp0, blk.Hp); // mana gate held: the burning block was NOT detonated
    }

    [Fact]
    public void Ignite_ArmsOnCast_AndImbuesNextDeflect()
    {
        var g = Make(); g.Serve();
        g.CastIgnite();                 // arm
        // drive a paddle hit: place ball just above paddle moving down at center
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height/2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, 200);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Balls[0].IgniteHitsLeft > 0);
    }

    [Fact]
    public void Ignite_ArmedButBallDrains_DoesNotLeakToNextLife()
    {
        var g = Make(); g.Serve();
        g.CastIgnite();                                  // arm, but never deflect
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(50, g.Level.Grid.Height + 999);
        g.Tick(SimConfig.Default.FixedDt);               // ball drains -> re-serve; arm must clear
        Assert.Equal(GamePhase.Serving, g.Phase);
        g.Serve();                                       // next life
        var p = g.Paddle;
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(p.Center.X, p.Center.Y - p.Height / 2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, 200);  // drive into the paddle
        g.Tick(SimConfig.Default.FixedDt);               // deflect
        Assert.Equal(0, g.Balls[0].IgniteHitsLeft);      // must NOT be imbued (no leak)
    }

    [Fact]
    public void Cheat_ParkBallAbovePaddle_WithIgniteArmed_ImbuesOnNextTick()
    {
        var g = Make(); g.Serve();
        g.CastIgnite();
        g.ApplyCheat("parkBallAbovePaddle", 0);
        g.Tick(SimConfig.Default.FixedDt); // deflect off paddle -> imbue
        Assert.True(g.Balls[0].IgniteHitsLeft > 0);
    }

    [Fact]
    public void IgnitedBall_LightsBlock_NoDirectDamage()
    {
        // Ignite redesign (2026-06-16): an ignite hit LIGHTS the block (slow burn) — it does NOT deal
        // direct/bonus damage. The block keeps its HP and starts burning instead.
        var g = Make(); g.Serve();
        var blk = g.Blocks[0];                 // hp 3
        g.Balls[0].IgniteHitsLeft = 2;               // pre-imbued
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(c.X, c.Y + SimConfig.Default.CellSize/2 + 6);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(3, blk.Hp);                     // no direct damage from the ignite hit
        Assert.True(blk.BurnRemaining > 0, "ignite should set the block burning");
    }

    [Fact]
    public void FireWall_Cast_ArmsBall_IgnitesAreaOnNextBlockHit()
    {
        // Fire Wall reverted 2026-06-16 to the LEGACY behaviour: cast arms the ball; its NEXT block hit
        // ignites an AREA of blocks (they burn over time via BurnSystem) — no rising placement wall.
        var g = Make(); g.Serve();
        g.ManaValue = 100;
        g.CastFireWall();
        Assert.True(g.Balls[0].FireWallArmed, "fire wall arms the ball");
        var blk = g.Blocks[0];
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.False(g.Balls[0].FireWallArmed, "the arm is consumed on the next block hit");
        Assert.True(blk.BurnRemaining > 0 || blk.Dead, "the hit block is set on fire");
    }

    [Fact]
    public void Turret_Cast_ActivatesAndFiresOnPaddleDeflect()
    {
        // Design: the turret is paddle-mounted and fires a bolt on each ball-catch,
        // NOT on a timer. See docs/specs/2026-06-12-fire-mage-spells.md.
        var g = Make(); g.Serve();
        g.ManaValue = 25; // turret mana cost
        g.CastTurret();
        Assert.True(g.TurretActive);

        // Park the ball away from the paddle: ticking alone fires nothing.
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(g.Paddle.Center.X, g.Level.Grid.Height * 0.5);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, 0);
        for (int i = 0; i < 60; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.Empty(g.Projectiles.FindAll(p => p.Kind == "turret"));

        // Deflect the ball off the paddle → exactly the catch that fires a bolt.
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(g.Paddle.Center.X,
            g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.NotEmpty(g.Projectiles.FindAll(p => p.Kind == "turret"));
    }

    [Fact]
    public void FireWall_TooLittleMana_DoesNothing()
    {
        var g = Make(); g.Serve(); g.ManaValue = 0;
        g.CastFireWall();
        Assert.False(g.Balls[0].FireWallArmed);
    }

    [Fact]
    public void PerfectDeflect_RaisesEvent_OnCentreBandHit()
    {
        // A deflect at the paddle centre (|t| < PerfectDeflectBand) is the skill reward.
        var g = Make(); g.Serve();
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(g.Paddle.Center.X,
            g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Contains(g.DrainEvents(), e => e.Kind == Arkanoid.Core.Sim.SimEventKind.PerfectDeflect);
    }

    [Fact]
    public void EdgeDeflect_DoesNotRaisePerfectDeflect()
    {
        // A deflect near the paddle edge is NOT perfect — no reward event.
        var g = Make(); g.Serve();
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(
            g.Paddle.Center.X + g.Paddle.Width / 2 - 2,
            g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.DoesNotContain(g.DrainEvents(), e => e.Kind == Arkanoid.Core.Sim.SimEventKind.PerfectDeflect);
    }
}
