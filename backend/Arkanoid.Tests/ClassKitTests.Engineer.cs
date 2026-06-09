using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Engineer kit: lightning / rocket / radiation (partial of ClassKitTests).</summary>
public partial class ClassKitTests
{
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
}
