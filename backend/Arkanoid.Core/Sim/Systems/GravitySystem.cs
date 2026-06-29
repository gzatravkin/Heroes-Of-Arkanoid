using System.Linq;

namespace Arkanoid.Core.Sim.Systems;

/// <summary>Gravity / falling-block primitive (§3 Rot &amp; Collapse, §1 Avalanche): collapse a column so
/// its surviving blocks fall down to fill any gaps below them. Pure grid op — blocks carry Col/Row and
/// the snapshot derives screen position from CellCenter, so changing Row "moves" them for free.</summary>
internal static class GravitySystem
{
    /// <summary>Drop every live block in <paramref name="col"/> so they rest densely at the bottom of the
    /// playfield (gravity). Indestructible blocks are anchors — blocks above one stack on top of it.</summary>
    internal static void CollapseColumn(GameInstance g, int col)
    {
        var inCol = g.Blocks.Where(b => !b.Dead && b.Col == col).OrderByDescending(b => b.Row).ToList();
        if (inCol.Count == 0) return;

        bool moved = false;
        int floor = g.Level.Grid.Rows - 1; // lowest empty row a falling block can reach next
        // On Hell descend levels the bottom row is the overrun/loss line — never collapse a block
        // onto it (a "helpful" gravity spell must not instantly doom the player).
        if (g.Level.DescendInterval > 0) floor = System.Math.Max(0, floor - 1);
        foreach (var b in inCol) // bottom-most first
        {
            if (b.Indestructible)
            {
                // An anchor doesn't move; the next block falls to just above it.
                floor = b.Row - 1;
                continue;
            }
            if (b.Row != floor) { b.Row = floor; moved = true; }
            floor--;
        }
        if (moved) { g.InvalidateBlockGrid(); g.MarkBlocksDirty(); g._log.Log(g.TickCount, "gravity", "collapse", $"col={col}"); }
    }
}
