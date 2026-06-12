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
        if (g.Rng.NextDouble() >= g.Config.Pickups.DropChance) return;
        SpawnGuaranteed(g, x, y);
    }

    /// <summary>
    /// Power-up targeted drop: 25% chance, spawns a specific power-up type.
    /// Called by BlockDamage for qualifying brick TypeIds.
    /// </summary>
    internal static void TrySpawnTypedBonus(GameInstance g, double x, double y, string effectType)
    {
        if (g.Rng.NextDouble() >= g.Config.Pickups.SpecialDropChance) return;
        SpawnWithType(g, x, y, effectType);
    }

    /// <summary>
    /// Spawn a power-up of a specific effect type regardless of the global catalog roll.
    /// Looks up the catalog for the icon; falls back to empty string if the entry is missing.
    /// </summary>
    internal static void SpawnWithType(GameInstance g, double x, double y, string effectType)
    {
        var def = g.BonusCatalog?.All.FirstOrDefault(d => d.Effect == effectType);
        g.Bonuses.Add(new Bonus
        {
            Id       = g._nextBonusId++,
            Pos      = new Vec2(x, y),
            Vel      = new Vec2(0, g.Config.Pickups.FallSpeed),
            Type     = effectType,
            Icon     = def?.Icon ?? "",
            Count    = def?.Count ?? 1,
            Duration = def?.Duration ?? 0,
            Full     = def?.Full ?? false,
            Alive    = true,
        });
    }

    /// <summary>
    /// Danger pays (docs/11 R4): enemy-behaviour blocks always drop a bonus, so
    /// threats are opportunities, not just friction. Also the no-roll spawn path.
    /// </summary>
    internal static void SpawnGuaranteed(GameInstance g, double x, double y)
    {
        if (g.BonusCatalog == null) return;

        var def = g.BonusCatalog.Pick(g.Rng.Range(g.BonusCatalog.Count));

        g.Bonuses.Add(new Bonus
        {
            Id       = g._nextBonusId++,
            Pos      = new Vec2(x, y),
            Vel      = new Vec2(0, g.Config.Pickups.FallSpeed),
            Type     = def.Effect,
            Icon     = def.Icon,
            Count    = def.Count,
            Duration = def.Duration,
            Full     = def.Full,
            Alive    = true,
        });
    }

    // -----------------------------------------------------------------------
    // Update (called every tick)
    // -----------------------------------------------------------------------

    internal static void UpdateBonuses(GameInstance g, double dt)
    {
        var drainLine  = g.DrainY;
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
            var bonusBox = Aabb.FromCenter(bonus.Pos, g.Config.Pickups.CatchHalfW, g.Config.Pickups.CatchHalfH);
            if (BoxesOverlap(bonusBox, paddleBox))
            {
                bonus.Alive = false;
                ApplyEffect(g, bonus);
                // Midas relic: every catch also pays crystals.
                if (g.HasRelic("midas"))
                {
                    g.Crystals += g.Config.MidasCrystals;
                }
                g.RaiseEvent(SimEventKind.BonusCaught, bonus.Pos.X, bonus.Pos.Y);
                continue;
            }

            // Past drain line → silently remove.
            if (bonus.Pos.Y - g.Config.Pickups.CatchHalfH > drainLine)
            {
                bonus.Alive = false;
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
                for (int i = 0; i < bonus.Count; i++) SpawnExtraBall(g);
                break;

            case "mana_surge":
                g.ManaValue = bonus.Full
                    ? g.ManaMaxValue
                    : System.Math.Min(g.ManaMaxValue, g.ManaValue + g.Config.Pickups.ManaSurgeAmount);
                break;

            case "wide_paddle":
                ActivateWidePaddle(g, bonus.Duration > 0 ? bonus.Duration : g.Config.Pickups.EffectDuration);
                break;

            case "slow_ball":
                if (!g.Powerups.SlowBallActive)
                {
                    g.Powerups.SlowBallActive = true;
                    g.Powerups.SlowBallTimer  = g.Config.Pickups.EffectDuration;
                    foreach (var b in g.Balls)
                        if (b.Alive && b.Vel.Length > 0)
                            b.Vel = b.Vel * g.Config.Pickups.SlowBallFactor;
                }
                else
                {
                    g.Powerups.SlowBallTimer = g.Config.Pickups.EffectDuration;
                }
                break;

            case "heal":
                g.Hp++;
                break;

            case "coins":
                g.Crystals += g.Config.Pickups.CoinsCrystals;
                break;

            case "fireshot":
                g.Powerups.FireshotActive = true;
                g.Powerups.FireshotTimer  = g.Config.Pickups.FireshotDuration;
                break;

            case "shield":
                g.Powerups.AutoSaveActive = true;
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
    }

    // -----------------------------------------------------------------------
    // Temp effect expiry
    // -----------------------------------------------------------------------

    private static void UpdateTempEffects(GameInstance g, double dt)
    {
        if (g.Powerups.WidePaddleActive)
        {
            g.Powerups.WidePaddleTimer -= dt;
            if (g.Powerups.WidePaddleTimer <= 0)
            {
                g.Paddle.Width -= g.Config.Pickups.WidePaddleBonus;
                g.Powerups.WidePaddleActive = false;
            }
        }

        if (g.Powerups.SlowBallActive)
        {
            g.Powerups.SlowBallTimer -= dt;
            if (g.Powerups.SlowBallTimer <= 0)
            {
                foreach (var b in g.Balls)
                    if (b.Alive && b.Vel.Length > 0)
                        b.Vel = b.Vel.Normalized() * g.Config.BallSpeed;
                g.Powerups.SlowBallActive = false;
            }
        }

        if (g.Powerups.FireshotActive)
        {
            g.Powerups.FireshotTimer -= dt;
            if (g.Powerups.FireshotTimer <= 0)
            {
                g.Powerups.FireshotActive = false;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    private static bool BoxesOverlap(Aabb a, Aabb b)
        => a.MinX <= b.MaxX && a.MaxX >= b.MinX
        && a.MinY <= b.MaxY && a.MaxY >= b.MinY;

    /// <summary>Activate or refresh the wide-paddle effect. Refreshes timer to whichever duration is longer.</summary>
    private static void ActivateWidePaddle(GameInstance g, double duration)
    {
        if (!g.Powerups.WidePaddleActive)
        {
            g.Powerups.WidePaddleActive = true;
            g.Paddle.Width += g.Config.Pickups.WidePaddleBonus;
        }
        g.Powerups.WidePaddleTimer = System.Math.Max(g.Powerups.WidePaddleTimer, duration);
    }
}
