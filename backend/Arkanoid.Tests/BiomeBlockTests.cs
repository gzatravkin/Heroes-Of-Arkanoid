using Arkanoid.Core.Blocks;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Tests for indestructible, ballPhases (ghost), and teleporter block mechanics
/// introduced with the biome variety update.
/// </summary>
public class BiomeBlockTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Build a minimal GameInstance from inline JSON catalog + level strings.
    /// </summary>
    private static GameInstance MakeGame(string typesJson, string levelJson, SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var catalog = BlockCatalog.FromJson($"{{\"types\":[{typesJson}]}}");
        var level = LevelLoader.FromJson(levelJson, catalog, cfg);
        return new GameInstance(level, cfg, seed: 1);
    }

    /// <summary>Place the ball directly on top of the given block (one radius above the block face).</summary>
    private static void AimBallAtBlock(GameInstance g, Block blk, Vec2 velocity)
    {
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        // Approach from below (positive Y is down): position ball just below the block centre
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = velocity;
    }

    // ---------------------------------------------------------------------------
    // 1. Indestructible block takes no damage but ball still bounces
    // ---------------------------------------------------------------------------

    [Fact]
    public void Indestructible_BlockTakesNoDamage_ButBallBounces()
    {
        // A single indestructible block + one normal block so the level is winnable
        var g = MakeGame(
            """
            {"id":"indestr","biome":"test","hp":99,"sprite":"s","needToKill":false,"indestructible":true},
            {"id":"normal", "biome":"test","hp":1, "sprite":"s","needToKill":true}
            """,
            """
            {"id":"t","biome":"test","cols":3,"rows":3,
             "rows_data":["ANA","...","..."],
             "legend":{"A":"indestr","N":"normal"}}
            """
        );
        g.Serve();

        var indestrBlk = g.Blocks.First(b => b.TypeId == "indestr");
        var velBefore = g.Balls[0].Vel;
        int hpBefore = indestrBlk.Hp;

        // Place ball just below the indestructible block, moving upward into it
        var c = g.Level.Grid.CellCenter(indestrBlk.Col, indestrBlk.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed); // moving up into block

        g.Tick(SimConfig.Default.FixedDt);

        // HP must be unchanged
        Assert.Equal(hpBefore, indestrBlk.Hp);
        Assert.False(indestrBlk.Dead);

        // Ball must have bounced (Vy flipped from negative to positive)
        Assert.True(g.Balls[0].Vel.Y > 0, "ball should have bounced back downward after hitting indestructible block");
    }

    // ---------------------------------------------------------------------------
    // 2. Ghost block (ballPhases): ball passes through, projectile still destroys it
    // ---------------------------------------------------------------------------

    [Fact]
    public void GhostBlock_BallPassesThrough_NoReflectionNoDamage()
    {
        // One ghost block + one normal needToKill block so the level is winnable via projectile
        var g = MakeGame(
            """
            {"id":"ghost","biome":"test","hp":2,"sprite":"s","needToKill":true,"ballPhases":true},
            {"id":"normal","biome":"test","hp":1,"sprite":"s","needToKill":true}
            """,
            """
            {"id":"t","biome":"test","cols":3,"rows":3,
             "rows_data":["GNA","...","..."],
             "legend":{"G":"ghost","N":"normal","A":"normal"}}
            """
        );
        g.Serve();

        var ghostBlk = g.Blocks.First(b => b.TypeId == "ghost");
        var c = g.Level.Grid.CellCenter(ghostBlk.Col, ghostBlk.Row);

        // Aim ball directly into the ghost block from below, moving upward
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        var upwardVel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Balls[0].Vel = upwardVel;

        g.Tick(SimConfig.Default.FixedDt);

        // Ball should NOT have been reflected: Vy still negative (still moving upward, or just passed through)
        Assert.True(g.Balls[0].Vel.Y < 0, "ball should pass through ghost block without reflection");
        // Ghost block should not have been damaged by ball
        Assert.Equal(2, ghostBlk.Hp);
        Assert.False(ghostBlk.Dead);
    }

    [Fact]
    public void GhostBlock_Projectile_DamagesAndDestroys()
    {
        // Same ghost block, now we cast a fireball at it
        var g = MakeGame(
            """
            {"id":"ghost","biome":"test","hp":1,"sprite":"s","needToKill":true,"ballPhases":true},
            {"id":"normal","biome":"test","hp":1,"sprite":"s","needToKill":false}
            """,
            """
            {"id":"t","biome":"test","cols":3,"rows":3,
             "rows_data":["GNA","...","..."],
             "legend":{"G":"ghost","N":"normal","A":"normal"}}
            """
        );
        g.Serve();

        var ghostBlk = g.Blocks.First(b => b.TypeId == "ghost");

        // Give enough mana and fire a fireball
        g.ManaValue = SimConfig.Default.FireballCost;
        g.CastFireball();
        Assert.Single(g.Projectiles);

        // Move the projectile directly onto the ghost block
        var c = g.Level.Grid.CellCenter(ghostBlk.Col, ghostBlk.Row);
        g.Projectiles[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 - 1);
        g.Projectiles[0].Vel = new Vec2(0, -SimConfig.Default.FireballSpeed);

        g.Tick(SimConfig.Default.FixedDt);

        // Ghost block should be dead (destroyed by projectile)
        Assert.True(ghostBlk.Dead, "projectile must be able to destroy ghost block");
    }

    // ---------------------------------------------------------------------------
    // 3. Teleporter: warps ball to the paired portal
    // ---------------------------------------------------------------------------

    [Fact]
    public void Teleporter_WarpsBallToPairedPortal()
    {
        // Two teleporter blocks at different positions, plus a normal needToKill block
        var g = MakeGame(
            """
            {"id":"tp","biome":"test","hp":1,"sprite":"s","needToKill":false,"indestructible":true,"teleporter":true},
            {"id":"normal","biome":"test","hp":1,"sprite":"s","needToKill":true}
            """,
            """
            {"id":"t","biome":"test","cols":5,"rows":3,
             "rows_data":["T...T","..N..","NNNNN"],
             "legend":{"T":"tp","N":"normal"}}
            """
        );
        g.Serve();

        var teleporters = g.Blocks.Where(b => b.Teleporter).ToList();
        Assert.Equal(2, teleporters.Count);

        var tp0 = teleporters[0];
        var tp1 = teleporters[1];
        var c0 = g.Level.Grid.CellCenter(tp0.Col, tp0.Row);
        var c1 = g.Level.Grid.CellCenter(tp1.Col, tp1.Row);

        // Place ball overlapping the first teleporter, moving upward
        g.Balls[0].Pos = new Vec2(c0.X, c0.Y + g.Balls[0].Radius * 0.5); // inside the block AABB
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Balls[0].TeleportCooldown = 0; // ensure cooldown is clear

        g.Tick(SimConfig.Default.FixedDt);

        // Ball should now be near the second teleporter's cell centre
        var dist = (g.Balls[0].Pos - c1).Length;
        Assert.True(dist < SimConfig.Default.CellSize,
            $"ball should be near tp1 after warp; actual dist={dist:F1}, ball={g.Balls[0].Pos}, tp1={c1}");

        // Cooldown should have been set
        Assert.True(g.Balls[0].TeleportCooldown > 0, "teleport cooldown should be > 0 after warp");
    }

    // ---------------------------------------------------------------------------
    // 4. Teleporter: cooldown prevents immediate re-warp
    // ---------------------------------------------------------------------------

    [Fact]
    public void Teleporter_RespectsCooldown_NoImmediateReWarp()
    {
        var cfg = new SimConfig(); // TeleportCooldownTicks = 18 (default)
        var g = MakeGame(
            """
            {"id":"tp","biome":"test","hp":1,"sprite":"s","needToKill":false,"indestructible":true,"teleporter":true},
            {"id":"normal","biome":"test","hp":1,"sprite":"s","needToKill":true}
            """,
            """
            {"id":"t","biome":"test","cols":5,"rows":3,
             "rows_data":["T...T","..N..","NNNNN"],
             "legend":{"T":"tp","N":"normal"}}
            """,
            cfg
        );
        g.Serve();

        var teleporters = g.Blocks.Where(b => b.Teleporter).ToList();
        var tp0 = teleporters[0];
        var tp1 = teleporters[1];
        var c0 = g.Level.Grid.CellCenter(tp0.Col, tp0.Row);
        var c1 = g.Level.Grid.CellCenter(tp1.Col, tp1.Row);

        // --- First tick: trigger warp from tp0 to tp1 ---
        g.Balls[0].Pos = new Vec2(c0.X, c0.Y + g.Balls[0].Radius * 0.5);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Balls[0].TeleportCooldown = 0;
        g.Tick(SimConfig.Default.FixedDt);

        Assert.True(g.Balls[0].TeleportCooldown > 0, "cooldown should be set after first warp");
        var posAfterWarp1 = g.Balls[0].Pos;

        // --- Move ball directly onto tp1 while cooldown is still active ---
        g.Balls[0].Pos = new Vec2(c1.X, c1.Y + g.Balls[0].Radius * 0.5);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        // TeleportCooldown > 0, so this should NOT warp

        g.Tick(SimConfig.Default.FixedDt);

        // Ball should NOT have been sent back to tp0 (would be near c0)
        var distToTp0 = (g.Balls[0].Pos - c0).Length;
        Assert.True(distToTp0 > SimConfig.Default.CellSize,
            $"ball should NOT be warped back to tp0 during cooldown; dist={distToTp0:F1}");
    }
}
