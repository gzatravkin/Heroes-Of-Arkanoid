using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Boss hazard spawning (UpdateBoss), hazard movement + paddle collision (UpdateHazards),
/// and direct player HP damage (DamagePlayer).
/// </summary>
internal static class CombatSystem
{
    // -----------------------------------------------------------------------
    // Boss
    // -----------------------------------------------------------------------

    internal static void UpdateBoss(GameInstance g, double dt)
    {
        var bossBlocks = g.Blocks.Where(b => !b.Dead && b.Boss).ToList();
        if (bossBlocks.Count == 0) return;

        g._bossAttackAccumulator += dt;
        while (g._bossAttackAccumulator >= g.Config.BossAttackInterval)
        {
            foreach (var boss in bossBlocks)
            {
                var origin = g.Level.Grid.CellCenter(boss.Col, boss.Row);
                // Deterministic horizontal lean toward the paddle (limited by aim strength)
                var dx   = g.Paddle.Center.X - origin.X;
                var aimX = dx * g.Config.BossHazardAimStrength;
                // Keep the downward component fixed at full speed for predictable dodging.
                var vel = new Vec2(aimX, g.Config.BossHazardSpeed);
                g.Hazards.Add(new Projectile {
                    Id     = g._nextHazardId++,
                    Pos    = origin,
                    Vel    = vel,
                    Damage = g.Config.BossHazardDamage,
                    Radius = g.Config.BossHazardRadius
                });
                g.RaiseEvent("bossAttack", origin.X, origin.Y);
                g._log.Log(g.TickCount, "boss", "hazard spawned",
                    $"bossId={boss.Id} paddleX={g.Paddle.Center.X:F1}");
            }
            g._bossAttackAccumulator -= g.Config.BossAttackInterval;
        }
    }

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
