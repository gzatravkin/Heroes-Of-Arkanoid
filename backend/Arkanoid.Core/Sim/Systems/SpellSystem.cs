using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Spell casting and per-tick spell updates. Split across partial files by class:
///   SpellSystem.cs           — mana regen, paddle-deflect imbue, shared projectile update.
///   SpellSystem.FireMage.cs  — Fireball / Ignite / FireWall / Turret.
///   SpellSystem.ClassSpells.cs — Paladin / Engineer / Necromancer spells.
/// </summary>
internal static partial class SpellSystem
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
        g.RaiseEvent("deflect", b.Pos.X, b.Pos.Y); // audio cue (G1)
        if (System.Math.Abs(t) < g.Config.PerfectDeflectBand)
        {
            var bonus = g.Config.ManaPerfectDeflectBonus
                + (g.HasRelic("overcharge") ? g.Config.OverchargeMana : 0);
            g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + bonus);
            g._log.Log(g.TickCount, "mana", "perfect deflect bonus", $"mana={g.ManaValue:F0} bonus={bonus:F0}");
        }
        // Echo core: arm the bonus-damage strike for the next block hit.
        if (g.BallCores.Contains("echo")) b.EchoArmed = true;
        // Paladin Penetration: armed cast lands on this deflect.
        ApplyPenetrationOnDeflect(g, b);
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
    // Shared projectile update (piercing + homing + AoE)
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
                // Ally bolts (allied statues) never hit fellow statues or walls — they
                // exist to clear the field for the player, not to eat themselves.
                if (pr.Kind == "allybolt" && (blk.IsStatue || blk.Indestructible)) continue;
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
