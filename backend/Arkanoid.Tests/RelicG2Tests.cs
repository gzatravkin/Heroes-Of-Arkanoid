using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim;
using Xunit;

public class RelicG2Tests
{
    private static GameInstance Make(string blocksJson, string levelJson,
        SimConfig? cfg = null, BonusCatalog? bonuses = null, RelicCatalog? relics = null)
    {
        var cat   = Arkanoid.Core.Blocks.BlockCatalog.FromJson(blocksJson);
        var level = Arkanoid.Core.Grid.LevelLoader.FromJson(levelJson, cat);
        var g = new GameInstance(level, cfg ?? SimConfig.Default, seed: 1, bonuses: bonuses, relics: relics);
        g.Serve();
        return g;
    }
    private static GameInstance MakeOneBlock(int hp, SimConfig? cfg = null, string typeId = "b")
        => Make($"{{\"types\":[{{\"id\":\"{typeId}\",\"biome\":\"t\",\"hp\":{hp},\"sprite\":\"s\",\"needToKill\":true}}]}}",
                $"{{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{{\"A\":\"{typeId}\"}}}}",
                cfg);
    private static void Hit(GameInstance g, Arkanoid.Core.Entities.Block blk) => K.Hit(g, blk);

    [Fact]
    public void Conductor_AddsOneLightningChainJump()
    {
        // A row of 1-hp blocks: lightning hits start + ChainJumps neighbours.
        const string blocks = "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}";
        const string level  = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":8,\"rows\":2,\"rows_data\":[\"AAAAAAAA\",\"AAAAAAAA\"],\"legend\":{\"A\":\"b\"}}";

        int KillsFromOneCast(bool conductor)
        {
            var g = Make(blocks, level);
            g.SetCharacter("engineer");
            if (conductor) g.AddRelic("conductor");
            g.ManaValue = 100;
            g.Balls[0].Vel = new Vec2(0, 0); // park
            g.CastSlot(0); // lightning
            return g.Blocks.FindAll(b => b.Dead).Count;
        }

        var baseline = KillsFromOneCast(false);
        var boosted  = KillsFromOneCast(true);
        Assert.Equal(baseline + 1, boosted);
    }


    [Fact]
    public void Overcharge_PaysExtraMana_OnPerfectCenterDeflect()
    {
        double ManaAfterCenterDeflect(bool relic)
        {
            var g = MakeOneBlock(9);
            if (relic) g.AddRelic("overcharge");
            g.ManaValue = 0;
            // Drop the ball straight onto the paddle's centre.
            g.Balls[0].Pos = new Vec2(g.Paddle.Center.X,
                g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 1);
            g.Balls[0].Vel = new Vec2(0, SimConfig.Default.BallSpeed);
            g.Tick(SimConfig.Default.FixedDt);
            return g.ManaValue;
        }

        var withRelic = ManaAfterCenterDeflect(true);
        var without   = ManaAfterCenterDeflect(false);
        Assert.True(withRelic >= without + 8.0 * 0.99,
            $"overcharge should add ~8: {without:F1} → {withRelic:F1}");
    }


    [Fact]
    public void SplitShot_SpawnsExtraBall_EveryNthKill()
    {
        // Inject catalog with cadence=2 to keep the test short (default=6).
        var relics = RelicCatalog.FromJson("{\"relics\":[{\"id\":\"split_shot\",\"effect\":\"\",\"magnitude\":2}]}");
        const string bJson = "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}";
        const string lJson = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":6,\"rows\":2,\"rows_data\":[\"AAAAAA\",\"AAAAAA\"],\"legend\":{\"A\":\"b\"}}";
        var g = Make(bJson, lJson, relics: relics);
        g.AddRelic("split_shot");
        Assert.Single(g.Balls);

        var b0 = g.Blocks[0]; var b1 = g.Blocks[1]; // capture before pruning
        Hit(g, b0); // kill 1 — no split yet
        Assert.Single(g.Balls);
        Hit(g, b1); // kill 2 — split!
        Assert.Equal(2, g.Balls.Count);
    }

