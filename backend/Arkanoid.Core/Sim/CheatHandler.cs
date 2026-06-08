using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim;

/// <summary>Handles all <c>ApplyCheat</c> operations.</summary>
internal static class CheatHandler
{
    internal static void Apply(GameInstance g, string op, double value)
    {
        g._log.Log(g.TickCount, "cheat", op, $"value={value}");
        if (op.StartsWith("addRelic:")) { g.AddRelic(op.Substring("addRelic:".Length)); return; }
        switch (op)
        {
            case "clearAllButN":
                var keep  = (int)value;
                var alive = g.Blocks.Where(b => !b.Dead).ToList();
                for (int i = 0; i < alive.Count - keep; i++) alive[i].Dead = true;
                break;
            case "winNow":
                foreach (var b in g.Blocks) b.Dead = true;
                g.Phase = GamePhase.Won;
                g.RaiseEvent("levelWon", 0, 0);
                break;
            case "loseNow":
                g.Phase = GamePhase.Lost;
                g.RaiseEvent("levelLost", 0, 0);
                break;
            case "setSeed":
                g.Rng = new Rng((int)value);
                break;
            case "setMana":
                g.ManaValue = System.Math.Clamp(value, 0, g.ManaMaxValue);
                break;
            case "loseBall":
                foreach (var b in g.Balls) b.Alive = false;
                break;
            case "parkBallAbovePaddle":
                if (g.Phase == GamePhase.Serving) g.Phase = GamePhase.Playing;
                foreach (var b in g.Balls)
                {
                    b.Alive = true;
                    b.Pos   = new Vec2(
                        g.Paddle.Center.X,
                        g.Paddle.Center.Y - g.Paddle.Height / 2 - b.Radius - 1);
                    b.Vel   = new Vec2(0, g.Config.BallSpeed); // downward → deflect next tick
                }
                break;
        }
    }
}
