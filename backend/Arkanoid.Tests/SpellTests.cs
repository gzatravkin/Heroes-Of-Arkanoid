using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Sim;
using Xunit;

public class SpellTests
{
    private static GameInstance Make()
    {
        var catalog = BlockCatalog.FromJson(
          "{\"types\":[{\"id\":\"b\",\"biome\":\"hell\",\"hp\":3,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
          "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":4,\"rows_data\":[\".A.\",\"...\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
          catalog);
        return new GameInstance(level, SimConfig.Default, 1);
    }

    [Fact]
    public void Mana_RegeneratesOverTime()
    {
        var g = Make(); g.Serve();
        var before = g.ManaValue;
        for (int i = 0; i < 60; i++) g.Tick(SimConfig.Default.FixedDt); // 1 second
        Assert.True(g.ManaValue > before);
    }

    [Fact]
    public void Fireball_RequiresMana_AndConsumesIt()
    {
        var g = Make(); g.Serve();
        g.ManaValue = SimConfig.Default.FireballCost;
        g.CastFireball();
        Assert.True(g.ManaValue < SimConfig.Default.FireballCost + 1e-9);
        Assert.Single(g.Projectiles);
    }

    [Fact]
    public void Fireball_TooLittleMana_DoesNothing()
    {
        var g = Make(); g.Serve();
        g.ManaValue = 0;
        g.CastFireball();
        Assert.Empty(g.Projectiles);
    }

    [Fact]
    public void Ignite_ArmsOnCast_AndImbuesNextDeflect()
    {
        var g = Make(); g.Serve();
        g.CastIgnite();                 // arm
        // drive a paddle hit: place ball just above paddle moving down at center
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height/2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, 200);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Balls[0].IgniteHitsLeft > 0);
    }

    [Fact]
    public void IgnitedBall_DealsBonusDamage()
    {
        var g = Make(); g.Serve();
        var blk = g.Level.Blocks[0];                 // hp 3
        g.Balls[0].IgniteHitsLeft = 2;               // pre-imbued
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(c.X, c.Y + SimConfig.Default.CellSize/2 + 6);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(1, blk.Hp);                     // 3 - (1 base + 1 ignite) = 1
    }
}
