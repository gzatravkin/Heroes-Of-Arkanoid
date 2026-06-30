using Arkanoid.Core.Math;
using Arkanoid.Core.Spells;

namespace Arkanoid.Core.Sim.Systems;

/// <summary>Conflagration — detonates ALL blocks currently on fire board-wide.
/// Requires at least one burning block to cast; fizzles otherwise.
/// Identity: "Ignite first, then blow it all up." Pairs with Ignite (slot 0).</summary>
internal static class ConflagrationSystem
{
    internal static void Cast(GameInstance g, SpellDef def)
    {
        var targets = g.Blocks.Where(b => !b.Dead && b.BurnRemaining > 0).ToList();
        if (targets.Count == 0)
        {
            // No burning blocks — fizzle, no mana spent.
            g.RaiseEvent(SimEventKind.SpellFizzle, g.Paddle.Center.X, g.Paddle.Center.Y);
            return;
        }
        if (!SpellSystem.Spend(g, def.ManaCost, def.Id)) return;

        int dmg = def.Damage + (g.SpellLevel(def.Id) - 1) * def.DamagePerLevel;
        int detonated = 0, chained = 0;
        foreach (var b in targets)
        {
            if (b.Dead) continue;
            var c = g.Level.Grid.CellCenter(b.Col, b.Row);
            g.RaiseEvent(SimEventKind.Explosion, c.X, c.Y);
            BlockDamage.DamageBlock(g, b, dmg, igniteSource: false, killMult: 0.5);
            detonated++;
            if (b.Dead)
            {
                BurnSystem.LightNeighbours(g, b, 1);
                chained++;
            }
            else
            {
                b.BurnRemaining = 0;
            }
        }
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "conflagration",
            $"detonated={detonated} chained={chained} dmgEach={dmg}");
    }
}
