using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Heaven Shield Statue (original ShieldStatue): on a cadence it grants every nearby block
/// a temporary damage shield, so the level keeps re-armouring until you destroy the statue.
/// Shielded blocks ignore damage (see BlockDamage) while their ShieldTimer is positive.
/// (The original altar/vase ally-toggle is deferred — see docs/08.)
/// </summary>
internal static class ShieldSystem
{
    internal static void Update(GameInstance g, double dt)
    {
        // Tick down existing shields.
        foreach (var b in g.Blocks)
            if (b.ShieldTimer > 0) b.ShieldTimer = System.Math.Max(0, b.ShieldTimer - dt);

        var statues = g.Blocks.Where(b => !b.Dead && b.ShieldStatue).ToList();
        if (statues.Count == 0) return;

        foreach (var st in statues)
        {
            st.EmitAccumulator += dt; // reuse the cadence accumulator (statues aren't emitters)
            if (st.EmitAccumulator < g.Config.ShieldStatueInterval) continue;
            st.EmitAccumulator -= g.Config.ShieldStatueInterval;

            int r = g.Config.ShieldStatueRadius;
            foreach (var nb in g.Blocks)
            {
                if (nb.Dead || nb == st || nb.ShieldStatue || nb.Indestructible) continue;
                int dist = System.Math.Max(System.Math.Abs(nb.Col - st.Col), System.Math.Abs(nb.Row - st.Row));
                if (dist <= r) nb.ShieldTimer = g.Config.ShieldDuration;
            }
            var c = g.Level.Grid.CellCenter(st.Col, st.Row);
            g.RaiseEvent("shield", c.X, c.Y);
            g._log.Log(g.TickCount, "shield", "pulse", $"id={st.Id}");
        }
    }
}
