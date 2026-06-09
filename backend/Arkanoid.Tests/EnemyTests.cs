using Arkanoid.Core.Blocks;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Tests for the ported original enemies/hazards (docs/08-enemy-behaviour-spec.md).
/// Pure-Core, deterministic.
/// </summary>
public class EnemyTests
{
    private static GameInstance Make(string blocksJson, string levelJson)
    {
        var catalog = BlockCatalog.FromJson(blocksJson);
        var level   = LevelLoader.FromJson(levelJson, catalog);
        var g = new GameInstance(level, SimConfig.Default, seed: 1);
        g.Serve();
        return g;
    }

    /// <summary>Drive the ball into a specific block from below and tick once (public path).</summary>
    private static void BallHit(GameInstance g, Block target)
    {
        var c = g.Level.Grid.CellCenter(target.Col, target.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
    }

    // ── Emitter (Hell spawner / Beholder / Melee statue) ──────────────────────

    [Fact]
    public void EmitterBlock_FiresHazard_AfterItsInterval()
    {
        var g = Make(
            "{\"types\":[{\"id\":\"e\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"emitter\":true,\"emitInterval\":1.0,\"emitAim\":\"paddle\"}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".E.\",\"...\",\"...\"],\"legend\":{\"E\":\"e\"}}");

        Assert.Empty(g.Hazards);
        // Advance just under the interval → still no hazard.
        for (int i = 0; i < 50; i++) g.Tick(0.016);
        Assert.Empty(g.Hazards);
        // Cross the 1.0s interval → exactly one hazard fired.
        for (int i = 0; i < 20; i++) g.Tick(0.016);
        Assert.True(g.Hazards.Count >= 1, $"expected an emitted hazard, got {g.Hazards.Count}");
    }

    [Fact]
    public void EmitterHazard_FallsDownward()
    {
        var g = Make(
            "{\"types\":[{\"id\":\"e\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"emitter\":true,\"emitInterval\":0.5,\"emitAim\":\"down\"}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".E.\",\"...\",\"...\"],\"legend\":{\"E\":\"e\"}}");
        for (int i = 0; i < 40; i++) g.Tick(0.016);
        Assert.NotEmpty(g.Hazards);
        Assert.True(g.Hazards[0].Vel.Y > 0, "hazard must travel downward toward the paddle");
    }

    // ── Bomb (chain explosion) ────────────────────────────────────────────────

    [Fact]
    public void Bomb_OnDeath_DamagesNeighboursInRadius()
    {
        // Row of: bomb at (1,0), plain blocks around it. Killing the bomb should hurt neighbours.
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true,\"bomb\":true,\"explodeRadius\":1}," +
            "{\"id\":\"p\",\"biome\":\"t\",\"hp\":5,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"pbp\",\"...\",\"...\"],\"legend\":{\"b\":\"b\",\"p\":\"p\"}}");

        var left  = g.Blocks[0]; // p
        var bomb  = g.Blocks[1]; // b
        var right = g.Blocks[2]; // p
        int leftBefore = left.Hp, rightBefore = right.Hp;

        BallHit(g, bomb); // ball kills the hp-1 bomb → it explodes
        Assert.True(bomb.Dead);
        Assert.True(left.Hp  < leftBefore,  "left neighbour took explosion damage");
        Assert.True(right.Hp < rightBefore, "right neighbour took explosion damage");
    }

    [Fact]
    public void Bomb_Chains_IntoAdjacentBomb()
    {
        var g = Make(
            "{\"types\":[" +
            "{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true,\"bomb\":true,\"explodeRadius\":1}," +
            "{\"id\":\"p\",\"biome\":\"t\",\"hp\":2,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\"bbp\",\"...\",\"...\"],\"legend\":{\"b\":\"b\",\"p\":\"p\"}}");
        var bomb1 = g.Blocks[0];
        var plain = g.Blocks[2]; // hp 2, two cells from bomb1 → only reached if bomb2 chains
        BallHit(g, bomb1);
        Assert.True(g.Blocks[1].Dead, "adjacent bomb chained");
        Assert.True(plain.Hp < 2, "second bomb's explosion reached the far block");
    }
}
