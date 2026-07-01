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
        int detonated = 0;
        bool bossAlreadyBurning = false;
        foreach (var b in targets)
        {
            if (b.Dead) continue;
            if (b.Boss) bossAlreadyBurning = true;
            var c = g.Level.Grid.CellCenter(b.Col, b.Row);
            g.RaiseEvent(SimEventKind.Explosion, c.X, c.Y);
            BlockDamage.DamageBlock(g, b, dmg, igniteSource: false, killMult: 0.5);
            detonated++;
            // Always extinguish fire — dead or alive — so the board has no burning blocks
            // after a detonation and the player must use Ignite again before recasting.
            b.BurnRemaining = 0;
        }

        // Boss splash (2026-07-03, level-balance-bot): Ignite is single-target ("whatever the ball
        // last touched") with no way to steer it onto the boss specifically, so a Conflagration
        // detonation almost always hits nearby fodder instead of the boss — confirmed via
        // bot.SpellDamage massively exceeding actual boss-HP reduction (e.g. 20 total damage dealt,
        // boss HP only dropped 3). Rocket gets around this with hardcoded homing (PickRocketTarget);
        // Conflagration's identity is already "board-wide" (see the class doc comment), so a boss that
        // wasn't itself burning still catches a share of the blast — keeps Fire Mage's boss-fight
        // capability from depending entirely on the ball happening to land an Ignite on the boss tile.
        if (!bossAlreadyBurning)
        {
            var boss = g.Blocks.FirstOrDefault(b => !b.Dead && b.Boss);
            if (boss != null)
            {
                var c = g.Level.Grid.CellCenter(boss.Col, boss.Row);
                g.RaiseEvent(SimEventKind.Explosion, c.X, c.Y);
                BlockDamage.DamageBlock(g, boss, System.Math.Max(1, dmg / 2), igniteSource: false, killMult: 0.5);
            }
        }

        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "conflagration",
            $"detonated={detonated} dmgEach={dmg}");
    }
}
