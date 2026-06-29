using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Multi-pattern, phased boss fight system.
///
/// Phases (derived from total alive boss HP fraction):
///   Phase 1 (> Boss.Phase2Threshold): slow, aimedShot / rain.
///   Phase 2 (> Boss.Phase3Threshold): faster, adds spread.
///   Phase 3 (enrage, ≤ Boss.Phase3Threshold): fastest, adds summon.
///
/// Each attack cycle:
///   1. Telegraph emitted (bossTelegraph event).
///   2. After Boss.TelegraphDuration seconds the attack fires (bossAttack event + hazards).
///
/// All tunables come from SimConfig.Boss / SimConfig.Enemies; determinism via Rng.
/// </summary>
internal static class BossSystem
{
    internal enum BossPattern { AimedShot, Rain, Spread, Summon }

    private static int ComputePhase(double hpFraction, SimConfig cfg)
    {
        if (hpFraction <= cfg.Boss.Phase3Threshold) return 3;
        if (hpFraction <= cfg.Boss.Phase2Threshold) return 2;
        return 1;
    }

    private static double AttackInterval(int phase, SimConfig cfg) => phase switch
    {
        3 => cfg.Boss.Phase3AttackInterval,
        2 => cfg.Boss.Phase2AttackInterval,
        _ => cfg.Boss.AttackInterval,
    };

    internal static void Update(GameInstance g, double dt)
    {
        var bossBlocks = g.Blocks.Where(b => !b.Dead && b.Boss).ToList();
        if (bossBlocks.Count == 0) return;

        int totalMaxHp = bossBlocks.Sum(b => b.MaxHp);
        int totalHp    = bossBlocks.Sum(b => b.Hp);
        double hpFrac  = totalMaxHp > 0 ? (double)totalHp / totalMaxHp : 0.0;
        int phase      = ComputePhase(hpFrac, g.Config);

        if (g.Boss.Phase != phase)
        {
            int prevPhase  = g.Boss.Phase;
            g.Boss.Phase   = phase;
            var bossOrigin = bossBlocks.Count > 0
                ? g.Level.Grid.CellCenter(bossBlocks[0].Col, bossBlocks[0].Row)
                : new Arkanoid.Core.Math.Vec2(g.Level.Grid.Width / 2, 0);
            g.RaiseEvent(SimEventKind.BossPhase, bossOrigin.X, bossOrigin.Y, phase);
        }

        double interval  = AttackInterval(phase, g.Config);
        double remaining = dt;

        if (g.Boss.TelegraphPending)
        {
            g.Boss.TelegraphTimer -= remaining;
            if (g.Boss.TelegraphTimer <= 0)
            {
                remaining = -g.Boss.TelegraphTimer;
                FirePattern(g, bossBlocks, g.Boss.PendingPattern);
                g.Boss.TelegraphPending = false;
            }
            else return;
        }

        g.Boss.AttackAccumulator += remaining;
        if (g.Boss.AttackAccumulator < interval) return;
        g.Boss.AttackAccumulator -= interval;

        BossPattern pattern = ChoosePattern(g, phase);

        var firstBoss = bossBlocks[0];
        var origin    = g.Level.Grid.CellCenter(firstBoss.Col, firstBoss.Row);
        g.RaiseEvent(SimEventKind.BossTelegraph, origin.X, origin.Y);

        if (g.Level.BossKind == BossKind.Hell && pattern == BossPattern.AimedShot)
        {
            g.Boss.FistCol = (int)System.Math.Clamp(
                (g.Paddle.Center.X - g.Config.BoardOriginX) / g.Config.CellSize,
                0, g.Level.Grid.Cols - 1);
            var colX = g.Level.Grid.CellCenter(g.Boss.FistCol, 0).X;
            g.RaiseEvent(SimEventKind.FistTelegraph, colX, origin.Y);
        }

        if (g.Level.BossKind == BossKind.Caverns)
            GoblinHop(g, bossBlocks);

        g.Boss.TelegraphPending = true;
        g.Boss.TelegraphTimer   = g.Config.Boss.TelegraphDuration;
        g.Boss.PendingPattern   = (int)pattern;
    }

    private static void GoblinHop(GameInstance g, List<Entities.Block> bossBlocks)
    {
        int[] anchors = { -g.Config.Boss.GoblinHopOffset, 0, g.Config.Boss.GoblinHopOffset };
        var curr = anchors[g.Boss.GoblinAnchorIdx % anchors.Length];
        g.Boss.GoblinAnchorIdx = (g.Boss.GoblinAnchorIdx + 1) % anchors.Length;
        var next  = anchors[g.Boss.GoblinAnchorIdx];
        var delta = next - curr;
        if (delta == 0) return;

        foreach (var b in bossBlocks)
        {
            var c = b.Col + delta;
            if (c < 0 || c >= g.Level.Grid.Cols) return;
        }
        foreach (var b in bossBlocks) b.Col += delta;
        var origin = g.Level.Grid.CellCenter(bossBlocks[0].Col, bossBlocks[0].Row);
        g.RaiseEvent(SimEventKind.BossHop, origin.X, origin.Y);
    }

