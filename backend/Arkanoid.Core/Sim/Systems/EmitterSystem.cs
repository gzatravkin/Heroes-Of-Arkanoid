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
        foreach (var blk in g.Blocks)
        {
            if (blk.Dead || !blk.Emitter) continue;
            var interval = blk.EmitInterval > 0 ? blk.EmitInterval : 2.5;
            blk.EmitAccumulator += dt;
            if (blk.EmitAccumulator < interval) continue;
            blk.EmitAccumulator -= interval;
            Fire(g, blk);
        }
    }

    private static void Fire(GameInstance g, Block blk)
    {
        var origin = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        var target = AimTarget(g, blk, origin);

        // Direction toward target, but always moving generally downward so it can reach the paddle.
        var dir = (target - origin);
        if (dir.Length < 0.0001) dir = new Vec2(0, 1);
        dir = dir.Normalized();
        // Bias downward: keep vy positive so an upward-aimed shot still descends.
        var vy = System.Math.Max(System.Math.Abs(dir.Y), 0.4);
        var vel = new Vec2(dir.X, vy).Normalized() * g.Config.EnemyHazardSpeed;

        g.Hazards.Add(new Projectile
        {
            Id     = g._nextHazardId++,
            Pos    = origin,
            Vel    = vel,
            Damage = g.Config.EnemyHazardDamage,
            Radius = g.Config.EnemyHazardRadius,
            Alive  = true,
        });
        g.RaiseEvent("enemyShot", origin.X, origin.Y);
        g._log.Log(g.TickCount, "emitter", "fired", $"id={blk.Id} aim={blk.EmitAim}");
    }

    private static Vec2 AimTarget(GameInstance g, Block blk, Vec2 origin)
    {
        switch (blk.EmitAim)
        {
            case "paddle":
                return g.Paddle.Center;
            case "ball":
                var ball = g.Balls.FirstOrDefault(b => b.Alive);
                return ball != null ? ball.Pos : g.Paddle.Center;
            default: // "down"
                return new Vec2(origin.X, origin.Y + 100);
        }
    }
}
