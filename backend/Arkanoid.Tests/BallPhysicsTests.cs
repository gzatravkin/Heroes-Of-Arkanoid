using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using Arkanoid.Core.Physics;
using Arkanoid.Core.Sim;
using Xunit;

public class BallPhysicsTests
{
    private static SimConfig Cfg => SimConfig.Default;

    [Fact]
    public void Ball_BouncesOffLeftWall()
    {
        var b = new Ball { Pos = new Vec2(5, 100), Vel = new Vec2(-200, 0), Radius = 8 };
        BallPhysics.ResolveWalls(b, boardW: 400, Cfg);
        Assert.True(b.Vel.X > 0); // reflected rightward
        Assert.True(b.Pos.X >= b.Radius);
    }

    [Fact]
    public void Ball_BouncesOffTopWall()
    {
        var b = new Ball { Pos = new Vec2(100, 4), Vel = new Vec2(0, -200), Radius = 8 };
        BallPhysics.ResolveWalls(b, boardW: 400, Cfg);
        Assert.True(b.Vel.Y > 0);
    }

    [Fact]
    public void PaddleHit_OnRightSide_PushesBallRight()
    {
        var paddle = new Paddle { Center = new Vec2(200, 300), Width = 96, Height = 16 };
        var b = new Ball { Pos = new Vec2(230, 290), Vel = new Vec2(0, 200), Radius = 8 };
        var hit = BallPhysics.ResolvePaddle(b, paddle, Cfg, out _);
        Assert.True(hit);
        Assert.True(b.Vel.Y < 0);  // bounced upward
        Assert.True(b.Vel.X > 0);  // right of center -> rightward
    }

    [Fact]
    public void PaddleHit_NeverProducesShallowAngle()
    {
        var paddle = new Paddle { Center = new Vec2(200, 300), Width = 96, Height = 16 };
        var b = new Ball { Pos = new Vec2(247, 290), Vel = new Vec2(0, 200), Radius = 8 }; // far edge
        BallPhysics.ResolvePaddle(b, paddle, Cfg, out _);
        var ratio = System.Math.Abs(b.Vel.Y) / b.Vel.Length;
        Assert.True(ratio >= Cfg.MinVerticalRatio - 1e-9);
    }
}
