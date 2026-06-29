using Arkanoid.Core.Math;
using Arkanoid.Core.Spells;

namespace Arkanoid.Core.Sim.Systems;

/// <summary>Conflagration (design §3 rework of Fireball) — the Fire Mage's reworked signature blast.
/// NO projectile: it DETONATES blocks in a fiery burst, dealing damage to each and chaining the flames
/// onward from any it kills. Identity = "ignite the board, then blow it all up": pre-existing fire makes
/// it BOARD-WIDE. Owner redesign 2026-06-16 — it is now SELF-SUFFICIENT: with no fire on the board it
/// still goes off, bursting the cluster of blocks around the ball, so a bare cast always does something
/// (setup with Ignite/Fire Wall just makes it far bigger). Only a truly empty board fizzles.</summary>
internal static class ConflagrationSystem
{
    internal static void Cast(GameInstance g, SpellDef def)
    {
        // Primary targets = every block already on fire (the "ignite then detonate" payoff = board-wide).
        var targets = g.Blocks.Where(b => !b.Dead && b.BurnRemaining > 0).ToList();
        bool selfSeeded = false;
        if (targets.Count == 0)
        {
            // SELF-SUFFICIENT: no fire on the board → burst the cluster of blocks AROUND THE BALL instead,
            // so a bare cast still does damage. (Pre-igniting just makes it hit the whole board.)
            var ball   = g.Balls.FirstOrDefault(b => b.Alive);
            Vec2 centre = ball != null ? ball.Pos : g.Paddle.Center;
            double r   = g.Config.CellSize * 2.5;
            bool Live(Entities.Block b) => !b.Dead && !b.Indestructible && !b.Boss;
            targets = g.Blocks.Where(b => Live(b)
                       && (g.Level.Grid.CellCenter(b.Col, b.Row) - centre).Length <= r).ToList();
            // Ball nowhere near a block → fall back to the few nearest so it never whiffs on a live board.
            if (targets.Count == 0)
                targets = g.Blocks.Where(Live)
                           .OrderBy(b => (g.Level.Grid.CellCenter(b.Col, b.Row) - centre).LengthSquared)
                           .Take(6).ToList();
            selfSeeded = true;
        }
        if (targets.Count == 0) { g.RaiseEvent(SimEventKind.SpellFizzle, g.Paddle.Center.X, g.Paddle.Center.Y); return; } // empty board
        if (!SpellSystem.Spend(g, def.ManaCost, def.Id)) return;

        int dmg = def.Damage + (g.SpellLevel(def.Id) - 1) * def.DamagePerLevel; // §6: +damage/level
        int detonated = 0, chained = 0;
        foreach (var b in targets)
        {
            if (b.Dead) continue; // a prior chain may already have collapsed it
            var c = g.Level.Grid.CellCenter(b.Col, b.Row);
            g.RaiseEvent(SimEventKind.Explosion, c.X, c.Y); // big fiery blast at each detonation
            BlockDamage.DamageBlock(g, b, dmg, igniteSource: false, killMult: 0.5);
            detonated++;
            if (b.Dead)
            {
                // A detonated block hurls its flames onward — the "chain of fire".
                BurnSystem.LightNeighbours(g, b, 1);
                chained++;
            }
            else
            {
                b.BurnRemaining = 0; // survivor: its ignite stack is spent by the detonation
            }
        }
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "conflagration",
            $"detonated={detonated} chained={chained} dmgEach={dmg} selfSeeded={selfSeeded}");
    }
}
