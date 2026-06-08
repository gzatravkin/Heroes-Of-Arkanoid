using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Per-ball update: position integration, wall resolve, teleporter warp,
/// ghost-phase skip, paddle deflect, block collision + reflection, and
/// teleport-cooldown decrement.
/// </summary>
internal static class BallSystem
{
    internal static void UpdateBall(GameInstance g, Ball b, double dt)
    {
        if (!b.Alive) return;

        // Decrement teleport cooldown once per tick (min 0)
        if (b.TeleportCooldown > 0) b.TeleportCooldown--;

        b.Pos += b.Vel * dt;
        if (g._log.Verbose)
            g._log.Log(g.TickCount, "ball", "move", $"id={b.Id} x={b.Pos.X:F1} y={b.Pos.Y:F1}");

        Arkanoid.Core.Physics.BallPhysics.ResolveWalls(b, g.Level.Grid.Width, g.Config);

        if (Arkanoid.Core.Physics.BallPhysics.ResolvePaddle(b, g.Paddle, g.Config, out var t))
            SpellSystem.OnPaddleHit(g, b, t);

        ResolveBlocks(g, b);
    }

    private static void ResolveBlocks(GameInstance g, Ball b)
    {
        var cell = g.Config.CellSize;
        // NOTE: on simultaneous overlap of two blocks (corner), we resolve the FIRST in
        // list order (deterministic). Sign-based reflection prevents sticking; exact face
        // selection is a known feel item deferred to the M1 demo pass.
        foreach (var blk in g.Blocks)
        {
            if (blk.Dead) continue;
            var c   = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            var box = Aabb.FromCenter(c, cell / 2, cell / 2);
            if (!box.IntersectsCircle(b.Pos, b.Radius)) continue;

            // Teleporter: warp ball to next teleporter in cycle (Hell signature mechanic)
            if (blk.Teleporter && b.TeleportCooldown == 0)
            {
                var teleporters = g.Blocks.Where(t => !t.Dead && t.Teleporter).ToList();
                if (teleporters.Count >= 2)
                {
                    int idx  = teleporters.IndexOf(blk);
                    var dest = teleporters[(idx + 1) % teleporters.Count];
                    var destCenter = g.Level.Grid.CellCenter(dest.Col, dest.Row);
                    // nudge one ball-radius along current velocity so ball exits cleanly
                    var nudge = b.Vel.Length > 0 ? b.Vel.Normalized() * b.Radius : new Vec2(0, -b.Radius);
                    b.Pos = destCenter + nudge;
                    b.TeleportCooldown = g.Config.TeleportCooldownTicks;
                    g.RaiseEvent("teleport", destCenter.X, destCenter.Y);
                    g._log.Log(g.TickCount, "teleport", "warped",
                        $"ball={b.Id} from=({blk.Col},{blk.Row}) to=({dest.Col},{dest.Row})");
                    return; // do not also reflect
                }
                // single teleporter: fall through to indestructible bounce below
            }

            // Ghost block (ballPhases): ball passes through entirely — no reflection, no damage
            if (blk.BallPhases) continue;

            // reflect by dominant penetration axis
            var dx = b.Pos.X - c.X;
            var dy = b.Pos.Y - c.Y;
            if (System.Math.Abs(dx) / (cell / 2) > System.Math.Abs(dy) / (cell / 2))
                b.Vel = new Vec2(System.Math.Sign(dx) * System.Math.Abs(b.Vel.X), b.Vel.Y);
            else
                b.Vel = new Vec2(b.Vel.X, System.Math.Sign(dy) * System.Math.Abs(b.Vel.Y));

            BlockDamage.DamageBlock(g, blk, Modifiers.BallDamage(g, blk, b.IgniteHitsLeft > 0),
                                    igniteSource: b.IgniteHitsLeft > 0);
            if (b.IgniteHitsLeft > 0) b.IgniteHitsLeft--;
            break; // one block per tick keeps it deterministic
        }
    }
}
