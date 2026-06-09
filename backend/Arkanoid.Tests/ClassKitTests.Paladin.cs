using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Paladin kit: shield / spear / duplicate (partial of ClassKitTests).</summary>
public partial class ClassKitTests
{
    // -------------------------------------------------------------------------
    // 2. Paladin — Shield
    // -------------------------------------------------------------------------

    [Fact]
    public void Paladin_Shield_SpawnsBarrier()
    {
        var g = Make("paladin");
        MaxManaAndServe(g);
        double manaBefore = g.ManaValue;
        g.CastSlot(0); // shield
        Assert.Single(g.Barriers);
        Assert.True(g.ManaValue < manaBefore, "Shield should cost mana");
    }

    [Fact]
    public void Paladin_Shield_ReflectsDownwardBallUpward()
    {
        var g = Make("paladin");
        MaxManaAndServe(g);
        g.CastSlot(0); // create barrier

        var barrier = g.Barriers[0];
        // Place ball directly above the barrier moving downward
        var ball = g.Balls[0];
        ball.Pos = new Vec2(barrier.CenterX, barrier.Y - ball.Radius - 1);
        ball.Vel = new Vec2(0, 400); // downward

        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Balls[0].Vel.Y < 0, $"Ball should now move upward; vy={g.Balls[0].Vel.Y:F1}");
    }

    [Fact]
    public void Paladin_Shield_NoMana_NoBarrier()
    {
        var g = Make("paladin");
        g.Serve();
        g.ManaValue = 0;
        g.CastSlot(0);
        Assert.Empty(g.Barriers);
    }

    [Fact]
    public void Paladin_Shield_ExpiresAfterLifetime()
    {
        var g = Make("paladin");
        MaxManaAndServe(g);
        g.CastSlot(0);
        Assert.Single(g.Barriers);
        // Park ball safely so it never drains, keeping Phase=Playing
        var ball = g.Balls[0];
        double elapsed = 0;
        while (elapsed < SimConfig.Default.ShieldLifetime + 0.5)
        {
            // Keep ball stationary above paddle
            ball.Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2 - ball.Radius - 2);
            ball.Vel = new Vec2(0, 0);
            g.Tick(SimConfig.Default.FixedDt);
            elapsed += SimConfig.Default.FixedDt;
        }
        Assert.Empty(g.Barriers);
    }

    // -------------------------------------------------------------------------
    // 3. Paladin — Spear
    // -------------------------------------------------------------------------

    [Fact]
    public void Paladin_Spear_SpawnsPiercingProjectile()
    {
        var g = Make("paladin");
        MaxManaAndServe(g);
        g.CastSlot(1); // spear
        Assert.Single(g.Projectiles);
        Assert.Equal("spear", g.Projectiles[0].Kind);
        Assert.True(g.Projectiles[0].PiercingHitsLeft > 0);
    }

    [Fact]
    public void Paladin_Spear_DamagesMultipleBlocksInLine()
    {
        // Build a column of 3 blocks stacked vertically (rows 0,1,2 same col)
        var cfg = SimConfig.Default;
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"test\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":5," +
            "\"rows_data\":[\".A.\",\".A.\",\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        var g = new GameInstance(level, cfg, seed: 1);
        g.SetCharacter("paladin");
        // Serve (required for Phase=Playing)
        g.ManaValue = g.ManaMaxValue;
        g.Serve();

        // Manually position the spear at the same X as the blocks, just below them
        g.CastSlot(1);
        var spear = g.Projectiles[0];
        var col1Center = level.Grid.CellCenter(1, 2); // bottom block of the column
        spear.Pos = new Vec2(col1Center.X, col1Center.Y + cfg.CellSize / 2 + spear.Radius + 2);
        spear.Vel = new Vec2(0, -cfg.SpearSpeed);

        // Run enough ticks for spear to hit all 3 blocks
        for (int i = 0; i < 200 && g.Blocks.Any(b => !b.Dead); i++)
            g.Tick(cfg.FixedDt);

        // At least 2 of the 3 blocks in line should be killed (hp=1 each, spear pierces 4)
        int dead = g.Blocks.Count(b => b.Dead);
        Assert.True(dead >= 2, $"Spear should pierce multiple blocks; dead={dead}");
    }

    [Fact]
    public void Paladin_Spear_NoMana_NoProjectile()
    {
        var g = Make("paladin");
        g.Serve();
        g.ManaValue = 0;
        g.CastSlot(1);
        Assert.Empty(g.Projectiles);
    }

    // -------------------------------------------------------------------------
    // 4. Paladin — Duplicate
    // -------------------------------------------------------------------------

    [Fact]
    public void Paladin_Duplicate_IncreasesBallCount()
    {
        var g = Make("paladin");
        MaxManaAndServe(g);
        int before = g.Balls.Count(b => b.Alive);
        g.CastSlot(2); // duplicate
        int after = g.Balls.Count(b => b.Alive);
        Assert.True(after > before, $"Duplicate should add balls; before={before} after={after}");
    }

    [Fact]
    public void Paladin_Duplicate_NoMana_NoBalls()
    {
        var g = Make("paladin");
        g.Serve();
        g.ManaValue = 0;
        int before = g.Balls.Count(b => b.Alive);
        g.CastSlot(2);
        Assert.Equal(before, g.Balls.Count(b => b.Alive));
    }
}
