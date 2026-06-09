using System.Linq;
using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Witchland Necromant (original NecromantController + RecuperableObject): while a
/// Necromant block is alive, every destroyed *normal* block is marked and revived after
/// a delay. The player must kill the Necromant (or out-pace it) to clear the level.
/// </summary>
internal static class NecromantSystem
{
    /// <summary>Called from BlockDamage when a block dies — queue a revival if a Necromant lives.</summary>
    internal static void OnBlockDestroyed(GameInstance g, Block blk)
    {
        // Don't revive the Necromant itself, bosses, or non-needToKill specials.
        if (blk.Necromant || blk.Boss || !blk.NeedToKill) return;
        if (!AnyNecromantAlive(g)) return;
        if (g._reviveQueue.Any(r => r.Block == blk)) return;
        g._reviveQueue.Add((blk, g.Config.NecromantReviveDelay));
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.RaiseEvent("deathMark", c.X, c.Y);
    }

    internal static void Update(GameInstance g, double dt)
    {
        if (g._reviveQueue.Count == 0) return;
        for (int i = g._reviveQueue.Count - 1; i >= 0; i--)
        {
            var (blk, t) = g._reviveQueue[i];
            t -= dt;
            if (t > 0) { g._reviveQueue[i] = (blk, t); continue; }

            g._reviveQueue.RemoveAt(i);
            // Only revive if a Necromant is still alive and the block is still dead.
            if (blk.Dead && AnyNecromantAlive(g))
            {
                blk.Dead = false;
                blk.Hp   = blk.MaxHp;
                var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                g.RaiseEvent("revive", c.X, c.Y);
                g._log.Log(g.TickCount, "necromant", "revived", $"id={blk.Id} col={blk.Col} row={blk.Row}");
            }
        }
    }

    private static bool AnyNecromantAlive(GameInstance g) => g.Blocks.Any(n => !n.Dead && n.Necromant);
}
