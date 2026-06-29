using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim;

/// <summary>Shared factory and positioning helpers used across all test classes.</summary>
internal static class K
{
    // One destructible block at col=1, row=0 with the given HP, biome "t".
    internal static GameInstance OneBlock(int hp, SimConfig? cfg = null,
        BonusCatalog? bonuses = null, RelicCatalog? relics = null, int seed = 1)
    {
        var cat   = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"t\",\"hp\":{hp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3," +
            "\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            cat);
        return new GameInstance(level, cfg ?? SimConfig.Default, seed, bonuses: bonuses, relics: relics);
    }

    // Two destructible blocks at (col=0,row=0) and (col=1,row=0).
    internal static GameInstance TwoBlocks(int hp, SimConfig? cfg = null, int seed = 1)
    {
        var cat   = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"t\",\"hp\":{hp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3," +
            "\"rows_data\":[\"AA.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            cat);
        return new GameInstance(level, cfg ?? SimConfig.Default, seed);
    }

    // Generic game from JSON type definitions (array items) and a full level JSON string.
    internal static GameInstance Game(string typesJson, string levelJson,
        SimConfig? cfg = null, BonusCatalog? bonuses = null)
    {
        var cat   = BlockCatalog.FromJson($"{{\"types\":[{typesJson}]}}");
        var level = LevelLoader.FromJson(levelJson, cat, cfg ?? SimConfig.Default);
        return new GameInstance(level, cfg ?? SimConfig.Default, seed: 1, bonuses: bonuses);
    }

    // Place ball just below a block, moving straight up; one Tick() will register the hit.
    internal static void AimAt(GameInstance g, Arkanoid.Core.Entities.Block blk)
    {
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
    }

    // Park ball motionless near the paddle so it cannot hit blocks.
    internal static void Park(GameInstance g)
    {
        g.Balls[0].Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Balls[0].Radius - 2);
        g.Balls[0].Vel = new Vec2(0, 0);
    }

    // Aim at block then tick once (combines AimAt + Tick).
    internal static void Hit(GameInstance g, Arkanoid.Core.Entities.Block blk)
    {
        AimAt(g, blk);
        g.Tick(SimConfig.Default.FixedDt);
    }

    // Full-catalog variant (blocksJson includes the outer "{"types":[...]}") + auto-Serve.
    internal static GameInstance FullGame(string blocksJson, string levelJson, SimConfig? cfg = null)
    {
        var cat   = BlockCatalog.FromJson(blocksJson);
        var level = LevelLoader.FromJson(levelJson, cat, cfg ?? SimConfig.Default);
        var g = new GameInstance(level, cfg ?? SimConfig.Default, seed: 1);
        g.Serve();
        return g;
    }
}
