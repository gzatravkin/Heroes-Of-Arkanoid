using Arkanoid.Core.Blocks;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Tests for the boss block mechanic: hazard spawning, paddle damage, dodge, HP-loss,
/// and boss-block destruction contributing to a win condition.
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

    // A minimal level with exactly one boss block (and one normal block to keep
    // the level winnable without setting the boss dead itself).
    private static GameInstance MakeBossGame(SimConfig? cfg = null)
        => MakeGame(
            """
            {"id":"boss","biome":"hell","hp":20,"sprite":"DemonBody","needToKill":true,"boss":true},
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
    // 1. Boss_SpawnsHazardsOverTime
    // -----------------------------------------------------------------------

    [Fact]
    public void Boss_SpawnsHazardsOverTime()
    {
        // Use a config where the attack interval is very short so a small dt triggers it.
        var cfg = new SimConfig { BossAttackInterval = 0.1 };
        var g = MakeBossGame(cfg);
        g.Serve();

        Assert.Empty(g.Hazards); // no hazards before any ticks

        // Tick past one attack interval (0.2 s > 0.1 s)
        var dt = cfg.BossAttackInterval * 2;
        g.Tick(dt);

        Assert.True(g.Hazards.Count > 0,
            $"Expected at least one hazard after ticking past BossAttackInterval; count={g.Hazards.Count}");
    }

    // -----------------------------------------------------------------------
    // 2. Hazard_HittingPaddle_DamagesPlayerHP
    // -----------------------------------------------------------------------

    [Fact]
    public void Hazard_HittingPaddle_DamagesPlayerHP()
    {
        var cfg = new SimConfig
        {
            BossHazardDamage = 1,
            BossHazardRadius = 9,
            BossAttackInterval = 999, // disable automatic spawning — we inject manually
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
    // 3. Hazard_DodgedPastBottom_NoDamage
    // -----------------------------------------------------------------------

    [Fact]
    public void Hazard_DodgedPastBottom_NoDamage()
    {
        var cfg = new SimConfig { BossHazardDamage = 1, BossAttackInterval = 999 };
        var g   = MakeBossGame(cfg);
        g.Serve();

        int livesBefore = g.Lives;

        // Place hazard far to the right of the paddle so it never hits, and below
        // the drain line so it registers as missed on the next tick.
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
    // 4. PlayerHP_DepletedByHazards_LosesLevel
    // -----------------------------------------------------------------------

    [Fact]
    public void PlayerHP_DepletedByHazards_LosesLevel()
    {
        var cfg = new SimConfig { BossHazardDamage = 1, BossAttackInterval = 999, StartLives = 2 };
        var g   = MakeBossGame(cfg);
        g.Serve();

        // DamagePlayer directly until Lives reaches 0.
        g.DamagePlayer(1); // 2 -> 1 — still playing
        Assert.Equal(GamePhase.Playing, g.Phase);

        g.DamagePlayer(1); // 1 -> 0 — should be Lost
        Assert.Equal(GamePhase.Lost, g.Phase);
    }

    // -----------------------------------------------------------------------
    // 5. BossBlock_Destroyed_ContributesToWin
    // -----------------------------------------------------------------------

    [Fact]
    public void BossBlock_Destroyed_ContributesToWin()
    {
        // Level with ONE boss block as the only needToKill block.
        var g = MakeGame(
            """
            {"id":"boss","biome":"hell","hp":1,"sprite":"DemonBody","needToKill":true,"boss":true}
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

        // Mark boss dead (simulates destruction via ball/projectile).
        boss.Dead = true;

        g.Tick(SimConfig.Default.FixedDt);

        Assert.Equal(GamePhase.Won, g.Phase);
    }
}
