using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

public class WinLoseTests
{
    private static GameInstance Make(string rows)
    {
        var catalog = BlockCatalog.FromJson(
          "{\"types\":[{\"id\":\"b\",\"biome\":\"hell\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
          $"{{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":3,\"rows_data\":[{rows}],\"legend\":{{\"A\":\"b\"}}}}",
          catalog);
        return new GameInstance(level, SimConfig.Default, 1);
    }

    [Fact]
    public void DrainingBall_DecrementsSpareBalls_AndReserves()
    {
        var g = Make("\".A.\",\"...\",\"...\"");
        g.Serve();
        g.Balls[0].Pos = new Vec2(50, g.Level.Grid.Height + 999);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(2, g.SpareBalls);
        Assert.Equal(GamePhase.Serving, g.Phase);
    }

    [Fact]
    public void DrainingAllBalls_LosesTheLevel()
    {
        var g = Make("\".A.\",\"...\",\"...\"");
        // StartBalls=3: drains 1-3 reserve (3->2->1->0), drain 4 => Lost
        for (int i = 0; i < 4; i++)
        {
            g.Serve();
            g.Balls[0].Pos = new Vec2(50, g.Level.Grid.Height + 999);
            g.Tick(SimConfig.Default.FixedDt);
        }
        Assert.Equal(GamePhase.Lost, g.Phase);
    }

    [Fact]
    public void ClearingAllNeedToKill_Wins()
    {
        var g = Make("\".A.\",\"...\",\"...\"");
        g.Serve();
        g.Level.Blocks[0].Dead = true;
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(GamePhase.Won, g.Phase);
    }
}
