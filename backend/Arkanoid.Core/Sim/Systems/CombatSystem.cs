using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Hazard movement + paddle collision (UpdateHazards) and direct player HP damage (DamagePlayer).
/// Boss hazard spawning has moved to BossSystem.
/// </summary>
internal static class CombatSystem
{
    // -----------------------------------------------------------------------
    // Hazards
    // -----------------------------------------------------------------------

    internal static void UpdateHazards(GameInstance g, double dt)
    {
        var drainLine = g.Level.Grid.Height + g.Config.CellSize * 2;
        var paddleBox = Aabb.FromCenter(g.Paddle.Center, g.Paddle.Width / 2, g.Paddle.Height / 2);

        foreach (var hz in g.Hazards)
        {
            if (!hz.Alive) continue;
            hz.Pos += hz.Vel * dt;

            if (paddleBox.IntersectsCircle(hz.Pos, hz.Radius))
            {
                hz.Alive = false;
                DamagePlayer(g, hz.Damage);
                g._log.Log(g.TickCount, "hazard", "hit paddle", $"damage={hz.Damage}");
                // playerHit event already raised inside DamagePlayer
                continue;
            }

            // Carts roll horizontally and despawn once they leave the board (not by falling).
            if (hz.Kind == "cart")
            {
                if (hz.Pos.X < -hz.Radius || hz.Pos.X > g.Level.Grid.Width + hz.Radius)
                    hz.Alive = false;
                continue;
            }

            if (hz.Pos.Y - hz.Radius > drainLine)
            {
                hz.Alive = false; // dodged / missed
                g._log.Log(g.TickCount, "hazard", "missed paddle", "");
            }
        }
        g.Hazards.RemoveAll(h => !h.Alive);
    }

    // -----------------------------------------------------------------------
    // Player HP
    // -----------------------------------------------------------------------

    internal static void DamagePlayer(GameInstance g, int dmg)
    {
        g.Lives -= dmg;
        g.RaiseEvent("playerHit", g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "hp", "player hit", $"lives={g.Lives}");
        if (g.Lives <= 0)
        {
            g.Phase = GamePhase.Lost;
            g.RaiseEvent("levelLost", 0, 0);
            g._log.Log(g.TickCount, "lose", "hp depleted");
        }
    }
}
