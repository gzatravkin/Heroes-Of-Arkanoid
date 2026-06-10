using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Falling bonus pickups:
///   – spawned when a block dies (BlockDamage calls SpawnBonus)
///   – fall downward each tick
///   – caught by the paddle AABB → effect applied + bonusCaught event raised
///   – fall past drain line → removed silently
///   – temp effects (wide_paddle, slow_ball) expire after BonusEffectDuration
/// </summary>
internal static class BonusSystem
{
    // -----------------------------------------------------------------------
    // Spawn
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called from BlockDamage when a block dies.  Rolls the drop chance and
    /// (if successful) adds a randomly-chosen Bonus at the block's position.
    /// </summary>
    internal static void TrySpawnBonus(GameInstance g, double x, double y)
    {
        if (g.BonusCatalog == null) return; // null-check before the roll: keeps RNG sequence stable in catalog-less tests
        if (g.Rng.NextDouble() >= g.Config.BonusDropChance) return;
        SpawnGuaranteed(g, x, y);
    }

    /// <summary>
    /// Danger pays (docs/11 R4): enemy-behaviour blocks always drop a bonus, so
    /// threats are opportunities, not just friction. Also the no-roll spawn path.
    /// </summary>
    internal static void SpawnGuaranteed(GameInstance g, double x, double y)
    {
        if (g.BonusCatalog == null) return;

        var defs = g.BonusCatalog.Defs;
        var idx  = (int)(g.Rng.NextDouble() * defs.Count) % defs.Count;
        var def  = defs[idx];

        g.Bonuses.Add(new Bonus
        {
            Id   = g._nextBonusId++,
            Pos  = new Vec2(x, y),
            Vel  = new Vec2(0, g.Config.BonusFallSpeed),
            Type = def.Effect,
            Icon = def.Icon,
            Alive = true,
        });
        g._log.Log(g.TickCount, "bonus", "spawned", $"id={g._nextBonusId-1} type={def.Effect} x={x:F0} y={y:F0}");
    }

    // -----------------------------------------------------------------------
    // Update (called every tick)
    // -----------------------------------------------------------------------

    internal static void UpdateBonuses(GameInstance g, double dt)
    {
        var drainLine  = g.Level.Grid.Height + g.Config.CellSize * 2;
        var paddleBox  = Aabb.FromCenter(g.Paddle.Center, g.Paddle.Width / 2, g.Paddle.Height / 2);

        foreach (var bonus in g.Bonuses)
        {
            if (!bonus.Alive) continue;

            // Lodestone relic: pickups drift horizontally toward the paddle.
            if (g.HasRelic("lodestone"))
            {
                var dx = g.Paddle.Center.X - bonus.Pos.X;
                var step = System.Math.Clamp(dx, -g.Config.LodestoneHoming * dt, g.Config.LodestoneHoming * dt);
                bonus.Pos = new Vec2(bonus.Pos.X + step, bonus.Pos.Y);
            }

            bonus.Pos += bonus.Vel * dt;

            // Catch check: AABB of the bonus centre overlapping the paddle box.
            var bonusBox = Aabb.FromCenter(bonus.Pos, g.Config.BonusCatchHalfW, g.Config.BonusCatchHalfH);
            if (BoxesOverlap(bonusBox, paddleBox))
            {
                bonus.Alive = false;
                ApplyEffect(g, bonus);
                // Midas relic: every catch also pays crystals.
                if (g.HasRelic("midas"))
                {
                    g.Crystals += g.Config.MidasCrystals;
                    g._log.Log(g.TickCount, "relic", "midas", $"crystals={g.Crystals}");
                }
                g.RaiseEvent("bonusCaught", bonus.Pos.X, bonus.Pos.Y);
                g._log.Log(g.TickCount, "bonus", "caught", $"id={bonus.Id} type={bonus.Type}");
                continue;
            }

            // Past drain line → silently remove.
            if (bonus.Pos.Y - g.Config.BonusCatchHalfH > drainLine)
            {
                bonus.Alive = false;
                g._log.Log(g.TickCount, "bonus", "missed", $"id={bonus.Id}");
            }
        }

        g.Bonuses.RemoveAll(b => !b.Alive);

        // Expire temporary effects.
        UpdateTempEffects(g, dt);
    }

