namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// End-of-tick win/lose resolution:
///   all needToKill blocks dead → Won;
///   all balls drained → paladin wall-save or spare-ball consume or Lost.
/// </summary>
internal static class WinLoseSystem
{
    internal static void ResolveDrainAndWin(GameInstance g)
    {
        if (!g.Blocks.Any(b => b.NeedToKill && !b.Dead))
        {
            g.Phase = GamePhase.Won;
            g._log.Log(g.TickCount, "win", "all needToKill cleared");
            g.RaiseEvent("levelWon", 0, 0);
            return;
        }

        var drainLine = g.Level.Grid.Height + g.Config.CellSize * 2;
        foreach (var b in g.Balls)
            if (b.Alive && b.Pos.Y - b.Radius > drainLine)
            { b.Alive = false; g._log.Log(g.TickCount, "drain", "ball lost", $"id={b.Id}"); }

        if (g.Balls.All(b => !b.Alive))
        {
            // Paladin passive: once per level, a lost ball is saved for free.
            if (g.Character == "paladin" && g._wallSaveAvailable)
            {
                g._wallSaveAvailable = false;
                g._log.Log(g.TickCount, "passive", "wall save", "paladin saved a ball — no spare ball consumed");
                g.SpawnBallOnPaddle();
                return;
            }

            if (g.SpareBalls <= 0)
            {
                g.Phase = GamePhase.Lost;
                g._log.Log(g.TickCount, "lose", "out of spare balls");
                g.RaiseEvent("levelLost", 0, 0);
                return;
            }
            g.SpareBalls--;
            g._log.Log(g.TickCount, "reserve", "re-serve", $"spareBalls={g.SpareBalls}");
            g.SpawnBallOnPaddle();
        }
    }
}
