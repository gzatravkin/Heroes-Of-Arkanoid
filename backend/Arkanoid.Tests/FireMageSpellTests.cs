using System.Linq;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Design-fidelity tests for the Fire Mage kit. These assert the *trigger and identity*
/// from docs/specs/2026-06-12-fire-mage-spells.md — not "a projectile spawned".
/// Written to FAIL against the pre-fix (archetype-flattened) implementation.
/// </summary>
public class FireMageSpellTests
{
    private static double Dt => SimConfig.Default.FixedDt;

    private static GameInstance FireMage(int blockHp = 9)
    {
        var g = K.OneBlock(blockHp);
        g.SetCharacter("fire_mage");
        g.Serve();
        g.ManaValue = 100;
        return g;
    }

    private static void Deflect(GameInstance g)
    {
        var p = g.Paddle;
        g.Balls[0].Pos = new Vec2(p.Center.X, p.Center.Y - p.Height / 2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Vec2(0, SimConfig.Default.BallSpeed); // moving down into the paddle
        g.Tick(Dt);
    }

    // ── Turret: fires on paddle catch, not on a timer ───────────────────────────

    [Fact]
    public void Turret_FiresOnPaddleDeflect_NotOnTimer()
    {
        var g = FireMage();
        // Freeze the ball mid-board, far from the paddle — no deflect can occur.
        g.Balls[0].Pos = new Vec2(g.Paddle.Center.X, g.Level.Grid.Height * 0.5);
        g.Balls[0].Vel = new Vec2(0, 0);

        g.CastTurret();
        Assert.True(g.TurretActive);

        // Run 3 seconds with zero paddle contact.
        for (int i = 0; i < (int)(3.0 / Dt); i++) g.Tick(Dt);
        Assert.Equal(0, g.Projectiles.Count(p => p.Kind == "turret"));   // RED today: timer auto-fires

        // A single paddle deflect must produce a bolt.
        int before = g.Projectiles.Count(p => p.Kind == "turret");
        Deflect(g);
        Assert.True(g.Projectiles.Count(p => p.Kind == "turret") > before,
            "turret should fire a bolt on paddle deflect");
    }

    // ── Ignite (owner redesign 2026-06-16): a SLOW, LIMITED DoT you set then survive ──
    // Identity: an ignite hit LIGHTS a block (no instant kill); it burns 1 dmg every ~7s; the fire
    // CREEPS one block at a time to a chain capped at (SpreadBlocksBase + spell level) blocks.

    [Fact]
    public void Ignite_LightsBlock_BurnsSlowly_NotInstant()
    {
        const string types = "{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}";
        const string level = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":7,\"rows\":6," +
                             "\"rows_data\":[\"AAAAAAA\",\".......\",\".......\",\".......\",\".......\",\".......\"]," +
                             "\"legend\":{\"A\":\"b\"}}";
        var g = K.Game(types, level);
        g.SetCharacter("fire_mage");
        g.Serve();

        Block At(int col) => g.Blocks.First(b => b.Col == col && b.Row == 0);
        var b3 = At(3); // middle of 7

        g.Balls[0].IgniteHitsLeft = 1; // one ignite touch
        K.AimAt(g, b3);
        g.Tick(Dt);

        // LIGHTS, does not instantly destroy (even a 1-hp block).
        Assert.False(b3.Dead, "ignite should light the block, not instantly destroy it");
        Assert.True(b3.BurnRemaining > 0, "the hit block should be burning");

        // Park the ball in an empty cell so only the burn acts (a stationary ball still collides by overlap).
        g.Balls[0].Vel = new Vec2(0, 0);
        g.Balls[0].Pos = g.Level.Grid.CellCenter(3, 4);

        // Still smouldering after 1s — the burn is slow (~7s per damage).
        for (int i = 0; i < (int)(1.0 / Dt); i++) g.Tick(Dt);
        Assert.False(b3.Dead, "a 1-hp block should still be burning after 1s (slow DoT)");

        // It burns down after a full burn interval (~7s).
        for (int i = 0; i < (int)(7.5 / Dt); i++) g.Tick(Dt);
        Assert.True(b3.Dead, "the block should burn down after ~7s");

        // Spread is LIMITED: a level-1 seed reaches at most SpreadBlocksBase + 1 = 3 blocks, so the
        // 7-wide row is never fully consumed by one ignite. Run long enough for any creep + burn.
        for (int i = 0; i < (int)(60.0 / Dt); i++) g.Tick(Dt);
        int alive = g.Blocks.Count(b => !b.Dead);
        Assert.True(alive >= 4, $"ignite chain should be capped (~3 blocks); {7 - alive} died, expected <=3");
    }

    [Fact]
    public void Ignite_BurnDamage_CappedByLevel()
    {
        // A single tanky block: at spell level 1 the burn cap is min(6, base2+1)=3, so it survives.
        const string types = "{\"id\":\"b\",\"biome\":\"t\",\"hp\":10,\"sprite\":\"s\",\"needToKill\":true}";
        const string level = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":1,\"rows\":6," +
                             "\"rows_data\":[\"A\",\".\",\".\",\".\",\".\",\".\"],\"legend\":{\"A\":\"b\"}}";
        var g = K.Game(types, level);
        g.SetCharacter("fire_mage");
        g.Serve();
        var blk = g.Blocks[0];

        g.Balls[0].IgniteHitsLeft = 1;
        K.AimAt(g, blk);
        g.Tick(Dt);
        // Park the ball in an empty cell so only the burn acts.
        g.Balls[0].Vel = new Vec2(0, 0);
        g.Balls[0].Pos = g.Level.Grid.CellCenter(0, 4);

        // Let the whole burn run out (well past 3 × 7s).
        for (int i = 0; i < (int)(40.0 / Dt); i++) g.Tick(Dt);
        Assert.False(blk.Dead, "a 10-hp block must survive a level-1 ignite (burn is capped)");
        Assert.Equal(7, blk.Hp); // 10 − 3 burn ticks
    }

    // ── Phoenix: a visible entity that orbits a ball and burns blocks ───────────

    [Fact]
    public void Phoenix_IsAnEntity_ThatOrbitsTheBall_NotAnInvisibleAura()
    {
        var g = FireMage();
        var ball = g.Balls[0];
        // Freeze the ball so the only motion is the phoenix orbiting it.
        ball.Pos = new Vec2(g.Paddle.Center.X, g.Level.Grid.Height * 0.5);
        ball.Vel = new Vec2(0, 0);

        g.CastSlot(4); // phoenix is slot 4 in the fire_mage kit
        Assert.Single(g.Phoenixes);

        var ph = g.Phoenixes[0];
        g.Tick(Dt);
        // Identity: the phoenix has its OWN position, orbiting at a radius from the ball.
        Assert.True((ph.Pos - ball.Pos).Length > ball.Radius,
            $"phoenix should orbit at a distance from the ball, was {(ph.Pos - ball.Pos).Length:F1}");

        // It moves as it orbits — position changes between ticks.
        var p0 = ph.Pos;
        for (int i = 0; i < 10; i++) g.Tick(Dt);
        Assert.True((ph.Pos - p0).Length > 1, "phoenix should move along its orbit");
    }

    [Fact]
    public void Phoenix_BurnsBlocksItSweepsOver()
    {
        // A ring of blocks around an empty centre; the ball sits at the centre so the
        // orbiting phoenix sweeps over the ring and damages it (the ball never touches them).
        const string types = "{\"id\":\"b\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true}";
        const string level = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":5,\"rows\":5," +
            "\"rows_data\":[\".....\",\".AAA.\",\".A.A.\",\".AAA.\",\".....\"],\"legend\":{\"A\":\"b\"}}";
        var g = K.Game(types, level);
        g.SetCharacter("fire_mage");
        g.Serve();
        g.ManaValue = 100;

        // Park the ball at the empty centre cell (2,2).
        var centre = g.Level.Grid.CellCenter(2, 2);
        g.Balls[0].Pos = new Vec2(centre.X, centre.Y);
        g.Balls[0].Vel = new Vec2(0, 0);

        int hpBefore = g.Blocks.Where(b => !b.Dead).Sum(b => b.Hp);
        g.CastSlot(4); // phoenix
        Assert.Single(g.Phoenixes);

        // Run a couple of full orbits.
        for (int i = 0; i < (int)(3.0 / Dt); i++) g.Tick(Dt);
        int hpAfter = g.Blocks.Where(b => !b.Dead).Sum(b => b.Hp);
        Assert.True(hpAfter < hpBefore,
            $"the orbiting phoenix should burn the surrounding ring ({hpBefore} → {hpAfter})");
    }

    // ── Conflagration — requires burning blocks; fizzles on bare cast ───────────

    [Fact]
    public void Conflagration_WithNoBurningBlocks_FizzlesAndSpendsNoMana()
    {
        // Design: Conflagration is "Ignite then detonate." With no fire on the board it must fizzle —
        // no mana spent, no damage — forcing the player to use Ignite first.
        const string types = "{\"id\":\"b\",\"biome\":\"t\",\"hp\":20,\"sprite\":\"s\",\"needToKill\":true}";
        const string level = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3," +
            "\"rows_data\":[\"AA.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}";
        var g = K.Game(types, level);
        g.SetCharacter("fire_mage");
        g.Serve();
        g.ManaValue = 100;

        var a = g.Blocks.First(b => b.Col == 0 && b.Row == 0);
        int hpBefore = a.Hp;

        g.CastFireball();
        g.Tick(Dt);

        Assert.Empty(g.Projectiles);                        // still never a projectile
        Assert.Equal(100, (int)g.ManaValue);               // no mana spent on fizzle
        Assert.Equal(hpBefore, a.Hp);                      // no damage dealt
    }

    [Fact]
    public void Conflagration_WithFire_DetonatesEveryBurningBlock_BoardWide_NotJustNearTheBall()
    {
        // The "ignite then detonate" payoff: when blocks are on fire, Conflagration bursts ALL of them,
        // anywhere on the board — even the one far from the ball (proving it's not just the local burst).
        const string types = "{\"id\":\"b\",\"biome\":\"t\",\"hp\":20,\"sprite\":\"s\",\"needToKill\":true}";
        const string level = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":7,\"rows\":3," +
            "\"rows_data\":[\"A.....A\",\".......\",\".......\"],\"legend\":{\"A\":\"b\"}}";
        var g = K.Game(types, level);
        g.SetCharacter("fire_mage");
        g.Serve();
        g.ManaValue = 100;

        var left  = g.Blocks.First(b => b.Col == 0);
        var right = g.Blocks.First(b => b.Col == 6);
        // Ball parked far from BOTH so only the "burning" detonation path can reach the far block.
        g.Balls[0].Pos = g.Level.Grid.CellCenter(3, 2);
        g.Balls[0].Vel = new Vec2(0, 0);

        left.BurnRemaining = 5.0;
        right.BurnRemaining = 5.0;
        int lBefore = left.Hp, rBefore = right.Hp;
        g.CastFireball();
        g.Tick(Dt);
        Assert.True(left.Hp < lBefore && right.Hp < rBefore,
            "every burning block detonates, even the one far from the ball");
        // Fire is fully consumed — no blocks remain burning so a second cast would fizzle.
        Assert.Equal(0, g.Blocks.Count(b => !b.Dead && b.BurnRemaining > 0));
    }

    // ── Fire Wall (reverted 2026-06-16 to LEGACY: arm a ball → next block hit ignites an AREA) ──

    [Fact]
    public void FireWall_ArmsBall_IgnitesAreaOnNextBlockHit()
    {
        const string types = "{\"id\":\"b\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true}";
        const string level = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":5,\"rows\":5," +
            "\"rows_data\":[\"AAAAA\",\"AAAAA\",\".....\",\".....\",\".....\"],\"legend\":{\"A\":\"b\"}}";
        var g = K.Game(types, level);
        g.SetCharacter("fire_mage");
        g.Serve();
        g.ManaValue = 100;
        g.CastFireWall();
        Assert.True(g.Balls[0].FireWallArmed, "fire wall arms the ball");

        // The ball's next block hit ignites an AREA of blocks (they burn over time via BurnSystem).
        var target = g.Blocks.First(b => b.Col == 2 && b.Row == 1);
        K.AimAt(g, target);
        g.Tick(Dt);
        Assert.False(g.Balls[0].FireWallArmed, "the arm is consumed on the next block hit");
        int burning = g.Blocks.Count(b => !b.Dead && b.BurnRemaining > 0);
        Assert.True(burning >= 3, $"fire wall should ignite an AREA of blocks on the hit; only {burning} burning");
    }

    [Fact]
    public void FireWall_NoMana_DoesNotArm()
    {
        var g = FireMage();
        g.ManaValue = 0;
        g.CastFireWall();
        Assert.False(g.Balls[0].FireWallArmed);
    }
}
