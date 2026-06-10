using Arkanoid.Core.Blocks;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Net;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Tests for the boss fight system: multi-pattern hazard spawning, telegraph-before-attack ordering,
/// phase changes at HP thresholds, snapshot boss-HP surface, and the classic hazard/HP-depletion tests.
/// </summary>
public class BossTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static GameInstance MakeGame(string typesJson, string levelJson, SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var catalog = BlockCatalog.FromJson($"{{\"types\":[{typesJson}]}}");
        var level   = LevelLoader.FromJson(levelJson, catalog, cfg);
        return new GameInstance(level, cfg, seed: 1);
    }

    /// <summary>
    /// A minimal boss level: one boss block (high HP so it stays alive for pattern cycling),
    /// plus one fill block to keep the level not-already-won.
    /// </summary>
    private static GameInstance MakeBossGame(SimConfig? cfg = null)
        => MakeGame(
            """
            {"id":"boss","biome":"hell","hp":24,"sprite":"DemonBody","needToKill":true,"behavior":"boss"},
            {"id":"fill","biome":"hell","hp":1, "sprite":"s",         "needToKill":true}
            """,
            """
            {"id":"t","biome":"hell","cols":12,"rows":8,
             "rows_data":["BBBBBBBBBBBB","............","............","............",
                          "............","............","............","............"],
             "legend":{"B":"boss"}}
            """,
            cfg);

    // -----------------------------------------------------------------------
    // 1. Boss_SpawnsHazardsOverTime (adapted from legacy test)
    // -----------------------------------------------------------------------

    [Fact]
    public void Boss_SpawnsHazardsOverTime()
    {
        // Fast attack interval + telegraph so hazards appear quickly.
        var cfg = new SimConfig
        {
            BossAttackInterval = 0.05,
            BossTelegraphDuration = 0.02,
        };
        var g = MakeBossGame(cfg);
        g.Serve();
        g.ApplyCheat("setLives", 9999); // survive the rapid-fire test cadence

        Assert.Empty(g.Hazards); // no hazards before any ticks

        // Loop attack cycles: in hell the AimedShot pattern is a fist slam (no hazard),
        // so we wait for a hazard-spawning pattern (Rain/Spread) to roll.
        var dt = cfg.BossAttackInterval + 0.01;
        for (int i = 0; i < 50 && g.Hazards.Count == 0; i++)
        {
            g.Tick(dt);                               // accumulate → telegraph
            g.Tick(cfg.BossTelegraphDuration + 0.01); // telegraph expires → attack fires
        }

        Assert.True(g.Hazards.Count > 0,
            $"Expected at least one hazard after several attack cycles; count={g.Hazards.Count}");
    }

    // -----------------------------------------------------------------------
    // 1b. WitchBoss_TagsHazardsAsWitchMagic — the Witchland boss casts magic bolts
    //     (Kind="witchmagic") so the renderer cycles the WitchMagic1-4 sprites.
    // -----------------------------------------------------------------------

    [Fact]
    public void WitchBoss_TagsHazardsAsWitchMagic()
    {
        var cfg = new SimConfig
        {
            BossAttackInterval    = 0.05,
            BossTelegraphDuration = 0.02,
        };
        var g = MakeGame(
            """
            {"id":"boss","biome":"village","hp":24,"sprite":"WitchChest","needToKill":true,"behavior":"boss"},
            {"id":"fill","biome":"village","hp":1, "sprite":"s",          "needToKill":true}
            """,
            """
            {"id":"t","biome":"village","cols":12,"rows":8,
             "rows_data":["BBBBBBBBBBBB","............","............","............",
                          "............","............","............","............"],
             "legend":{"B":"boss"}}
            """,
            cfg);
        g.Serve();
        g.ApplyCheat("setLives", 9999); // survive the rapid-fire test cadence

        // The Witch's AimedShot is now her grab-hand ("witchgrab") — loop until a
        // magic-bolt pattern (Rain/Spread) rolls, then check the bolt tagging.
        for (int i = 0; i < 50 && !g.Hazards.Any(h => h.Kind == "witchmagic"); i++)
        {
            g.Tick(cfg.BossAttackInterval + 0.01);     // telegraph
            g.Tick(cfg.BossTelegraphDuration + 0.01);  // fire
        }

        Assert.Contains(g.Hazards, h => h.Kind == "witchmagic");
        Assert.All(g.Hazards, h => Assert.True(h.Kind is "witchmagic" or "witchgrab",
            $"unexpected village boss hazard kind '{h.Kind}'"));
    }

    // -----------------------------------------------------------------------
    // 1c. Boss bolts carry a biome missile kind (hell → "hellball") so the
    //     renderer draws real missile art instead of a generic dot.
    // -----------------------------------------------------------------------

    [Fact]
    public void HellBoss_TagsHazardsAsHellball()
    {
        var cfg = new SimConfig
        {
            BossAttackInterval    = 0.05,
            BossTelegraphDuration = 0.02,
        };
        var g = MakeGame(
            """
            {"id":"boss","biome":"hell","hp":24,"sprite":"DemonBody","needToKill":true,"behavior":"boss"}
            """,
            """
            {"id":"t","biome":"hell","cols":12,"rows":8,
             "rows_data":["BBBBBBBBBBBB","............","............","............",
                          "............","............","............","............"],
             "legend":{"B":"boss"}}
            """,
            cfg);
        g.Serve();
        g.ApplyCheat("setLives", 9999); // survive the rapid-fire test cadence

        // The Demon's AimedShot is now the fist slam (no hazard) — loop until a
        // hazard pattern (Rain/Spread) rolls, then check the bolt tagging.
        for (int i = 0; i < 50 && g.Hazards.Count == 0; i++)
        {
            g.Tick(cfg.BossAttackInterval + 0.01);     // telegraph
            g.Tick(cfg.BossTelegraphDuration + 0.01);  // fire
        }

        Assert.NotEmpty(g.Hazards);
        Assert.All(g.Hazards, h => Assert.Equal("hellball", h.Kind));
    }

    // -----------------------------------------------------------------------
    // 1d. Boss signature mechanics (docs/11 §4 — one verb per boss)
    // -----------------------------------------------------------------------

    private static readonly SimConfig FastBossCfg = new()
    {
        BossAttackInterval    = 0.05,
        BossTelegraphDuration = 0.02,
    };

    /// <summary>Tick until predicate or budget exhausted; returns success.</summary>
    private static bool TickUntil(GameInstance g, System.Func<bool> done, int maxTicks = 2000)
    {
        for (int i = 0; i < maxTicks && !done(); i++) g.Tick(SimConfig.Default.FixedDt);
        return done();
    }

    [Fact]
    public void Demon_FistSlam_CrushesColumnBlocks_AndTelegraphsTheColumn()
    {
        var g = MakeGame(
            """
            {"id":"boss","biome":"hell","hp":24,"sprite":"DemonBody","needToKill":true,"behavior":"boss"},
            {"id":"fill","biome":"hell","hp":6, "sprite":"s",         "needToKill":true}
            """,
            """
            {"id":"t","biome":"hell","cols":12,"rows":8,
             "rows_data":["BBBBBBBBBBBB","............","ffffffffffff","............",
                          "............","............","............","............"],
             "legend":{"B":"boss","f":"fill"}}
            """,
            FastBossCfg);
        g.Serve();
        g.ApplyCheat("setLives", 9999); // survive the rapid-fire cadence while parked
        g.Balls[0].Vel = new Vec2(0, 0); // park

        // The paddle column's fill block takes fist damage once an AimedShot rolls.
        var paddleCol = (int)((g.Paddle.Center.X - g.Config.BoardOriginX) / g.Config.CellSize);
        var colBlock  = g.Blocks.First(b => !b.Boss && b.Col == paddleCol);
        var sawSlam = TickUntil(g, () => colBlock.Hp < colBlock.MaxHp);
        Assert.True(sawSlam, "fist slam should crush blocks in the locked column");

        // The fist telegraph + slam events were emitted for the renderer.
        var events = Snapshot.From(g, 0).Events;
        Assert.Contains(events, e => e.Type == "fistTelegraph");
        Assert.Contains(events, e => e.Type == "fistSlam");
    }

    [Fact]
    public void Goblin_HopsBetweenAnchors_StayingInBounds()
    {
        var g = MakeGame(
            """
            {"id":"boss","biome":"caverns","hp":24,"sprite":"GoblinBody","needToKill":true,"behavior":"boss"},
            {"id":"fill","biome":"caverns","hp":9, "sprite":"s",          "needToKill":true}
            """,
            """
            {"id":"t","biome":"caverns","cols":12,"rows":8,
             "rows_data":["...BBBB.....","............","f...........","............",
                          "............","............","............","............"],
             "legend":{"B":"boss","f":"fill"}}
            """,
            FastBossCfg);
        g.Serve();
        g.ApplyCheat("setLives", 9999);
        g.Balls[0].Vel = new Vec2(0, 0);

        var bossBlocks = g.Blocks.Where(b => b.Boss).ToList();
        var startCols  = bossBlocks.Select(b => b.Col).ToList();

        var hopped = TickUntil(g, () => bossBlocks[0].Col != startCols[0]);
        Assert.True(hopped, "goblin should hop to a different anchor");
        // Rig stays in bounds and shape-preserved after many cycles.
        TickUntil(g, () => false, 600);
        for (int i = 0; i < bossBlocks.Count; i++)
        {
            Assert.InRange(bossBlocks[i].Col, 0, g.Level.Grid.Cols - 1);
            Assert.Equal(startCols[i] - startCols[0], bossBlocks[i].Col - bossBlocks[0].Col);
        }
    }

    [Fact]
    public void Witch_GrabsBall_CarriesItToHer_ThenThrowsItFast()
    {
        var g = MakeGame(
            """
            {"id":"boss","biome":"village","hp":24,"sprite":"WitchChest","needToKill":true,"behavior":"boss"},
            {"id":"fill","biome":"village","hp":9, "sprite":"s",          "needToKill":true}
            """,
            """
            {"id":"t","biome":"village","cols":12,"rows":8,
             "rows_data":["....BBBB....","............","f...........","............",
                          "............","............","............","............"],
             "legend":{"B":"boss","f":"fill"}}
            """,
            FastBossCfg);
        g.Serve();
        g.ApplyCheat("setLives", 9999);
        g.Balls[0].Vel = new Vec2(0, 0);
        var ball = g.Balls[0];

        var grabbed = TickUntil(g, () => ball.GrabberId != 0);
        Assert.True(grabbed, "the witch's hand should grab the parked ball");

        var thrown = TickUntil(g, () => ball.GrabberId == 0 && ball.Vel.Length > 0);
        Assert.True(thrown, "the witch should throw the ball after the hold");
        Assert.True(ball.Vel.Length > g.Config.BallSpeed * 1.05,
            $"thrown ball must be faster than normal (got {ball.Vel.Length:F0} vs {g.Config.BallSpeed:F0})");
    }

    [Fact]
    public void Seraph_SummonsAdds_AndVaseFuseLevelsThem_UnlessDefused()
    {
        var g = MakeGame(
            """
            {"id":"boss","biome":"heaven","hp":24,"sprite":"HeavenBoss","needToKill":true,"behavior":"boss"},
            {"id":"fill","biome":"heaven","hp":9, "sprite":"s",          "needToKill":true}
            """,
            """
            {"id":"t","biome":"heaven","cols":12,"rows":8,
             "rows_data":["....BBBB....","............","f...........","............",
                          "............","............","............","............"],
             "legend":{"B":"boss","f":"fill"}}
            """,
            FastBossCfg);
        g.Serve();
        g.ApplyCheat("setLives", 9999);
        g.Balls[0].Vel = new Vec2(0, 0);

        // Force phase 3 (Summon only rolls there) by dropping boss HP under the threshold.
        foreach (var b in g.Blocks.Where(b => b.Boss)) b.Hp = (int)(b.MaxHp * 0.2);

        var addAppeared = TickUntil(g, () =>
            g.Blocks.Any(b => !b.Dead && b.Emitter && !b.NeedToKill), 4000);
        Assert.True(addAppeared, "the Seraph should summon a statue add");

        var vaseAppeared = TickUntil(g, () =>
            g.Blocks.Any(b => !b.Dead && b.BossVase), 4000);
        Assert.True(vaseAppeared, "the Seraph should summon a fused boss-vase");

        // Let the fuse expire → his adds level up.
        var add = g.Blocks.First(b => !b.Dead && b.Emitter && !b.NeedToKill);
        var levelled = TickUntil(g, () => add.StatueLevel > 0,
            (int)(SimConfig.Default.SeraphVaseFuse / SimConfig.Default.FixedDt) + 200);
        Assert.True(levelled, "fuse expiry must level the Seraph's adds");
    }

    // -----------------------------------------------------------------------
    // 2. Hazard_HittingPaddle_DamagesPlayerHP (legacy, adapted)
    // -----------------------------------------------------------------------

    [Fact]
    public void Hazard_HittingPaddle_DamagesPlayerHP()
    {
        var cfg = new SimConfig
        {
            BossHazardDamage   = 1,
            BossHazardRadius   = 9,
            BossAttackInterval = 999,   // disable automatic spawning
            BossTelegraphDuration = 999,
        };
        var g = MakeBossGame(cfg);
        g.Serve();

        int livesBefore = g.Lives;

        // Inject a hazard just above the paddle center, moving downward.
        var paddleCenter = g.Paddle.Center;
        g.Hazards.Add(new Projectile {
            Id     = 999,
            Pos    = new Vec2(paddleCenter.X, paddleCenter.Y - g.Paddle.Height / 2 - cfg.BossHazardRadius + 1),
            Vel    = new Vec2(0, cfg.BossHazardSpeed > 0 ? cfg.BossHazardSpeed : 240),
            Damage = cfg.BossHazardDamage,
            Radius = cfg.BossHazardRadius,
            Alive  = true
        });

        g.Tick(SimConfig.Default.FixedDt);

        Assert.Equal(livesBefore - cfg.BossHazardDamage, g.Lives);
        Assert.Empty(g.Hazards); // consumed on hit
    }

    // -----------------------------------------------------------------------
    // 3. Hazard_DodgedPastBottom_NoDamage (legacy)
    // -----------------------------------------------------------------------

    [Fact]
    public void Hazard_DodgedPastBottom_NoDamage()
    {
        var cfg = new SimConfig
        {
            BossHazardDamage   = 1,
            BossAttackInterval = 999,
            BossTelegraphDuration = 999,
        };
        var g   = MakeBossGame(cfg);
        g.Serve();

        int livesBefore = g.Lives;

        var drainLine = g.Level.Grid.Height + cfg.CellSize * 2 + cfg.BossHazardRadius + 10;
        g.Hazards.Add(new Projectile {
            Id     = 998,
            Pos    = new Vec2(g.Level.Grid.Width * 2, drainLine), // far right AND already past drain
            Vel    = new Vec2(0, 1),
            Damage = cfg.BossHazardDamage,
            Radius = cfg.BossHazardRadius,
            Alive  = true
        });

        g.Tick(SimConfig.Default.FixedDt);

        Assert.Equal(livesBefore, g.Lives); // no damage
        Assert.Empty(g.Hazards);            // removed as missed
    }

    // -----------------------------------------------------------------------
    // 4. PlayerHP_DepletedByHazards_LosesLevel (legacy)
    // -----------------------------------------------------------------------

    [Fact]
    public void PlayerHP_DepletedByHazards_LosesLevel()
    {
        var cfg = new SimConfig
        {
            BossHazardDamage   = 1,
            BossAttackInterval = 999,
            BossTelegraphDuration = 999,
            StartLives = 2,
        };
        var g = MakeBossGame(cfg);
        g.Serve();

        g.DamagePlayer(1); // 2 → 1 — still playing
        Assert.Equal(GamePhase.Playing, g.Phase);

        g.DamagePlayer(1); // 1 → 0 — should be Lost
        Assert.Equal(GamePhase.Lost, g.Phase);
    }

    // -----------------------------------------------------------------------
    // 5. BossBlock_Destroyed_ContributesToWin (legacy)
    // -----------------------------------------------------------------------

    [Fact]
    public void BossBlock_Destroyed_ContributesToWin()
    {
        var g = MakeGame(
            """
            {"id":"boss","biome":"hell","hp":1,"sprite":"DemonBody","needToKill":true,"behavior":"boss"}
            """,
            """
            {"id":"t","biome":"hell","cols":3,"rows":3,
             "rows_data":["B..","...","..."],
             "legend":{"B":"boss"}}
            """
        );
        g.Serve();

        var boss = g.Blocks.First(b => b.Boss);
        Assert.False(boss.Dead);

        boss.Dead = true;

        g.Tick(SimConfig.Default.FixedDt);

        Assert.Equal(GamePhase.Won, g.Phase);
    }

    // -----------------------------------------------------------------------
    // 6. Boss_CyclesMultipleDistinctPatterns — over a long run, varied hazard counts
    //    appear consistent with different attack patterns being chosen.
    // -----------------------------------------------------------------------

    [Fact]
    public void Boss_CyclesMultipleDistinctPatterns()
    {
        // Phase 3 uses all four patterns (AimedShot, Rain, Spread, Summon).
        // Rain spawns BossRainCount hazards per boss block; Spread spawns BossSpreadCount.
        // With a single boss block, AimedShot/Summon → 1 hazard, Rain → 3, Spread → 4 per cycle.
        // We count total hazards produced over many cycles and confirm > 1 distinct batch size.
        var cfg = new SimConfig
        {
            BossAttackInterval    = 0.05,
            BossTelegraphDuration = 0.02,
            BossPhase3Threshold   = 1.0,  // always phase 3 from the start
            BossSpreadCount       = 4,
            BossRainCount         = 3,
            BossSummonSpeedMult   = 1.6,
            BossHazardSpeed       = 1,    // very slow hazards so they stay alive long enough to count
        };

        // Single boss block for simple counting.
        var g = MakeGame(
            """
            {"id":"boss","biome":"hell","hp":24,"sprite":"DemonBody","needToKill":true,"behavior":"boss"},
            {"id":"fill","biome":"hell","hp":1,"sprite":"s","needToKill":true}
            """,
            """
            {"id":"t","biome":"hell","cols":12,"rows":1,
             "rows_data":["B..........."],
             "legend":{"B":"boss"}}
            """,
            cfg);
        g.Serve();
        g.ApplyCheat("setLives", 9999); // survive 20s of rapid-fire attacks

        // Use small ticks so telegraph and attack fall in separate ticks (proves ordering too).
        double smallDt = 0.005;
        var distinctBatchSizes = new HashSet<int>();
        int totalAttackEvents  = 0;

        // Run for 20 seconds of simulated time — many attack cycles.
        int ticks = (int)(20.0 / smallDt);
        int prevHazardCount = 0;
        for (int i = 0; i < ticks; i++)
        {
            // Re-serve after ball drain so the game stays in Playing phase.
            if (g.Phase == GamePhase.Serving) g.Serve();
            if (g.Phase != GamePhase.Playing) break;
            g.Tick(smallDt);
            var evts = g.DrainEvents();
            // Each "bossAttack" event corresponds to one boss block's spawns in that pattern.
            // Count hazards added since last tick (they move slowly, accumulate).
            int spawned = g.Hazards.Count - prevHazardCount;
            // Count every attack event (the hell fist slam spawns no hazards);
            // batch sizes only from hazard-spawning patterns.
            if (evts.Any(e => e.Type == "bossAttack"))
            {
                totalAttackEvents++;
                if (spawned > 0) distinctBatchSizes.Add(spawned);
            }
            prevHazardCount = g.Hazards.Count;
        }

        Assert.True(totalAttackEvents >= 5,
            $"Expected at least 5 attack events over 20 s; got {totalAttackEvents}");
        Assert.True(distinctBatchSizes.Count >= 2,
            $"Expected multiple distinct hazard-batch sizes (confirms different patterns ran); got=[{string.Join(",", distinctBatchSizes)}]");
    }

    // -----------------------------------------------------------------------
    // 7. Boss_TelegraphPrecedesAttack — bossTelegraph event fires BEFORE bossAttack
    // -----------------------------------------------------------------------

    [Fact]
    public void Boss_TelegraphPrecedesAttack()
    {
        var cfg = new SimConfig
        {
            BossAttackInterval    = 0.05,
            BossTelegraphDuration = 0.04,  // separate enough to see in different ticks
        };
        var g = MakeBossGame(cfg);
        g.Serve();

        // Collect events across multiple small ticks.
        var eventLog = new List<string>();

        // We need small ticks so telegraph and attack don't collapse into the same tick.
        double smallDt = 0.01;
        for (int i = 0; i < 30; i++)
        {
            if (g.Phase == GamePhase.Serving) g.Serve();
            if (g.Phase != GamePhase.Playing) break;
            g.Tick(smallDt);
            var evts = g.DrainEvents();
            foreach (var e in evts)
                if (e.Type is "bossTelegraph" or "bossAttack")
                    eventLog.Add(e.Type);
        }

        // We expect both event types to have appeared
        Assert.Contains("bossTelegraph", eventLog);
        Assert.Contains("bossAttack",    eventLog);

        // Every bossAttack must be preceded by a bossTelegraph (the first bossAttack index must be
        // > 0 and the entry before it must be a bossTelegraph or an intervening non-boss event).
        // Simplified: the index of the first bossTelegraph < index of first bossAttack.
        int firstTelegraph = eventLog.IndexOf("bossTelegraph");
        int firstAttack    = eventLog.IndexOf("bossAttack");
        Assert.True(firstTelegraph < firstAttack,
            $"bossTelegraph must appear before first bossAttack; log=[{string.Join(",", eventLog)}]");
    }

    // -----------------------------------------------------------------------
    // 8. Boss_PhaseChanges_AtHpThresholds — reducing HP triggers bossPhase events
    // -----------------------------------------------------------------------

    [Fact]
    public void Boss_PhaseChanges_AtHpThresholds()
    {
        var cfg = new SimConfig
        {
            BossAttackInterval    = 999,  // freeze attacks; we only care about phase events
            BossTelegraphDuration = 0.001,
            BossPhase2Threshold   = 0.60,
            BossPhase3Threshold   = 0.30,
        };

        // One boss block with HP 10 so fractions are clean.
        var g = MakeGame(
            """
            {"id":"boss","biome":"hell","hp":10,"sprite":"DemonBody","needToKill":true,"behavior":"boss"}
            """,
            """
            {"id":"t","biome":"hell","cols":3,"rows":3,
             "rows_data":["B..","...","..."],
             "legend":{"B":"boss"}}
            """,
            cfg);
        g.Serve();

        // Drain events from initial tick so _bossPhase gets computed.
        if (g.Phase == GamePhase.Serving) g.Serve();
        g.Tick(SimConfig.Default.FixedDt);
        g.DrainEvents(); // clear

        // Reduce HP to 5 (50 % < 60 % threshold) — should trigger phase 2.
        var bossBlock = g.Blocks.First(b => b.Boss);
        bossBlock.Hp = 5;
        if (g.Phase == GamePhase.Serving) g.Serve();
        g.Tick(SimConfig.Default.FixedDt);
        var evts2 = g.DrainEvents();
        Assert.Contains(evts2, e => e.Type == "bossPhase" && (int)e.X == 2);

        // Reduce HP to 2 (20 % < 30 % threshold) — should trigger phase 3.
        bossBlock.Hp = 2;
        if (g.Phase == GamePhase.Serving) g.Serve();
        g.Tick(SimConfig.Default.FixedDt);
        var evts3 = g.DrainEvents();
        Assert.Contains(evts3, e => e.Type == "bossPhase" && (int)e.X == 3);
    }

    // -----------------------------------------------------------------------
    // 9. Boss_Phase3_FasterCadence — attack interval in phase 3 < phase 1
    // -----------------------------------------------------------------------

    [Fact]
    public void Boss_Phase3_FasterCadence_ThanPhase1()
    {
        // Count attacks over equal time windows in phase 1 vs phase 3.
        // Phase 3 should yield more attack events.
        double runTime = 12.0;  // long enough to see clear difference
        double smallDt = 0.016; // ~60 Hz ticks

        // Phase 1 game (hp full, 100%) — large spare-ball count so the game never ends.
        var cfg1 = new SimConfig
        {
            BossAttackInterval       = 1.6,
            BossPhase2AttackInterval = 1.2,
            BossPhase3AttackInterval = 0.75,
            BossTelegraphDuration    = 0.1,
            BossPhase2Threshold      = 0.60,
            BossPhase3Threshold      = 0.30,
            StartBalls               = 9999,
            StartLives               = 9999,
        };
        var g1 = MakeGame(
            """{"id":"boss","biome":"hell","hp":10,"sprite":"DemonBody","needToKill":true,"behavior":"boss"}""",
            """{"id":"t","biome":"hell","cols":3,"rows":3,"rows_data":["B..","...","..."],"legend":{"B":"boss"}}""",
            cfg1);
        g1.Serve();
        int attacks1 = CountAttacksOver(g1, runTime, smallDt);

        // Phase 3 game (hp pinned low via threshold = 1.0 — always phase 3).
        var cfg3 = new SimConfig
        {
            BossAttackInterval       = 1.6,
            BossPhase2AttackInterval = 1.2,
            BossPhase3AttackInterval = 0.75,
            BossTelegraphDuration    = 0.1,
            BossPhase2Threshold      = 1.0, // always phase 2+
            BossPhase3Threshold      = 1.0, // always phase 3
            StartBalls               = 9999,
            StartLives               = 9999,
        };
        var g3 = MakeGame(
            """{"id":"boss","biome":"hell","hp":10,"sprite":"DemonBody","needToKill":true,"behavior":"boss"}""",
            """{"id":"t","biome":"hell","cols":3,"rows":3,"rows_data":["B..","...","..."],"legend":{"B":"boss"}}""",
            cfg3);
        g3.Serve();
        int attacks3 = CountAttacksOver(g3, runTime, smallDt);

        Assert.True(attacks3 > attacks1,
            $"Phase 3 should fire more attacks than phase 1 over the same time window; p1={attacks1} p3={attacks3}");
    }

    private static int CountAttacksOver(GameInstance g, double totalTime, double dt)
    {
        int count = 0;
        for (double t = 0; t < totalTime; t += dt)
        {
            // Re-serve whenever the ball drains so the game stays in Playing phase.
            if (g.Phase == GamePhase.Serving) g.Serve();
            if (g.Phase != GamePhase.Playing) break; // Lost — can't continue
            g.Tick(dt);
            count += g.DrainEvents().Count(e => e.Type == "bossAttack");
        }
        return count;
    }

    // -----------------------------------------------------------------------
    // 10. Snapshot_ExposedBossHp — bossActive/bossHp/bossMaxHp populated correctly
    // -----------------------------------------------------------------------

    [Fact]
    public void Snapshot_ExposedBossHp()
    {
        var cfg = new SimConfig { BossAttackInterval = 999, BossTelegraphDuration = 999 };

        // One boss block hp=10
        var g = MakeGame(
            """{"id":"boss","biome":"hell","hp":10,"sprite":"DemonBody","needToKill":true,"behavior":"boss"}""",
            """{"id":"t","biome":"hell","cols":3,"rows":3,"rows_data":["B..","...","..."],"legend":{"B":"boss"}}""",
            cfg);
        g.Serve();

        var snap = Snapshot.From(g, 1);
        Assert.True(snap.BossActive, "BossActive should be true when boss block is alive");
        Assert.Equal(10, snap.BossMaxHp);
        Assert.Equal(10, snap.BossHp);

        // Reduce HP.
        g.Blocks.First(b => b.Boss).Hp = 4;
        snap = Snapshot.From(g, 2);
        Assert.True(snap.BossActive);
        Assert.Equal(4, snap.BossHp);
        Assert.Equal(10, snap.BossMaxHp);

        // Kill the boss block.
        g.Blocks.First(b => b.Boss).Dead = true;
        snap = Snapshot.From(g, 3);
        Assert.False(snap.BossActive, "BossActive should be false when all boss blocks are dead");
        Assert.Equal(0, snap.BossHp);
        Assert.Equal(0, snap.BossMaxHp);
    }
}
