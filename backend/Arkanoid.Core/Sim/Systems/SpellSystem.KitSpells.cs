using System.Linq;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// G2c kit-completion spells (docs/09 G2 — every class reaches a 5-spell kit):
///   Fire Mage  PHOENIX     — a firebird circles your ball, searing nearby blocks.
///   Paladin    PENETRATION — arms the next deflect with phase-through hits.
///   Paladin    LAST DAY    — for a while, every top-wall bounce smites the ball's column.
///   Engineer   MAGNET      — balls steer toward the nearest block (aim assist).
///   Engineer   OVERLOAD    — places a friendly chain-bomb block above the paddle.
///   Necromancer BONE GOLEM — a slow heavy projectile that pierces several blocks.
///   Necromancer SKELETAL MAGE — a fan of skeleton bolts from the paddle.
/// </summary>
internal static partial class SpellSystem
{
    // ── Casts ──────────────────────────────────────────────────────────────────

    internal static void CastPhoenix(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.PhoenixCost, "phoenix")) return;
        g._phoenixRemaining = g.Config.PhoenixDuration;
        g._phoenixAccum     = 0;
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "phoenix cast", $"duration={g.Config.PhoenixDuration}");
    }

    internal static void CastPenetration(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.PenetrationCost, "penetration")) return;
        g._penetrationArmed = true;
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "penetration armed", "");
    }

    internal static void CastLastDay(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.LastDayCost, "lastday")) return;
        g._lastDayRemaining = g.Config.LastDayDuration;
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "last day cast", $"duration={g.Config.LastDayDuration}");
    }

    internal static void CastMagnet(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.MagnetCost, "magnet")) return;
        g._magnetRemaining = g.Config.MagnetDuration;
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "magnet cast", $"duration={g.Config.MagnetDuration}");
    }

    internal static void CastOverload(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        // Find a free cell near the paddle's column, a few rows above the paddle line.
        int col = (int)System.Math.Clamp(
            (g.Paddle.Center.X - g.Config.BoardOriginX) / g.Config.CellSize,
            0, g.Level.Grid.Cols - 1);
        int row = System.Math.Max(0, g.Level.Grid.Rows - g.Config.OverloadPlacementRow);
        if (g.Blocks.Any(b => !b.Dead && b.Col == col && b.Row == row))
        { g._log.Log(g.TickCount, "spell", "overload denied", "cell occupied"); return; }
        if (!Spend(g, g.Config.OverloadCost, "overload")) return;

        g.Blocks.Add(new Block
        {
            Id = g.NextBlockId(), Col = col, Row = row,
            Hp = 1, MaxHp = 1, TypeId = "overload_bomb",
            Sprite = "GrateBomb", NeedToKill = false,
            Behavior = BlockBehavior.Bomb, ExplodeRadius = g.Config.OverloadRadius,
        });
        var c = g.Level.Grid.CellCenter(col, row);
        g.RaiseEvent("spellCast", c.X, c.Y);
        g._log.Log(g.TickCount, "spell", "overload placed", $"cell=({col},{row})");
    }

    internal static void CastGolem(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.GolemCost, "golem")) return;
        g.Projectiles.Add(new Projectile
        {
            Id     = g._nextProjId++,
            Pos    = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2),
            Vel    = new Vec2(0, -g.Config.GolemSpeed),
            Damage = g.Config.GolemDamage,
            Radius = g.Config.BallRadius * 1.5,
            Alive  = true,
            Kind   = "golem",
            PiercingHitsLeft = g.Config.GolemPierce,
        });
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "golem cast", "");
    }

    internal static void CastMage(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.MageCost, "mage")) return;
        int n = g.Config.MageBolts;
        double halfRad = g.Config.MageFanHalfAngleDeg * System.Math.PI / 180.0;
        for (int i = 0; i < n; i++)
        {
            double t = n > 1 ? (double)i / (n - 1) : 0.5;
            double angle = -halfRad + t * 2 * halfRad; // offset from straight up
            var vel = new Vec2(System.Math.Sin(angle), -System.Math.Cos(angle)) * g.Config.SkeletonBulletSpeed;
            g.Projectiles.Add(new Projectile
            {
                Id = g._nextProjId++,
                Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2),
                Vel = vel,
                Damage = g.Config.SkeletonBulletDamage,
                Radius = g.Config.BallRadius,
                Alive = true,
                Kind = "skeleton_bullet",
            });
        }
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "skeletal mage volley", $"bolts={n}");
    }

    // ── Per-tick updates ───────────────────────────────────────────────────────

    internal static void UpdateKitSpells(GameInstance g, double dt)
    {
        // Phoenix: sear blocks around the first living ball on a cadence.
        if (g._phoenixRemaining > 0)
        {
            g._phoenixRemaining -= dt;
            g._phoenixAccum     += dt;
            var ball = g.Balls.FirstOrDefault(b => b.Alive);
            if (ball != null && g._phoenixAccum >= g.Config.PhoenixTickInterval)
            {
                g._phoenixAccum -= g.Config.PhoenixTickInterval;
                foreach (var blk in g.Blocks.Where(b => !b.Dead && !b.Indestructible).ToList())
                {
                    var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                    if ((c - ball.Pos).Length > g.Config.PhoenixRadius) continue;
                    BlockDamage.DamageBlock(g, blk, g.Config.PhoenixDamage, igniteSource: false);
                    g.RaiseEvent("burn", c.X, c.Y);
                }
                g.RaiseEvent("phoenix", ball.Pos.X, ball.Pos.Y);
            }
        }

        // mod_cannons paddle mod: permanent slow side-cannon volleys (docs/04 §4.4).
        if (g.PaddleMods.Contains("mod_cannons"))
        {
            g._cannonAccumulator += dt;
            if (g._cannonAccumulator >= g.Config.PaddleModCannonInterval)
            {
                g._cannonAccumulator -= g.Config.PaddleModCannonInterval;
                var py = g.Paddle.Center.Y - g.Paddle.Height / 2;
                foreach (var px in new[] { g.Paddle.Center.X - g.Paddle.Width / 2,
                                           g.Paddle.Center.X + g.Paddle.Width / 2 })
                {
                    g.Projectiles.Add(new Projectile
                    {
                        Id     = g._nextProjId++,
                        Pos    = new Vec2(px, py),
                        Vel    = new Vec2(0, -g.Config.TurretBulletSpeed),
                        Damage = g.Config.TurretDamage,
                        Radius = g.Config.BallRadius * 0.6,
                        Kind   = "turret",
                    });
                }
                g.RaiseEvent("turretShot", g.Paddle.Center.X, py);
            }
        }

        // Last Day: tick down the smite window and its per-bounce cooldown.
        if (g._lastDayRemaining > 0) g._lastDayRemaining -= dt;
        if (g._lastDayCooldown  > 0) g._lastDayCooldown  -= dt;

        // Magnet: bend ball headings toward the nearest destructible block.
        if (g._magnetRemaining > 0)
        {
            g._magnetRemaining -= dt;
            var maxTurn = g.Config.MagnetSteerDegPerSec * System.Math.PI / 180.0 * dt;
            foreach (var b in g.Balls)
            {
                if (!b.Alive || b.GrabberId != 0) continue;
                var speed = b.Vel.Length;
                if (speed < 1e-6) continue;
                Block? target = null;
                double best = double.MaxValue;
                foreach (var blk in g.Blocks)
                {
                    if (blk.Dead || blk.Indestructible) continue;
                    var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                    var d = (c - b.Pos).Length;
                    if (d < best) { best = d; target = blk; }
                }
                if (target == null) continue;
                var tc = g.Level.Grid.CellCenter(target.Col, target.Row);
                var want = System.Math.Atan2((tc - b.Pos).Y, (tc - b.Pos).X);
                var have = System.Math.Atan2(b.Vel.Y, b.Vel.X);
                var diff = System.Math.IEEERemainder(want - have, System.Math.PI * 2);
                var turn = System.Math.Clamp(diff, -maxTurn, maxTurn);
                var ang  = have + turn;
                b.Vel = new Vec2(System.Math.Cos(ang), System.Math.Sin(ang)) * speed;
            }
        }
    }

    /// <summary>Last Day top-wall smite — called from BallSystem when a ball bounces off the ceiling.</summary>
    internal static void OnTopWallBounce(GameInstance g, Ball b)
    {
        if (g._lastDayRemaining <= 0 || g._lastDayCooldown > 0) return;
        g._lastDayCooldown = g.Config.LastDayCooldown;
        int col = (int)System.Math.Clamp(
            (b.Pos.X - g.Config.BoardOriginX) / g.Config.CellSize, 0, g.Level.Grid.Cols - 1);
        foreach (var blk in g.Blocks.Where(x => !x.Dead && !x.Boss && x.Col == col).ToList())
            BlockDamage.DamageBlock(g, blk, g.Config.LastDayDamage, igniteSource: false);
        var colX = g.Level.Grid.CellCenter(col, 0).X;
        g.RaiseEvent("judgement", colX, g.Level.Grid.Height);
        g._log.Log(g.TickCount, "spell", "last day smite", $"col={col}");
    }

    /// <summary>Paladin Penetration: applied on the deflect after arming.</summary>
    internal static void ApplyPenetrationOnDeflect(GameInstance g, Ball b)
    {
        if (!g._penetrationArmed) return;
        g._penetrationArmed = false;
        b.PhasesLeft += g.Config.PenetrationHits;
        g.RaiseEvent("penetration", b.Pos.X, b.Pos.Y);
        g._log.Log(g.TickCount, "spell", "penetration applied", $"hits={g.Config.PenetrationHits}");
    }

    /// <summary>Shared mana gate.</summary>
    private static bool Spend(GameInstance g, double cost, string spell)
    {
        if (g.ManaValue < cost)
        { g._log.Log(g.TickCount, "spell", $"{spell} denied", $"mana={g.ManaValue:F0} need={cost}"); return false; }
        g.ManaValue -= cost;
        return true;
    }
}
