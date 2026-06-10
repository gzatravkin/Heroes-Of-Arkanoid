using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// G2 build-depth tests (docs/09): the 13 new relics + the 3 new ball cores and
/// their fusions. Pure-Core, deterministic.
/// </summary>
public class RelicG2Tests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static GameInstance Make(string blocksJson, string levelJson,
        SimConfig? cfg = null, BonusCatalog? bonuses = null)
    {
        var catalog = BlockCatalog.FromJson(blocksJson);
        var level   = LevelLoader.FromJson(levelJson, catalog);
        var g = new GameInstance(level, cfg ?? SimConfig.Default, seed: 1, bonuses: bonuses);
        g.Serve();
        return g;
    }

    private static GameInstance MakeOneBlock(int hp, SimConfig? cfg = null, string typeId = "b")
        => Make(
            $"{{\"types\":[{{\"id\":\"{typeId}\",\"biome\":\"t\",\"hp\":{hp},\"sprite\":\"s\",\"needToKill\":true}}]}}",
            $"{{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{{\"A\":\"{typeId}\"}}}}",
            cfg);

    /// <summary>Drive the ball into the given block from below; one tick lands the hit.</summary>
    private static void Hit(GameInstance g, Arkanoid.Core.Entities.Block blk)
    {
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
    }

    // ── conductor ──────────────────────────────────────────────────────────────

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

    // ── overcharge ─────────────────────────────────────────────────────────────

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
        Assert.True(withRelic >= without + SimConfig.Default.OverchargeMana * 0.99,
            $"overcharge should add ~{SimConfig.Default.OverchargeMana}: {without:F1} → {withRelic:F1}");
    }

    // ── split_shot + souljar (kill-cadence payouts) ────────────────────────────

    [Fact]
    public void SplitShot_SpawnsExtraBall_EveryNthKill()
    {
        var cfg = new SimConfig { SplitShotEvery = 2 };
        var g = Make(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":6,\"rows\":2,\"rows_data\":[\"AAAAAA\",\"AAAAAA\"],\"legend\":{\"A\":\"b\"}}",
            cfg);
        g.AddRelic("split_shot");
        Assert.Single(g.Balls);

        Hit(g, g.Blocks[0]); // kill 1 — no split yet
        Assert.Single(g.Balls);
        Hit(g, g.Blocks[1]); // kill 2 — split!
        Assert.Equal(2, g.Balls.Count);
    }

    [Fact]
    public void Souljar_PaysOneCrystal_EveryNthKill()
    {
        var cfg = new SimConfig { SouljarEvery = 2 };
        var g = Make(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":6,\"rows\":2,\"rows_data\":[\"AAAAAA\",\"AAAAAA\"],\"legend\":{\"A\":\"b\"}}",
            cfg);
        g.AddRelic("souljar");
        Hit(g, g.Blocks[0]);
        Assert.Equal(0, g.Crystals);
        Hit(g, g.Blocks[1]);
        Assert.Equal(1, g.Crystals);
    }

    // ── lodestone + midas (bonus pickups) ──────────────────────────────────────

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
            Vel = new Vec2(0, SimConfig.Default.BonusFallSpeed), Type = "heal", Icon = "i", Alive = true,
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
        Assert.Equal(SimConfig.Default.MidasCrystals, g.Crystals);
    }

    // ── ember_heart ────────────────────────────────────────────────────────────

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

        Assert.Equal(HitsAfterDeflect(false) + SimConfig.Default.EmberHeartBonusHits,
                     HitsAfterDeflect(true));
    }

    // ── second_wind ────────────────────────────────────────────────────────────

    [Fact]
    public void SecondWind_NegatesTheFirstHpLossOnly()
    {
        var g = MakeOneBlock(9);
        g.AddRelic("second_wind");
        int lives = g.Lives;
        g.DamagePlayer(1);
        Assert.Equal(lives, g.Lives);     // first loss negated
        g.DamagePlayer(1);
        Assert.Equal(lives - 1, g.Lives); // second loss lands
    }

    // ── lead_paddle (tradeoff) ─────────────────────────────────────────────────

    [Fact]
    public void LeadPaddle_WidensPaddle_ButSlowsRegen()
    {
        var g = MakeOneBlock(9);
        var w0 = g.Paddle.Width;
        g.AddRelic("lead_paddle");
        Assert.Equal(w0 * SimConfig.Default.LeadPaddleWidthMult, g.Paddle.Width, 3);

        var gBase = MakeOneBlock(9);
        g.ManaValue = 0; gBase.ManaValue = 0;
        g.Balls[0].Vel = new Vec2(0, 0); gBase.Balls[0].Vel = new Vec2(0, 0);
        for (int i = 0; i < 60; i++) { g.Tick(SimConfig.Default.FixedDt); gBase.Tick(SimConfig.Default.FixedDt); }
        Assert.True(g.ManaValue < gBase.ManaValue, "lead paddle must slow regen");
    }

    // ── sapper (caverns-keyed) ─────────────────────────────────────────────────

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
        Assert.Equal(4 - SimConfig.Default.BombDamage, FarBlockHpAfterBomb(true));    // in reach
    }

    // ── hellwalker (hell-keyed) ────────────────────────────────────────────────

    [Fact]
    public void Hellwalker_SavesTheBallFromLava_OncePerServe()
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

        // A drained ball re-serves instantly (consuming a spare), so the spare count
        // is the observable — not Ball.Alive.
        Hit(g, lava);
        Assert.Equal(spares, g.SpareBalls); // first touch saved — no spare spent

        Hit(g, lava);
        Assert.Equal(spares - 1, g.SpareBalls); // second touch drains
    }

    // ── ghost_lens (village-keyed) ─────────────────────────────────────────────

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

        Assert.Equal(HpAfterGhostHit(false) - SimConfig.Default.GhostLensBonus,
                     HpAfterGhostHit(true));
    }

    // ── pillar_doctrine (heaven-keyed) ─────────────────────────────────────────

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

        Assert.Equal(HpAfterHit(false) - SimConfig.Default.PillarDoctrineBonus,
                     HpAfterHit(true));
    }

    // ── G2b ball cores ─────────────────────────────────────────────────────────

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
        Assert.Equal(SimConfig.Default.GhostCoreCharges, g.Balls[0].PhasesLeft);

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
        Assert.Equal(9 - SimConfig.Default.BallDamage - SimConfig.Default.EchoBonus, blk.Hp);

        // Echo spent — the next hit is normal.
        Hit(g, blk);
        Assert.Equal(9 - 2 * SimConfig.Default.BallDamage - SimConfig.Default.EchoBonus, blk.Hp);
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
        Assert.True(emitter.EmitAccumulator <= -SimConfig.Default.FrostFreezeSeconds * 0.9,
            $"frost should set the cadence back (got {emitter.EmitAccumulator:F2})");

        var g2 = Make(blocks, level.Replace("\"id\":\"t\"", "\"id\":\"t2\""));
        g2.AddBallCore("frost");
        g2.AddBallCore("echo"); // echo+frost = Stasis fusion
        var emitter2 = g2.Blocks[0];
        Hit(g2, emitter2);
        Assert.True(emitter2.EmitAccumulator <=
            -SimConfig.Default.FrostFreezeSeconds * SimConfig.Default.StasisFreezeMult * 0.9,
            $"stasis should double the freeze (got {emitter2.EmitAccumulator:F2})");
    }

    [Fact]
    public void MoltenFusion_EnablesAndDeepensFireSpread_ForAnyCharacter()
    {
        const string blocks = "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":4,\"sprite\":\"s\",\"needToKill\":true}]}";
        const string level  = "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"AA.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}";

        var g = Make(blocks.Replace("\"hp\":4", "\"hp\":1"), level);
        g.SetCharacter("paladin"); // not a fire mage — spread comes from the fusion alone
        g.AddBallCore("heavy");
        g.AddBallCore("ember");
        var neighbour = g.Blocks[1];
        g.Balls[0].IgniteHitsLeft = 5;
        int before = neighbour.Hp;
        Hit(g, g.Blocks[0]); // ignited kill → molten spread
        Assert.Equal(before - (1 + SimConfig.Default.MoltenChipBonus), neighbour.Hp);
    }

    [Fact]
    public void PhantomFusion_GrantsExtraPhaseCharges()
    {
        var g = MakeUnserved(9);
        g.AddBallCore("ghost");
        g.AddBallCore("split");
        g.Serve();
        Assert.Equal(SimConfig.Default.PhantomPhaseCharges, g.Balls[0].PhasesLeft);
    }
}
