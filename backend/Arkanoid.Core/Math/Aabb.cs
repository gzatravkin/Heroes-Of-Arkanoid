namespace Arkanoid.Core.Math;

public readonly struct Aabb
{
    public readonly double MinX, MinY, MaxX, MaxY;
    public Aabb(double minX, double minY, double maxX, double maxY)
    { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }

    public static Aabb FromCenter(Vec2 c, double halfW, double halfH)
        => new(c.X - halfW, c.Y - halfH, c.X + halfW, c.Y + halfH);

    public bool Contains(Vec2 p) => p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;

    /// <summary>True if a circle of radius r centered at c overlaps this box.</summary>
    public bool IntersectsCircle(Vec2 c, double r)
    {
        var nx = System.Math.Clamp(c.X, MinX, MaxX);
        var ny = System.Math.Clamp(c.Y, MinY, MaxY);
        var dx = c.X - nx; var dy = c.Y - ny;
        return dx * dx + dy * dy <= r * r;
    }
}
