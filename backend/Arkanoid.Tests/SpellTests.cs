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
}
