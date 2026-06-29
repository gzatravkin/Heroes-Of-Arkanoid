using Arkanoid.Core.Blocks;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Engineer kit: lightning / rocket / radiation (partial of ClassKitTests).</summary>
public partial class ClassKitTests
{

    [Fact]
    public void Engineer_Lightning_DamagesMultipleBlocks()
    {
        var g = MakeGrid("engineer", blockHp: 20);
        MaxManaAndServe(g);
        int hpBefore = g.Blocks.Sum(b => b.Hp);
        g.CastSlot(0); // lightning
        int hpAfter = g.Blocks.Sum(b => b.Hp);
        // Total damage = (1 + chain jumps) × damage per hit minimum
        int minExpectedDamage = 2; // lightning damage per hit
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
        Assert.Contains(events, e => e.Kind == SimEventKind.Lightning);
    }


    [Fact]
    public void Engineer_Lightning_StrikesBlockNearestToBall()
    {
        // Design (tasks list.md): lightning strikes the block nearest to the live ball, not a random one.
        // Place one block far from the ball (col=0) and one close (col=4), ball positioned near col=4.
        var cfg = SimConfig.Default;
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"test\",\"hp\":10,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":5,\"rows\":2," +
            "\"rows_data\":[\"A...A\",\".....\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        var g = new GameInstance(level, cfg, seed: 1);
        g.SetCharacter("engineer");
        g.Serve();
        g.ManaValue = g.ManaMaxValue;

        // Block[0] at col=0, Block[1] at col=4.
        // Position the ball right below the block at col=4 so it's nearest.
        var closeBlk = g.Blocks.First(b => b.Col == 4);
        var closeCenter = level.Grid.CellCenter(closeBlk.Col, closeBlk.Row);
        g.Balls[0].Pos = new Vec2(closeCenter.X, closeCenter.Y + cfg.CellSize * 2);

        int hpFar   = g.Blocks.First(b => b.Col == 0).Hp;
        int hpClose = closeBlk.Hp;
        g.CastSlot(0); // lightning
        Assert.True(closeBlk.Hp < hpClose, "lightning should hit the block nearest to the ball first");
        Assert.Equal(hpFar, g.Blocks.First(b => b.Col == 0).Hp); // far block untouched by initial strike
    }

    // Rocket reverted 2026-06-16 to the LEGACY piloted homing DAMAGE missile: a projectile that homes to
    // the nearest block and explodes (AoE). No longer the no-damage Concussion Charge.
    [Fact]
    public void Engineer_Rocket_LaunchesHomingDamageProjectile()
    {
        var g = MakeGrid("engineer", blockHp: 5);
        MaxManaAndServe(g);
        g.CastSlot(1); // Rocket
        var rocket = g.Projectiles.FirstOrDefault(p => p.Kind == "rocket");
        Assert.NotNull(rocket);                       // it IS a projectile now
        Assert.True(rocket!.Homing, "the rocket homes");
        Assert.True(rocket.Damage > 0, "the rocket deals damage (not the no-damage Concussion)");
    }

    [Fact]
    public void Engineer_Rocket_HomesIn_AndExplodesDamagingBlocks()
    {
        var g = MakeGrid("engineer", blockHp: 9);
        MaxManaAndServe(g);
        int hp0 = g.Blocks.Sum(b => b.Hp);
        g.CastSlot(1);
        for (int i = 0; i < (int)(3.0 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Blocks.Sum(b => b.Hp) < hp0, "the rocket homed in and exploded, damaging blocks");
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


    [Fact]
    public void Engineer_Rocket_PrioritizesHighHpBlockOverNearestLowHp()
    {
        // Design (tasks list.md): rocket targets highest-HP block in priority tier 4, not just nearest.
        // Two blocks: one near the rocket (low HP) and one far (high HP). Rocket should home to far one.
        var cfg = SimConfig.Default;
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[" +
            "{\"id\":\"near\",\"biome\":\"test\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}," +
            "{\"id\":\"far\",\"biome\":\"test\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true}" +
            "]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":5,\"rows\":2," +
            "\"rows_data\":[\"A...B\",\".....\"],\"legend\":{\"A\":\"near\",\"B\":\"far\"}}",
            catalog);
        var g = new GameInstance(level, cfg, seed: 1);
        g.SetCharacter("engineer");
        g.Serve();
        g.ManaValue = g.ManaMaxValue;
        g.CastSlot(1); // rocket
        var rocket = g.Projectiles.First(p => p.Kind == "rocket");
        // Run rocket for a few ticks and check it steers toward the high-HP block (right side)
        for (int i = 0; i < 10; i++) g.Tick(cfg.FixedDt);
        // The rocket should be moving RIGHT (toward high-HP block at col=4)
        Assert.True(rocket.Vel.X > 0, $"rocket should steer right (toward high-HP block), vx={rocket.Vel.X:F1}");
    }

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
        // Block at row 3 (last row): CellCenter.Y ~112, zone spawns at ~144 → distance ~32 < radius 80.
        var catalog2 = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"test\",\"hp\":5,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level2 = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":4," +
            "\"rows_data\":[\"...\",\"...\",\"...\",\".A.\"],\"legend\":{\"A\":\"b\"}}",
            catalog2);
        var g = new GameInstance(level2, SimConfig.Default, seed: 42);
        g.SetCharacter("engineer");
        g.ManaValue = g.ManaMaxValue;
        g.Serve();
        g.CastSlot(2);
        int hpBefore = g.Blocks.Sum(b => b.Hp);
        // Run past one damage interval (0.5s)
        for (int i = 0; i < (int)(SimConfig.Default.TickHz * 0.5 /* radiation damage interval */) + 5; i++)
            g.Tick(SimConfig.Default.FixedDt);
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
        // Keep blocks unkillable so the (now wider) zone can't clear the board and end the level — this test
        // isolates ZONE EXPIRY, and a Won level freezes the sim so the zone would never tick down.
        foreach (var b in g.Blocks) { b.MaxHp = 9999; b.Hp = 9999; }
        var ball = g.Balls[0];
        double elapsed = 0;
        while (elapsed < 4.0 /* radiation lifetime */ + 0.5)
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
