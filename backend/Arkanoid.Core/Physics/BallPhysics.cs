using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Physics;

public static class BallPhysics
{
    public static void ResolveWalls(Ball b, double boardW, SimConfig cfg)
    {
        if (b.Pos.X - b.Radius < 0) { b.Pos = new Vec2(b.Radius, b.Pos.Y); b.Vel = new Vec2(System.Math.Abs(b.Vel.X), b.Vel.Y); }
        else if (b.Pos.X + b.Radius > boardW) { b.Pos = new Vec2(boardW - b.Radius, b.Pos.Y); b.Vel = new Vec2(-System.Math.Abs(b.Vel.X), b.Vel.Y); }
        if (b.Pos.Y - b.Radius < 0) { b.Pos = new Vec2(b.Pos.X, b.Radius); b.Vel = new Vec2(b.Vel.X, System.Math.Abs(b.Vel.Y)); }
    }

    /// <summary>Paddle deflection by hit position. Returns true on contact; outputs normalized offset t in [-1,1].</summary>
    public static bool ResolvePaddle(Ball b, Paddle p, SimConfig cfg, out double t)
    {
        t = 0;
        var top = p.Center.Y - p.Height / 2;
        var half = p.Width / 2;
        bool overlapX = b.Pos.X >= p.Center.X - half - b.Radius && b.Pos.X <= p.Center.X + half + b.Radius;
        bool atTop = b.Vel.Y > 0 && b.Pos.Y + b.Radius >= top && b.Pos.Y <= p.Center.Y;
        if (!(overlapX && atTop)) return false;

        t = System.Math.Clamp((b.Pos.X - p.Center.X) / half, -1, 1);
        var maxRad = (cfg.PaddleMaxDeflectAngleDeg + p.DeflectAngleBonusDeg) * System.Math.PI / 180.0;
        var angle = t * maxRad;                    // 0 = straight up, ± = lean
        var speed = cfg.BallSpeed;
        var vx = System.Math.Sin(angle) * speed;
        var vy = -System.Math.Cos(angle) * speed;  // always upward
        b.Vel = ClampVertical(new Vec2(vx, vy), cfg);
        b.Pos = new Vec2(b.Pos.X, top - b.Radius - 0.1);
        return true;
    }

    /// <summary>Enforce a minimum vertical component so the ball never crawls horizontally.</summary>
    public static Vec2 ClampVertical(Vec2 v, SimConfig cfg)
    {
        var speed = v.Length;
        if (speed < 1e-6) return v;
        var ny = v.Y / speed;
        if (System.Math.Abs(ny) >= cfg.MinVerticalRatio) return v;
        var sign = ny < 0 ? -1 : 1;
        var vy = sign * cfg.MinVerticalRatio;
        var vx = System.Math.Sqrt(System.Math.Max(0, 1 - vy * vy)) * (v.X < 0 ? -1 : 1);
        return new Vec2(vx, vy) * speed;
    }
}