    // -----------------------------------------------------------------------
    // Effect application
    // -----------------------------------------------------------------------

    private static void ApplyEffect(GameInstance g, Bonus bonus)
    {
        switch (bonus.Type)
        {
            case "extra_ball":
                SpawnExtraBall(g);
                break;

            case "mana_surge":
                g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + g.Config.ManaSurgeAmount);
                g._log.Log(g.TickCount, "bonus", "mana_surge", $"mana={g.ManaValue:F0}");
                break;

            case "wide_paddle":
                if (!g._widePaddleActive)
                {
                    g._widePaddleActive = true;
                    g._widePaddleTimer  = g.Config.BonusEffectDuration;
                    g.Paddle.Width     += g.Config.WidePaddleBonus;
                    g._log.Log(g.TickCount, "bonus", "wide_paddle start", $"w={g.Paddle.Width:F0}");
                }
                else
                {
                    // Refresh timer if already active.
                    g._widePaddleTimer = g.Config.BonusEffectDuration;
                }
                break;

            case "slow_ball":
                if (!g._slowBallActive)
                {
                    g._slowBallActive = true;
                    g._slowBallTimer  = g.Config.BonusEffectDuration;
                    foreach (var b in g.Balls)
                        if (b.Alive && b.Vel.Length > 0)
                            b.Vel = b.Vel * g.Config.SlowBallFactor;
                    g._log.Log(g.TickCount, "bonus", "slow_ball start", "");
                }
                else
                {
                    g._slowBallTimer = g.Config.BonusEffectDuration;
                }
                break;

            case "heal":
                g.Lives++;
                g._log.Log(g.TickCount, "bonus", "heal", $"lives={g.Lives}");
                break;

            case "coins":
                g.Crystals += g.Config.CoinsBonus;
                g._log.Log(g.TickCount, "bonus", "coins", $"crystals={g.Crystals}");
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Extra ball helper
    // -----------------------------------------------------------------------

    internal static void SpawnExtraBall(GameInstance g)
    {
        // Clone the first living ball with a slightly different angle.
        var src = g.Balls.FirstOrDefault(b => b.Alive);
        if (src == null) return;

        var extraVel = new Vec2(-src.Vel.X * 0.85 + src.Vel.Y * 0.15, src.Vel.Y);
        if (extraVel.Length < 1e-6) extraVel = new Vec2(0, -g.Config.BallSpeed);
        else extraVel = extraVel.Normalized() * src.Vel.Length;

        g.Balls.Add(new Arkanoid.Core.Entities.Ball
        {
            Id     = g._nextBallId++,
            Radius = g.Config.BallRadius,
            Pos    = new Vec2(src.Pos.X + g.Config.BallRadius * 2 + 2, src.Pos.Y),
            Vel    = extraVel,
            Alive  = true,
        });
        g._log.Log(g.TickCount, "bonus", "extra_ball spawned", $"id={g._nextBallId-1}");
    }

    // -----------------------------------------------------------------------
    // Temp effect expiry
    // -----------------------------------------------------------------------

    private static void UpdateTempEffects(GameInstance g, double dt)
    {
        if (g._widePaddleActive)
        {
            g._widePaddleTimer -= dt;
            if (g._widePaddleTimer <= 0)
            {
                g.Paddle.Width    -= g.Config.WidePaddleBonus;
                g._widePaddleActive = false;
                g._log.Log(g.TickCount, "bonus", "wide_paddle expired", $"w={g.Paddle.Width:F0}");
            }
        }

        if (g._slowBallActive)
        {
            g._slowBallTimer -= dt;
            if (g._slowBallTimer <= 0)
            {
                // Restore ball speed to the configured baseline for all living balls.
                foreach (var b in g.Balls)
                    if (b.Alive && b.Vel.Length > 0)
                        b.Vel = b.Vel.Normalized() * g.Config.BallSpeed;
                g._slowBallActive = false;
                g._log.Log(g.TickCount, "bonus", "slow_ball expired", "");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    private static bool BoxesOverlap(Aabb a, Aabb b)
        => a.MinX <= b.MaxX && a.MaxX >= b.MinX
        && a.MinY <= b.MaxY && a.MaxY >= b.MinY;
}
