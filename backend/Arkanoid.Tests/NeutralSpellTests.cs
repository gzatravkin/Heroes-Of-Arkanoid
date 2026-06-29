using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Recall + Slow Time — class-less "shared ball/paddle tech" pool spells ported from the old
/// StarWarrior kit (docs/04 §3). Recall steers balls home; Slow Time dampens ball speed.
/// </summary>
public class NeutralSpellTests
{
    private static readonly double Dt = SimConfig.Default.FixedDt;

    private static GameInstance Make()
    {
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"hell\",\"hp\":10,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":8," +
            "\"rows_data\":[\".A.\",\"...\",\"...\",\"...\",\"...\",\"...\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        return new GameInstance(level, SimConfig.Default, 1);
    }

    [Fact]
    public void NeutralSpells_AreInTheSharedPool()
    {
        var pool = CharacterCatalog.Default.Pool();
        Assert.Contains("recall", pool);
        Assert.Contains("slowtime", pool);
        // …and are not anyone's signature.
        Assert.DoesNotContain("recall", CharacterCatalog.Default.Signatures);
        Assert.DoesNotContain("slowtime", CharacterCatalog.Default.Signatures);
    }

    [Fact]
    public void Recall_SteersBallTowardPaddle()
    {
        var g = Make();
        g.SetLoadout(new[] { "recall" });
        g.ManaValue = 100;
        g.Serve();
        var ball = g.Balls[0];
        // Ball above the paddle, moving up-and-away (toward the pit-escape direction).
        ball.Pos = new Vec2(g.Paddle.Center.X + 50, g.Paddle.Center.Y - 100);
        ball.Vel = new Vec2(180, -260);
        g.CastSlot(0); // recall
        double vyBefore = ball.Vel.Y;
        for (int i = 0; i < 5; i++) g.Tick(Dt);
        Assert.True(g.Balls[0].Vel.Y > vyBefore,
            $"recall should turn the ball back down toward the paddle; vy {vyBefore} -> {g.Balls[0].Vel.Y}");
    }

    [Fact]
    public void Raise_SummonsFriendlyHelperBall()
    {
        // Necromancer signature (docs/04 §3): summons a friendly skeleton helper-ball.
        var g = Make();
        g.SetLoadout(new[] { "raise" });
        g.ManaValue = 100;
        g.Serve();
        int before = g.Balls.Count;
        g.CastSlot(0); // raise
        Assert.True(g.Balls.Count > before, "Raise should summon a helper-ball");
        Assert.Contains(g.Balls, b => b.Summoned && b.Alive);
    }

    [Fact]
    public void SlowTime_ReducesBallSpeed()
    {
        var g = Make();
        g.SetLoadout(new[] { "slowtime" });
        g.ManaValue = 100;
        g.Serve();
        var ball = g.Balls[0];
        ball.Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - 200);
        ball.Vel = new Vec2(0, -g.Config.BallSpeed); // full speed upward
        g.CastSlot(0); // slow time
        g.Tick(Dt);
        Assert.True(g.Balls[0].Vel.Length < g.Config.BallSpeed * 0.9,
            $"slow time should reduce ball speed; got {g.Balls[0].Vel.Length} of {g.Config.BallSpeed}");
    }
}
