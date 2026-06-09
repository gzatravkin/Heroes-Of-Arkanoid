using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>Fire-Mage spells: Fireball / Ignite / FireWall / Turret (+ their per-tick updates).</summary>
internal static partial class SpellSystem
{
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
}