    [Fact]
    public void Souljar_PaysOneCrystal_EveryNthKill()
    {
        // Inject catalog with cadence=2 to keep the test short (default=5).
        var relics = RelicCatalog.FromJson("{\"relics\":[{\"id\":\"souljar\",\"effect\":\"\",\"magnitude\":2}]}");
        const string bJson = "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}";
        const string lJson = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":6,\"rows\":2,\"rows_data\":[\"AAAAAA\",\"AAAAAA\"],\"legend\":{\"A\":\"b\"}}";
        var g = Make(bJson, lJson, relics: relics);
        g.AddRelic("souljar");
        var b0 = g.Blocks[0]; var b1 = g.Blocks[1]; // capture before pruning
        Hit(g, b0);
        Assert.Equal(1, g.Crystals); // 1 base combo crystal, souljar not triggered yet (kill 1 of 2)
        Hit(g, b1);
        Assert.Equal(3, g.Crystals); // 2 base combo crystals + 1 souljar bonus (triggered at 2nd kill)
    }


    [Fact]
    public void Lodestone_DriftsBonusesTowardThePaddle()
    {
        var g = MakeOneBlock(9);
        g.AddRelic("lodestone");
        g.Balls[0].Vel = new Vec2(0, 0); // park
        var startX = g.Paddle.Center.X - SimConfig.Default.CellSize * 2;
        g.Bonuses.Add(new Arkanoid.Core.Entities.Bonus
        {
            Id = 1, Pos = new Vec2(startX, 10),
            Vel = new Vec2(0, SimConfig.Default.Pickups.FallSpeed), Type = "heal", Icon = "i", Alive = true,
        });
        for (int i = 0; i < 30; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Bonuses[0].Pos.X > startX, "bonus should drift toward the paddle's x");
    }

