using System.Linq;
using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Witchland Reviver block: while a Reviver block is alive, every destroyed *normal*
/// block is queued for revival after a delay. The player must kill the Reviver (or
/// out-pace it) to clear the level.
/// </summary>
internal static class ReviverSystem
{
    /// <summary>Called from BlockDamage when a block dies — queue a revival if a Reviver block lives.</summary>
    internal static void OnBlockDestroyed(GameInstance g, Block blk)
    {
        // Don't revive the Reviver itself, bosses, or non-needToKill specials.
        if (blk.Reviver || blk.Boss || !blk.NeedToKill) return;
        if (!AnyReviverAlive(g)) return;
        if (g._reviveQueue.Any(r => r.Block == blk)) return;
        g._reviveQueue.Add((blk, g.Config.Enemies.ReviveDelay));
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.RaiseEvent(SimEventKind.DeathMark, c.X, c.Y);
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
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            // Only revive if a Reviver block is still alive and the block is still dead.
            if (blk.Dead && AnyReviverAlive(g))
            {
                blk.Dead = false;
                blk.Hp   = blk.MaxHp;
                g.InvalidateBlockGrid();
                g.RaiseEvent(SimEventKind.Revive, c.X, c.Y);
                g._log.Log(g.TickCount, "reviver", "revived", $"id={blk.Id} col={blk.Col} row={blk.Row}");
            }
            else
            {
                // Reviver died first — the renderer clears the death-mark sphere.
                g.RaiseEvent(SimEventKind.ReviveCancelled, c.X, c.Y);
                g._log.Log(g.TickCount, "reviver", "revive cancelled", $"id={blk.Id}");
            }
        }
    }

    private static bool AnyReviverAlive(GameInstance g) => g.Blocks.Any(n => !n.Dead && n.Reviver);
}
