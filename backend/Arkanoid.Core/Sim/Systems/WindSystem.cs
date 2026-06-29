using System.Linq;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Heaven WindMaster (original WindMasterScript): a block that continuously pushes any
/// ball within its radius away from itself, with a force that falls off linearly to the
/// edge. Applied as a pure deflection — the ball's speed is preserved, only its heading
/// bends — so it warps your aim without making the ball run away.
/// </summary>
internal static class WindSystem
{
    internal static void Update(GameInstance g, double dt)
    {
        var winds = g.Blocks.Where(b => !b.Dead && b.WindMaster).ToList();
        if (winds.Count == 0) return;

        foreach (var b in g.Balls)
        {
            if (!b.Alive) continue;
            var speed = b.Vel.Length;
            if (speed < 0.0001) continue;
            var vel = b.Vel;

            foreach (var w in winds)
            {
                var wc   = g.Level.Grid.CellCenter(w.Col, w.Row);
                var d    = b.Pos - wc;
                var dist = d.Length;
                if (dist < 0.0001 || dist > g.Config.Enemies.WindMasterRadius) continue;
                var falloff = 1 - dist / g.Config.Enemies.WindMasterRadius;
                vel += d.Normalized() * g.Config.Enemies.WindMasterForce * falloff * dt;
            }

            // Preserve speed, only bend the heading.
            if (vel.Length > 0.0001)
                b.Vel = vel.Normalized() * speed;
        }
    }
}
