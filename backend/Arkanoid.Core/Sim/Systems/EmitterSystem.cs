using System.Linq;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Enemy emitter blocks — the data-driven port of the original Hell Ball Spawner,
/// Witchland Beholder, and Heaven Melee Statue (all "block that periodically shoots a
/// hazard at you"). Each emitter accumulates time and fires a <see cref="Projectile"/>
/// into <see cref="GameInstance.Hazards"/> (reusing CombatSystem's paddle/HP handling).
/// EmitAim: "down" straight, "paddle" tracks the paddle, "ball" tracks the nearest ball.
/// </summary>
internal static class EmitterSystem
{
    internal static void Update(GameInstance g, double dt)
    {
        // The `charging` telegraph flag is time-based, so the block snapshot must refresh
        // while any emitter is in (or just left) its telegraph window — otherwise the cached
        // DTOs serve a stale flag whenever the ball isn't chipping a block to bump the version.
        bool refreshTelegraph = false;
        foreach (var blk in g.Blocks)
        {
            if (blk.Dead) continue;

            // Cart-spawner: periodically rolls a horizontal cart hazard across the board.
            if (blk.Cart)
            {
                blk.EmitAccumulator += dt;
                if (blk.EmitAccumulator < g.Config.Enemies.CartInterval) continue;
                blk.EmitAccumulator -= g.Config.Enemies.CartInterval;
                LaunchCart(g, blk);
                continue;
            }

            if (!blk.Emitter) continue;
            var interval = blk.EmitInterval > 0 ? blk.EmitInterval : g.Config.Enemies.DefaultEmitInterval;
            // Vase level-ups make statues fire faster (the risk half of the trade).
            if (blk.StatueLevel > 0)
                interval /= 1 + blk.StatueLevel * g.Config.Enemies.VaseLevelHaste;
            blk.EmitAccumulator += dt;
            // Within the telegraph window (or about to fire) — keep the snapshot live.
            if (blk.AllyTimer <= 0 && interval - blk.EmitAccumulator <= g.Config.Enemies.EmitTelegraphWindow)
                refreshTelegraph = true;
            if (blk.EmitAccumulator < interval) continue;
            // §3 Containment Field (Engineer): an emitter standing inside a containment zone is
            // SUPPRESSED — it can't fire. Hold it charged (so it fires the instant the field expires).
            if (IsContained(g, blk)) { blk.EmitAccumulator = interval; continue; }
            blk.EmitAccumulator -= interval;
            refreshTelegraph = true; // just fired → charging flag must clear next snapshot
            // Allied (Altar) statues fight FOR the player: bolts at blocks, not the paddle.
            if (blk.AllyTimer > 0) FireAllyBolt(g, blk);
            else Fire(g, blk);
        }
        if (refreshTelegraph) g.MarkBlocksDirty();
    }

    /// <summary>True if the block sits inside an active Containment Field zone (§3 — the reworked
    /// Radiation). Zones are the Engineer's only zone spell, so any active zone is a containment field.</summary>
    private static bool IsContained(GameInstance g, Block blk)
    {
        if (g.Zones.Count == 0) return false;
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        foreach (var z in g.Zones)
        {
            if (!z.Alive || !z.Suppresses) continue;
            if ((c - new Arkanoid.Core.Math.Vec2(z.X, z.Y)).Length <= z.Radius) return true;
        }
        return false;
    }

    /// <summary>Allied statue shot: a friendly bolt at the nearest destructible block (docs/11 convert).</summary>
    private static void FireAllyBolt(GameInstance g, Block blk)
    {
        var origin = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        Block? target = null;
        double best = double.MaxValue;
        foreach (var nb in g.Blocks)
        {
            if (nb.Dead || nb == blk || nb.Indestructible || nb.IsStatue || nb.Boss) continue;
            var nc = g.Level.Grid.CellCenter(nb.Col, nb.Row);
            var d  = (nc - origin).Length;
            if (d < best) { best = d; target = nb; }
        }
        if (target == null) return; // nothing left to shoot — hold fire

        var tc  = g.Level.Grid.CellCenter(target.Col, target.Row);
        var dir = (tc - origin).Normalized();
        g.Projectiles.Add(new Projectile
        {
            Id     = g._nextProjId++,
            // Spawn outside the statue's own cell so the bolt doesn't hit its caster.
            Pos    = origin + dir * g.Config.CellSize * 0.75,
            Vel    = dir * g.Config.Enemies.HazardSpeed,
            Damage = g.Config.Enemies.AllyBoltDamage,
            Radius = g.Config.Enemies.HazardRadius,
            Alive  = true,
            Kind   = "allybolt",
        });
        g.RaiseEvent(SimEventKind.AllyShot, origin.X, origin.Y);
    }

    /// <summary>Roll a cart obstacle left→right ABOVE the paddle. Option A (2026-06-15): the cart DEFLECTS
    /// the ball and never touches the paddle line — a left-right paddle can't dodge a full-line HP sweep, so
    /// the cart is a moving ball-obstacle (mine cart on rails), not a paddle hazard.</summary>
    private static void LaunchCart(GameInstance g, Block blk)
    {
        var y = g.Paddle.Center.Y - g.Config.CellSize * 3; // rolls through the dodge zone above the paddle
        g.Hazards.Add(new Projectile
        {
            Id       = g._nextHazardId++,
            Pos      = new Vec2(0, y),
            Vel      = new Vec2(g.Config.Enemies.CartSpeed, 0),
            Damage   = 0,                                  // not a paddle hazard — it bounces the ball
            Radius   = g.Config.Enemies.HazardRadius * 1.6,
            Alive    = true,
            Kind     = "cart",
            Behavior = HazardBehavior.Cart,
            // Telegraph: sit inert at the edge first so the player reads it before it rolls.
            Warmup   = g.Config.Enemies.CartTelegraph,
        });
        g.RaiseEvent(SimEventKind.Cart, 0, y);
    }

    private static void Fire(GameInstance g, Block blk)
    {
        var origin = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        // Level-UX rework (2026-06-15, Option 1 "fair projectiles"): emitters fire STRAIGHT DOWN their own
        // column — a fixed, telegraphed lane the player can read and step out of. No more live homing on the
        // paddle/ball (which produced shots you couldn't dodge while also catching the ball). EmitAim is
        // retained as data but no longer tracks a moving target.
        var vel = new Vec2(0, g.Config.Enemies.HazardSpeed);

        g.Hazards.Add(new Projectile
        {
            Id     = g._nextHazardId++,
            Pos    = origin,
            Vel    = vel,
            Damage = g.Config.Enemies.HazardDamage,
            Radius = g.Config.Enemies.HazardRadius,
            Alive  = true,
            Kind   = blk.MissileKind,
        });
        g.RaiseEvent(SimEventKind.EnemyShot, origin.X, origin.Y);
    }
}
