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
    internal static Bonus SpawnWithType(GameInstance g, double x, double y, string effectType)
    {
        // Resolve by real effect id OR legacy catalog id (e.g. "powerup_wide" → "wide_paddle"),
        // and always store the canonical effect so ApplyEffect recognises it.
        var def = g.BonusCatalog?.All.FirstOrDefault(d => d.Effect == effectType || d.Id == effectType);
        var bonus = new Bonus
        {
            Id       = g._nextBonusId++,
            Pos      = new Vec2(x, y),
            Vel      = new Vec2(0, g.Config.Pickups.FallSpeed),
            Type     = def?.Effect ?? effectType,
            Icon     = def?.Icon ?? "",
            Count    = def?.Count ?? 1,
            Duration = def?.Duration ?? 0,
            Full     = def?.Full ?? false,
            Alive    = true,
        };
        g.Bonuses.Add(bonus);
        return bonus;
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

        // Index loop (not foreach): §1 Sleight of Hand can spawn a duplicate pickup mid-update, which
        // appends to g.Bonuses — a foreach would throw. New entries are simply processed on later indices.
        for (int _i = 0; _i < g.Bonuses.Count; _i++)
        {
            var bonus = g.Bonuses[_i];
            if (!bonus.Alive) continue;

            // Lodestone relic: pickups drift horizontally toward the paddle.
            if (Modifiers.HasLodestone(g))
            {
                var dx   = g.Paddle.Center.X - bonus.Pos.X;
                var rate = Modifiers.LodestoneSpeed(g);
                var step = System.Math.Clamp(dx, -rate * dt, rate * dt);
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
                if (Modifiers.HasMidas(g))
                    g.Crystals += Modifiers.MidasCrystals(g);
                g.RaiseEvent(SimEventKind.BonusCaught, bonus.Pos.X, bonus.Pos.Y);
                CardSystem.OnBonusCaught(g, bonus); // §1 Sleight of Hand duplicates a centre-caught pickup
                continue;
            }

            // Past drain line → silently remove.
            if (bonus.Pos.Y - g.Config.Pickups.CatchHalfH > drainLine)
            {
                bonus.Alive = false;
            }
        }

        g.Bonuses.RemoveAll(b => !b.Alive);
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
                if (!EffectSystem.HasEffect(g, "slow_ball"))
                {
                    foreach (var b in g.Balls)
                        if (b.Alive && b.Vel.Length > 0)
                            b.Vel = b.Vel * g.Config.Pickups.SlowBallFactor;
                }
                EffectSystem.Add(g, "slow_ball", g.Config.Pickups.EffectDuration);
                break;

            case "heal":
                g.Hp++;
                break;

            case "coins":
                // docs/04 §5: treasure pickups feed in-run GOLD (spent at shops), not the
                // Crystals meta-stream that flows to the Profile at level clear.
                g.Gold += g.Config.Pickups.CoinsGold;
                break;

            case "fireshot":
                EffectSystem.Add(g, "fireshot", g.Config.Pickups.FireshotDuration);
                break;

            case "shield":
                g._autoSaveActive = true;
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
    // Helper
    // -----------------------------------------------------------------------

    private static bool BoxesOverlap(Aabb a, Aabb b)
        => a.MinX <= b.MaxX && a.MaxX >= b.MinX
        && a.MinY <= b.MaxY && a.MaxY >= b.MinY;

    /// <summary>Activate or refresh the wide-paddle effect. Refreshes timer to whichever duration is longer.</summary>
    internal static void ActivateWidePaddle(GameInstance g, double duration)
    {
        var existing = g._effects.Find(e => e.Id == "wide_paddle");
        if (existing == null)
        {
            g.Paddle.Width += g.Config.Pickups.WidePaddleBonus;
            g._effects.Add(new Sim.ActiveEffect { Id = "wide_paddle", Remaining = duration });
        }
        else
        {
            existing.Remaining = System.Math.Max(existing.Remaining, duration);
        }
    }
}
