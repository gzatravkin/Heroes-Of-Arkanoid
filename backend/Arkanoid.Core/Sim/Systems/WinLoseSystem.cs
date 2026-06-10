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
        // Heaven Judgement (docs/12): survive the trial — win on the timer alone.
        if (g.Level.SurviveTime > 0 && g.ElapsedPlayTime >= g.Level.SurviveTime)
        {
            g.Phase = GamePhase.Won;
            g._log.Log(g.TickCount, "win", "survived the trial");
            g.RaiseEvent("levelWon", 0, 0);
            return;
        }

        // Caverns Demolition (docs/12): the clock ran out before the collapse.
        if (g.Level.TimeLimit > 0 && g.ElapsedPlayTime >= g.Level.TimeLimit)
        {
            g.Phase = GamePhase.Lost;
            g._log.Log(g.TickCount, "lose", "time limit expired");
            g.RaiseEvent("timeUp", 0, 0);
            g.RaiseEvent("levelLost", 0, 0);
            return;
        }

        if (!g.Blocks.Any(b => b.NeedToKill && !b.Dead))
        {
            // Multi-floor collapse (docs/12 Caverns): clear a floor → the next slides in.
            if (g.FloorIndex < g.Level.ExtraFloors.Count)
            {
                var next = g.Level.ExtraFloors[g.FloorIndex];
                g.FloorIndex++;
                g.Blocks.RemoveAll(b => !b.Boss); // keep a live boss across floors, drop debris
                g.Blocks.AddRange(next);
                g.RaiseEvent("floorDown", 0, 0);
                g._log.Log(g.TickCount, "floor", "collapsed to next floor", $"floor={g.FloorIndex}");
                return;
            }
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
            // Shield power-up (task 1.2): one-touch auto-save — catches the ball and re-serves
            // without consuming a spare ball.  Cleared after use.
            if (g._shieldActive)
            {
                g._shieldActive = false;
                g._log.Log(g.TickCount, "powerup", "shield save", "ball caught");
                g.RaiseEvent("shieldSave", g.Paddle.Center.X, g.Paddle.Center.Y);
                g.SpawnBallOnPaddle();
                return;
            }

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
