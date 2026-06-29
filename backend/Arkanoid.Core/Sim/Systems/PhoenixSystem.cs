using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Fire-Mage Phoenix: spawns a visible entity that orbits a ball and burns blocks it
/// sweeps over. Replaces the old invisible AoE pulse centered on the ball. See the spec.
/// </summary>
internal static class PhoenixSystem
{
    // Bespoke tuning (phoenix is not a generic archetype).
    private const double AngularSpeedRadPerSec = 3.0; // ~0.48 orbits / second

    /// <summary>Cast: spawn a phoenix bound to a random live ball.</summary>
    internal static void Cast(GameInstance g, Arkanoid.Core.Spells.SpellDef def)
    {
        if (!SpellSystem.Spend(g, def.ManaCost, def.Id)) return;
        var ball = g.Balls.FirstOrDefault(b => b.Alive);
        if (ball == null) return;
        var duration = def.Duration + (g.SpellLevel(def.Id) - 1) * def.DurationPerLevel;
        var orbit    = def.Radius > 0 ? def.Radius : 56;
        var ph = new Phoenix
        {
            Id             = g._nextPhoenixId++,
            TargetBallId   = ball.Id,
            Angle          = 0,
            OrbitRadius    = orbit,
            AngularSpeed   = AngularSpeedRadPerSec,
            HitRadius      = g.Config.CellSize * 1.3,  // wider sweep (balance 2026-06-16): carves a clear ring
            Damage         = def.Damage > 0 ? def.Damage : 1,
            DamageInterval = def.TickInterval > 0 ? def.TickInterval : 0.4,
            Lifetime       = duration,
        };
        ph.Pos = ball.Pos + new Vec2(orbit, 0);
        g.Phoenixes.Add(ph);
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    internal static void Update(GameInstance g, double dt)
    {
        for (int i = g.Phoenixes.Count - 1; i >= 0; i--)
        {
            var ph = g.Phoenixes[i];
            ph.Lifetime -= dt;
            if (ph.Lifetime <= 0) { g.Phoenixes.RemoveAt(i); continue; }

            // Track the bound ball; re-target if it drained.
            var ball = g.Balls.FirstOrDefault(b => b.Id == ph.TargetBallId && b.Alive)
                       ?? g.Balls.FirstOrDefault(b => b.Alive);
            if (ball != null)
            {
                ph.TargetBallId = ball.Id;
                ph.Angle += ph.AngularSpeed * dt;
                ph.Pos = ball.Pos + new Vec2(
                    System.Math.Cos(ph.Angle) * ph.OrbitRadius,
                    System.Math.Sin(ph.Angle) * ph.OrbitRadius);
            }

            // Damage blocks under the phoenix on a cadence.
            ph.DamageAccum += dt;
            while (ph.DamageAccum >= ph.DamageInterval)
            {
                ph.DamageAccum -= ph.DamageInterval;
                foreach (var blk in g.Blocks.Where(b => !b.Dead && !b.Indestructible).ToList())
                {
                    var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                    if ((c - ph.Pos).Length > ph.HitRadius) continue;
                    BlockDamage.DamageBlock(g, blk, ph.Damage, igniteSource: false, killMult: 0.5);
                    g.RaiseEvent(SimEventKind.Burn, c.X, c.Y);
                }
            }
            g.RaiseEvent(SimEventKind.Phoenix, ph.Pos.X, ph.Pos.Y);
        }
    }
}
