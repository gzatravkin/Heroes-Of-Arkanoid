using Arkanoid.Core.Math;
using Xunit;

public class MathTests
{
    [Fact]
    public void Normalize_ProducesUnitLength()
    {
        var v = new Vec2(3, 4).Normalized();
        Assert.Equal(1.0, v.Length, 5);
    }

    [Fact]
    public void Reflect_AcrossHorizontalWall_FlipsY()
    {
        var v = new Vec2(2, -5);
        var r = v.Reflect(new Vec2(0, 1)); // floor normal points up
        Assert.Equal(2, r.X, 5);
        Assert.Equal(5, r.Y, 5);
    }

    [Fact]
    public void Aabb_Contains_PointInside()
    {
        var box = new Aabb(0, 0, 10, 4);
        Assert.True(box.Contains(new Vec2(5, 2)));
        Assert.False(box.Contains(new Vec2(11, 2)));
    }
}
