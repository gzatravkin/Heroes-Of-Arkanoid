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
        // Tick down existing shields + statue pacification (central decrement for AllyTimer).
        foreach (var b in g.Blocks)
        {
            if (b.ShieldTimer > 0) b.ShieldTimer = System.Math.Max(0, b.ShieldTimer - dt);
            if (b.AllyTimer  > 0) b.AllyTimer  = System.Math.Max(0, b.AllyTimer  - dt);
        }

        var statues = g.Blocks.Where(b => !b.Dead && b.ShieldStatue).ToList();
        if (statues.Count == 0) return;

        foreach (var st in statues)
        {
            var interval = g.Config.ShieldStatueInterval;
            // Vase level-ups make shield statues pulse faster too (risk half of the trade).
            if (st.StatueLevel > 0) interval /= 1 + st.StatueLevel * g.Config.VaseLevelHaste;
            st.EmitAccumulator += dt; // reuse the cadence accumulator (statues aren't emitters)
            if (st.EmitAccumulator < interval) continue;
            st.EmitAccumulator -= interval;

            int r = g.Config.ShieldStatueRadius;
            var c = g.Level.Grid.CellCenter(st.Col, st.Row);
            var allied = st.AllyTimer > 0;
            foreach (var nb in g.Blocks)
            {
                if (nb.Dead || nb == st || nb.ShieldStatue || nb.Indestructible) continue;
                int dist = System.Math.Max(System.Math.Abs(nb.Col - st.Col), System.Math.Abs(nb.Row - st.Row));
                if (dist > r) continue;
                if (allied)
                    // Allied (Altar) shield statue CORRUPTS: it damages what it once protected.
                    BlockDamage.DamageBlock(g, nb, g.Config.CorruptDamage, igniteSource: false);
                else
                    nb.ShieldTimer = g.Config.ShieldDuration;
            }
            g.RaiseEvent(allied ? "corrupt" : "shield", c.X, c.Y);
            g._log.Log(g.TickCount, "shield", allied ? "corrupt pulse" : "pulse", $"id={st.Id}");
        }
    }
}
