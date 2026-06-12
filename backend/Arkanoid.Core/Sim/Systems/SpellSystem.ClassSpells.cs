using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Class spells beyond the Fire-Mage kit:
///   Paladin     — Shield / Spear / Duplicate
///   Engineer    — Lightning / Rocket / Radiation
///   Necromancer — Decay / Skeleton / Drain
/// plus their per-tick updates (barriers, zones, skeleton fire, drain timer).
/// </summary>
internal static partial class SpellSystem
{
    // -----------------------------------------------------------------------
    // Cast — Paladin: Shield
    // -----------------------------------------------------------------------

    internal static void CastShield(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.PaladinBarrierCost, "shield")) return;
        var shieldY = g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Config.BallRadius;
        g.Barriers.Add(new Entities.Barrier {
            Id            = g._nextBarrierId++,
            Y             = shieldY,
            CenterX       = g.Paddle.Center.X,
            Width         = g.Paddle.Width * g.Config.PaladinBarrierWidthMult,
            LifeRemaining = g.Config.PaladinBarrierLifetime
        });
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Paladin: Spear (piercing projectile)
    // -----------------------------------------------------------------------

    internal static void CastSpear(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.SpearCost, "spear")) return;
        g.Projectiles.Add(new Projectile {
            Id               = g._nextProjId++,
            Pos              = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height),
            Vel              = new Vec2(0, -g.Config.SpearSpeed),
            Damage           = g.Config.SpearDamage,
            Radius           = g.Config.BallRadius * 0.5,
            Kind             = "spear",
            PiercingHitsLeft = g.Config.SpearPiercingHits
        });
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Paladin: Duplicate (split active ball)
    // -----------------------------------------------------------------------

    internal static void CastDuplicate(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        var alive = g.Balls.Where(b => b.Alive).ToList();
        if (alive.Count == 0) return;
        if (!Spend(g, g.Config.DuplicateCost, "duplicate")) return;
        // Duplicate the first alive ball N times; each copy gets a small velocity splay
        var src = alive[0];
        for (int i = 0; i < g.Config.DuplicateExtraBalls; i++)
        {
            // Deterministic splay: rotate velocity 15 degrees clockwise per copy
            double angleDeg = 15.0 * (i + 1);
            double rad = angleDeg * System.Math.PI / 180.0;
            double cos = System.Math.Cos(rad), sin = System.Math.Sin(rad);
            var newVel = new Vec2(src.Vel.X * cos - src.Vel.Y * sin,
                                  src.Vel.X * sin + src.Vel.Y * cos);
            g.Balls.Add(new Ball {
                Id             = g._nextBallId++,
                Radius         = src.Radius,
                Pos            = new Vec2(src.Pos.X + (i + 1) * (src.Radius * 2 + 2), src.Pos.Y),
                Vel            = newVel,
                Alive          = true,
                IgniteHitsLeft = src.IgniteHitsLeft,
                DecayHitsLeft  = src.DecayHitsLeft
            });
        }
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Engineer: Lightning (chain damage)
    // -----------------------------------------------------------------------

    internal static void CastLightning(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        var alive = g.Blocks.Where(b => !b.Dead).ToList();
        if (alive.Count == 0) return;
        if (!Spend(g, g.Config.LightningCost, "lightning")) return;

        // Pick a random alive block as the starting target (deterministic via Rng)
        int startIdx = g.Rng.Range(alive.Count);
        var current  = alive[startIdx];
        var hit      = new HashSet<int> { current.Id };

        BlockDamage.DamageBlock(g, current, g.Config.LightningDamage, igniteSource: false);
        var cPos = g.Level.Grid.CellCenter(current.Col, current.Row);
        g.RaiseEvent(SimEventKind.Lightning, cPos.X, cPos.Y);

        // Jump to nearby blocks up to ChainJumps times (+1 with the Conductor relic)
        var chainJumps = g.Config.LightningChainJumps + (g.HasRelic("conductor") ? 1 : 0);
        for (int jump = 0; jump < chainJumps; jump++)
        {
            var candidates = alive
                .Where(b => !b.Dead && !hit.Contains(b.Id))
                .Select(b => new { blk = b, center = g.Level.Grid.CellCenter(b.Col, b.Row) })
                .Where(x => (x.center - cPos).Length <= g.Config.LightningChainRadius)
                .ToList();
            if (candidates.Count == 0) break;
            int nextIdx = g.Rng.Range(candidates.Count);
            var next    = candidates[nextIdx];
            current = next.blk;
            cPos    = next.center;
            hit.Add(current.Id);
            BlockDamage.DamageBlock(g, current, g.Config.LightningDamage, igniteSource: false);
            g.RaiseEvent(SimEventKind.Lightning, cPos.X, cPos.Y);
        }
    }

    // -----------------------------------------------------------------------
    // Cast — Engineer: Rocket (homing + AoE)
    // -----------------------------------------------------------------------

    internal static void CastRocket(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.RocketCost, "rocket")) return;
        g.Projectiles.Add(new Projectile {
            Id        = g._nextProjId++,
            Pos       = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height),
            Vel       = new Vec2(0, -g.Config.RocketSpeed),
            Damage    = g.Config.RocketDamage,
            Radius    = g.Config.BallRadius * 0.7,
            Kind      = "rocket",
            Homing    = true,
            AoeRadius = g.Config.RocketAoeRadius
        });
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Engineer: Radiation (AoE zone)
    // -----------------------------------------------------------------------

    internal static void CastRadiation(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.RadiationCost, "radiation")) return;
        // Zone spawns at the paddle position; it will immediately start ticking damage on nearby blocks
        g.Zones.Add(new Zone {
            Id             = g._nextZoneId++,
            X              = g.Paddle.Center.X,
            Y              = g.Paddle.Center.Y - g.Paddle.Height,
            Radius         = g.Config.RadiationRadius,
            LifeRemaining  = g.Config.RadiationLifetime,
            DamagePerTick  = g.Config.RadiationDamage,
            DamageInterval = g.Config.RadiationDamageInterval
        });
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Necromancer: Decay (imbue ball with decay)
    // -----------------------------------------------------------------------

    internal static void CastDecay(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.DecayCost, "decay")) return;
        g._decayArmed = true;
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Necromancer: Skeleton (summon auto-firer)
    // -----------------------------------------------------------------------

    internal static void CastSkeleton(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.SkeletonCost, "skeleton")) return;
        g._skeletonRemaining  = g.Config.SkeletonDuration;
        g._skeletonAccumulator = 0;
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Necromancer: Drain (bonus mana on kills for a duration)
    // -----------------------------------------------------------------------

    internal static void CastDrain(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (!Spend(g, g.Config.DrainCost, "drain")) return;
        g._drainRemaining = g.Config.DrainDuration;
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Per-tick updates — new spells
    // -----------------------------------------------------------------------

    internal static void UpdateBarriers(GameInstance g, double dt)
    {
        foreach (var b in g.Barriers)
        {
            if (!b.Alive) continue;
            b.LifeRemaining -= dt;
            if (b.LifeRemaining <= 0) b.Alive = false;
        }
        g.Barriers.RemoveAll(b => !b.Alive);
    }

    internal static void UpdateZones(GameInstance g, double dt)
    {
        var cell = g.Config.CellSize;
        foreach (var zone in g.Zones)
        {
            if (!zone.Alive) continue;
            zone.Accumulator    += dt;
            zone.LifeRemaining  -= dt;
            while (zone.Accumulator >= zone.DamageInterval)
            {
                foreach (var blk in g.Blocks)
                {
                    if (blk.Dead) continue;
                    var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                    if ((c - new Vec2(zone.X, zone.Y)).Length <= zone.Radius)
                    {
                        BlockDamage.DamageBlock(g, blk, zone.DamagePerTick, igniteSource: false);
                        g.RaiseEvent(SimEventKind.Radiation, c.X, c.Y);
                    }
                }
                zone.Accumulator -= zone.DamageInterval;
            }
            if (zone.LifeRemaining <= 0) zone.Alive = false;
        }
        g.Zones.RemoveAll(z => !z.Alive);
    }

    internal static void UpdateSkeleton(GameInstance g, double dt)
    {
        if (g._skeletonRemaining <= 0) return;
        g._skeletonRemaining  -= dt;
        g._skeletonAccumulator += dt;
        while (g._skeletonAccumulator >= g.Config.SkeletonFireInterval)
        {
            g.Projectiles.Add(new Projectile {
                Id     = g._nextProjId++,
                Pos    = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2),
                Vel    = new Vec2(0, -g.Config.SkeletonBulletSpeed),
                Damage = g.Config.SkeletonBulletDamage,
                Radius = g.Config.BallRadius * 0.5,
                Kind   = "skeleton_bullet"
            });
            g.RaiseEvent(SimEventKind.SkeletonShot, g.Paddle.Center.X, g.Paddle.Center.Y);
            g._skeletonAccumulator -= g.Config.SkeletonFireInterval;
        }
    }

    internal static void UpdateDrain(GameInstance g, double dt)
    {
        if (g._drainRemaining <= 0) return;
        g._drainRemaining -= dt;
    }

    /// <summary>
    /// Extra mana earned per kill when Drain is active (called from Modifiers.KillManaGain logic
    /// via Drain state check in GameInstance).
    /// </summary>
    internal static double DrainBonusMana(GameInstance g)
        => g.SpellDrainActive ? g.Config.DrainBonusManaPerKill : 0.0;
}
