using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Multi-pattern, phased boss fight system.
///
/// Phases (derived from total alive boss HP fraction):
///   Phase 1 (> BossPhase2Threshold): slow, aimedShot / rain.
///   Phase 2 (> BossPhase3Threshold): faster, adds spread.
///   Phase 3 (enrage, ≤ BossPhase3Threshold): fastest, adds summon.
///
/// Each attack cycle:
///   1. Telegraph emitted (bossTelegraph event).
///   2. After BossTelegraphDuration seconds the attack fires (bossAttack event + hazards).
///
/// All tunables come from SimConfig; determinism via Rng.
/// </summary>
internal static class BossSystem
{
    // -----------------------------------------------------------------------
    // Pattern enum (kept internal — tests inspect via event / hazard counts)
    // -----------------------------------------------------------------------

    internal enum BossPattern { AimedShot, Rain, Spread, Summon }

    // -----------------------------------------------------------------------
    // Phase detection helpers
    // -----------------------------------------------------------------------

    /// <summary>Returns 1, 2, or 3.</summary>
    private static int ComputePhase(double hpFraction, SimConfig cfg)
    {
        if (hpFraction <= cfg.BossPhase3Threshold) return 3;
        if (hpFraction <= cfg.BossPhase2Threshold) return 2;
        return 1;
    }

    private static double AttackInterval(int phase, SimConfig cfg) => phase switch
    {
        3 => cfg.BossPhase3AttackInterval,
        2 => cfg.BossPhase2AttackInterval,
        _ => cfg.BossAttackInterval,
    };

    // -----------------------------------------------------------------------
    // Main tick entry
    // -----------------------------------------------------------------------

    internal static void Update(GameInstance g, double dt)
    {
        var bossBlocks = g.Blocks.Where(b => !b.Dead && b.Boss).ToList();
        if (bossBlocks.Count == 0) return;

        // --- Phase tracking / change detection ---
        int totalMaxHp = bossBlocks.Sum(b => b.MaxHp);
        int totalHp    = bossBlocks.Sum(b => b.Hp);
        double hpFrac  = totalMaxHp > 0 ? (double)totalHp / totalMaxHp : 0.0;
        int phase      = ComputePhase(hpFrac, g.Config);

        if (g._bossPhase != phase)
        {
            int prevPhase  = g._bossPhase;
            g._bossPhase   = phase;
            g.RaiseEvent("bossPhase", phase, prevPhase);
            g._log.Log(g.TickCount, "boss", "phase changed", $"phase={phase} hpFrac={hpFrac:F2}");
        }

        double interval   = AttackInterval(phase, g.Config);
        double remaining  = dt; // how much time budget is left in this tick

        // --- Telegraph timer: if one is pending, count it down first ---
        if (g._bossTelegraphPending)
        {
            g._bossTelegraphTimer -= remaining;
            if (g._bossTelegraphTimer <= 0)
            {
                // Telegraph expired; the overshoot is credited back as remaining time.
                remaining = -g._bossTelegraphTimer;
                FirePattern(g, bossBlocks, g._bossPendingPattern);
                g._bossTelegraphPending = false;
                // Fall through: remaining time may now advance the attack accumulator.
            }
            else
            {
                // Still waiting for the telegraph window — hold the attack accumulator.
                return;
            }
        }

        // --- Attack interval accumulator ---
        g._bossAttackAccumulator += remaining;
        if (g._bossAttackAccumulator < interval) return;
        g._bossAttackAccumulator -= interval;

        // --- Choose next pattern ---
        BossPattern pattern = ChoosePattern(g, phase);

        // --- Emit telegraph ---
        var firstBoss = bossBlocks[0];
        var origin    = g.Level.Grid.CellCenter(firstBoss.Col, firstBoss.Row);
        g.RaiseEvent("bossTelegraph", origin.X, origin.Y);
        g._log.Log(g.TickCount, "boss", "telegraph", $"pattern={pattern} phase={phase}");

        // --- Arm delayed attack ---
        g._bossTelegraphPending = true;
        g._bossTelegraphTimer   = g.Config.BossTelegraphDuration;
        g._bossPendingPattern   = (int)pattern;
    }

    // -----------------------------------------------------------------------
    // Pattern selection (deterministic via Rng)
    // -----------------------------------------------------------------------

