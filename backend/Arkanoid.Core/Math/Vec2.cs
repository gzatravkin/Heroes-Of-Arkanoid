namespace Arkanoid.Core.Math;

public readonly struct Vec2
{
    public readonly double X;
    public readonly double Y;
    public Vec2(double x, double y) { X = x; Y = y; }

    public double LengthSquared => X * X + Y * Y;
    public double Length => System.Math.Sqrt(X * X + Y * Y);

    public Vec2 Normalized()
    {
        var len = Length;
        return len <= 1e-9 ? new Vec2(0, 0) : new Vec2(X / len, Y / len);
    }

    public double Dot(Vec2 o) => X * o.X + Y * o.Y;

    /// <summary>Reflect this vector across a surface with unit normal n.</summary>
    public Vec2 Reflect(Vec2 n)
    {
        var d = 2 * Dot(n);
        return new Vec2(X - d * n.X, Y - d * n.Y);
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
}
