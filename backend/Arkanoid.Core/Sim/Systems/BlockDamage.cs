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
    /// <paramref name="igniteSource"/> is true, fire may spread to neighbours.
    /// If <paramref name="decaySource"/> is true, necromancer decay spread applies instead.
    /// </summary>
    internal static void DamageBlock(GameInstance g, Block blk, int dmg, bool igniteSource, bool decaySource = false)
    {
        if (blk.Indestructible) return;
        if (blk.ShieldTimer > 0) { g.RaiseEvent("shieldBlock", 0, 0); return; } // shielded: immune
        blk.Hp -= dmg;
        g._log.Log(g.TickCount, "block", blk.Hp <= 0 ? "destroyed" : "hit",
                   $"id={blk.Id} col={blk.Col} row={blk.Row} hp={blk.Hp} dmg={dmg} ignite={igniteSource} decay={decaySource}");
        if (blk.Hp <= 0 && !blk.Dead)
        {
            blk.Dead = true;
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            g.RaiseEvent("blockDestroyed", c.X, c.Y);
            g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + Modifiers.KillManaGain(g));
            // Danger pays (docs/11 R4): killing an enemy-behaviour block always drops
            // a bonus; plain bricks keep the random roll. Bosses pay via level rewards.
            if (blk.Behavior != BlockBehavior.None && !blk.Boss)
                BonusSystem.SpawnGuaranteed(g, c.X, c.Y);
            else
                BonusSystem.TrySpawnBonus(g, c.X, c.Y);
            if (igniteSource && Modifiers.ShouldSpreadFire(g))
                SpreadFire(g, blk);
            if (decaySource)
                SpreadDecay(g, blk);
            if (blk.Bomb)
                Explode(g, blk);
            if (blk.Vase)
                BallSystem.PacifyStatues(g); // breaking the vase pacifies the statues
            NecromantSystem.OnBlockDestroyed(g, blk);
        }
    }

    /// <summary>
    /// Bomb block detonation: damage every block within ExplodeRadius cells (Chebyshev),
    /// chaining into other bombs the same frame (a freshly-killed bomb re-enters DamageBlock).
    /// </summary>
    internal static void Explode(GameInstance g, Block origin)
    {
        int radius = origin.ExplodeRadius > 0 ? origin.ExplodeRadius : 1;
        var c = g.Level.Grid.CellCenter(origin.Col, origin.Row);
        g.RaiseEvent("explosion", c.X, c.Y);
        g._log.Log(g.TickCount, "bomb", "exploded", $"id={origin.Id} radius={radius}");
        // Snapshot the neighbour set first so chained deaths don't mutate the loop.
        var victims = g.Blocks.Where(nb =>
            !nb.Dead && nb != origin &&
            System.Math.Max(System.Math.Abs(nb.Col - origin.Col), System.Math.Abs(nb.Row - origin.Row)) <= radius
        ).ToList();
        foreach (var nb in victims)
            if (!nb.Dead)
                DamageBlock(g, nb, g.Config.BombDamage, igniteSource: false);
    }

    /// <summary>
    /// Spread necromancer decay from a killed block to neighbours within
    /// Manhattan distance ≤ DecaySpreadRange, chipping each by DecaySpreadChip.
    /// </summary>
    internal static void SpreadDecay(GameInstance g, Block origin)
    {
        int range = g.Config.DecaySpreadRange;
        int chip  = g.Config.DecaySpreadChip;
        foreach (var nb in g.Blocks)
        {
            if (nb.Dead || nb == origin) continue;
            int dist = System.Math.Abs(nb.Col - origin.Col) + System.Math.Abs(nb.Row - origin.Row);
            if (dist > range) continue;
            nb.Hp -= chip;
            var c = g.Level.Grid.CellCenter(nb.Col, nb.Row);
            g.RaiseEvent("decay", c.X, c.Y);
            g._log.Log(g.TickCount, "decay", "spread chip", $"id={nb.Id} hp={nb.Hp} chip={chip}");
            if (nb.Hp <= 0 && !nb.Dead)
            {
                nb.Dead = true;
                g.RaiseEvent("blockDestroyed", c.X, c.Y);
                g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + Modifiers.KillManaGain(g));
            }
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
