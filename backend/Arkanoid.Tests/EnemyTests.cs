using Arkanoid.Core.Blocks;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

public class EnemyTests
{
    private static GameInstance Make(string blocksJson, string levelJson) => K.FullGame(blocksJson, levelJson);
    private static void Park(GameInstance g)   => K.Park(g);
    private static void BallHit(GameInstance g, Block target) => K.Hit(g, target);


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

    [Fact]
    public void Emitter_FiresStraightDownLane_DoesNotHomeOnPaddle()
    {
        // Level-UX rework (2026-06-15, Option 1 "fair projectiles"): even an emitAim="paddle" emitter must
        // fire STRAIGHT DOWN its own column — a predictable, dodgeable lane — not lean toward the paddle.
        var g = Make(
            "{\"types\":[{\"id\":\"e\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":0.5,\"emitAim\":\"paddle\"}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":5,\"rows\":5,\"rows_data\":[\"E....\",\".....\",\".....\",\".....\",\".....\"],\"legend\":{\"E\":\"e\"}}");
        g.SetPaddleX(5 * 32);                        // park the paddle far to the RIGHT of the emitter (col 0)
        for (int i = 0; i < 40; i++) g.Tick(0.016);
        Assert.NotEmpty(g.Hazards);
        var h = g.Hazards[0];
        Assert.Equal(0, h.Vel.X, precision: 3);      // no horizontal lean toward the paddle
        Assert.True(h.Vel.Y > 0, "still descends");
    }


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


