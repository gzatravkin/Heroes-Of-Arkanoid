using Arkanoid.Core.Entities;
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
            case "setLives":
                g.Lives = System.Math.Max(0, (int)value);
                break;
            case "setBalls":
                g.SpareBalls = System.Math.Max(0, (int)value);
                break;
            case "chipBlocks":
                // Damage every normal block by `value` (deterministic; for showing damage states).
                foreach (var bl in g.Blocks.Where(b => !b.Dead && !b.Indestructible && !b.Boss).ToList())
                    Systems.BlockDamage.DamageBlock(g, bl, (int)value, igniteSource: false);
                break;
            case "dropStalactites":
                Systems.StalactiteSystem.BossDrop(g, System.Math.Max(1, (int)value));
                break;
            case "fastForward":
                // Deterministic time-travel for testing time-based mechanics (emitters,
                // boss cadence). Freeze balls so they don't drain, then advance N ticks.
                if (g.Phase == GamePhase.Serving) g.Phase = GamePhase.Playing;
                foreach (var b in g.Balls) b.Vel = new Vec2(0, 0);
                int ffN = System.Math.Clamp((int)value, 0, 2000);
                for (int i = 0; i < ffN; i++) g.Tick(g.Config.FixedDt);
                break;
            case "setBossHp":
                // value = percent (0..100) of each live boss block's max HP.
                var bossFrac = System.Math.Clamp(value / 100.0, 0, 1);
                foreach (var bb in g.Blocks.Where(b => !b.Dead && b.Boss))
                    bb.Hp = System.Math.Max(0, (int)System.Math.Round(bb.MaxHp * bossFrac));
                break;
            case "loseBall":
                foreach (var b in g.Balls) b.Alive = false;
                break;
            case "ballToBlock":
                // Drive the first ball into the block with id == value (collision next tick
                // via the public path) — used by tests to trigger contact behaviours (bat grab).
                var target = g.Blocks.FirstOrDefault(b => !b.Dead && b.Id == (int)value);
                if (target != null)
                {
                    if (g.Phase == GamePhase.Serving) g.Phase = GamePhase.Playing;
                    var tc = g.Level.Grid.CellCenter(target.Col, target.Row);
                    var ball = g.Balls.FirstOrDefault(b => b.Alive);
                    if (ball != null)
                    {
                        // Place slightly overlapping so the contact registers on the next
                        // tick even if a later fastForward freezes the ball's velocity.
                        ball.Pos = new Vec2(tc.X, tc.Y + g.Config.CellSize / 2 + ball.Radius - 1);
                        ball.Vel = new Vec2(0, -g.Config.BallSpeed);
                    }
                }
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

            case "spawnBonus":
                // Force-spawn a bonus of the given catalog index (value = index) above the paddle.
                if (g.BonusCatalog != null && g.BonusCatalog.Defs.Count > 0)
                {
                    var idx = System.Math.Max(0, (int)value % g.BonusCatalog.Defs.Count);
                    var def = g.BonusCatalog.Defs[idx];
                    g.Bonuses.Add(new Bonus
                    {
                        Id    = g._nextBonusId++,
                        Pos   = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height * 3),
                        Vel   = new Vec2(0, g.Config.BonusFallSpeed),
                        Type  = def.Effect,
                        Icon  = def.Icon,
                        Alive = true,
                    });
                    g._log.Log(g.TickCount, "cheat", "spawnBonus", $"type={def.Effect}");
                }
                break;
        }
    }
}
