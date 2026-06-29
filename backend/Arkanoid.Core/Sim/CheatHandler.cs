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
        // spawnPowerUp:<typeName> — e.g. "spawnPowerUp:wide" spawns a powerup_wide above the paddle.
        if (op.StartsWith("spawnPowerUp:"))
        {
            Systems.BonusSystem.SpawnWithType(g,
                g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height * 3,
                "powerup_" + op.Substring("spawnPowerUp:".Length));
            return;
        }
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
                g.RaiseEvent(SimEventKind.LevelWon, 0, 0);
                break;
            case "loseNow":
                g.Phase = GamePhase.Lost;
                g.RaiseEvent(SimEventKind.LevelLost, 0, 0);
                break;
            case "setSeed":
                g.Rng = new Rng((int)value);
                break;
            case "setMana":
                g.ManaValue = System.Math.Clamp(value, 0, g.ManaMaxValue);
                break;
            case "setCritChance":
                g.CritChance = System.Math.Clamp(value, 0, 1); // test can force 100% to demo crits
                break;
            case "setCritDamage":
                g.CritDamage = System.Math.Max(1.0, value);
                break;
            case "freezeMana":
                // value != 0 freezes regen (deterministic HUD tests); 0 unfreezes.
                g._manaRegenFrozen = value != 0;
                break;
            case "setLives":
                g.Hp = System.Math.Max(0, (int)value);
                break;
            case "setBalls":
                g.SpareBalls = System.Math.Max(0, (int)value);
                break;
            case "spawnBonusRight":
                // Spawn a pickup offset to the RIGHT of the paddle but within Concussion's yank range,
                // so a test can prove the yank pulls it back toward the paddle (leftward).
                Systems.BonusSystem.SpawnWithType(g,
                    g.Paddle.Center.X + 120, g.Paddle.Center.Y - g.Paddle.Height * 4, "powerup_wide");
                break;
            case "teslaPulse":
                // Charge both Tesla walls on the first live ball (fires a curtain if armed) — for demos.
                var tb = g.Balls.FirstOrDefault(b => b.Alive) ?? g.Balls.FirstOrDefault();
                if (tb != null)
                {
                    Systems.TeslaGridSystem.OnWallBounce(g, tb, left: true);
                    Systems.TeslaGridSystem.OnWallBounce(g, tb, left: false);
                }
                break;
            case "rotHits":
                // Apply a Rot & Collapse hit (lower max HP + damage; kills collapse the column) to N
                // normal blocks — for demoing the gravity collapse without ball RNG.
                int rotN = value > 0 ? (int)value : 12;
                foreach (var rb in g.Blocks.Where(b => !b.Dead && !b.Indestructible && !b.Boss).Take(rotN).ToList())
                    Systems.BlockDamage.DamageBlock(g, rb, 3, igniteSource: false, decaySource: true);
                break;
            case "spawnEnemyBolt":
                // Drop an enemy bolt down a column — for demoing the Bone Golem bodying enemy fire.
                // If a golem is up, drop it just above the golem (deterministic soak); otherwise straight
                // down the paddle's column (without a golem it descends to the paddle and damages HP).
                if (g.Phase == GamePhase.Serving) g.Phase = GamePhase.Playing;
                var boltGolem = g.Minions.FirstOrDefault(mm => mm.Kind == "golem" && mm.Alive);
                double boltX = boltGolem?.X ?? g.Paddle.Center.X;
                // Golem demo: drop it just above the golem. Otherwise drop it a few cells above the PADDLE so
                // it reaches the paddle quickly (controllable timing for the Riposte parry demo).
                double boltY = boltGolem != null
                    ? boltGolem.Y - boltGolem.Height
                    : g.Paddle.Center.Y - g.Config.CellSize * 3;
                g.Hazards.Add(new Projectile
                {
                    Id     = g._nextHazardId++,
                    Pos    = new Vec2(boltX, boltY),
                    Vel    = new Vec2(0, g.Config.Enemies.HazardSpeed),
                    Damage = g.Config.Enemies.HazardDamage,
                    Radius = g.Config.Enemies.HazardRadius,
                    Alive  = true,
                    Kind   = "bolt",
                });
                break;
            case "freezeBall":
                // Stop all balls in place (no drain, no block hits) so a test can stage a stable
                // board — e.g. ignite the field and detonate it with Conflagration deterministically.
                foreach (var fb in g.Balls) fb.Vel = new Vec2(0, 0);
                break;
            case "setGhost":
                // Force every ball's Witchland phase: value != 0 → ghost (hits the ghost layer), 0 → normal.
                // For deterministic phase screenshots/tests of the village double board.
                foreach (var gb in g.Balls) gb.Ghost = value != 0;
                break;
            case "igniteBlocks":
                // Set N normal blocks alight (value = count, 0 = all) — for demoing Conflagration
                // detonating the board's ignite stacks without first arming the ball with Ignite.
                int igN = value > 0 ? (int)value : int.MaxValue;
                foreach (var bl in g.Blocks.Where(b => !b.Dead && !b.Indestructible && !b.Boss).Take(igN).ToList())
                    bl.BurnRemaining = 5.0;
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
            case "setCombo":
                // value = desired multiplier (1–4). Sets Combo.Count to the minimum that produces it.
                g.Combo.Multiplier = System.Math.Min(4, System.Math.Max(1, (int)value));
                g.Combo.Count = (g.Combo.Multiplier - 1) * 3;
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

            case "damageBlock":
                // Deal exactly 1 HP of damage to the block with id == value without ball physics.
                // Used to activate spawners/triggers (hp: maxHp → maxHp-1) without the oscillation
                // that ballToBlock causes (ball stuck 1px inside block hits every tick until dead).
                var dmgTarget = g.Blocks.FirstOrDefault(b => !b.Dead && b.Id == (int)value);
                if (dmgTarget != null)
                    Systems.BlockDamage.DamageBlock(g, dmgTarget, 1, igniteSource: false, decaySource: false);
                break;
            case "spawnBonus":
                // Force-spawn a bonus of the given catalog index (value = index) above the paddle.
                if (g.BonusCatalog != null && g.BonusCatalog.Count > 0)
                {
                    var idx = System.Math.Max(0, (int)value % g.BonusCatalog.Count);
                    Systems.BonusSystem.SpawnWithType(g,
                        g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height * 3,
                        g.BonusCatalog.Pick(idx).Effect);
                }
                break;
        }
    }
}
