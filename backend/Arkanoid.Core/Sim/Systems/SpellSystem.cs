using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Spell casting and per-tick spell updates:
///   CastFireball / CastIgnite / CastFireWall / CastTurret
///   UpdateProjectiles / UpdateFireWalls / UpdateTurret
///   RegenMana / perfect-deflect bonus / ApplyIgniteOnDeflect
/// </summary>
internal static class SpellSystem
{
    // -----------------------------------------------------------------------
    // Mana regen
    // -----------------------------------------------------------------------

    internal static void RegenMana(GameInstance g, double dt)
    {
        g.ManaValue = System.Math.Min(g.ManaMaxValue,
            g.ManaValue + g.Config.ManaRegenPerSec * Modifiers.ManaRegenMult(g) * dt);
    }

    // -----------------------------------------------------------------------
    // Paddle hit extras (perfect-deflect bonus + ignite imbue)
    // -----------------------------------------------------------------------

    internal static void OnPaddleHit(GameInstance g, Entities.Ball b, double t)
    {
        g._log.Log(g.TickCount, "paddle", "deflect", $"t={t:F2} vx={b.Vel.X:F1} vy={b.Vel.Y:F1}");
        if (System.Math.Abs(t) < g.Config.PerfectDeflectBand)
        {
            g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + g.Config.ManaPerfectDeflectBonus);
            g._log.Log(g.TickCount, "mana", "perfect deflect bonus", $"mana={g.ManaValue:F0}");
        }
        ApplyIgniteOnDeflect(g, b);
        ApplyDecayOnDeflect(g, b);
    }

    internal static void ApplyIgniteOnDeflect(GameInstance g, Entities.Ball b)
    {
        if (!g._igniteArmed) return;
        b.IgniteHitsLeft = Modifiers.IgniteHits(g);
        g._igniteArmed = false;
        g._log.Log(g.TickCount, "ignite", "imbued ball", $"id={b.Id} hits={b.IgniteHitsLeft}");
        g.RaiseEvent("ignite", b.Pos.X, b.Pos.Y);
    }

    internal static void ApplyDecayOnDeflect(GameInstance g, Entities.Ball b)
    {
        if (!g._decayArmed) return;
        b.DecayHitsLeft = g.Config.DecayHits;
        g._decayArmed = false;
        g._log.Log(g.TickCount, "decay", "imbued ball", $"id={b.Id} hits={b.DecayHitsLeft}");
        g.RaiseEvent("decay", b.Pos.X, b.Pos.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Fireball
    // -----------------------------------------------------------------------

    internal static void CastFireball(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.FireballCost)
        { g._log.Log(g.TickCount, "spell", "fireball denied", $"mana={g.ManaValue:F0} need={g.Config.FireballCost}"); return; }
        g.ManaValue -= g.Config.FireballCost;
        g.Projectiles.Add(new Projectile {
            Id     = g._nextProjId++,
            Pos    = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height),
            Vel    = new Vec2(0, -g.Config.FireballSpeed),
            Damage = Modifiers.FireballDamage(g),
            Radius = g.Config.BallRadius
        });
        g._log.Log(g.TickCount, "spell", "fireball cast", $"mana={g.ManaValue:F0}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Ignite
    // -----------------------------------------------------------------------

    internal static void CastIgnite(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.IgniteCost) return;
        g.ManaValue -= g.Config.IgniteCost;
        g._igniteArmed = true;
        g._log.Log(g.TickCount, "ignite", "armed", $"mana={g.ManaValue:F0}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — FireWall
    // -----------------------------------------------------------------------

    internal static void CastFireWall(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.FireWallCost)
        { g._log.Log(g.TickCount, "spell", "firewall denied", $"mana={g.ManaValue:F0} need={g.Config.FireWallCost}"); return; }
        g.ManaValue -= g.Config.FireWallCost;
        var wallY = g.Level.Grid.Height;
        g.FireWalls.Add(new FireWall {
            Id            = g._nextWallId++,
            Y             = wallY,
            Width         = g.Level.Grid.Width,
            LifeRemaining = g.Config.FireWallLifetime
        });
        g._log.Log(g.TickCount, "spell", "firewall cast", $"mana={g.ManaValue:F0}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, wallY);
    }

    // -----------------------------------------------------------------------
    // Cast — Turret
    // -----------------------------------------------------------------------

    internal static void CastTurret(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.TurretCost)
        { g._log.Log(g.TickCount, "spell", "turret denied", $"mana={g.ManaValue:F0} need={g.Config.TurretCost}"); return; }
        g.ManaValue -= g.Config.TurretCost;
        g._turretRemaining  = Modifiers.TurretDuration(g);
        g._turretAccumulator = 0;
        g._log.Log(g.TickCount, "spell", "turret cast", $"mana={g.ManaValue:F0}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Per-tick updates
    // -----------------------------------------------------------------------

    internal static void UpdateProjectiles(GameInstance g, double dt)
    {
        var cell = g.Config.CellSize;
        foreach (var pr in g.Projectiles)
        {
            if (!pr.Alive) continue;

            // Homing update (rocket steers toward nearest block)
            if (pr.Homing) UpdateRocketHoming(pr, g, dt);

            pr.Pos += pr.Vel * dt;
            if (pr.Pos.Y < -cell || pr.Pos.Y > g.Level.Grid.Height + cell * 3) { pr.Alive = false; continue; }

            foreach (var blk in g.Blocks)
            {
                if (blk.Dead) continue;
                var c   = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                var box = Arkanoid.Core.Math.Aabb.FromCenter(c, cell / 2, cell / 2);
                if (!box.IntersectsCircle(pr.Pos, pr.Radius)) continue;

                BlockDamage.DamageBlock(g, blk, pr.Damage, igniteSource: false);

                // AoE explosion (rocket)
                if (pr.AoeRadius > 0)
                {
                    foreach (var nb in g.Blocks)
                    {
                        if (nb.Dead || nb == blk) continue;
                        var nc = g.Level.Grid.CellCenter(nb.Col, nb.Row);
                        if ((nc - pr.Pos).Length <= pr.AoeRadius)
                        {
                            BlockDamage.DamageBlock(g, nb, g.Config.RocketAoeDamage, igniteSource: false);
                            g.RaiseEvent("explosion", nc.X, nc.Y);
                        }
                    }
                    g.RaiseEvent("explosion", c.X, c.Y);
                    pr.Alive = false;
                    break;
                }

                // Piercing (spear)
                if (pr.PiercingHitsLeft > 0)
                {
                    pr.PiercingHitsLeft--;
                    if (pr.PiercingHitsLeft <= 0) pr.Alive = false;
                    break; // one block per tick even for piercing (deterministic)
                }

                pr.Alive = false;
                break;
            }
        }
        g.Projectiles.RemoveAll(p => !p.Alive);
    }

    internal static void UpdateFireWalls(GameInstance g, double dt)
    {
        foreach (var wall in g.FireWalls)
        {
            if (!wall.Alive) continue;
            wall.Y           -= g.Config.FireWallRiseSpeed * dt;
            wall.Accumulator += dt;
            while (wall.Accumulator >= g.Config.FireWallDamageInterval)
            {
                foreach (var blk in g.Blocks)
                {
                    if (blk.Dead) continue;
                    var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                    if (c.Y >= wall.Y - g.Config.FireWallBandHalfHeight &&
                        c.Y <= wall.Y + g.Config.FireWallBandHalfHeight)
                    {
                        BlockDamage.DamageBlock(g, blk, Modifiers.FireWallDamage(g), igniteSource: false);
                        g.RaiseEvent("burn", c.X, c.Y);
                    }
                }
                wall.Accumulator -= g.Config.FireWallDamageInterval;
            }
            wall.LifeRemaining -= dt;
            if (wall.LifeRemaining <= 0 || wall.Y < -g.Config.CellSize)
                wall.Alive = false;
        }
        g.FireWalls.RemoveAll(w => !w.Alive);
    }

    internal static void UpdateTurret(GameInstance g, double dt)
    {
        if (g._turretRemaining <= 0) return;
        g._turretRemaining  -= dt;
        g._turretAccumulator += dt;
        while (g._turretAccumulator >= g.Config.TurretFireInterval)
        {
            g.Projectiles.Add(new Projectile {
                Id     = g._nextProjId++,
                Pos    = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2),
                Vel    = new Vec2(0, -g.Config.TurretBulletSpeed),
                Damage = g.Config.TurretDamage,
                Radius = g.Config.BallRadius * 0.6,
                Kind   = "turret"
            });
            g.RaiseEvent("turretShot", g.Paddle.Center.X, g.Paddle.Center.Y);
            g._turretAccumulator -= g.Config.TurretFireInterval;
        }
    }

    // -----------------------------------------------------------------------
    // Cast — Paladin: Shield
    // -----------------------------------------------------------------------

    internal static void CastShield(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.ShieldCost)
        { g._log.Log(g.TickCount, "spell", "shield denied", $"mana={g.ManaValue:F0} need={g.Config.ShieldCost}"); return; }
        g.ManaValue -= g.Config.ShieldCost;
        var shieldY = g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Config.BallRadius;
        g.Barriers.Add(new Entities.Barrier {
            Id            = g._nextBarrierId++,
            Y             = shieldY,
            CenterX       = g.Paddle.Center.X,
            Width         = g.Paddle.Width * g.Config.ShieldWidthMult,
            LifeRemaining = g.Config.ShieldLifetime
        });
        g._log.Log(g.TickCount, "spell", "shield cast", $"mana={g.ManaValue:F0} y={shieldY:F1}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Paladin: Spear (piercing projectile)
    // -----------------------------------------------------------------------

    internal static void CastSpear(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.SpearCost)
        { g._log.Log(g.TickCount, "spell", "spear denied", $"mana={g.ManaValue:F0} need={g.Config.SpearCost}"); return; }
        g.ManaValue -= g.Config.SpearCost;
        g.Projectiles.Add(new Projectile {
            Id               = g._nextProjId++,
            Pos              = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height),
            Vel              = new Vec2(0, -g.Config.SpearSpeed),
            Damage           = g.Config.SpearDamage,
            Radius           = g.Config.BallRadius * 0.5,
            Kind             = "spear",
            PiercingHitsLeft = g.Config.SpearPiercingHits
        });
        g._log.Log(g.TickCount, "spell", "spear cast", $"mana={g.ManaValue:F0}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Paladin: Duplicate (split active ball)
    // -----------------------------------------------------------------------

    internal static void CastDuplicate(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.DuplicateCost)
        { g._log.Log(g.TickCount, "spell", "duplicate denied", $"mana={g.ManaValue:F0} need={g.Config.DuplicateCost}"); return; }
        var alive = g.Balls.Where(b => b.Alive).ToList();
        if (alive.Count == 0) return;
        g.ManaValue -= g.Config.DuplicateCost;
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
        g._log.Log(g.TickCount, "spell", "duplicate cast", $"mana={g.ManaValue:F0} balls={g.Balls.Count(b => b.Alive)}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Engineer: Lightning (chain damage)
    // -----------------------------------------------------------------------

    internal static void CastLightning(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.LightningCost)
        { g._log.Log(g.TickCount, "spell", "lightning denied", $"mana={g.ManaValue:F0} need={g.Config.LightningCost}"); return; }
        var alive = g.Blocks.Where(b => !b.Dead).ToList();
        if (alive.Count == 0) return;
        g.ManaValue -= g.Config.LightningCost;

        // Pick a random alive block as the starting target (deterministic via Rng)
        int startIdx = g.Rng.Range(alive.Count);
        var current  = alive[startIdx];
        var hit      = new HashSet<int> { current.Id };

        BlockDamage.DamageBlock(g, current, g.Config.LightningDamage, igniteSource: false);
        var cPos = g.Level.Grid.CellCenter(current.Col, current.Row);
        g.RaiseEvent("lightning", cPos.X, cPos.Y);

        // Jump to nearby blocks up to ChainJumps times
        for (int jump = 0; jump < g.Config.LightningChainJumps; jump++)
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
            g.RaiseEvent("lightning", cPos.X, cPos.Y);
        }
        g._log.Log(g.TickCount, "spell", "lightning cast", $"mana={g.ManaValue:F0} hits={hit.Count}");
    }

    // -----------------------------------------------------------------------
    // Cast — Engineer: Rocket (homing + AoE)
    // -----------------------------------------------------------------------

    internal static void CastRocket(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.RocketCost)
        { g._log.Log(g.TickCount, "spell", "rocket denied", $"mana={g.ManaValue:F0} need={g.Config.RocketCost}"); return; }
        g.ManaValue -= g.Config.RocketCost;
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
        g._log.Log(g.TickCount, "spell", "rocket cast", $"mana={g.ManaValue:F0}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Engineer: Radiation (AoE zone)
    // -----------------------------------------------------------------------

    internal static void CastRadiation(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.RadiationCost)
        { g._log.Log(g.TickCount, "spell", "radiation denied", $"mana={g.ManaValue:F0} need={g.Config.RadiationCost}"); return; }
        g.ManaValue -= g.Config.RadiationCost;
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
        g._log.Log(g.TickCount, "spell", "radiation cast", $"mana={g.ManaValue:F0}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Necromancer: Decay (imbue ball with decay)
    // -----------------------------------------------------------------------

    internal static void CastDecay(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.DecayCost) return;
        g.ManaValue -= g.Config.DecayCost;
        g._decayArmed = true;
        g._log.Log(g.TickCount, "decay", "armed", $"mana={g.ManaValue:F0}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Necromancer: Skeleton (summon auto-firer)
    // -----------------------------------------------------------------------

    internal static void CastSkeleton(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.SkeletonCost)
        { g._log.Log(g.TickCount, "spell", "skeleton denied", $"mana={g.ManaValue:F0} need={g.Config.SkeletonCost}"); return; }
        g.ManaValue -= g.Config.SkeletonCost;
        g._skeletonRemaining  = g.Config.SkeletonDuration;
        g._skeletonAccumulator = 0;
        g._log.Log(g.TickCount, "spell", "skeleton cast", $"mana={g.ManaValue:F0}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // -----------------------------------------------------------------------
    // Cast — Necromancer: Drain (bonus mana on kills for a duration)
    // -----------------------------------------------------------------------

    internal static void CastDrain(GameInstance g)
    {
        if (g.Phase != GamePhase.Playing) return;
        if (g.ManaValue < g.Config.DrainCost)
        { g._log.Log(g.TickCount, "spell", "drain denied", $"mana={g.ManaValue:F0} need={g.Config.DrainCost}"); return; }
        g.ManaValue -= g.Config.DrainCost;
        g._drainRemaining = g.Config.DrainDuration;
        g._log.Log(g.TickCount, "spell", "drain cast", $"mana={g.ManaValue:F0}");
        g.RaiseEvent("spellCast", g.Paddle.Center.X, g.Paddle.Center.Y);
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
                        g.RaiseEvent("radiation", c.X, c.Y);
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
            g.RaiseEvent("skeletonShot", g.Paddle.Center.X, g.Paddle.Center.Y);
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
        => g.DrainActive ? g.Config.DrainBonusManaPerKill : 0.0;

    // -----------------------------------------------------------------------
    // Projectile update (extended for piercing + homing + AoE)
    // -----------------------------------------------------------------------

    internal static void UpdateRocketHoming(Projectile pr, GameInstance g, double dt)
    {
        if (!pr.Homing) return;
        var alive = g.Blocks.Where(b => !b.Dead).ToList();
        if (alive.Count == 0) return;
        // Find nearest alive block
        Block? nearest = null;
        double nearest2 = double.MaxValue;
        foreach (var blk in alive)
        {
            var c  = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            var d2 = (c - pr.Pos).LengthSquared;
            if (d2 < nearest2) { nearest2 = d2; nearest = blk; }
        }
        if (nearest is null) return;
        var dir = (g.Level.Grid.CellCenter(nearest.Col, nearest.Row) - pr.Pos);
        if (dir.Length > 0)
        {
            var steer = dir.Normalized() * g.Config.RocketHomingStrength * dt;
            pr.Vel += steer;
            // Clamp to a reasonable max speed so it doesn't overshoot wildly
            if (pr.Vel.Length > g.Config.RocketSpeed * 2)
                pr.Vel = pr.Vel.Normalized() * g.Config.RocketSpeed * 2;
        }
    }
}