    [Fact]
    public void Cart_LaunchesHorizontalHazard_AfterInterval()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"cart\"}," +
            "{\"id\":\"z\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":8,\"rows\":3,\"rows_data\":[\"k......z\",\"........\",\"........\"],\"legend\":{\"k\":\"k\",\"z\":\"z\"}}");
        Park(g);
        for (int i = 0; i < (int)(SimConfig.Default.Enemies.CartInterval / SimConfig.Default.FixedDt) + 2; i++)
            g.Tick(SimConfig.Default.FixedDt);
        var cart = g.Hazards.FirstOrDefault(h => h.Kind == "cart");
        Assert.NotNull(cart);
        Assert.True(System.Math.Abs(cart!.Vel.X) > 0 && cart.Vel.Y == 0, "cart rolls horizontally");
    }


    [Fact]
    public void Lava_BallPassesThrough_AndDestroysIt_OnContact()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"l\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"lava\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"l.k\",\"...\",\"...\"],\"legend\":{\"l\":\"l\",\"k\":\"k\"}}");
        int sparesBefore = g.SpareBalls;
        var lava = g.Blocks[0];
        BallHit(g, lava); // ball passes THROUGH lava (no bounce, no spare) and DESTROYS it (2026-06-15)
        Assert.Equal(sparesBefore, g.SpareBalls);
        Assert.True(lava.Dead, "the ball destroys the lava cell it flies over");
    }


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


    [Fact]
    public void EnemyBlockKill_AlwaysDropsBonus_PlainBrickStaysRandom()
    {
        var blocksJson =
            "{\"types\":[" +
            "{\"id\":\"e\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":99,\"emitAim\":\"down\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}";
        var levelJson =
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"e.k\",\"...\",\"...\"],\"legend\":{\"e\":\"e\",\"k\":\"k\"}}";
        var catalog = BlockCatalog.FromJson(blocksJson);
        var level   = LevelLoader.FromJson(levelJson, catalog);
        var bonuses = Arkanoid.Core.Bonuses.BonusCatalog.FromJson(
            "{\"bonuses\":[{\"id\":\"coins\",\"name\":\"Treasure\",\"icon\":\"i\",\"effect\":\"coins\"}]}");
        // DropChance 0 → any drop from the enemy must come from the guaranteed path.
        var g = new GameInstance(level, new SimConfig { Pickups = new() { DropChance = 0 } }, seed: 1, bonuses: bonuses);
        g.Serve();

        var emitter = g.Blocks[0];
        var brick   = g.Blocks[1];
        BallHit(g, emitter); // kill the emitter
        Assert.True(emitter.Dead);
        Assert.Single(g.Bonuses); // guaranteed drop

        BallHit(g, brick); // kill the plain brick
        Assert.True(brick.Dead);
        Assert.Single(g.Bonuses); // still 1 — plain bricks keep the random roll (chance 0 here)
    }


    [Fact]
    public void Snapshot_FlagsEmitterCharging_OnlyInsideTelegraphWindow()
    {
        var g = Make(
            "{\"types\":[{\"id\":\"e\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":1.0,\"emitAim\":\"down\"}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".E.\",\"...\",\"...\"],\"legend\":{\"E\":\"e\"}}");
        Park(g);

        // 0.3s into a 1.0s interval (0.5s window) → not charging yet.
        for (int i = 0; i < 18; i++) g.Tick(1.0 / 60);
        Assert.False(Arkanoid.Core.Net.Snapshot.From(g, 0).Blocks[0].Charging);

        // 0.7s in → inside the 0.5s telegraph window.
        for (int i = 0; i < 24; i++) g.Tick(1.0 / 60);
        Assert.True(Arkanoid.Core.Net.Snapshot.From(g, 0).Blocks[0].Charging);
    }

    [Fact]
    public void Snapshot_FlagsAlliedStatue_AfterAltarHit()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"a\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"altar\"}," +
            "{\"id\":\"m\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":0.5,\"emitAim\":\"paddle\"}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"a.m\",\"...\",\"...\"],\"legend\":{\"a\":\"a\",\"m\":\"m\"}}");
        Assert.False(Arkanoid.Core.Net.Snapshot.From(g, 0).Blocks[1].Allied);
        BallHit(g, g.Blocks[0]);
        Assert.True(Arkanoid.Core.Net.Snapshot.From(g, 0).Blocks[1].Allied);
    }


    [Fact]
    public void ReviverKilled_BeforeReviveTimer_RaisesReviveCancelled()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"n\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"Reviver\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            // third needToKill block keeps the level un-won so the sim keeps ticking
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"n.k\",\"..k\",\"...\"],\"legend\":{\"n\":\"n\",\"k\":\"k\"}}");
        var reviver = g.Blocks[0];
        var brick   = g.Blocks[1];

        BallHit(g, brick);                  // dies, gets death-marked
        Assert.True(brick.Dead);
        BallHit(g, reviver);                // kill the Reviver before the revive fires
        Assert.True(reviver.Dead);
        Park(g);
        for (int i = 0; i < (int)(SimConfig.Default.Enemies.ReviveDelay / SimConfig.Default.FixedDt) + 3; i++)
            g.Tick(SimConfig.Default.FixedDt);

        Assert.True(brick.Dead, "block stays dead once the Reviver is gone");
        var events = Arkanoid.Core.Net.Snapshot.From(g, 0).Events;
        Assert.Contains(events, e => e.Type == "reviveCancelled");
    }


    private const string BatBlocksJson =
        "{\"types\":[" +
        "{\"id\":\"v\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"behavior\":\"bat\"}," +
        "{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}";
    private const string BatLevelJson =
        "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":6,\"rows_data\":[\"v.k\",\"...\",\"...\",\"...\",\"...\",\"...\"],\"legend\":{\"v\":\"v\",\"k\":\"k\"}}";

    [Fact]
    public void Bat_HoldsBall_ThenReleasesAndRewardsWidePaddle()
    {
        // Bat reverted 2026-06-16 to the LEGACY reward: it HOLDS the ball briefly, then releases it AND
        // grants a wide-paddle buff — a risk→reward, NOT a drain threat.
        var g = Make(BatBlocksJson, BatLevelJson);
        var bat    = g.Blocks[0];
        int spares = g.SpareBalls;

        BallHit(g, bat);
        Assert.True(bat.Dead, "bat block becomes the carrier");
        var carrier = g.Hazards.FirstOrDefault(h => h.Kind == "bat" && h.CarriedBallId == g.Balls[0].Id);
        Assert.NotNull(carrier);
        Assert.True(carrier!.Vel.Y < 0, "the bat hovers (does NOT drag the ball toward the drain)");
        Assert.Equal(0, carrier.Damage);
        var heldBall = g.Balls[0];

        // Run just past the hold time: the ball is released alive (no spare lost) + a wide paddle is granted.
        for (int i = 0; i < (int)(3.3 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(heldBall.Alive, "the held ball is released, not drained");
        Assert.Equal(0, heldBall.GrabberId);
        Assert.Equal(spares, g.SpareBalls);                 // no spare consumed — the bat is not a threat
        Assert.True(g.WidePaddleActive, "holding the bat the full time rewards a wide paddle");
    }

    [Fact]
    public void Bat_Carrier_PoppedBySecondBall_RescuesTheBall()
    {
        var g = Make(BatBlocksJson, BatLevelJson);
        var bat = g.Blocks[0];
        BallHit(g, bat);
        var carrier = g.Hazards.First(h => h.Kind == "bat" && h.CarriedBallId > 0);

        // A second ball overlapping the carrier pops it.
        g.Balls.Add(new Ball
        {
            Id = 999, Radius = SimConfig.Default.BallRadius,
            Pos = carrier.Pos, Vel = new Arkanoid.Core.Math.Vec2(0, 0), Alive = true,
        });
        g.Tick(SimConfig.Default.FixedDt);

        Assert.DoesNotContain(g.Hazards, h => h.Kind == "bat" && h.CarriedBallId > 0);
        Assert.Equal(0, g.Balls[0].GrabberId);
        Assert.True(g.Balls[0].Vel.Y < 0, "rescued ball is released upward");
        // The bat flees as the harmless flyaway.
        Assert.Contains(g.Hazards, h => h.Kind == "bat" && h.CarriedBallId == 0 && h.Vel.Y < 0);
    }


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


    [Fact]
    public void AlliedMeleeStatue_FiresAllyBolts_ThatDamageBlocks()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"a\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"altar\"}," +
            "{\"id\":\"m\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":0.3,\"emitAim\":\"paddle\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":4,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":5,\"rows\":5,\"rows_data\":[\"a.m.k\",\".....\",\".....\",\".....\",\".....\"],\"legend\":{\"a\":\"a\",\"m\":\"m\",\"k\":\"k\"}}");
        var brick = g.Blocks[2];
        int hpBefore = brick.Hp;

        BallHit(g, g.Blocks[0]); // ally via altar
        Park(g);
        // Past the cadence: the allied statue shoots BLOCKS (Projectiles), not the paddle (Hazards).
        for (int i = 0; i < 120 && brick.Hp == hpBefore; i++) g.Tick(SimConfig.Default.FixedDt);

        Assert.Empty(g.Hazards);
        Assert.True(brick.Hp < hpBefore, "ally bolt damaged the brick");
    }

    [Fact]
    public void AlliedShieldStatue_CorruptsNeighbours_InsteadOfShielding()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"a\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"altar\"}," +
            "{\"id\":\"d\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"shieldStatue\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":4,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":4,\"rows\":4,\"rows_data\":[\"a.dk\",\"....\",\"....\",\"....\"],\"legend\":{\"a\":\"a\",\"d\":\"d\",\"k\":\"k\"}}");
        var brick = g.Blocks[2];
        int hpBefore = brick.Hp;

        BallHit(g, g.Blocks[0]); // ally via altar
        Park(g);
        var pulseTicks = (int)(SimConfig.Default.Enemies.ShieldStatueInterval / SimConfig.Default.FixedDt) + 5;
        for (int i = 0; i < pulseTicks; i++) g.Tick(SimConfig.Default.FixedDt);

        Assert.True(brick.Hp < hpBefore, "allied shield statue corrupts (damages) its neighbours");
        Assert.True(brick.ImmunityTimer <= 0, "no shield granted while allied");
    }

    [Fact]
    public void VaseBreak_LevelsStatues_FasterFire_AndBiggerKillReward()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"v\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"vase\"}," +
            "{\"id\":\"m\",\"biome\":\"t\",\"hp\":2,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":1.0,\"emitAim\":\"paddle\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":5,\"rows\":5,\"rows_data\":[\"v.m.k\",\".....\",\".....\",\".....\",\".....\"],\"legend\":{\"v\":\"v\",\"m\":\"m\",\"k\":\"k\"}}");
        var statue = g.Blocks[1];

        BallHit(g, g.Blocks[0]); // break the vase
        Assert.Equal(1, statue.StatueLevel);

        // Faster fire: interval 1.0 / (1 + 0.35) ≈ 0.74s → a hazard exists by 0.9s.
        Park(g);
        for (int i = 0; i < (int)(0.9 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.NotEmpty(g.Hazards);

        // Bigger reward: killing the levelled statue pays bonus mana.
        g.ManaValue = 0;
        BallHit(g, statue);
        BallHit(g, statue);
        Assert.True(statue.Dead);
        Assert.True(g.ManaValue >= SimConfig.Default.Enemies.VaseKillManaPerLevel,
            $"levelled statue kill must pay bonus mana (got {g.ManaValue})");
    }


    [Fact]
    public void FallingStalactite_DamagesBlocksItPassesThrough()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"s\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"stalactite\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":4,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":6,\"rows_data\":[\"s..\",\"...\",\"k..\",\"...\",\"...\",\"...\"],\"legend\":{\"s\":\"s\",\"k\":\"k\"}}");
        var brick = g.Blocks[1];
        int hpBefore = brick.Hp;

        // Park the ball under the stalactite's column to trigger the drop.
        var sc = g.Level.Grid.CellCenter(g.Blocks[0].Col, g.Blocks[0].Row);
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(sc.X, sc.Y + SimConfig.Default.CellSize * 4);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, 0);
        // It shakes (telegraph) for StalactiteArmDelay, THEN detaches into a falling hazard.
        for (int i = 0; i < 40 && !g.Hazards.Any(h => h.Kind == "stalactite"); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.Contains(g.Hazards, h => h.Kind == "stalactite");

        // The falling spike smashes the brick beneath on its way down.
        for (int i = 0; i < 240 && brick.Hp == hpBefore; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(brick.Hp < hpBefore, "stalactite damaged the block it fell through");
    }


    [Fact]
    public void Cauldron_SiphonsMana_WhileAlive_AndRefundsOnKill()
    {
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[" +
            "{\"id\":\"c\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"cauldron\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"c.k\",\"...\",\"...\"],\"legend\":{\"c\":\"c\",\"k\":\"k\"}}",
            catalog);
        // Regen off so the siphon is observable in isolation.
        var g = new GameInstance(level, new SimConfig { ManaRegenPerSec = 0 }, seed: 1);
        g.Serve();
        var cauldron = g.Blocks[0];
        g.ManaValue = 50;
        Park(g);

        // 2 seconds of siphon at the configured rate.
        for (int i = 0; i < (int)(2.0 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        var expectedSiphon = 2.0 * SimConfig.Default.Enemies.CauldronSiphonPerSec;
        Assert.True(g.ManaValue < 50, "cauldron siphons the player's mana");
        Assert.True(cauldron.StoredMana > expectedSiphon * 0.9, $"stored {cauldron.StoredMana:F1}");

        // Killing it refunds everything it stole.
        var manaBeforeKill = g.ManaValue;
        BallHit(g, cauldron);
        Assert.True(cauldron.Dead);
        Assert.True(g.ManaValue >= manaBeforeKill + cauldron.StoredMana * 0.9,
            $"refund expected: before={manaBeforeKill:F1} after={g.ManaValue:F1}");
    }


    [Fact]
    public void LavaSpawner_CreepsLava_AndRetractsWhenKilled()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"s\",\"biome\":\"t\",\"hp\":2,\"sprite\":\"s\",\"needToKill\":false,\"behavior\":\"lavaSpawner\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":5,\"rows\":5,\"rows_data\":[\"....k\",\"..S..\",\".....\",\".....\",\".....\"],\"legend\":{\"S\":\"s\",\"k\":\"k\"}}");
        var spawner = g.Blocks.First(b => b.LavaSpawner);
        Park(g);

        // Lava only flows after the spawner takes its first hit (hp 2→1).
        BallHit(g, spawner);
        Assert.Equal(1, spawner.Hp); // not dead, just activated
        Park(g); // re-park so the ball doesn't bounce back and kill the spawner

        // Two creep intervals → two lava cells owned by the spawner.
        var creepTicks = (int)(SimConfig.Default.Enemies.LavaCreepInterval / SimConfig.Default.FixedDt) + 2;
        for (int i = 0; i < creepTicks * 2; i++) g.Tick(SimConfig.Default.FixedDt);
        var crept = g.Blocks.Where(b => !b.Dead && b.Lava && b.OwnerId == spawner.Id).ToList();
        Assert.True(crept.Count >= 2, $"expected ≥2 crept lava cells, got {crept.Count}");

        // The cap holds.
        for (int i = 0; i < creepTicks * 10; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Blocks.Count(b => !b.Dead && b.Lava && b.OwnerId == spawner.Id)
            <= SimConfig.Default.Enemies.LavaCreepMax, "creep cap respected");

        // Counterplay: kill the spawner → its lava retracts.
        BallHit(g, spawner);
        Assert.True(spawner.Dead);
        Assert.DoesNotContain(g.Blocks, b => !b.Dead && b.Lava && b.OwnerId == spawner.Id);
    }


    private const string PlainBlocksJson =
        "{\"types\":[{\"id\":\"k\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}";

    [Fact]
    public void SurviveTimer_WinsTheLevel_WhenItExpires()
    {
        var catalog = BlockCatalog.FromJson(PlainBlocksJson);
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"k..\",\"...\",\"...\"],\"legend\":{\"k\":\"k\"},\"surviveTime\":1.0}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, seed: 1);
        g.Serve();
        Park(g);
        for (int i = 0; i < (int)(1.2 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(GamePhase.Won, g.Phase);
    }

    [Fact]
    public void TimeLimit_LosesTheLevel_WhenItExpires()
    {
        var catalog = BlockCatalog.FromJson(PlainBlocksJson);
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"k..\",\"...\",\"...\"],\"legend\":{\"k\":\"k\"},\"timeLimit\":1.0}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, seed: 1);
        g.Serve();
        Park(g);
        for (int i = 0; i < (int)(1.2 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(GamePhase.Lost, g.Phase);
    }

    [Fact]
    public void DescendingBlocks_PressDown_AndOverrunLoses()
    {
        var catalog = BlockCatalog.FromJson(PlainBlocksJson);
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":4,\"rows_data\":[\"k..\",\"...\",\"...\",\"...\"],\"legend\":{\"k\":\"k\"},\"descendInterval\":0.5}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, seed: 1);
        g.Serve();
        Park(g);
        var blk = g.Blocks[0];
        Assert.Equal(0, blk.Row);

        for (int i = 0; i < (int)(0.6 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(1, blk.Row);

        // Two more descents reach the bottom row (3) → overrun → lost.
        for (int i = 0; i < (int)(1.2 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(GamePhase.Lost, g.Phase);
    }

    [Fact]
    public void MultiFloor_AdvancesToNextFloor_ThenWins()
    {
        var catalog = BlockCatalog.FromJson(PlainBlocksJson);
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":4,\"rows_data\":[\"k..\",\"...\",\"...\",\"...\"]," +
            "\"legend\":{\"k\":\"k\"},\"floors\":[[\"..k\",\"...\",\"...\",\"...\"]]}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, seed: 1);
        g.Serve();

        BallHit(g, g.Blocks[0]); // clear floor 1
        Assert.Equal(GamePhase.Playing, g.Phase);
        Assert.Equal(1, g.FloorIndex);
        var nextBlock = g.Blocks.Single(b => !b.Dead);
        Assert.Equal(2, nextBlock.Col); // floor 2's layout

        BallHit(g, nextBlock); // clear floor 2 → win
        Assert.Equal(GamePhase.Won, g.Phase);
    }

    [Fact]
    public void StatueEscalation_LevelsStatues_OnTheInterval()
    {
        var g2catalog = BlockCatalog.FromJson(
            "{\"types\":[" +
            "{\"id\":\"m\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":99,\"emitAim\":\"paddle\"}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"m..\",\"...\",\"...\"],\"legend\":{\"m\":\"m\"},\"escalateInterval\":0.5}",
            g2catalog);
        var g = new GameInstance(level, SimConfig.Default, seed: 1);
        g.Serve();
        Park(g);
        var statue = g.Blocks[0];
        for (int i = 0; i < (int)(1.2 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(statue.StatueLevel >= 2, $"expected ≥2 escalations, got {statue.StatueLevel}");
    }


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
        for (int i = 0; i < (int)(SimConfig.Default.Enemies.ShieldStatueInterval / SimConfig.Default.FixedDt) + 2; i++)
            g.Tick(SimConfig.Default.FixedDt);
        Assert.True(plain.ImmunityTimer > 0, "neighbour was shielded");

        int hpBefore = plain.Hp;
        BallHit(g, plain);
        Assert.Equal(hpBefore, plain.Hp); // immune while shielded

        // Let the shield wear off, then damage lands.
        Park(g);
        for (int i = 0; i < (int)(SimConfig.Default.Enemies.StatueImmunityDuration / SimConfig.Default.FixedDt) + 5; i++)
            g.Tick(SimConfig.Default.FixedDt);
        Assert.True(plain.ImmunityTimer <= 0, "shield wore off");
        BallHit(g, plain);
        Assert.True(plain.Hp < hpBefore, "damage lands after the shield expires");
    }


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
        // Above the time-accel speed floor (RampedBallSpeed≈360 at t≈0) so only wind affects the magnitude.
        var speed = SimConfig.Default.BallSpeed * 1.5;
        g.Balls[0].Pos = new Vec2(wc.X + g.Config.Enemies.WindMasterRadius * 0.4, wc.Y);
        g.Balls[0].Vel = new Vec2(0, -speed);
        g.Tick(SimConfig.Default.FixedDt);

        Assert.True(g.Balls[0].Vel.X > 0, $"ball should be pushed right (away), vx={g.Balls[0].Vel.X:F2}");
        Assert.Equal(speed, g.Balls[0].Vel.Length, 1); // speed preserved (deflection only)
    }


    [Fact]
    public void Necromant_RevivesDestroyedBlock_AfterDelay_AndStopsWhenDead()
    {
        // Necromant 'N' (col0) + a normal block 'p' (col2). Kill p → it revives while N lives.
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"n\",\"biome\":\"t\",\"hp\":3,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"Reviver\"}," +
            "{\"id\":\"p\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"n.p\",\"...\",\"...\"],\"legend\":{\"n\":\"n\",\"p\":\"p\"}}");

        var necro = g.Blocks[0];
        var plain = g.Blocks[1];

        // Kill the plain block via the ball, then park the ball so it can't chew the Necromant.
        BallHit(g, plain);
        Assert.True(plain.Dead, "plain block destroyed");
        Park(g);

        // Before the delay elapses it stays dead; after it, the Necromant revives it.
        for (int i = 0; i < (int)(SimConfig.Default.Enemies.ReviveDelay / SimConfig.Default.FixedDt) + 5; i++)
            g.Tick(SimConfig.Default.FixedDt);
        Assert.False(plain.Dead, "Necromant should have revived the block");
        Assert.Equal(plain.MaxHp, plain.Hp);
        // A REGULAR necromant raises a regular corpse back as a REGULAR block (owner 2026-06-16): the
        // corpse keeps its nature, so a normal ball can re-kill it.
        Assert.False(plain.BallPhases, "regular necromant revives a regular block as REGULAR (not ghost)");

        // Kill the Necromant, then destroy the (revived) block again via the ball → it must NOT revive.
        necro.Hp = 0; necro.Dead = true;
        BallHit(g, plain);
        Assert.True(plain.Dead, "block destroyed again");
        Park(g);
        for (int i = 0; i < (int)(SimConfig.Default.Enemies.ReviveDelay / SimConfig.Default.FixedDt) + 5; i++)
            g.Tick(SimConfig.Default.FixedDt);
        Assert.True(plain.Dead, "no revival once the Necromant is dead");
    }


    [Fact]
    public void GhostNecromant_RaisesGhostCorpse_AsGhost_RegularNecromantIgnoresIt()
    {
        // Symmetric necromancy (owner 2026-06-16): a GHOST necromant 'M' (col0, ballPhases+Reviver) raises
        // a GHOST block 'b' (col2) back as a ghost. A regular necromant 'n' (col4) must NOT touch a ghost corpse.
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"m\",\"biome\":\"t\",\"hp\":3,\"sprite\":\"s\",\"needToKill\":true,\"ballPhases\":true,\"behavior\":\"Reviver\"}," +
            "{\"id\":\"n\",\"biome\":\"t\",\"hp\":3,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"Reviver\"}," +
            "{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true,\"ballPhases\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":5,\"rows\":3,\"rows_data\":[\"m.b.n\",\".....\",\".....\"],\"legend\":{\"m\":\"m\",\"b\":\"b\",\"n\":\"n\"}}");

        var ghostNecro = g.Blocks.First(x => x.TypeId == "m");
        var regNecro   = g.Blocks.First(x => x.TypeId == "n");
        var ghostBlk   = g.Blocks.First(x => x.TypeId == "b");

        // A PHASED ball kills the ghost block → ghost corpse, raised by the ghost necromant as a ghost.
        g.Balls[0].Ghost = true;
        BallHit(g, ghostBlk);
        Assert.True(ghostBlk.Dead, "phased ball destroys the ghost block");
        Park(g);
        for (int i = 0; i < (int)(SimConfig.Default.Enemies.ReviveDelay / SimConfig.Default.FixedDt) + 5; i++)
            g.Tick(SimConfig.Default.FixedDt);
        Assert.False(ghostBlk.Dead, "ghost necromant raises the ghost corpse");
        Assert.True(ghostBlk.BallPhases, "…and it returns on the GHOST layer");

        // Now kill the GHOST necromant (leaving only the regular necromant). The ghost corpse must NOT
        // revive — a regular necromant cannot raise a ghost.
        ghostNecro.Hp = 0; ghostNecro.Dead = true;
        Assert.False(regNecro.Dead, "regular necromant still alive");
        g.Balls[0].Ghost = true;
        BallHit(g, ghostBlk);
        Assert.True(ghostBlk.Dead, "ghost block destroyed again");
        Park(g);
        for (int i = 0; i < (int)(SimConfig.Default.Enemies.ReviveDelay / SimConfig.Default.FixedDt) + 5; i++)
            g.Tick(SimConfig.Default.FixedDt);
        Assert.True(ghostBlk.Dead, "a regular necromant does not raise a ghost corpse");
    }


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

        // Ball moves directly beneath the stalactite → it shakes (telegraph), then detaches into a falling hazard.
        g.Balls[0].Pos = new Vec2(sc.X, sc.Y + g.Config.CellSize);
        for (int i = 0; i < 40 && !stal.Dead; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(stal.Dead, "stalactite detached after the telegraph");
        Assert.Single(g.Hazards);
        Assert.True(g.Hazards[0].Vel.Y > 0, "stalactite falls downward");
        Assert.Equal("stalactite", g.Hazards[0].Kind);
    }


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
        var bomb2 = g.Blocks[1];
        var plain = g.Blocks[2]; // hp 2, two cells from bomb1 → only reached if bomb2 chains
        BallHit(g, bomb1);
        Assert.True(bomb2.Dead, "adjacent bomb chained");
        Assert.True(plain.Hp < 2, "second bomb's explosion reached the far block");
    }
}