    private static BossPattern ChoosePattern(GameInstance g, int phase)
    {
        // Weighted pool per phase so each phase has a distinct feel
        return phase switch
        {
            1 => g.Rng.NextDouble() < 0.6 ? BossPattern.AimedShot : BossPattern.Rain,
            2 => g.Rng.NextDouble() switch
            {
                < 0.35 => BossPattern.AimedShot,
                < 0.65 => BossPattern.Rain,
                _      => BossPattern.Spread,
            },
            _ => g.Rng.NextDouble() switch        // phase 3
            {
                < 0.25 => BossPattern.AimedShot,
                < 0.45 => BossPattern.Rain,
                < 0.70 => BossPattern.Spread,
                _      => BossPattern.Summon,
            },
        };
    }

    // -----------------------------------------------------------------------
    // Fire a pattern (spawn hazards + emit bossAttack)
    // -----------------------------------------------------------------------

    private static void FirePattern(GameInstance g, List<Entities.Block> bossBlocks, int patternInt)
    {
        var pattern = (BossPattern)patternInt;

        foreach (var boss in bossBlocks)
        {
            var origin = g.Level.Grid.CellCenter(boss.Col, boss.Row);

            switch (pattern)
            {
                case BossPattern.AimedShot:
                    SpawnAimedShot(g, origin);
                    break;

                case BossPattern.Rain:
                    SpawnRain(g, origin);
                    break;

                case BossPattern.Spread:
                    SpawnSpread(g, origin);
                    break;

                case BossPattern.Summon:
                    SpawnSummon(g, origin);
                    break;
            }

            g.RaiseEvent("bossAttack", origin.X, origin.Y);
            g._log.Log(g.TickCount, "boss", "attack fired",
                $"pattern={pattern} bossId={boss.Id} paddleX={g.Paddle.Center.X:F1}");
        }
    }

    // -----------------------------------------------------------------------
    // Individual pattern spawners
    // -----------------------------------------------------------------------

    /// <summary>AimedShot — single hazard aimed at paddle X.</summary>
    private static void SpawnAimedShot(GameInstance g, Vec2 origin)
    {
        var dx   = g.Paddle.Center.X - origin.X;
        var aimX = dx * g.Config.BossHazardAimStrength;
        var vel  = new Vec2(aimX, g.Config.BossHazardSpeed);
        AddHazard(g, origin, vel);
    }

    /// <summary>Rain — BossRainCount hazards at random X positions across the board width.</summary>
    private static void SpawnRain(GameInstance g, Vec2 origin)
    {
        double boardW = g.Level.Grid.Width;
        for (int i = 0; i < g.Config.BossRainCount; i++)
        {
            double x   = g.Rng.Range(0, boardW);
            var pos    = new Vec2(x, origin.Y);
            var vel    = new Vec2(0, g.Config.BossHazardSpeed);
            AddHazard(g, pos, vel);
        }
    }

    /// <summary>Spread — fan of BossSpreadCount hazards centred on the paddle at ±BossSpreadHalfAngleDeg.</summary>
    private static void SpawnSpread(GameInstance g, Vec2 origin)
    {
        int count    = g.Config.BossSpreadCount;
        double halfRad = g.Config.BossSpreadHalfAngleDeg * System.Math.PI / 180.0;
        double speed = g.Config.BossHazardSpeed;

        for (int i = 0; i < count; i++)
        {
            // Evenly distribute angles from -half to +half
            double t     = count > 1 ? (double)i / (count - 1) : 0.5;
            double angle = -halfRad + t * 2 * halfRad; // radians offset from straight down
            // "straight down" direction is (0, +1); rotate by angle around Z
            double vx = speed * System.Math.Sin(angle);
            double vy = speed * System.Math.Cos(angle);
            AddHazard(g, origin, new Vec2(vx, vy));
        }
    }

    /// <summary>Summon — one fast minion hazard that strongly tracks the paddle X.</summary>
    private static void SpawnSummon(GameInstance g, Vec2 origin)
    {
        var dx   = g.Paddle.Center.X - origin.X;
        var aimX = dx * g.Config.BossSummonAimStrength;
        var vy   = g.Config.BossHazardSpeed * g.Config.BossSummonSpeedMult;
        var vel  = new Vec2(aimX, vy);
        AddHazard(g, origin, vel);
    }

    // -----------------------------------------------------------------------
    // Hazard factory
    // -----------------------------------------------------------------------

    private static void AddHazard(GameInstance g, Vec2 pos, Vec2 vel)
        => g.Hazards.Add(new Projectile
        {
            Id     = g._nextHazardId++,
            Pos    = pos,
            Vel    = vel,
            Damage = g.Config.BossHazardDamage,
            Radius = g.Config.BossHazardRadius,
            Alive  = true,
        });
}
