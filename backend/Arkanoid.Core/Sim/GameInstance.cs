using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim;

public sealed class GameInstance
{
    public SimConfig Config { get; }
    public LevelData Level { get; }
    public Rng Rng { get; private set; }

    public GamePhase Phase { get; private set; } = GamePhase.Serving;
    public int Lives { get; private set; }
    public int SpareBalls { get; private set; }

    public Paddle Paddle { get; }
    public List<Ball> Balls { get; } = new();
    public List<Block> Blocks => Level.Blocks;

    private int _nextBallId = 1;

    public GameInstance(LevelData level, SimConfig config, int seed)
    {
        Level = level; Config = config; Rng = new Rng(seed);
        Lives = config.StartLives;
        SpareBalls = config.StartBalls;
        Paddle = new Paddle {
            Width = config.PaddleWidth,
            Height = config.PaddleHeight,
            Center = new Vec2(level.Grid.Width / 2.0, level.Grid.Height + config.CellSize)
        };
        SpawnBallOnPaddle();
    }

    private void SpawnBallOnPaddle()
    {
        Balls.Clear();
        Balls.Add(new Ball {
            Id = _nextBallId++,
            Radius = Config.BallRadius,
            Pos = new Vec2(Paddle.Center.X, Paddle.Center.Y - Paddle.Height / 2 - Config.BallRadius - 1),
            Vel = new Vec2(0, 0),
            Alive = true
        });
        Phase = GamePhase.Serving;
    }

    public void Serve()
    {
        if (Phase != GamePhase.Serving) return;
        // launch upward with a small deterministic horizontal lean
        var lean = Rng.Range(-0.25, 0.25);
        Balls[0].Vel = new Vec2(lean, -1).Normalized() * Config.BallSpeed;
        Phase = GamePhase.Playing;
    }

    public void SetPaddleX(double x)
    {
        var half = Paddle.Width / 2;
        var clamped = System.Math.Clamp(x, half, Level.Grid.Width - half);
        Paddle.Center = new Vec2(clamped, Paddle.Center.Y);
    }

    public void Tick(double dt)
    {
        if (Phase != GamePhase.Playing) return;
        foreach (var b in Balls)
            b.Pos += b.Vel * dt;   // collisions added in M1
    }
}
