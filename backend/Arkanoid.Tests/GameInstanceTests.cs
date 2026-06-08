using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Sim;
using Xunit;

public class GameInstanceTests
{
    private static GameInstance MakeInstance()
    {
        var catalog = BlockCatalog.FromJson("""
          {"types":[{"id":"hell_basic","biome":"hell","hp":2,"sprite":"HellStandart","needToKill":true}]}
        """);
        var level = LevelLoader.FromJson("""
          {"id":"t","biome":"hell","cols":3,"rows":2,"rows_data":["...","A.."],"legend":{"A":"hell_basic"}}
        """, catalog);
        return new GameInstance(level, SimConfig.Default, seed: 123);
    }

    [Fact]
    public void NewInstance_StartsServing_WithConfiguredResources()
    {
        var g = MakeInstance();
        Assert.Equal(GamePhase.Serving, g.Phase);
        Assert.Equal(3, g.Lives);
        Assert.Equal(3, g.SpareBalls);
        Assert.Single(g.Balls);
    }

    [Fact]
    public void Serve_MovesToPlaying_AndBallGainsVelocity()
    {
        var g = MakeInstance();
        g.Serve();
        Assert.Equal(GamePhase.Playing, g.Phase);
        Assert.True(g.Balls[0].Vel.Length > 0);
    }

    [Fact]
    public void Tick_AdvancesBallPosition()
    {
        var g = MakeInstance();
        g.Serve();
        var y0 = g.Balls[0].Pos.Y;
        g.Tick(SimConfig.Default.FixedDt);
        Assert.NotEqual(y0, g.Balls[0].Pos.Y);
    }
}
