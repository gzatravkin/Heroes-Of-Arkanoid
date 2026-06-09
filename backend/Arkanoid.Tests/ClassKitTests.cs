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
public class ClassKitTests
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

    // -------------------------------------------------------------------------
    // 5. Engineer — Lightning
    // -------------------------------------------------------------------------

    [Fact]
    public void Engineer_Lightning_DamagesMultipleBlocks()
    {
        var g = MakeGrid("engineer", blockHp: 20);
        MaxManaAndServe(g);
        int hpBefore = g.Blocks.Sum(b => b.Hp);
        g.CastSlot(0); // lightning
        int hpAfter = g.Blocks.Sum(b => b.Hp);
        // Total damage = (1 + chain jumps) × damage per hit minimum
        int minExpectedDamage = SimConfig.Default.LightningDamage;
        Assert.True(hpBefore - hpAfter >= minExpectedDamage,
            $"Lightning should deal ≥{minExpectedDamage} total damage; was {hpBefore - hpAfter}");
    }

    [Fact]
    public void Engineer_Lightning_NoMana_NoDamage()
    {
        var g = MakeGrid("engineer", blockHp: 10);
        g.Serve();
        g.ManaValue = 0;
        int hpBefore = g.Blocks.Sum(b => b.Hp);
        g.CastSlot(0);
        Assert.Equal(hpBefore, g.Blocks.Sum(b => b.Hp));
    }

    [Fact]
    public void Engineer_Lightning_RaisesLightningEvents()
    {
        var g = MakeGrid("engineer", blockHp: 20);
        MaxManaAndServe(g);
        g.CastSlot(0);
        var events = g.DrainEvents();
        Assert.Contains(events, e => e.Type == "lightning");
    }

    // -------------------------------------------------------------------------
    // 6. Engineer — Rocket
    // -------------------------------------------------------------------------

    [Fact]
    public void Engineer_Rocket_SpawnsHomingProjectile()
    {
        var g = Make("engineer");
        MaxManaAndServe(g);
        g.CastSlot(1); // rocket
        Assert.Single(g.Projectiles);
        var rocket = g.Projectiles[0];
        Assert.Equal("rocket", rocket.Kind);
        Assert.True(rocket.Homing);
        Assert.True(rocket.AoeRadius > 0);
    }

    [Fact]
    public void Engineer_Rocket_DamagesBlockAndNeighbors()
    {
        var g = MakeGrid("engineer", blockHp: 5);
        MaxManaAndServe(g);
        g.CastSlot(1); // rocket
        int hpBefore = g.Blocks.Sum(b => b.Hp);

        // Advance until rocket hits something (or timeout at 5 seconds sim-time)
        int maxTicks = (int)(SimConfig.Default.TickHz * 5);
        for (int i = 0; i < maxTicks && g.Projectiles.Any(p => p.Kind == "rocket"); i++)
            g.Tick(SimConfig.Default.FixedDt);

        int hpAfter = g.Blocks.Sum(b => b.Hp);
        Assert.True(hpAfter < hpBefore, "Rocket should have damaged at least one block");
    }

    [Fact]
    public void Engineer_Rocket_NoMana_NoProjectile()
    {
        var g = Make("engineer");
        g.Serve();
        g.ManaValue = 0;
        g.CastSlot(1);
        Assert.Empty(g.Projectiles);
    }

    // -------------------------------------------------------------------------
    // 7. Engineer — Radiation
    // -------------------------------------------------------------------------

    [Fact]
    public void Engineer_Radiation_SpawnsZone()
    {
        var g = Make("engineer");
        MaxManaAndServe(g);
        g.CastSlot(2); // radiation
        Assert.Single(g.Zones);
    }

    [Fact]
    public void Engineer_Radiation_DamagesBlocksInAreaOverTime()
    {
        // Use a config where the block is definitely within the radiation radius
        var cfg = new SimConfig { RadiationRadius = 400 }; // large enough to hit the test block
        var g = Make("engineer", blockHp: 5, cfg: cfg);
        g.ManaValue = g.ManaMaxValue;
        g.Serve();
        g.CastSlot(2);
        int hpBefore = g.Blocks.Sum(b => b.Hp);
        // Run past one damage interval
        for (int i = 0; i < (int)(cfg.TickHz * cfg.RadiationDamageInterval) + 5; i++)
            g.Tick(cfg.FixedDt);
        int hpAfter = g.Blocks.Sum(b => b.Hp);
        Assert.True(hpAfter < hpBefore, $"Radiation should damage blocks in its area; hp {hpBefore}→{hpAfter}");
    }

    [Fact]
    public void Engineer_Radiation_ExpiresAfterLifetime()
    {
        var g = Make("engineer");
        MaxManaAndServe(g);
        g.CastSlot(2);
        Assert.Single(g.Zones);
        var ball = g.Balls[0];
        double elapsed = 0;
        while (elapsed < SimConfig.Default.RadiationLifetime + 0.5)
        {
            // Park ball so Phase stays Playing
            ball.Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2 - ball.Radius - 2);
            ball.Vel = new Vec2(0, 0);
            g.Tick(SimConfig.Default.FixedDt);
            elapsed += SimConfig.Default.FixedDt;
        }
        Assert.Empty(g.Zones);
    }

    [Fact]
    public void Engineer_Radiation_NoMana_NoZone()
    {
        var g = Make("engineer");
        g.Serve();
        g.ManaValue = 0;
        g.CastSlot(2);
        Assert.Empty(g.Zones);
    }

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
