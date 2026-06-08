using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Shared block-damage and fire-spread logic.  Called by BallSystem, SpellSystem, and
/// any other system that destroys blocks — single implementation, no duplication.
/// </summary>
internal static class BlockDamage
{
    /// <summary>
    /// Apply <paramref name="dmg"/> to <paramref name="blk"/>.  If the block dies and
    /// <paramref name="igniteSource"/> is true, fire may spread to neighbours (depends
    /// on character/relic via <see cref="Modifiers"/>).
    /// </summary>
    internal static void DamageBlock(GameInstance g, Block blk, int dmg, bool igniteSource)
    {
        if (blk.Indestructible) return;
        blk.Hp -= dmg;
        g._log.Log(g.TickCount, "block", blk.Hp <= 0 ? "destroyed" : "hit",
                   $"id={blk.Id} col={blk.Col} row={blk.Row} hp={blk.Hp} dmg={dmg} ignite={igniteSource}");
        if (blk.Hp <= 0 && !blk.Dead)
        {
            blk.Dead = true;
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            g.RaiseEvent("blockDestroyed", c.X, c.Y);
            g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + Modifiers.KillManaGain(g));
            if (igniteSource && Modifiers.ShouldSpreadFire(g))
                SpreadFire(g, blk);
        }
    }

    /// <summary>Spread fire from a just-destroyed ignited block to its neighbours.</summary>
    internal static void SpreadFire(GameInstance g, Block origin)
    {
        var chip = Modifiers.SpreadChip(g);
        (int dc, int dr)[] cardinal  = { (1,0), (-1,0), (0,1), (0,-1) };
        (int dc, int dr)[] diagonals = { (1,1), (1,-1), (-1,1), (-1,-1) };
        var neighbors = Modifiers.SpreadIncludesDiagonals(g)
            ? cardinal.Concat(diagonals)
            : (IEnumerable<(int, int)>)cardinal;
        foreach (var (dc, dr) in neighbors)
        {
            var nb = g.Blocks.FirstOrDefault(b => !b.Dead && b.Col == origin.Col + dc && b.Row == origin.Row + dr);
            if (nb != null)
            {
                nb.Hp -= chip;
                g._log.Log(g.TickCount, "burn", "spread chip", $"id={nb.Id} hp={nb.Hp} chip={chip}");
                var c = g.Level.Grid.CellCenter(nb.Col, nb.Row);
                g.RaiseEvent("burn", c.X, c.Y);
                if (nb.Hp <= 0) { nb.Dead = true; g.RaiseEvent("blockDestroyed", c.X, c.Y); }
            }
        }
    }
}