    private static BossPattern ChoosePattern(GameInstance g, int phase)
    {
        return phase switch
        {
            1 => g.Rng.NextDouble() < 0.6 ? BossPattern.AimedShot : BossPattern.Rain,
            2 => g.Rng.NextDouble() switch
            {
                < 0.35 => BossPattern.AimedShot,
                < 0.65 => BossPattern.Rain,
                _      => BossPattern.Spread,
            },
            _ => g.Rng.NextDouble() switch
            {
                < 0.25 => BossPattern.AimedShot,
                < 0.45 => BossPattern.Rain,
                < 0.70 => BossPattern.Spread,
                _      => BossPattern.Summon,
            },
        };
    }

    private static void FirePattern(GameInstance g, List<Entities.Block> bossBlocks, int patternInt)
    {
        var pattern = (BossPattern)patternInt;

        var kind = g.Level.BossKind switch
        {
            BossKind.Village => "witchmagic",
            BossKind.Hell    => "hellball",
            BossKind.Heaven  => "heavenmissile",
            BossKind.Caverns => "stalactite",
            _                => "",
        };

        foreach (var boss in bossBlocks)
        {
            var origin = g.Level.Grid.CellCenter(boss.Col, boss.Row);

            switch (pattern)
            {
                case BossPattern.AimedShot:
                    if (g.Level.BossKind == BossKind.Hell)         FistSlam(g);
                    else if (g.Level.BossKind == BossKind.Village) SpawnWitchGrab(g, origin);
                    else                                            SpawnAimedShot(g, origin, kind);
                    break;
                case BossPattern.Rain:
                    SpawnRain(g, origin, kind);
                    break;
                case BossPattern.Spread:
                    SpawnSpread(g, origin, kind);
                    break;
                case BossPattern.Summon:
                    if (g.Level.BossKind == BossKind.Heaven) SeraphSummon(g, boss);
                    else                                      SpawnSummon(g, origin, kind);
                    break;
            }

            g.RaiseEvent(SimEventKind.BossAttack, origin.X, origin.Y);
        }
    }

    private static void FistSlam(GameInstance g)
    {
        if (g.Boss.FistCol < 0) return;
        int col  = g.Boss.FistCol;
        g.Boss.FistCol = -1;
        var colX = g.Level.Grid.CellCenter(col, 0).X;

        foreach (var blk in g.Blocks.Where(b => !b.Dead && !b.Boss && b.Col == col).ToList())
            BlockDamage.DamageBlock(g, blk, g.Config.Boss.FistBlockDamage, igniteSource: false, killMult: 0.5);

        var half = g.Config.CellSize / 2;
        if (System.Math.Abs(g.Paddle.Center.X - colX) <= half + g.Paddle.Width / 2)
            CombatSystem.DamagePlayer(g, g.Config.Boss.FistDamage);

        g.RaiseEvent(SimEventKind.FistSlam, colX, g.Level.Grid.Height);
    }

    private static void SpawnWitchGrab(GameInstance g, Vec2 origin)
    {
        if (g.Hazards.Any(h => h.Alive && h.Behavior == HazardBehavior.WitchGrab)) return;
        var ball = g.Balls.FirstOrDefault(b => b.Alive && b.GrabberId == 0);
        if (ball == null) return;
        var dir = (ball.Pos - origin).Normalized();
        g.Hazards.Add(new Projectile
        {
            Id       = g._nextHazardId++,
            Pos      = origin,
            Vel      = dir * g.Config.Boss.WitchGrabSpeed,
            Damage   = 0,
            Radius   = g.Config.Enemies.HazardRadius * 1.4,
            Alive    = true,
            Kind     = "witchgrab",
            Behavior = HazardBehavior.WitchGrab,
        });
        g.RaiseEvent(SimEventKind.WitchGrabCast, origin.X, origin.Y);
    }

