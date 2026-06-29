using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Heaven Shield Statue (original ShieldStatue): on a cadence it grants every nearby block
/// a temporary damage shield, so the level keeps re-armouring until you destroy the statue.
/// Shielded blocks ignore damage (see BlockDamage) while their ImmunityTimer is positive.
/// (The original altar/vase ally-toggle is deferred — see docs/08.)
/// </summary>
internal static class ShieldSystem
{
    internal static void Update(GameInstance g, double dt)
    {
        // The `shielded`/`allied` flags are time-based, so the block snapshot must refresh
        // while any timer is live — otherwise the cached DTOs serve a stale flag whenever the
        // ball isn't chipping a block to bump the version.
        bool dirty = false;
        // Tick down existing shields + statue pacification (central decrement for AllyTimer).
        foreach (var b in g.Blocks)
        {
            if (b.ImmunityTimer > 0) { b.ImmunityTimer = System.Math.Max(0, b.ImmunityTimer - dt); dirty = true; }
            if (b.AllyTimer  > 0)    { b.AllyTimer  = System.Math.Max(0, b.AllyTimer  - dt); dirty = true; }
        }

        var statues = g.Blocks.Where(b => !b.Dead && b.ShieldStatue).ToList();
        if (statues.Count == 0) { if (dirty) g.MarkBlocksDirty(); return; }

        foreach (var st in statues)
        {
            var interval = g.Config.Enemies.ShieldStatueInterval;
            // Vase level-ups make shield statues pulse faster too (risk half of the trade).
            if (st.StatueLevel > 0) interval /= 1 + st.StatueLevel * g.Config.Enemies.VaseLevelHaste;
            st.EmitAccumulator += dt; // reuse the cadence accumulator (statues aren't emitters)
            if (st.EmitAccumulator < interval) continue;
            st.EmitAccumulator -= interval;

            int r = g.Config.Enemies.ShieldStatueRadius;
            var c = g.Level.Grid.CellCenter(st.Col, st.Row);
            var allied = st.AllyTimer > 0;
            foreach (var nb in g.Blocks)
            {
                if (nb.Dead || nb == st || nb.ShieldStatue || nb.Indestructible) continue;
                int dist = System.Math.Max(System.Math.Abs(nb.Col - st.Col), System.Math.Abs(nb.Row - st.Row));
                if (dist > r) continue;
                if (allied)
                    // Allied (Altar) shield statue CORRUPTS: it damages what it once protected.
                    BlockDamage.DamageBlock(g, nb, g.Config.Enemies.CorruptDamage, igniteSource: false, killMult: 0.5);
                else
                    { nb.ImmunityTimer = g.Config.Enemies.StatueImmunityDuration; dirty = true; }
            }
            g.RaiseEvent(allied ? SimEventKind.Corrupt : SimEventKind.Shield, c.X, c.Y);
        }
        if (dirty) g.MarkBlocksDirty();
    }
}
