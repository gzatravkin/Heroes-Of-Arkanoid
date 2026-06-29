using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Paladin kit: shield / spear / duplicate (partial of ClassKitTests).</summary>
public partial class ClassKitTests
{

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
    public void Paladin_Shield_ReflectsEnemyBulletUpward()
    {
        // Shield reverted 2026-06-16 to the LEGACY behaviour: it reflects enemy bullets that cross it back
        // UP as the player's own projectiles (which damage blocks) — no longer a pit-save for the ball.
        var g = Make("paladin");
        MaxManaAndServe(g);
        g.CastSlot(0); // shield barrier
        var barrier = g.Barriers[0];
        // An enemy bolt descending onto the barrier line.
        g.Hazards.Add(new Arkanoid.Core.Entities.Projectile {
            Id = 999, Pos = new Vec2(barrier.CenterX, barrier.Y), Vel = new Vec2(0, 300),
            Damage = 1, Radius = 5, Alive = true, Kind = "bolt",
            Behavior = Arkanoid.Core.Entities.HazardBehavior.None,
        });
        g.Tick(SimConfig.Default.FixedDt);
        Assert.DoesNotContain(g.Hazards, h => h.Id == 999 && h.Alive); // the enemy bolt was consumed
        var reflected = g.Projectiles.FirstOrDefault(p => p.Kind == "shieldbolt");
        Assert.NotNull(reflected);
        Assert.True(reflected!.Vel.Y < 0, "the reflected bolt flies upward as a player projectile");
        Assert.True(reflected.Damage > 0, "the reflected bolt damages blocks");
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
        while (elapsed < 4.0 /* barrier lifetime */ + 0.5)
        {
            // Keep ball stationary above paddle
            ball.Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2 - ball.Radius - 2);
            ball.Vel = new Vec2(0, 0);
            g.Tick(SimConfig.Default.FixedDt);
            elapsed += SimConfig.Default.FixedDt;
        }
        Assert.Empty(g.Barriers);
    }


    [Fact]
    public void Paladin_Spear_LaunchesPiercingProjectile()
    {
        // Spear reverted 2026-06-16 to the LEGACY piercing damage projectile (not the Lance of Dawn pillar).
        var g = Make("paladin");
        MaxManaAndServe(g);
        g.CastSlot(1); // Spear
        var spear = g.Projectiles.FirstOrDefault(p => p.Kind == "spear");
        Assert.NotNull(spear);
        Assert.True(spear!.PiercingHitsLeft > 1, "the spear pierces multiple blocks");
        Assert.Empty(g.Pillars); // no Lance pillar anymore
    }

    [Fact]
    public void Paladin_Spear_PiercesAndDamagesMultipleBlocks()
    {
        var g = MakeGrid("paladin", blockHp: 1);
        MaxManaAndServe(g);
        int alive0 = g.Blocks.Count(b => !b.Dead);
        g.CastSlot(1);
        for (int i = 0; i < (int)(2.0 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Blocks.Count(b => !b.Dead) <= alive0 - 2, "the spear punched through more than one block");
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

    [Fact]
    public void Paladin_Duplicate_SpawnsSmallerBalls()
    {
        // docs/01 §61: Duplication clones a ball into N *smaller* balls (not same-size copies).
        var g = Make("paladin");
        MaxManaAndServe(g);
        double srcRadius = g.Balls[0].Radius;
        g.CastSlot(2); // duplicate
        var clone = g.Balls.Last();
        Assert.True(clone.Radius < srcRadius,
            $"Clone radius {clone.Radius} should be smaller than source {srcRadius}");
    }
}
