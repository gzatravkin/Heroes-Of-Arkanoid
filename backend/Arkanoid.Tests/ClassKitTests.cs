using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// P6 class-kit tests: CastSlot dispatch, Paladin (shield/spear/duplicate),
/// Engineer (lightning/rocket/radiation), Necromancer (decay/skeleton/drain).
/// Fire Mage path intentionally not retested here — covered by SpellTests.cs.
/// </summary>
public partial class ClassKitTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Single block at (col=1, row=0) with the given HP.
    /// cols=3, rows=4 gives a board wide enough for all spells.
    /// </summary>
    private static GameInstance Make(string character = "fire_mage", int blockHp = 3, SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var catalog = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"test\",\"hp\":{blockHp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":4," +
            "\"rows_data\":[\".A.\",\"...\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        var g = new GameInstance(level, cfg, seed: 42);
        g.SetCharacter(character);
        return g;
    }

    /// <summary>
    /// Grid of 9 blocks (3×3) so AoE / chain spells have targets.
    /// Uses a bigger board: cols=5, rows=6.
    /// </summary>
    private static GameInstance MakeGrid(string character, int blockHp = 3, SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var catalog = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"test\",\"hp\":{blockHp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":5,\"rows\":6," +
            "\"rows_data\":[\"AAA..\",\"AAA..\",\"AAA..\",\".....\",\".....\",\".....\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        var g = new GameInstance(level, cfg, seed: 42);
        g.SetCharacter(character);
        return g;
    }

    /// <summary>Set mana to max and serve the ball so Phase == Playing.</summary>
    private static void MaxManaAndServe(GameInstance g)
    {
        g.ManaValue = g.ManaMaxValue;
        g.Serve();
    }

    // -------------------------------------------------------------------------
    // 1. CastSlot dispatch
    // -------------------------------------------------------------------------

    [Fact]
    public void CastSlot_FireMage_Slot0_CastsIgnite()
    {
        var g = Make("fire_mage");
        MaxManaAndServe(g);
        // Fire mage slot 0 = ignite; result: ball gets imbued on next deflect
        g.CastSlot(0);
        // Drive a paddle hit to confirm ignite was armed (same pattern as SpellTests)
        var ball = g.Balls[0];
        ball.Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2 - ball.Radius - 1);
        ball.Vel = new Vec2(0, 200);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Balls[0].IgniteHitsLeft > 0, "Slot 0 ignite should imbue ball on deflect");
    }

    [Fact]
    public void CastSlot_FireMage_Slot1_CastsFireball()
    {
        var g = Make("fire_mage");
        MaxManaAndServe(g);
        g.CastSlot(1); // fireball
        Assert.Single(g.Projectiles);
    }

    [Fact]
    public void CastSlot_Paladin_Slot0_CastsShield()
    {
        var g = Make("paladin");
        MaxManaAndServe(g);
        g.CastSlot(0); // shield
        Assert.Single(g.Barriers);
    }

    [Fact]
    public void CastSlot_Engineer_Slot0_CastsLightning()
    {
        var g = MakeGrid("engineer", blockHp: 10);
        MaxManaAndServe(g);
        int hpBefore = g.Blocks.Sum(b => b.Hp);
        g.CastSlot(0); // lightning
        int hpAfter = g.Blocks.Sum(b => b.Hp);
        Assert.True(hpAfter < hpBefore, "Lightning should have damaged at least one block");
    }

    [Fact]
    public void CastSlot_Necromancer_Slot0_CastsDecay()
    {
        var g = Make("necromancer");
        MaxManaAndServe(g);
        g.CastSlot(0); // decay → arms decay, ball gets DecayHitsLeft on next deflect
        // Drive a paddle hit to confirm decay was armed
        var ball = g.Balls[0];
        ball.Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2 - ball.Radius - 1);
        ball.Vel = new Vec2(0, 200);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Balls[0].DecayHitsLeft > 0, "Slot 0 decay should imbue ball with decay hits");
    }

    [Fact]
    public void CastSlot_OutOfRange_DoesNothing()
    {
        var g = Make("paladin");
        MaxManaAndServe(g);
        g.CastSlot(99); // no spell at slot 99
        Assert.Empty(g.Barriers);
        Assert.Empty(g.Projectiles);
    }


    // -------------------------------------------------------------------------
    // 11. Mana cost respected across all classes
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("paladin",     0)] // shield
    [InlineData("paladin",     1)] // spear
    [InlineData("paladin",     2)] // duplicate
    [InlineData("engineer",    0)] // lightning
    [InlineData("engineer",    1)] // rocket
    [InlineData("engineer",    2)] // radiation
    [InlineData("necromancer", 1)] // skeleton
    [InlineData("necromancer", 2)] // drain
    public void InsufficientMana_PreventsCast(string character, int slot)
    {
        var g = MakeGrid(character, blockHp: 20);
        g.Serve();
        g.ManaValue = 0;
        int hpBefore   = g.Blocks.Sum(b => b.Hp);
        int ballsBefore = g.Balls.Count(b => b.Alive);

        g.CastSlot(slot);

        // Nothing should have changed: no barriers, zones, projectiles, active effects, or block damage
        Assert.Empty(g.Barriers);
        Assert.Empty(g.Zones);
        Assert.Empty(g.Projectiles);
        Assert.False(g.SkeletonActive);
        Assert.False(g.DrainActive);
        Assert.Equal(hpBefore,    g.Blocks.Sum(b => b.Hp));
        Assert.Equal(ballsBefore, g.Balls.Count(b => b.Alive));
    }
}
