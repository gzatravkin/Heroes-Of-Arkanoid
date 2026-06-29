using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Arkanoid.Core.Sim.Systems;
using Xunit;

/// <summary>
/// §2 Modules — slot-bound PASSIVES (ModuleSystem). Each test encodes the module's DESIGN trigger + lever
/// (CLAUDE.md), and the structural invariant that an UNEQUIPPED module never fires. Batch 1: Tidal Core,
/// Hollow Ball, Gyro Paddle, Drumhead Paddle (the core/ball/paddle deflect-and-serve cluster).
/// </summary>
public class ModuleSystemTests
{
    private static GameInstance Make(int cols, int rows, int topRows, int hp)
    {
        var catalog = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"hell\",\"hp\":{hp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        var full  = new string('A', cols);
        var empty = new string('.', cols);
        var rowsData = string.Join(",", Enumerable.Range(0, rows)
            .Select(r => "\"" + (r < topRows ? full : empty) + "\""));
        var level = LevelLoader.FromJson(
            $"{{\"id\":\"t\",\"biome\":\"hell\",\"cols\":{cols},\"rows\":{rows},\"rows_data\":[{rowsData}],\"legend\":{{\"A\":\"b\"}}}}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, 1);
        g.Serve();
        return g;
    }

    private static void Equip(GameInstance g, string id, int level = 1)
        => g.SetModules(new Dictionary<string, int> { [id] = level });

    private static Arkanoid.Core.Entities.Block BlockAt(GameInstance g, int col, int row)
        => g.Blocks.First(b => !b.Dead && b.Col == col && b.Row == row);

    // ── Tidal Core: alternates HEAVY (damage) / SWIFT (speed) each deflect ──────
    [Fact]
    public void TidalCore_AlternatesHeavyAndSwift_EachDeflect()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "tidal_core"); // starts in HEAVY
        var b = g.Balls[0];
        Assert.Equal(2, ModuleSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // heavy → +1+level
        ModuleSystem.OnPaddleHit(g, b, 0.5, false);                            // → SWIFT
        Assert.Equal(0, ModuleSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // swift → no damage bonus
        b.Vel = new Vec2(g.Config.BallSpeed, 0);
        ModuleSystem.OnBallTick(g, b, 0.016);
        Assert.True(b.Vel.Length > g.Config.BallSpeed, "swift mode flies faster");
        ModuleSystem.OnPaddleHit(g, b, 0.5, false);                            // → back to HEAVY
        Assert.Equal(2, ModuleSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0)));
    }

    // ── Hollow Ball: big radius (coverage) + low per-hit damage ─────────────────
    [Fact]
    public void HollowBall_ServesBig_AndHitsSofter()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "hollow_ball");
        var b = g.Balls[0];
        ModuleSystem.OnServe(g, b);
        Assert.True(b.Radius > g.Config.BallRadius, "hollow ball is bigger (wide coverage)");
        Assert.Equal(-1, ModuleSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // softer hits
    }

    [Fact]
    public void Modules_Unequipped_NoEffect()
    {
        var g = Make(3, 2, 1, 30); // nothing equipped
        var b = g.Balls[0];
        Assert.Equal(0, ModuleSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0)));
        double r = b.Radius; ModuleSystem.OnServe(g, b); Assert.Equal(r, b.Radius);
    }

    [Fact]
    public void HollowBall_DriftsErratically()
    {
        // The light hollow ball wobbles its heading over time (its downside) — the unequipped ball does not.
        double HeadingChange(bool equip)
        {
            var g = Make(3, 2, 1, 30);
            if (equip) Equip(g, "hollow_ball");
            var b = g.Balls[0];
            b.Vel = new Vec2(0, -g.Config.BallSpeed);
            double a0 = System.Math.Atan2(b.Vel.Y, b.Vel.X);
            for (int i = 0; i < 30; i++) ModuleSystem.OnBallTick(g, b, 0.016);
            return System.Math.Abs(System.Math.Atan2(b.Vel.Y, b.Vel.X) - a0);
        }
        Assert.True(HeadingChange(true) > 0.02, "hollow ball drifts");
        Assert.Equal(0.0, HeadingChange(false), 6); // no module → perfectly straight
    }

    [Fact]
    public void HollowBall_RealHit_NeverDropsBelowOne()
    {
        // Full path: even at base 1 power, Hollow's −1 penalty is floored at ≥1 damage by BallSystem.
        var g = Make(3, 4, 3, 30);
        Equip(g, "hollow_ball");
        var blk = BlockAt(g, 1, 0);
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        var b = g.Balls[0];
        b.Pos = new Vec2(c.X, c.Y + g.Config.CellSize / 2 + b.Radius - 1);
        b.Vel = new Vec2(0, -g.Config.BallSpeed);
        int hp0 = blk.Hp;
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(blk.Hp < hp0, "a hollow hit still does at least 1 damage (floored)");
    }

    // ── Gyro Paddle: deflect angle driven by paddle velocity ────────────────────
    [Fact]
    public void GyroPaddle_PaddleVelocity_WhipsTheBallSideways()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "gyro_paddle");
        var b = g.Balls[0];
        b.Vel = new Vec2(0, -g.Config.BallSpeed); // straight up
        g._paddleVelX = 250;                        // paddle sweeping RIGHT
        ModuleSystem.OnPaddleHit(g, b, 0.0, false);
        Assert.True(b.Vel.X > 0, $"a right-sweeping paddle whips the ball right; vx={b.Vel.X:0.0}");

        b.Vel = new Vec2(0, -g.Config.BallSpeed);
        g._paddleVelX = -250;                       // sweeping LEFT
        ModuleSystem.OnPaddleHit(g, b, 0.0, false);
        Assert.True(b.Vel.X < 0, $"a left-sweeping paddle whips the ball left; vx={b.Vel.X:0.0}");
    }

    // ── Drumhead Paddle: perfect-centre deflect → shockwave up the paddle's column ──
    [Fact]
    public void DrumheadPaddle_PerfectDeflect_DamagesItsColumn()
    {
        var g = Make(3, 3, topRows: 3, hp: 30); // col1 has blocks at rows 0,1,2
        Equip(g, "drumhead_paddle");
        g.SetPaddleX(g.Level.Grid.CellCenter(1, 0).X); // align the paddle with column 1
        var b = g.Balls[0];
        int colHpBefore = g.Blocks.Where(bl => bl.Col == 1).Sum(bl => bl.Hp);
        int offColBefore = g.Blocks.Where(bl => bl.Col == 0).Sum(bl => bl.Hp);
        ModuleSystem.OnPaddleHit(g, b, 0.0, isPerfect: true);
        Assert.True(g.Blocks.Where(bl => bl.Col == 1).Sum(bl => bl.Hp) < colHpBefore, "shockwave hits the paddle's column");
        Assert.Equal(offColBefore, g.Blocks.Where(bl => bl.Col == 0).Sum(bl => bl.Hp)); // other columns untouched
    }

    [Fact]
    public void DrumheadPaddle_NonPerfectDeflect_NoShockwave()
    {
        var g = Make(3, 3, topRows: 3, hp: 30);
        Equip(g, "drumhead_paddle");
        g.SetPaddleX(g.Level.Grid.CellCenter(1, 0).X);
        int before = g.Blocks.Where(bl => bl.Col == 1).Sum(bl => bl.Hp);
        ModuleSystem.OnPaddleHit(g, g.Balls[0], 0.6, isPerfect: false); // off-centre
        Assert.Equal(before, g.Blocks.Where(bl => bl.Col == 1).Sum(bl => bl.Hp)); // no shockwave
    }

    // ── Batch 2: Gravity Well — pulls the ball toward the block centroid ─────────
    [Fact]
    public void GravityWell_PullsBallTowardTheBlockMass()
    {
        var g = Make(5, 2, 1, 30); // blocks across cols 0..4 → centroid at the centre column
        Equip(g, "gravity_well");
        var b = g.Balls[0];
        double centroidX = g.Level.Grid.CellCenter(2, 0).X;
        b.Pos = new Vec2(g.Level.Grid.OriginX + 2, g.Paddle.Center.Y - 40); // far LEFT of the mass
        b.Vel = new Vec2(0, -g.Config.BallSpeed); // straight up
        for (int i = 0; i < 20; i++) ModuleSystem.OnBallTick(g, b, 0.016);
        Assert.True(b.Vel.X > 0, $"the well should pull the ball right toward the mass; vx={b.Vel.X:0.0}");
    }

    // ── Batch 2: Toll Roads — gold only from crit / perfect-deflect kills ────────
    [Fact]
    public void TollRoads_PaysOnlyCritOrPerfectKills_AndDoubles()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "toll_roads");
        g.LastHitWasCrit = false; g._tollPerfectWindow = 0;
        Assert.Equal(0, ModuleSystem.KillCrystals(g, 3)); // ordinary kill → no gold
        g.LastHitWasCrit = true;
        Assert.Equal(6, ModuleSystem.KillCrystals(g, 3)); // crit kill → double
        g.LastHitWasCrit = false; g._tollPerfectWindow = 1.0;
        Assert.Equal(6, ModuleSystem.KillCrystals(g, 3)); // inside perfect window → double
    }

    [Fact]
    public void TollRoads_Unequipped_NormalPayout()
    {
        var g = Make(3, 2, 1, 30);
        Assert.Equal(3, ModuleSystem.KillCrystals(g, 3)); // no module → unchanged
    }

    // ── Batch 2: Brittle Glass — huge damage, shatters on indestructible ─────────
    [Fact]
    public void BrittleGlass_HitsHuge_ButShattersOnIndestructible()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "brittle_glass");
        var b = g.Balls[0];
        Assert.Equal(5, ModuleSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // +3+2*level(1)=5 huge
        // Hitting a destructible block: survives.
        ModuleSystem.OnBlockHit(g, b, BlockAt(g, 1, 0), 5, 30);
        Assert.True(b.Alive);
    }

    [Fact]
    public void BrittleGlass_ShattersOnIndestructibleHit()
    {
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"w\",\"biome\":\"hell\",\"hp\":1,\"sprite\":\"s\",\"indestructible\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":2,\"rows_data\":[\".A.\",\"...\"],\"legend\":{\"A\":\"w\"}}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, 1);
        g.Serve();
        Equip(g, "brittle_glass");
        var b = g.Balls[0];
        var wall = g.Blocks.First(bl => bl.Indestructible);
        ModuleSystem.OnBlockHit(g, b, wall, 0, 1);
        Assert.False(b.Alive); // shattered on the unbreakable block
    }

    // ── Batch 2: Spin-Loaded — edge hits impart curving spin that decays ─────────
    [Fact]
    public void SpinLoaded_EdgeHit_ImpartsSpin_CentreHitDoesNot()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "spin_loaded");
        var b = g.Balls[0];
        ModuleSystem.OnPaddleHit(g, b, 0.1, false); // near-centre
        Assert.Equal(0, b.Spin);
        ModuleSystem.OnPaddleHit(g, b, 0.7, false); // edge
        Assert.True(System.Math.Abs(b.Spin) > 0, "edge hit imparts spin");
    }

    [Fact]
    public void SpinLoaded_SpinCurvesTheBall_AndDecays()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "spin_loaded");
        var b = g.Balls[0];
        b.Vel = new Vec2(0, -g.Config.BallSpeed);
        b.Spin = 2.0;
        double a0 = System.Math.Atan2(b.Vel.Y, b.Vel.X);
        double spin0 = b.Spin;
        for (int i = 0; i < 20; i++) ModuleSystem.OnBallTick(g, b, 0.016);
        Assert.NotEqual(a0, System.Math.Atan2(b.Vel.Y, b.Vel.X)); // heading curved
        Assert.True(b.Spin < spin0, "spin decays");
    }

    // ── Batch 3: Pressure Cooker — field descends on a timer, kills push it back ──
    [Fact]
    public void PressureCooker_DescendsTheFieldOnATimer()
    {
        var g = Make(3, 4, topRows: 2, hp: 30); // rows 0,1 occupied; lastRow=3
        Equip(g, "pressure_cooker");
        var top = BlockAt(g, 1, 0); int r0 = top.Row;
        ModuleSystem.Update(g, 6.0); // one full interval → descend
        Assert.Equal(r0 + 1, top.Row); // the whole field dropped a row
    }

    [Fact]
    public void PressureCooker_KillsPushTheFieldBackUp()
    {
        var g = Make(3, 4, topRows: 2, hp: 30);
        Equip(g, "pressure_cooker"); // L1 → 4 kills to push back
        var top = BlockAt(g, 1, 0);
        ModuleSystem.Update(g, 6.0); // descend → row 1
        Assert.Equal(1, top.Row);
        for (int i = 0; i < 4; i++) ModuleSystem.OnBlockDestroyed(g, top);
        Assert.Equal(0, top.Row); // 4 kills shoved it back up
    }

    [Fact]
    public void PressureCooker_OverrunLosesTheLevel()
    {
        var g = Make(3, 3, topRows: 2, hp: 30); // rows 0,1; lastRow=2 → the row-1 blocks overrun on a descend
        Equip(g, "pressure_cooker");
        ModuleSystem.Update(g, 6.0); // row1 → row2 (lastRow) = overrun
        Assert.Equal(GamePhase.Lost, g.Phase);
    }

    // ── Batch 3: Riposte Paddle — a MOVING paddle parries enemy fire back as damage ──
    [Fact]
    public void RipostePaddle_MovingPaddle_ParriesHazardIntoABolt()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "riposte_paddle");
        var hz = new Arkanoid.Core.Entities.Projectile { Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y),
            Vel = new Vec2(0, 200), Damage = 1, Radius = 6, Alive = true, Kind = "bolt" };
        g._paddleVelX = 10; // actively swinging
        bool parried = ModuleSystem.OnHazardHitPaddle(g, hz);
        Assert.True(parried);
        Assert.Contains(g.Projectiles, p => p.Kind == "riposte" && p.Vel.Y < 0); // upward damaging bolt
    }

    [Fact]
    public void RipostePaddle_StillPaddle_TakesTheHit()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "riposte_paddle");
        var hz = new Arkanoid.Core.Entities.Projectile { Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y),
            Vel = new Vec2(0, 200), Damage = 1, Radius = 6, Alive = true, Kind = "bolt" };
        g._paddleVelX = 0; // stationary → no parry
        Assert.False(ModuleSystem.OnHazardHitPaddle(g, hz));
        Assert.DoesNotContain(g.Projectiles, p => p.Kind == "riposte");
    }

    [Fact]
    public void RipostePaddle_Unequipped_NeverParries()
    {
        var g = Make(3, 2, 1, 30); // no module
        var hz = new Arkanoid.Core.Entities.Projectile { Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y),
            Vel = new Vec2(0, 200), Damage = 1, Radius = 6, Alive = true, Kind = "bolt" };
        g._paddleVelX = 50; // even a fast paddle → no parry without the module
        Assert.False(ModuleSystem.OnHazardHitPaddle(g, hz));
    }

    [Fact]
    public void RipostePaddle_BoltActuallyDamagesBlocks()
    {
        // The reflected bolt is a real projectile that damages blocks it reaches (full sim path).
        var g = Make(3, 6, topRows: 1, hp: 30); // a block high up; tall board so the bolt flies up to it
        Equip(g, "riposte_paddle");
        var blk = BlockAt(g, 1, 0);
        // Put the paddle under that column and parry a hazard there.
        g.SetPaddleX(g.Level.Grid.CellCenter(1, 0).X);
        var hz = new Arkanoid.Core.Entities.Projectile { Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y),
            Vel = new Vec2(0, 200), Damage = 1, Radius = 6, Alive = true, Kind = "bolt" };
        g._paddleVelX = 10;
        Assert.True(ModuleSystem.OnHazardHitPaddle(g, hz));
        int hp0 = blk.Hp;
        for (int i = 0; i < (int)(2.0 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(blk.Hp < hp0, "the riposte bolt flew up and damaged the block");
    }

    [Fact]
    public void PressureCooker_Unequipped_NoDescent()
    {
        var g = Make(3, 4, topRows: 2, hp: 30); // no module
        var top = BlockAt(g, 1, 0); int r0 = top.Row;
        ModuleSystem.Update(g, 12.0); // plenty of time
        Assert.Equal(r0, top.Row); // field never descends without the module
    }

    // ── Batch 4: Twin Soul Core — two weaker twins; tether slices between them ───
    [Fact]
    public void TwinSoulCore_AfterServe_SpawnsSecondTwin_BothWeaker()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "twin_soul_core");
        g.Balls[0].Twin = true; // OnServe would set this; simulate the served twin
        ModuleSystem.AfterServe(g);
        Assert.Equal(2, g.Balls.Count(b => b.Twin && b.Alive)); // partner spawned
        Assert.Equal(-1, ModuleSystem.BallDamageBonus(g, g.Balls[0], BlockAt(g, 1, 0))); // each twin hits softer
    }

    [Fact]
    public void TwinSoulCore_TetherSlicesBlocksBetweenTheTwins()
    {
        var g = Make(5, 1, topRows: 1, hp: 30); // a row of blocks; tether spans across it
        Equip(g, "twin_soul_core");
        double rowY = g.Level.Grid.CellCenter(2, 0).Y;
        var a = g.Balls[0]; a.Twin = true; a.Pos = new Vec2(g.Level.Grid.CellCenter(0, 0).X, rowY);
        g.Balls.Add(new Arkanoid.Core.Entities.Ball { Id = 99, Twin = true, Alive = true, Radius = a.Radius,
            Pos = new Vec2(g.Level.Grid.CellCenter(4, 0).X, rowY), Vel = new Vec2(0, -1) });
        var mid = BlockAt(g, 2, 0); int hp0 = mid.Hp;
        ModuleSystem.Update(g, 0.3); // > slice cadence
        Assert.NotNull(g.TwinTether);
        Assert.True(mid.Hp < hp0, "the tether sliced the block between the twins");
    }

    [Fact]
    public void TwinSoulCore_LoseOneTwin_TetherDies()
    {
        var g = Make(5, 1, topRows: 1, hp: 30);
        Equip(g, "twin_soul_core");
        g.Balls[0].Twin = true;
        g.Balls.Add(new Arkanoid.Core.Entities.Ball { Id = 99, Twin = true, Alive = true, Radius = 8, Pos = new Vec2(100, 50), Vel = new Vec2(0,-1) });
        ModuleSystem.Update(g, 0.3);
        Assert.NotNull(g.TwinTether);          // two twins → tether live
        g.Balls[1].Alive = false;              // lose one twin
        ModuleSystem.Update(g, 0.3);
        Assert.Null(g.TwinTether);             // tether dies
    }

    // ── Batch 4: Fission Core — splits every Nth kill, fuses on a catch ──────────
    [Fact]
    public void FissionCore_SplitsEveryNthKill_IntoSmallerBalls()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "fission_core"); // L1 → split every 4 kills
        var b = g.Balls[0]; b.Fission = true; b.Vel = new Vec2(0, -g.Config.BallSpeed);
        double r0 = b.Radius;
        int before = g.Balls.Count(x => x.Alive);
        for (int i = 0; i < 4; i++) ModuleSystem.OnBlockDestroyed(g, BlockAt(g, 1, 0));
        Assert.True(g.Balls.Count(x => x.Alive) > before, "the ball split into fragments");
        Assert.True(g.Balls.All(x => !x.Fission || x.Radius < r0 + 0.01), "fragments are smaller");
    }

    [Fact]
    public void FissionCore_PaddleCatch_FusesFragmentsIntoABiggerBall()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "fission_core");
        var keep = g.Balls[0]; keep.Fission = true; keep.Radius = g.Config.BallRadius * 0.7;
        g.Balls.Add(new Arkanoid.Core.Entities.Ball { Id = 98, Fission = true, Alive = true, Radius = g.Config.BallRadius * 0.7, Pos = new Vec2(50,50), Vel = new Vec2(0,-1) });
        g.Balls.Add(new Arkanoid.Core.Entities.Ball { Id = 97, Fission = true, Alive = true, Radius = g.Config.BallRadius * 0.7, Pos = new Vec2(60,50), Vel = new Vec2(0,-1) });
        double r0 = keep.Radius;
        ModuleSystem.OnPaddleHit(g, keep, 0.0, false);
        Assert.Equal(1, g.Balls.Count(x => x.Fission && x.Alive)); // fused down to one
        Assert.True(keep.Radius > r0, "the fused ball is bigger");
    }
}
