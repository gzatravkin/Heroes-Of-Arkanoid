using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Generic timed-effect system.  Replaces 10+ ad-hoc XRemaining/XAccumulator fields on
/// GameInstance with a single <see cref="ActiveEffect"/> list.
/// </summary>
internal static class EffectSystem
{
    internal static bool HasEffect(GameInstance g, string id)
    {
        foreach (var e in g._effects)
            if (e.Id == id) return true;
        return false;
    }

    internal static double RemainingOf(GameInstance g, string id)
    {
        foreach (var e in g._effects)
            if (e.Id == id) return e.Remaining;
        return 0;
    }

    /// <summary>
    /// Add or refresh an effect.  Re-casting resets remaining to <paramref name="duration"/>
    /// and resets the cadence accumulator to 0.
    /// </summary>
    internal static void Add(GameInstance g, string id, double duration, double tickInterval = 0)
    {
        foreach (var e in g._effects)
        {
            if (e.Id != id) continue;
            e.Remaining = duration;
            e.Accum     = 0;
            return;
        }
        g._effects.Add(new ActiveEffect { Id = id, Remaining = duration, TickInterval = tickInterval });
    }

    /// <summary>Slow Time ball-speed factor while the spell is active (StarWarrior-derived utility).</summary>
    private const double SlowTimeFactor = 0.5;

    internal static void Update(GameInstance g, double dt)
    {
        // Per-frame steering when magnet is active (not cadence-based).
        if (HasEffect(g, "magnet")) UpdateMagnet(g, dt);
        // Recall steers balls home toward the paddle (anti-drain save); Slow Time dampens ball speed.
        if (HasEffect(g, "recall"))   UpdateRecall(g, dt);
        if (HasEffect(g, "slowtime")) UpdateSlowTime(g);

        for (int i = g._effects.Count - 1; i >= 0; i--)
        {
            var e = g._effects[i];
            e.Remaining -= dt;

            if (e.TickInterval > 0)
            {
                e.Accum += dt;
                while (e.Accum >= e.TickInterval)
                {
                    OnTick(g, e.Id);
                    e.Accum -= e.TickInterval;
                }
            }

            if (e.Remaining <= 0)
            {
                OnExpire(g, e.Id);
                g._effects.RemoveAt(i);
            }
        }
    }

    private static void OnTick(GameInstance g, string id)
    {
        // No cadence-tick effects remain: turret fires on paddle deflect (SpellSystem.OnPaddleHit),
        // phoenix is a visible orbiting entity (PhoenixSystem), and §3 reworked skeleton into the
        // Bonewalker minion (BonewalkerSystem) — none of them tick on a timer here.
    }

    private static void OnExpire(GameInstance g, string id)
    {
        switch (id)
        {
            case "wide_paddle":
                g.Paddle.Width -= g.Config.Pickups.WidePaddleBonus;
                break;

            case "slow_ball":
            case "slowtime":
                foreach (var b in g.Balls)
                    if (b.Alive && b.Vel.Length > 0)
                        b.Vel = b.Vel.Normalized() * g.Config.BallSpeed;
                break;
        }
    }

    /// <summary>Recall: steer every free ball toward the paddle (StarWarrior "Back" — anti-drain save).</summary>
    private static void UpdateRecall(GameInstance g, double dt)
    {
        var def     = g.GetSpellDef("recall");
        var maxTurn = (def?.SteerDegPerSec ?? 240) * System.Math.PI / 180.0 * dt;
        var paddle  = g.Paddle.Center;
        foreach (var b in g.Balls)
        {
            if (!b.Alive || b.GrabberId != 0) continue;
            var speed = b.Vel.Length;
            if (speed < 1e-6) continue;
            var want = System.Math.Atan2(paddle.Y - b.Pos.Y, paddle.X - b.Pos.X);
            var have = System.Math.Atan2(b.Vel.Y, b.Vel.X);
            var diff = System.Math.IEEERemainder(want - have, System.Math.PI * 2);
            var turn = System.Math.Clamp(diff, -maxTurn, maxTurn);
            var ang  = have + turn;
            b.Vel = new Vec2(System.Math.Cos(ang), System.Math.Sin(ang)) * speed;
        }
    }

    /// <summary>Slow Time: clamp ball speed down while active (a defensive aiming aid). Restored on expire.</summary>
    private static void UpdateSlowTime(GameInstance g)
    {
        double target = g.RampedBallSpeed * SlowTimeFactor;
        foreach (var b in g.Balls)
        {
            if (!b.Alive) continue;
            var speed = b.Vel.Length;
            if (speed > target && speed > 1e-6)
                b.Vel = b.Vel.Normalized() * target;
        }
    }

    private static void UpdateMagnet(GameInstance g, double dt)
    {
        var def    = g.GetSpellDef("magnet");
        var maxTurn = (def?.SteerDegPerSec ?? 120) * System.Math.PI / 180.0 * dt;
        foreach (var b in g.Balls)
        {
            if (!b.Alive || b.GrabberId != 0) continue;
            var speed = b.Vel.Length;
            if (speed < 1e-6) continue;
            Block? target = null;
            double best = double.MaxValue;
            foreach (var blk in g.Blocks)
            {
                if (blk.Dead || blk.Indestructible) continue;
                var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                var d = (c - b.Pos).Length;
                if (d < best) { best = d; target = blk; }
            }
            if (target == null) continue;
            var tc   = g.Level.Grid.CellCenter(target.Col, target.Row);
            var want = System.Math.Atan2((tc - b.Pos).Y, (tc - b.Pos).X);
            var have = System.Math.Atan2(b.Vel.Y, b.Vel.X);
            var diff = System.Math.IEEERemainder(want - have, System.Math.PI * 2);
            var turn = System.Math.Clamp(diff, -maxTurn, maxTurn);
            var ang  = have + turn;
            b.Vel = new Vec2(System.Math.Cos(ang), System.Math.Sin(ang)) * speed;
        }
    }
}