    private static void SeraphSummon(GameInstance g, Entities.Block boss)
    {
        var adds = g.Blocks.Count(b => !b.Dead && b.Emitter && !b.NeedToKill);
        var wantVase = g.Boss.SeraphSummonVase && adds > 0;
        g.Boss.SeraphSummonVase = !g.Boss.SeraphSummonVase;

        int row = System.Math.Min(boss.Row + 2, g.Level.Grid.Rows - 1);
        int col = -1;
        for (int offset = 0; offset < g.Level.Grid.Cols; offset++)
        {
            foreach (var c in new[] { boss.Col + offset, boss.Col - offset })
            {
                if (c < 0 || c >= g.Level.Grid.Cols) continue;
                if (g.BlockAt(c, row) == null) { col = c; break; }
            }
            if (col >= 0) break;
        }
        if (col < 0) return;

        if (wantVase)
        {
            var vase = new Entities.Block
            {
                Id = g.NextBlockId(), Col = col, Row = row,
                Hp = 2, MaxHp = 2, TypeId = "boss_vase",
                Sprite = "HeavenVaza", NeedToKill = false,
                Behavior = BlockBehavior.BossVase,
            };
            vase.FuseTimer = g.Config.Boss.SeraphVaseFuse;
            g.Blocks.Add(vase);
            var cp = g.Level.Grid.CellCenter(col, row);
            g.RaiseEvent(SimEventKind.SeraphVase, cp.X, cp.Y);
        }
        else if (adds < g.Config.Boss.SeraphMaxAdds)
        {
            g.Blocks.Add(new Entities.Block
            {
                Id = g.NextBlockId(), Col = col, Row = row,
                Hp = g.Config.Boss.SeraphAddHp, MaxHp = g.Config.Boss.SeraphAddHp,
                TypeId = "seraph_add", Sprite = "HeavenMeleeStatue", NeedToKill = false,
                Behavior = BlockBehavior.Emitter,
                EmitInterval = g.Config.Enemies.DefaultEmitInterval,
                EmitAim = "paddle", MissileKind = "heavenmissile",
            });
            var cp = g.Level.Grid.CellCenter(col, row);
            g.RaiseEvent(SimEventKind.SeraphAdd, cp.X, cp.Y);
        }
    }

    internal static void UpdateVaseFuses(GameInstance g, double dt)
    {
        foreach (var blk in g.Blocks)
        {
            if (blk.Dead || !blk.BossVase) continue;
            blk.FuseTimer -= dt;
            if (blk.FuseTimer > 0) continue;
            blk.Dead = true;
            BallSystem.LevelUpStatues(g);
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            g.RaiseEvent(SimEventKind.VaseShatter, c.X, c.Y);
        }
    }

    private static void SpawnAimedShot(GameInstance g, Vec2 origin, string kind = "")
    {
        var dx   = g.Paddle.Center.X - origin.X;
        var aimX = dx * g.Config.Boss.HazardAimStrength;
        var vel  = new Vec2(aimX, g.Config.Boss.HazardSpeed);
        AddHazard(g, origin, vel, kind);
    }

    private static void SpawnRain(GameInstance g, Vec2 origin, string kind = "")
    {
        if (g.Level.BossKind == BossKind.Caverns)
        {
            StalactiteSystem.BossDrop(g, g.Config.Boss.RainCount);
            return;
        }
        double boardW = g.Level.Grid.Width;
        for (int i = 0; i < g.Config.Boss.RainCount; i++)
        {
            double x = g.Rng.Range(0, boardW);
            AddHazard(g, new Vec2(x, origin.Y), new Vec2(0, g.Config.Boss.HazardSpeed), kind);
        }
    }

    private static void SpawnSpread(GameInstance g, Vec2 origin, string kind = "")
    {
        int count      = g.Config.Boss.SpreadCount;
        double halfRad = g.Config.Boss.SpreadHalfAngleDeg * System.Math.PI / 180.0;
        double speed   = g.Config.Boss.HazardSpeed;

        for (int i = 0; i < count; i++)
        {
            double t     = count > 1 ? (double)i / (count - 1) : 0.5;
            double angle = -halfRad + t * 2 * halfRad;
            AddHazard(g, origin, new Vec2(speed * System.Math.Sin(angle), speed * System.Math.Cos(angle)), kind);
        }
    }

    private static void SpawnSummon(GameInstance g, Vec2 origin, string kind = "")
    {
        var dx  = g.Paddle.Center.X - origin.X;
        var vel = new Vec2(dx * g.Config.Boss.SummonAimStrength,
                           g.Config.Boss.HazardSpeed * g.Config.Boss.SummonSpeedMult);
        AddHazard(g, origin, vel, kind);
    }

    private static void AddHazard(GameInstance g, Vec2 pos, Vec2 vel, string kind = "")
        => g.Hazards.Add(new Projectile
        {
            Id     = g._nextHazardId++,
            Pos    = pos,
            Vel    = vel,
            Damage = g.Config.Boss.HazardDamage,
            Radius = g.Config.Boss.HazardRadius,
            Alive  = true,
            Kind   = kind,
        });
}
