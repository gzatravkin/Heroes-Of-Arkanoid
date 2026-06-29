using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using Arkanoid.Core.Spells;

namespace Arkanoid.Core.Sim.Systems;

/// <summary>Lance of Dawn (design §3 rework of Spear) — NOT a piercing projectile. It drops a temporary
/// SOLID pillar at the paddle's column that the ball banks off (a deflector for trick angles), lasting a
/// few seconds. Deals no damage; it's a positioning tool.</summary>
internal static class LanceSystem
{
    internal static void Cast(GameInstance g, SpellDef def)
    {
        if (!SpellSystem.Spend(g, def.ManaCost, def.Id)) return;
        double cell = g.Config.CellSize;
        double life = def.Lifetime + (g.SpellLevel(def.Id) - 1) * def.DurationPerLevel;
        // Cap active pillars so the lane can't be spam-cluttered — drop the oldest.
        while (g.Pillars.Count >= 3) g.Pillars.RemoveAt(0);
        g.Pillars.Add(new Pillar
        {
            Id            = g._nextPillarId++,
            CenterX       = g.Paddle.Center.X,
            CenterY       = g.Level.Grid.Height * 0.55, // mid play-area, in the ball's travel lane
            Width         = cell * 0.8,
            Height        = cell * 4,
            LifeRemaining = life,
        });
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Level.Grid.Height * 0.55);
        g._log.Log(g.TickCount, "spell", "lance", $"pillar x={g.Paddle.Center.X:0} life={life:0.0}");
    }

    internal static void Update(GameInstance g, double dt)
    {
        if (g.Pillars.Count == 0) return;
        foreach (var p in g.Pillars)
            if ((p.LifeRemaining -= dt) <= 0) p.Alive = false;
        g.Pillars.RemoveAll(p => !p.Alive);
    }

    /// <summary>Bank the ball off any pillar it overlaps (circle-vs-AABB reflection on the shallow axis).</summary>
    internal static void Resolve(GameInstance g, Ball b)
    {
        foreach (var p in g.Pillars)
        {
            if (!p.Alive) continue;
            double halfW = p.Width / 2, halfH = p.Height / 2;
            double dx = b.Pos.X - p.CenterX, dy = b.Pos.Y - p.CenterY;
            double cx = System.Math.Clamp(dx, -halfW, halfW);
            double cy = System.Math.Clamp(dy, -halfH, halfH);
            double distX = b.Pos.X - (p.CenterX + cx), distY = b.Pos.Y - (p.CenterY + cy);
            if (distX * distX + distY * distY >= b.Radius * b.Radius) continue;

            double overlapX = halfW + b.Radius - System.Math.Abs(dx);
            double overlapY = halfH + b.Radius - System.Math.Abs(dy);
            int sx = dx >= 0 ? 1 : -1, sy = dy >= 0 ? 1 : -1;
            if (overlapX <= overlapY) // shallower on X → bank horizontally off a side face
            {
                b.Vel = new Vec2(sx * System.Math.Abs(b.Vel.X), b.Vel.Y);
                b.Pos = new Vec2(p.CenterX + sx * (halfW + b.Radius + 0.5), b.Pos.Y);
            }
            else // bank vertically off the top/bottom face
            {
                b.Vel = new Vec2(b.Vel.X, sy * System.Math.Abs(b.Vel.Y));
                b.Pos = new Vec2(b.Pos.X, p.CenterY + sy * (halfH + b.Radius + 0.5));
            }
            // Keep the ball off a near-horizontal pillar↔wall lock (same guard blocks use).
            b.Vel = Physics.BallPhysics.EnforceMinAngle(b.Vel);
            g.RaiseEvent(SimEventKind.Deflect, b.Pos.X, b.Pos.Y);
        }
    }
}
