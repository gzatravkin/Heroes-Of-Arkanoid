using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Per-biome pacing modes (docs/12 identity matrix):
///   Hell  — descend: every DescendInterval all blocks press one row down;
///           a block reaching the bottom row overruns the player (level lost).
///   Heaven — escalation: every EscalateInterval all statues self-level
///           (dawdling makes the trial harder).
/// Witchland's revive clock (necromant) and Caverns' floors (WinLoseSystem)
/// are owned by their own systems.
/// </summary>
internal static class PacingSystem
{
    internal static void Update(GameInstance g, double dt)
    {
        if (g.Level.DescendInterval > 0)
        {
            g._descendAccumulator += dt;
            if (g._descendAccumulator >= g.Level.DescendInterval)
            {
                g._descendAccumulator -= g.Level.DescendInterval;
                Descend(g);
            }
        }

        if (g.Level.EscalateInterval > 0)
        {
            g._escalateAccumulator += dt;
            if (g._escalateAccumulator >= g.Level.EscalateInterval)
            {
                g._escalateAccumulator -= g.Level.EscalateInterval;
                if (g.Blocks.Any(b => !b.Dead && b.IsStatue))
                {
                    BallSystem.LevelUpStatues(g);
                    g._log.Log(g.TickCount, "pacing", "statues escalated");
                }
            }
        }
    }

    /// <summary>Press every alive block one row down; overrun at the bottom row loses the level.</summary>
    private static void Descend(GameInstance g)
    {
        var lastRow = g.Level.Grid.Rows - 1;
        foreach (var b in g.Blocks)
        {
            if (b.Dead || b.Boss) continue;
            b.Row++;
            if (b.Row >= lastRow && b.NeedToKill)
            {
                g.Phase = GamePhase.Lost;
                g.RaiseEvent("overrun", g.Level.Grid.CellCenter(b.Col, b.Row).X, g.Level.Grid.Height);
                g.RaiseEvent("levelLost", 0, 0);
                g._log.Log(g.TickCount, "pacing", "overrun — blocks reached the paddle line");
                return;
            }
        }
        g.RaiseEvent("descend", 0, 0);
        g._log.Log(g.TickCount, "pacing", "blocks descended");
    }
}