    [Fact]
    public void Midas_PaysCrystals_OnEveryCatch()
    {
        var bonuses = BonusCatalog.FromJson(
            "{\"bonuses\":[{\"id\":\"heal\",\"name\":\"Heal\",\"icon\":\"i\",\"effect\":\"heal\"}]}");
        var g = Make(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            bonuses: bonuses);
        g.AddRelic("midas");
        g.Balls[0].Vel = new Vec2(0, 0);
        g.ApplyCheat("spawnBonus", 0); // heal falls onto the paddle
        for (int i = 0; i < 240 && g.Crystals == 0; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(2 /* MidasCrystals */, g.Crystals);
    }


    [Fact]
    public void EmberHeart_ExtendsIgniteHits()
    {
        int HitsAfterDeflect(bool relic)
        {
            var g = MakeOneBlock(9);
            if (relic) g.AddRelic("ember_heart");
            g.ManaValue = 100;
            g.CastIgnite(); // arms ignite for the next deflect
            g.Balls[0].Pos = new Vec2(g.Paddle.Center.X,
                g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 1);
            g.Balls[0].Vel = new Vec2(0, SimConfig.Default.BallSpeed);
            g.Tick(SimConfig.Default.FixedDt);
            return g.Balls[0].IgniteHitsLeft;
        }

        Assert.Equal(HitsAfterDeflect(false) + 2 /* EmberHeartBonusHits */,
                     HitsAfterDeflect(true));
    }


    [Fact]
    public void SecondWind_NegatesTheFirstHpLossOnly()
    {
        var g = MakeOneBlock(9);
        g.AddRelic("second_wind");
        int lives = g.Hp;
        g.DamagePlayer(1);
        Assert.Equal(lives, g.Hp);     // first loss negated
        g.DamagePlayer(1);
        Assert.Equal(lives - 1, g.Hp); // second loss lands
    }


    [Fact]
    public void LeadPaddle_WidensPaddle_ButSlowsRegen()
    {
        var g = MakeOneBlock(9);
        var w0 = g.Paddle.Width;
        g.AddRelic("lead_paddle");
        Assert.Equal(w0 * 1.25 /* LeadPaddleWidthMult */, g.Paddle.Width, 3);

        var gBase = MakeOneBlock(9);
        g.ManaValue = 0; gBase.ManaValue = 0;
        g.Balls[0].Vel = new Vec2(0, 0); gBase.Balls[0].Vel = new Vec2(0, 0);
        for (int i = 0; i < 60; i++) { g.Tick(SimConfig.Default.FixedDt); gBase.Tick(SimConfig.Default.FixedDt); }
        Assert.True(g.ManaValue < gBase.ManaValue, "lead paddle must slow regen");
    }


    [Fact]
    public void Sapper_ExtendsBombRadius()
    {
        const string blocks =
            "{\"types\":[" +
            "{\"id\":\"x\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"bomb\",\"explodeRadius\":1}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":4,\"sprite\":\"s\",\"needToKill\":true}]}";
        // far block sits 2 cells from the bomb — outside radius 1, inside 1+sapper.
        const string level =
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":5,\"rows\":2,\"rows_data\":[\"X.k..\",\".....\"],\"legend\":{\"X\":\"x\",\"k\":\"k\"}}";

        int FarBlockHpAfterBomb(bool relic)
        {
            var g = Make(blocks, level);
            if (relic) g.AddRelic("sapper");
            var far = g.Blocks[1];
            Hit(g, g.Blocks[0]); // detonate the bomb
            return far.Hp;
        }

        Assert.Equal(4, FarBlockHpAfterBomb(false));                                  // untouched
        Assert.Equal(4 - SimConfig.Default.Enemies.BombDamage, FarBlockHpAfterBomb(true));    // in reach
    }


    [Fact]
    public void Hellwalker_LavaBallPassesThrough_NoSpareConsumed()
    {
        const string blocks =
            "{\"types\":[" +
            "{\"id\":\"l\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true,\"behavior\":\"lava\"}," +
            "{\"id\":\"k\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true}]}";
        const string level =
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"L.k\",\"...\",\"...\"],\"legend\":{\"L\":\"l\",\"k\":\"k\"}}";
        var g = Make(blocks, level);
        g.AddRelic("hellwalker");
        var lava = g.Blocks[0];
        int spares = g.SpareBalls;

        // Ball passes through lava — no spare consumed regardless of Hellwalker.
        Hit(g, lava);
        Assert.Equal(spares, g.SpareBalls);

        Hit(g, lava);
        Assert.Equal(spares, g.SpareBalls);
    }


    [Fact]
    public void GhostLens_BoostsGhostBallDamage()
    {
        const string blocks =
            "{\"types\":[{\"id\":\"gb\",\"biome\":\"t\",\"hp\":5,\"sprite\":\"s\",\"needToKill\":true,\"ballPhases\":true}]}";
        const string level =
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"gb\"}}";

        int HpAfterGhostHit(bool relic)
        {
            var g = Make(blocks, level);
            if (relic) g.AddRelic("ghost_lens");
            g.Balls[0].Ghost = true; // a ghost ball collides WITH ghost blocks
            var blk = g.Blocks[0];
            Hit(g, blk);
            return blk.Hp;
        }

        Assert.Equal(HpAfterGhostHit(false) - 1 /* GhostLensBonus */,
                     HpAfterGhostHit(true));
    }


    [Fact]
    public void PillarDoctrine_BoostsDamage_VsColumnsAndStatues()
    {
        int HpAfterHit(bool relic)
        {
            var g = MakeOneBlock(5, typeId: "heaven_column_top");
            if (relic) g.AddRelic("pillar_doctrine");
            var blk = g.Blocks[0];
            Hit(g, blk);
            return blk.Hp;
        }

        Assert.Equal(HpAfterHit(false) - 1 /* PillarDoctrineBonus */,
                     HpAfterHit(true));
    }


    /// <summary>Build an un-served instance so cores can be added before the first serve.</summary>
    private static GameInstance MakeUnserved(int hp)
    {
        var catalog = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"t\",\"hp\":{hp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        return new GameInstance(level, SimConfig.Default, seed: 1);
    }

    [Fact]
    public void GhostCore_PhasesThroughTheFirstBlock_NoBounce()
    {
        var g = MakeUnserved(5);
        g.AddBallCore("ghost");
        g.Serve();
        Assert.Equal(1 /* GhostCoreCharges */, g.Balls[0].PhasesLeft);

        var blk = g.Blocks[0];
        Hit(g, blk);
        Assert.Equal(5 - SimConfig.Default.BallDamage, blk.Hp);
        Assert.True(g.Balls[0].Vel.Y < 0, "phase-through keeps the ball flying upward");
        Assert.Equal(0, g.Balls[0].PhasesLeft);

        // Second contact bounces normally.
        Hit(g, blk);
        Assert.True(g.Balls[0].Vel.Y > 0, "with charges spent, the ball reflects");
    }

    [Fact]
    public void EchoCore_FirstHitAfterDeflect_DealsBonus()
    {
        var g = MakeOneBlock(9);
        g.AddBallCore("echo");
        // Deflect off the paddle to arm the echo.
        g.Balls[0].Pos = new Vec2(g.Paddle.Center.X,
            g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Vec2(0, SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Balls[0].EchoArmed);

        var blk = g.Blocks[0];
        Hit(g, blk);
        Assert.Equal(9 - SimConfig.Default.BallDamage - 1 /* EchoBonus */, blk.Hp);

        // Echo spent — the next hit is normal.
        Hit(g, blk);
        Assert.Equal(9 - 2 * SimConfig.Default.BallDamage - 1 /* EchoBonus */, blk.Hp);
    }

    [Fact]
    public void FrostCore_FreezesEmitterCadence_StasisDoubles()
    {
        const string blocks =
            "{\"types\":[{\"id\":\"e\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":1.0,\"emitAim\":\"down\"}]}";
        const string level =
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"e\"}}";

        var g = Make(blocks, level);
        g.AddBallCore("frost");
        var emitter = g.Blocks[0];
        Hit(g, emitter);
        Assert.True(emitter.EmitAccumulator <= -2.0 * 0.9, // FrostFreezeSeconds = 2.0
            $"frost should set the cadence back (got {emitter.EmitAccumulator:F2})");

        var g2 = Make(blocks, level.Replace("\"id\":\"t\"", "\"id\":\"t2\""));
        g2.AddBallCore("frost");
        g2.AddBallCore("echo"); // echo+frost = Stasis fusion
        var emitter2 = g2.Blocks[0];
        Hit(g2, emitter2);
        Assert.True(emitter2.EmitAccumulator <= -2.0 * 2.0 * 0.9, // FrostFreezeSeconds=2.0, StasisFreezeMult=2.0
            $"stasis should double the freeze (got {emitter2.EmitAccumulator:F2})");
    }

    [Fact]
    public void MoltenFusion_EnablesAndDeepensFireSpread_ForAnyCharacter()
    {
        // 2-hp blocks side by side. A non-fire-mage with the Molten fusion (heavy+ember)
        // both ENABLES fire spread and DEEPENS it (2 dmg/burn tick), so a 2-hp neighbour
        // dies within a single burn tick — base burn (1/tick) would leave it alive.
        const string blocks = "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":2,\"sprite\":\"s\",\"needToKill\":true}]}";
        const string level  = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"AA.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}";

        var g = Make(blocks, level);
        g.SetCharacter("paladin"); // not a fire mage — spread comes from the fusion alone
        g.AddBallCore("heavy");    // +1 ball damage → kills the 2-hp origin in one ignited hit
        g.AddBallCore("ember");    // heavy+ember = Molten fusion
        var origin    = g.Blocks[0];
        var neighbour = g.Blocks[1];
        g.Balls[0].IgniteHitsLeft = 5;
        Hit(g, origin);            // ignite lights the origin (slow burn); molten will creep fire to the neighbour
        Assert.True(origin.BurnRemaining > 0 || origin.Dead, "ignite should light the origin");

        // Park the ball just above the paddle (clear of blocks) so only fire spread acts. Run long enough
        // for the slow creep (~2.5s) plus a deepened burn tick (~7s) to kill the 2-hp neighbour.
        g.Balls[0].Vel = new Vec2(0, 0);
        g.Balls[0].Pos = new Vec2(g.Paddle.Center.X,
            g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 2);
        for (int i = 0; i < (int)(12.0 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(neighbour.Dead,
            "molten fusion should spread fire and burn down the neighbour for any character");
    }

    [Fact]
    public void PhantomFusion_GrantsExtraPhaseCharges()
    {
        var g = MakeUnserved(9);
        g.AddBallCore("ghost");
        g.AddBallCore("split");
        g.Serve();
        Assert.Equal(2 /* PhantomPhaseCharges */, g.Balls[0].PhasesLeft);
    }
}
