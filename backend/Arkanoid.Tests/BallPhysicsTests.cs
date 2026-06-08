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

    [Fact]
    public void Ball_DamagesBlock_AndBouncesOff()
    {
        var catalog = Arkanoid.Core.Blocks.BlockCatalog.FromJson(
          "{\"types\":[{\"id\":\"b\",\"biome\":\"hell\",\"hp\":2,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = Arkanoid.Core.Grid.LevelLoader.FromJson(
          "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":3,\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
          catalog);
        var g = new GameInstance(level, SimConfig.Default, 1);
        g.Serve();
        var block = level.Blocks[0];
        var c = level.Grid.CellCenter(block.Col, block.Row);
        // place ball just under the block moving up
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + 6);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(1, block.Hp);            // took 1 damage
        Assert.True(g.Balls[0].Vel.Y > 0);    // bounced downward
    }
}
