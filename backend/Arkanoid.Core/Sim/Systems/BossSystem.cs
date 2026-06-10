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

        // Demon fist (hell): lock the strike column NOW so the slam is dodgeable, and
        // telegraph it visually at the column's x (docs/11 boss verbs).
        if (g.Level.Biome == "hell" && pattern == BossPattern.AimedShot)
        {
            g._bossFistCol = (int)System.Math.Clamp(
                (g.Paddle.Center.X - g.Config.BoardOriginX) / g.Config.CellSize,
                0, g.Level.Grid.Cols - 1);
            var colX = g.Level.Grid.CellCenter(g._bossFistCol, 0).X;
            g.RaiseEvent("fistTelegraph", colX, origin.Y);
        }

        // Goblin (caverns): hop to the next anchor as the attack winds up — repositioning
        // resets the player's aim solution (the original's 3-position hop).
        if (g.Level.Biome == "caverns")
            GoblinHop(g, bossBlocks);

        // --- Arm delayed attack ---
        g._bossTelegraphPending = true;
        g._bossTelegraphTimer   = g.Config.BossTelegraphDuration;
        g._bossPendingPattern   = (int)pattern;
    }

    /// <summary>Move the Goblin's boss blocks to the next of 3 anchors (−N, 0, +N columns).</summary>
    private static void GoblinHop(GameInstance g, List<Entities.Block> bossBlocks)
    {
        int[] anchors = { -g.Config.GoblinHopOffset, 0, g.Config.GoblinHopOffset };
        var curr = anchors[g._goblinAnchorIdx % anchors.Length];
        g._goblinAnchorIdx = (g._goblinAnchorIdx + 1) % anchors.Length;
        var next  = anchors[g._goblinAnchorIdx];
        var delta = next - curr;
        if (delta == 0) return;

        // Bounds check against the whole rig before moving.
        foreach (var b in bossBlocks)
        {
            var c = b.Col + delta;
            if (c < 0 || c >= g.Level.Grid.Cols) return; // would clip the wall — skip this hop
        }
        foreach (var b in bossBlocks) b.Col += delta;
        var origin = g.Level.Grid.CellCenter(bossBlocks[0].Col, bossBlocks[0].Row);
        g.RaiseEvent("bossHop", origin.X, origin.Y);
        g._log.Log(g.TickCount, "boss", "hopped", $"delta={delta}");
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

        // Every boss bolt carries a biome kind so the renderer draws the correct missile
        // art (WitchMagic1-4 cycle, HellBallMissile, heaven Missile, falling stalactites)
        // instead of a generic dot.
        var kind = g.Level.Biome switch
        {
            "village" => "witchmagic",
            "hell"    => "hellball",
            "heaven"  => "heavenmissile",
            "caverns" => "stalactite",
            _         => "",
        };

        foreach (var boss in bossBlocks)
        {
            var origin = g.Level.Grid.CellCenter(boss.Col, boss.Row);

            switch (pattern)
            {
                case BossPattern.AimedShot:
                    // Signature overrides: Demon slams a fist column; the Witch grabs the ball.
                    if (g.Level.Biome == "hell")         FistSlam(g);
                    else if (g.Level.Biome == "village") SpawnWitchGrab(g, origin);
                    else                                  SpawnAimedShot(g, origin, kind);
                    break;

                case BossPattern.Rain:
                    SpawnRain(g, origin, kind);
                    break;

                case BossPattern.Spread:
                    SpawnSpread(g, origin, kind);
                    break;

                case BossPattern.Summon:
                    // Signature override: the Seraph summons statue adds / a fused vase.
                    if (g.Level.Biome == "heaven") SeraphSummon(g, boss);
                    else                            SpawnSummon(g, origin, kind);
                    break;
            }

            g.RaiseEvent("bossAttack", origin.X, origin.Y);
            g._log.Log(g.TickCount, "boss", "attack fired",
                $"pattern={pattern} bossId={boss.Id} paddleX={g.Paddle.Center.X:F1}");
        }
    }

    // -----------------------------------------------------------------------
    // Signature mechanics (docs/11 §4 — one verb per boss)
    // -----------------------------------------------------------------------

    /// <summary>Demon: slam the column locked at telegraph time — HP hit if the paddle
    /// stayed there, and every block in the column is crushed for FistBlockDamage
    /// (slams open lanes the player can exploit).</summary>
    private static void FistSlam(GameInstance g)
    {
        if (g._bossFistCol < 0) return;
        int col  = g._bossFistCol;
        g._bossFistCol = -1;
        var colX = g.Level.Grid.CellCenter(col, 0).X;

        foreach (var blk in g.Blocks.Where(b => !b.Dead && !b.Boss && b.Col == col).ToList())
            BlockDamage.DamageBlock(g, blk, g.Config.FistBlockDamage, igniteSource: false);

        var half = g.Config.CellSize / 2;
        if (System.Math.Abs(g.Paddle.Center.X - colX) <= half + g.Paddle.Width / 2)
            CombatSystem.DamagePlayer(g, g.Config.BossFistDamage);

        g.RaiseEvent("fistSlam", colX, g.Level.Grid.Height);
        g._log.Log(g.TickCount, "boss", "fist slam", $"col={col}");
    }

    /// <summary>Witch: send the grab-hand homing after a ball (one in flight at a time).
    /// CombatSystem owns the grab/carry/throw lifecycle.</summary>
    private static void SpawnWitchGrab(GameInstance g, Vec2 origin)
    {
        if (g.Hazards.Any(h => h.Alive && h.Kind == "witchgrab")) return;
        var ball = g.Balls.FirstOrDefault(b => b.Alive && b.GrabberId == 0);
        if (ball == null) return;
        var dir = (ball.Pos - origin).Normalized();
        g.Hazards.Add(new Projectile
        {
            Id     = g._nextHazardId++,
            Pos    = origin,
            Vel    = dir * g.Config.WitchGrabSpeed,
            Damage = 0, // the grab itself never chips HP — the stolen ball is the threat
            Radius = g.Config.EnemyHazardRadius * 1.4,
            Alive  = true,
            Kind   = "witchgrab",
        });
        g.RaiseEvent("witchGrabCast", origin.X, origin.Y);
        g._log.Log(g.TickCount, "boss", "witch grab cast", "");
    }

    /// <summary>Seraph: alternate between summoning a melee-statue add (capped) and a
    /// fused boss-vase that levels his adds unless the player destroys it first.</summary>
    private static void SeraphSummon(GameInstance g, Entities.Block boss)
    {
        var adds = g.Blocks.Count(b => !b.Dead && b.Emitter && !b.NeedToKill);
        var wantVase = g._seraphSummonVase && adds > 0;
        g._seraphSummonVase = !g._seraphSummonVase;

        // Find a free cell two rows below the boss, scanning outward from its column.
        int row = System.Math.Min(boss.Row + 2, g.Level.Grid.Rows - 1);
        int col = -1;
        for (int offset = 0; offset < g.Level.Grid.Cols; offset++)
        {
            foreach (var c in new[] { boss.Col + offset, boss.Col - offset })
            {
                if (c < 0 || c >= g.Level.Grid.Cols) continue;
                if (!g.Blocks.Any(b => !b.Dead && b.Col == c && b.Row == row)) { col = c; break; }
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
            vase.FuseTimer = g.Config.SeraphVaseFuse;
            g.Blocks.Add(vase);
            g.RaiseEvent("seraphVase", g.Level.Grid.CellCenter(col, row).X, g.Level.Grid.CellCenter(col, row).Y);
            g._log.Log(g.TickCount, "boss", "seraph vase summoned", $"cell=({col},{row}) fuse={vase.FuseTimer}");
        }
        else if (adds < g.Config.SeraphMaxAdds)
        {
            g.Blocks.Add(new Entities.Block
            {
                Id = g.NextBlockId(), Col = col, Row = row,
                Hp = g.Config.SeraphAddHp, MaxHp = g.Config.SeraphAddHp, TypeId = "seraph_add",
                Sprite = "HeavenMeleeStatue", NeedToKill = false,
                Behavior = BlockBehavior.Emitter, EmitInterval = g.Config.DefaultEmitInterval,
                EmitAim = "paddle", MissileKind = "heavenmissile",
            });
            g.RaiseEvent("seraphAdd", g.Level.Grid.CellCenter(col, row).X, g.Level.Grid.CellCenter(col, row).Y);
            g._log.Log(g.TickCount, "boss", "seraph add summoned", $"cell=({col},{row})");
        }
    }

    /// <summary>Tick the fuses on Seraph boss-vases: expiry shatters them and levels his adds.</summary>
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
            g.RaiseEvent("vaseShatter", c.X, c.Y);
            g._log.Log(g.TickCount, "boss", "vase shattered — adds levelled", "");
        }
    }

    // -----------------------------------------------------------------------
    // Individual pattern spawners
    // -----------------------------------------------------------------------

    /// <summary>AimedShot — single hazard aimed at paddle X.</summary>
    private static void SpawnAimedShot(GameInstance g, Vec2 origin, string kind = "")
    {
        var dx   = g.Paddle.Center.X - origin.X;
        var aimX = dx * g.Config.BossHazardAimStrength;
        var vel  = new Vec2(aimX, g.Config.BossHazardSpeed);
        AddHazard(g, origin, vel, kind);
    }

    /// <summary>Rain — BossRainCount hazards at random X. The Caverns Goblin rains stalactites.</summary>
    private static void SpawnRain(GameInstance g, Vec2 origin, string kind = "")
    {
        if (g.Level.Biome == "caverns")
        {
            StalactiteSystem.BossDrop(g, g.Config.BossRainCount);
            return;
        }
        double boardW = g.Level.Grid.Width;
        for (int i = 0; i < g.Config.BossRainCount; i++)
        {
            double x   = g.Rng.Range(0, boardW);
            var pos    = new Vec2(x, origin.Y);
            var vel    = new Vec2(0, g.Config.BossHazardSpeed);
            AddHazard(g, pos, vel, kind);
        }
    }

    /// <summary>Spread — fan of BossSpreadCount hazards centred on the paddle at ±BossSpreadHalfAngleDeg.</summary>
    private static void SpawnSpread(GameInstance g, Vec2 origin, string kind = "")
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
            AddHazard(g, origin, new Vec2(vx, vy), kind);
        }
    }

    /// <summary>Summon — one fast minion hazard that strongly tracks the paddle X.</summary>
    private static void SpawnSummon(GameInstance g, Vec2 origin, string kind = "")
    {
        var dx   = g.Paddle.Center.X - origin.X;
        var aimX = dx * g.Config.BossSummonAimStrength;
        var vy   = g.Config.BossHazardSpeed * g.Config.BossSummonSpeedMult;
        var vel  = new Vec2(aimX, vy);
        AddHazard(g, origin, vel, kind);
    }

    // -----------------------------------------------------------------------
    // Hazard factory
    // -----------------------------------------------------------------------

    private static void AddHazard(GameInstance g, Vec2 pos, Vec2 vel, string kind = "")
        => g.Hazards.Add(new Projectile
        {
            Id     = g._nextHazardId++,
            Pos    = pos,
            Vel    = vel,
            Damage = g.Config.BossHazardDamage,
            Radius = g.Config.BossHazardRadius,
            Alive  = true,
            Kind   = kind,
        });
}
