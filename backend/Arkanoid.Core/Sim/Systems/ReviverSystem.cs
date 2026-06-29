using System.Linq;
using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Witchland Reviver block (necromant): while a same-layer Reviver lives, every destroyed block is
/// queued for revival after a delay. The corpse keeps its NATURE (owner 2026-06-16):
///   • a REGULAR block leaves a regular corpse → only a regular necromant raises it (as a regular block);
///   • a GHOST block leaves a ghost corpse      → only a GHOST necromant (ballPhases) raises it (as a ghost).
/// A regular necromant never touches a ghost corpse and vice-versa. The player must kill the matching
/// necromant (or out-pace it) to clear that layer — and a ghost necromant can only be hit by a phased ball.
/// </summary>
internal static class ReviverSystem
{
    /// <summary>Called from BlockDamage when a block dies — queue a revival if a SAME-LAYER Reviver lives.</summary>
    internal static void OnBlockDestroyed(GameInstance g, Block blk)
    {
        // Don't revive a Reviver itself, bosses, or non-needToKill specials.
        if (blk.Reviver || blk.Boss || !blk.NeedToKill) return;
        // Only a necromant on the corpse's OWN layer can raise it.
        if (!AnyReviverAlive(g, blk.BallPhases)) return;
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
            // Revive only if a same-layer Reviver still lives. The block returns on the SAME layer it
            // died on (regular → regular, ghost → ghost): a corpse keeps its nature.
            if (blk.Dead && AnyReviverAlive(g, blk.BallPhases))
            {
                blk.Dead = false;
                blk.Hp   = blk.MaxHp;
                g.InvalidateBlockGrid();
                g.RaiseEvent(SimEventKind.Revive, c.X, c.Y);
            }
            else
            {
                // The matching necromant died first — the renderer clears the death-mark sphere.
                g.RaiseEvent(SimEventKind.ReviveCancelled, c.X, c.Y);
            }
        }
    }

    /// <summary>Is a Reviver alive on the given layer? ghost=true → a ghost necromant (ballPhases).</summary>
    private static bool AnyReviverAlive(GameInstance g, bool ghost)
        => g.Blocks.Any(n => !n.Dead && n.Reviver && n.BallPhases == ghost);
}
