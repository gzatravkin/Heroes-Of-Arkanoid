using Arkanoid.Core.Blocks;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Tests for the ported original enemies/hazards (docs/08-enemy-behaviour-spec.md).
/// Pure-Core, deterministic.
/// </summary>
public class EnemyTests
{
    private static GameInstance Make(string blocksJson, string levelJson)
    {
        var catalog = BlockCatalog.FromJson(blocksJson);
        var level   = LevelLoader.FromJson(levelJson, catalog);
        var g = new GameInstance(level, SimConfig.Default, seed: 1);
        g.Serve();
        return g;
    }

    /// <summary>Park the ball motionless near the paddle so it can't hit blocks during a wait.</summary>
    private static void Park(GameInstance g)
    {
        g.Balls[0].Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Balls[0].Radius - 2);
        g.Balls[0].Vel = new Vec2(0, 0);
    }

    /// <summary>Drive the ball into a specific block from below and tick once (public path).</summary>
    private static void BallHit(GameInstance g, Block target)
    {
        var c = g.Level.Grid.CellCenter(target.Col, target.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
    }

    // ── Emitter (Hell spawner / Beholder / Melee statue) ──────────────────────

    [Fact]
    public void EmitterBlock_FiresHazard_AfterItsInterval()
    {
        var g = Make(
            "{\"types\":[{\"id\":\"e\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":1.0,\"emitAim\":\"paddle\"}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".E.\",\"...\",\"...\"],\"legend\":{\"E\":\"e\"}}");

        Assert.Empty(g.Hazards);
        // Advance just under the interval → still no hazard.
        for (int i = 0; i < 50; i++) g.Tick(0.016);
        Assert.Empty(g.Hazards);
        // Cross the 1.0s interval → exactly one hazard fired.
        for (int i = 0; i < 20; i++) g.Tick(0.016);
        Assert.True(g.Hazards.Count >= 1, $"expected an emitted hazard, got {g.Hazards.Count}");
    }

    [Fact]
    public void EmitterHazard_FallsDownward()
    {
        var g = Make(
            "{\"types\":[{\"id\":\"e\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":0.5,\"emitAim\":\"down\"}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".E.\",\"...\",\"...\"],\"legend\":{\"E\":\"e\"}}");
        for (int i = 0; i < 40; i++) g.Tick(0.016);
        Assert.NotEmpty(g.Hazards);
        Assert.True(g.Hazards[0].Vel.Y > 0, "hazard must travel downward toward the paddle");
    }

    // ── Bomb (chain explosion) ────────────────────────────────────────────────

    [Fact]
    public void Bomb_OnDeath_DamagesNeighboursInRadius()
    {
        // Row of: bomb at (1,0), plain blocks around it. Killing the bomb should hurt neighbours.
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"bomb\",\"explodeRadius\":1}," +
            "{\"id\":\"p\",\"biome\":\"t\",\"hp\":5,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"pbp\",\"...\",\"...\"],\"legend\":{\"b\":\"b\",\"p\":\"p\"}}");

        var left  = g.Blocks[0]; // p
        var bomb  = g.Blocks[1]; // b
        var right = g.Blocks[2]; // p
        int leftBefore = left.Hp, rightBefore = right.Hp;

        BallHit(g, bomb); // ball kills the hp-1 bomb → it explodes
        Assert.True(bomb.Dead);
        Assert.True(left.Hp  < leftBefore,  "left neighbour took explosion damage");
        Assert.True(right.Hp < rightBefore, "right neighbour took explosion damage");
    }

    // ── Cart (rolls a horizontal hazard across the paddle line) ───────────────

    [Fact]
    public void Cart_LaunchesHorizontalHazard_AfterInterval()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"cart\"}," +
            "{\"id\":\"z\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":8,\"rows\":3,\"rows_data\":[\"k......z\",\"........\",\"........\"],\"legend\":{\"k\":\"k\",\"z\":\"z\"}}");
        Park(g);
        for (int i = 0; i < (int)(SimConfig.Default.CartInterval / SimConfig.Default.FixedDt) + 2; i++)
            g.Tick(SimConfig.Default.FixedDt);
        var cart = g.Hazards.FirstOrDefault(h => h.Kind == "cart");
        Assert.NotNull(cart);
        Assert.True(System.Math.Abs(cart!.Vel.X) > 0 && cart.Vel.Y == 0, "cart rolls horizontally");
    }

    // ── Lava (deadly block drains the ball) ───────────────────────────────────

    [Fact]
    public void Lava_DrainsBall_OnContact()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"l\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"lava\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"l.k\",\"...\",\"...\"],\"legend\":{\"l\":\"l\",\"k\":\"k\"}}");
        int sparesBefore = g.SpareBalls;
        BallHit(g, g.Blocks[0]); // the lava block drains the ball → a spare is consumed on re-serve
        Assert.True(g.SpareBalls < sparesBefore, "lava drained the ball (a spare was consumed)");
    }

    // ── Altar / Vase pacify the Heaven statues ────────────────────────────────

    [Fact]
    public void Altar_PacifiesStatues_SoEmitterHoldsFire()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"a\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"altar\"}," +
            "{\"id\":\"m\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":0.5,\"emitAim\":\"paddle\"}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"a.m\",\"...\",\"...\"],\"legend\":{\"a\":\"a\",\"m\":\"m\"}}");
        var statue = g.Blocks[1];

        // Hit the altar → statue is pacified.
        BallHit(g, g.Blocks[0]);
        Assert.True(statue.AllyTimer > 0, "statue pacified by the altar");

        // While pacified, the statue emits no hazards even past its interval.
        Park(g);
        for (int i = 0; i < 60; i++) g.Tick(SimConfig.Default.FixedDt); // ~1s > 0.5s interval
        Assert.Empty(g.Hazards);
    }

    // ── Bat (grabs the ball, then releases + flies away) ──────────────────────

    [Fact]
    public void Bat_GrabsBall_ThenReleasesAndFliesAway()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"v\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"behavior\":\"bat\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"v.k\",\"...\",\"...\"],\"legend\":{\"v\":\"v\",\"k\":\"k\"}}");
        var bat = g.Blocks[0];

        BallHit(g, bat);
        Assert.True(g.Balls[0].GrabbedTimer > 0, "ball is held by the bat");
        Assert.Equal(0, g.Balls[0].Vel.Length, 3); // pinned

        // Hold elapses → ball released (moving again) and the bat flew away.
        for (int i = 0; i < (int)(SimConfig.Default.BatHoldTime / SimConfig.Default.FixedDt) + 3; i++)
            g.Tick(SimConfig.Default.FixedDt);
        Assert.True(bat.Dead, "bat flew away after releasing the ball");
        Assert.True(g.Balls[0].Vel.Length > 0, "ball is moving again after release");
        Assert.True(g.Balls[0].GrabbedTimer <= 0);
    }

    [Fact]
    public void BatRelease_SpawnsHarmlessFlyaway_ThatDespawnsAboveBoard()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"v\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"behavior\":\"bat\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"v.k\",\"...\",\"...\"],\"legend\":{\"v\":\"v\",\"k\":\"k\"}}");
        var bat   = g.Blocks[0];
        int lives = g.Lives;

        BallHit(g, bat);
        for (int i = 0; i < (int)(SimConfig.Default.BatHoldTime / SimConfig.Default.FixedDt) + 3; i++)
            g.Tick(SimConfig.Default.FixedDt);

        // Release spawned the visual flyaway: harmless, flying upward.
        var fly = g.Hazards.FirstOrDefault(h => h.Kind == "bat");
        Assert.NotNull(fly);
        Assert.Equal(0, fly!.Damage);
        Assert.True(fly.Vel.Y < 0, "flyaway bat drifts upward");

        // It despawns above the board without ever costing HP.
        for (int i = 0; i < 600; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.DoesNotContain(g.Hazards, h => h.Kind == "bat");
        Assert.Equal(lives, g.Lives);
    }

    // ── Emitter missile kinds (renderer draws real art from these tags) ───────

    [Fact]
    public void Emitter_TagsHazard_WithConfiguredMissileKind()
    {
        var g = Make(
            "{\"types\":[{\"id\":\"e\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":0.5,\"emitAim\":\"down\",\"missileKind\":\"beholdermissile\"}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".E.\",\"...\",\"...\"],\"legend\":{\"E\":\"e\"}}");
        for (int i = 0; i < 40; i++) g.Tick(0.016);
        Assert.NotEmpty(g.Hazards);
        Assert.All(g.Hazards, h => Assert.Equal("beholdermissile", h.Kind));
    }

    // ── Ghost Portal (phase toggle swaps which blocks are solid) ──────────────

    [Fact]
    public void GhostPortal_TogglesPhase_GhostBallHitsGhostBlocksAndPhasesNormal()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"r\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"behavior\":\"portal\"}," +
            "{\"id\":\"p\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true}," +
            "{\"id\":\"x\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"ballPhases\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"r.p\",\"x..\",\"...\"],\"legend\":{\"r\":\"r\",\"p\":\"p\",\"x\":\"x\"}}");

        var portal = g.Blocks[0];
        var normal = g.Blocks[1];
        var ghost  = g.Blocks[2];

        // Hit the portal → the ball becomes a ghost.
        BallHit(g, portal);
        Assert.True(g.Balls[0].Ghost, "portal toggled the ball to ghost phase");

        // Ghost ball passes THROUGH a normal block (no damage).
        int pBefore = normal.Hp;
        BallHit(g, normal);
        Assert.Equal(pBefore, normal.Hp);

        // Ghost ball COLLIDES with a ghost (ballPhases) block, damaging it.
        int xBefore = ghost.Hp;
        BallHit(g, ghost);
        Assert.True(ghost.Hp < xBefore, "ghost ball should damage the ghost block");
    }

    // ── Shield Statue (temporary block immunity) ──────────────────────────────

    [Fact]
    public void ShieldStatue_MakesNeighbourImmune_ThenWearsOff()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"d\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"shieldStatue\"}," +
            "{\"id\":\"p\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"d.p\",\"...\",\"...\"],\"legend\":{\"d\":\"d\",\"p\":\"p\"}}");
        var plain = g.Blocks[1];

        // Advance past one shield pulse (ball parked so it doesn't interfere).
        Park(g);
        for (int i = 0; i < (int)(SimConfig.Default.ShieldStatueInterval / SimConfig.Default.FixedDt) + 2; i++)
            g.Tick(SimConfig.Default.FixedDt);
        Assert.True(plain.ShieldTimer > 0, "neighbour was shielded");

        int hpBefore = plain.Hp;
        BallHit(g, plain);
        Assert.Equal(hpBefore, plain.Hp); // immune while shielded

        // Let the shield wear off, then damage lands.
        Park(g);
        for (int i = 0; i < (int)(SimConfig.Default.ShieldDuration / SimConfig.Default.FixedDt) + 5; i++)
            g.Tick(SimConfig.Default.FixedDt);
        Assert.True(plain.ShieldTimer <= 0, "shield wore off");
        BallHit(g, plain);
        Assert.True(plain.Hp < hpBefore, "damage lands after the shield expires");
    }

    // ── WindMaster (deflects the ball away) ───────────────────────────────────

    [Fact]
    public void WindMaster_PushesBallAway_PreservingSpeed()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"w\",\"biome\":\"t\",\"hp\":4,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"windMaster\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"w.k\",\"...\",\"...\"],\"legend\":{\"w\":\"w\",\"k\":\"k\"}}");

        var wind = g.Blocks[0];
        var wc = g.Level.Grid.CellCenter(wind.Col, wind.Row);

        // Ball just to the RIGHT of the windmaster, within radius, moving straight up.
        var speed = SimConfig.Default.BallSpeed;
        g.Balls[0].Pos = new Vec2(wc.X + g.Config.WindMasterRadius * 0.4, wc.Y);
        g.Balls[0].Vel = new Vec2(0, -speed);
        g.Tick(SimConfig.Default.FixedDt);

        Assert.True(g.Balls[0].Vel.X > 0, $"ball should be pushed right (away), vx={g.Balls[0].Vel.X:F2}");
        Assert.Equal(speed, g.Balls[0].Vel.Length, 1); // speed preserved (deflection only)
    }

    // ── Necromant (revives destroyed blocks) ──────────────────────────────────

    [Fact]
    public void Necromant_RevivesDestroyedBlock_AfterDelay_AndStopsWhenDead()
    {
        // Necromant 'N' (col0) + a normal block 'p' (col2). Kill p → it revives while N lives.
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"n\",\"biome\":\"t\",\"hp\":3,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"necromant\"}," +
            "{\"id\":\"p\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"n.p\",\"...\",\"...\"],\"legend\":{\"n\":\"n\",\"p\":\"p\"}}");

        var necro = g.Blocks[0];
        var plain = g.Blocks[1];

        // Kill the plain block via the ball, then park the ball so it can't chew the Necromant.
        BallHit(g, plain);
        Assert.True(plain.Dead, "plain block destroyed");
        Park(g);

        // Before the delay elapses it stays dead; after it, the Necromant revives it.
        for (int i = 0; i < (int)(SimConfig.Default.NecromantReviveDelay / SimConfig.Default.FixedDt) + 5; i++)
            g.Tick(SimConfig.Default.FixedDt);
        Assert.False(plain.Dead, "Necromant should have revived the block");
        Assert.Equal(plain.MaxHp, plain.Hp);

        // Kill the Necromant, then destroy the (revived) block again via the ball → it must NOT revive.
        necro.Hp = 0; necro.Dead = true;
        BallHit(g, plain);
        Assert.True(plain.Dead, "block destroyed again");
        Park(g);
        for (int i = 0; i < (int)(SimConfig.Default.NecromantReviveDelay / SimConfig.Default.FixedDt) + 5; i++)
            g.Tick(SimConfig.Default.FixedDt);
        Assert.True(plain.Dead, "no revival once the Necromant is dead");
    }

    // ── Stalactite (drops when a ball passes beneath) ─────────────────────────

    [Fact]
    public void Stalactite_Drops_WhenBallPassesBeneath()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"l\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"stalactite\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":4,\"rows_data\":[\".L.\",\"...\",\"...\",\"..k\"],\"legend\":{\"L\":\"l\",\"k\":\"k\"}}");

        var stal = g.Blocks[0];
        var sc = g.Level.Grid.CellCenter(stal.Col, stal.Row);

        // No drop while the ball is nowhere near its column.
        g.Balls[0].Pos = new Vec2(sc.X + 999, sc.Y + 50);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.False(stal.Dead);
        Assert.Empty(g.Hazards);

        // Ball moves directly beneath the stalactite → it detaches into a falling hazard.
        g.Balls[0].Pos = new Vec2(sc.X, sc.Y + g.Config.CellSize);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(stal.Dead, "stalactite detached");
        Assert.Single(g.Hazards);
        Assert.True(g.Hazards[0].Vel.Y > 0, "stalactite falls downward");
        Assert.Equal("stalactite", g.Hazards[0].Kind);
    }

    // ── Colour-paired teleporters ─────────────────────────────────────────────

    [Fact]
    public void Teleporter_WarpsToSameColourPartner_NotOtherColour()
    {
        // Row: red(0,0) blue(1,0) red(2,0). Ball hitting the first red must land on the
        // OTHER red (col 2), never the blue.
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"r\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"teleporter\",\"teleportColor\":0}," +
            "{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"teleporter\",\"teleportColor\":1}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":4,\"rows_data\":[\"rbr\",\"...\",\"...\",\"..k\"],\"legend\":{\"r\":\"r\",\"b\":\"b\",\"k\":\"k\"}}");

        var redA = g.Blocks[0]; // (0,0)
        var redB = g.Blocks[2]; // (2,0)
        var destCenter = g.Level.Grid.CellCenter(redB.Col, redB.Row);

        // Drive the ball into the first red teleporter.
        var c = g.Level.Grid.CellCenter(redA.Col, redA.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);

        // Ball must be near the OTHER red (col 2), not the blue (col 1).
        Assert.True(System.Math.Abs(g.Balls[0].Pos.X - destCenter.X) < SimConfig.Default.CellSize,
            $"ball warped to x={g.Balls[0].Pos.X:F1}, expected near red partner x={destCenter.X:F1}");
    }

    [Fact]
    public void Bomb_Chains_IntoAdjacentBomb()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"bomb\",\"explodeRadius\":1}," +
            "{\"id\":\"p\",\"biome\":\"t\",\"hp\":2,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"bbp\",\"...\",\"...\"],\"legend\":{\"b\":\"b\",\"p\":\"p\"}}");
        var bomb1 = g.Blocks[0];
        var plain = g.Blocks[2]; // hp 2, two cells from bomb1 → only reached if bomb2 chains
        BallHit(g, bomb1);
        Assert.True(g.Blocks[1].Dead, "adjacent bomb chained");
        Assert.True(plain.Hp < 2, "second bomb's explosion reached the far block");
    }
}
