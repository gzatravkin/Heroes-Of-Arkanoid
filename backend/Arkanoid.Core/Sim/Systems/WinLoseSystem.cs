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
            g.RaiseEvent(SimEventKind.LevelWon, 0, 0);
            return;
        }

        // Caverns Demolition (docs/12): the clock ran out before the collapse.
        if (g.Level.TimeLimit > 0 && g.ElapsedPlayTime >= g.Level.TimeLimit)
        {
            g.Phase = GamePhase.Lost;
            g.RaiseEvent(SimEventKind.TimeUp, 0, 0);
            g.RaiseEvent(SimEventKind.LevelLost, 0, 0);
            return;
        }

        if (!g.Blocks.Any(b => b.NeedToKill && !b.Dead))
        {
            // Continuous Rift (2026-06-16): clear a floor → PAUSE for the §8 1-of-3 draft; the pick applies live
            // and then GameInstance.AdvanceRiftFloor slides the next floor in (leftovers → sides).
            if (g.RiftMode && g.FloorIndex < g.ExtraFloors.Count)
            {
                g.BeginRiftDraft();
                return;
            }
            // Multi-floor collapse (docs/12 Caverns): clear a floor → the next slides in (no draft).
            if (g.FloorIndex < g.ExtraFloors.Count)
            {
                var next = g.ExtraFloors[g.FloorIndex];
                g.FloorIndex++;
                g.Blocks.RemoveAll(b => !b.Boss);    // caverns: keep a live boss, drop debris
                g.Blocks.AddRange(next);
                g.RaiseEvent(SimEventKind.FloorDown, 0, 0);
                return;
            }
            g.Phase = GamePhase.Won;
            g.RaiseEvent(SimEventKind.LevelWon, 0, 0);
            return;
        }

        var drainLine = g.DrainY;
        foreach (var b in g.Balls)
            if (b.Alive && b.Pos.Y - b.Radius > drainLine)
            { b.Alive = false; g._log.Log(g.TickCount, "drain", "ball lost", $"id={b.Id}"); }

        if (g.Balls.All(b => !b.Alive))
        {
            // Shield power-up (task 1.2): one-touch auto-save — catches the ball and re-serves
            // without consuming a spare ball.  Cleared after use.
            if (g._autoSaveActive)
            {
                g._autoSaveActive = false;
                g.RaiseEvent(SimEventKind.ShieldSave, g.Paddle.Center.X, g.Paddle.Center.Y);
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

            // §5.5 Paladin ★3: the first ball-drain each level is saved (an extra free save).
            if (g.HasPerk(Meta.StatResolver.PalSaveDrain) && g._perkSaveAvailable)
            {
                g._perkSaveAvailable = false;
                g._log.Log(g.TickCount, "perk", "drain save", "paladin ★3 saved the first ball-drain this level");
                g.SpawnBallOnPaddle();
                return;
            }

            if (g.SpareBalls <= 0)
            {
                g.Phase = GamePhase.Lost;
                g.RaiseEvent(SimEventKind.LevelLost, 0, 0);
                return;
            }
            g.SpareBalls--;
            g._log.Log(g.TickCount, "reserve", "re-serve", $"spareBalls={g.SpareBalls}");
            g.SpawnBallOnPaddle();
        }
    }

    /// <summary>Continuous Rift descend (owner 2026-06-16): drop dead/destructible debris, but KEEP every
    /// surviving indestructible "immortal" block and slide it to the nearest side column — they accumulate
    /// there across floors as a growing obstacle while the next floor fills the centre.</summary>
    internal static void SlideLeftoversToSides(GameInstance g)
    {
        g.Blocks.RemoveAll(b => b.Dead || b.Boss || !b.Indestructible); // keep only live indestructibles
        int cols = g.Level.Grid.Cols;
        var occupied = new System.Collections.Generic.HashSet<(int, int)>();
        foreach (var b in g.Blocks)
        {
            bool left = b.Col < cols / 2;
            int dir = left ? 1 : -1;
            int col = left ? 0 : cols - 1;
            // pack inward from the edge if that side cell is already taken by an earlier leftover
            while (occupied.Contains((col, b.Row)) && col > 0 && col < cols - 1) col += dir;
            b.Col = col;
            occupied.Add((col, b.Row));
        }
        g.InvalidateBlockGrid();
    }
}
