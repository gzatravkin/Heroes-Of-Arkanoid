using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Dungeon minibosses (docs/04 §6.2): the mid-floor of a 3+-floor run is a difficulty spike
/// with an elite enemy.</summary>
public class MinibossTests
{
    private static GameInstance Make()
    {
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"hell\",\"hp\":2,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":5,\"rows\":6," +
            "\"rows_data\":[\".....\",\".AAA.\",\".....\",\".....\",\".....\",\".....\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        return new GameInstance(level, SimConfig.Default, 1);
    }

    [Fact]
    public void IsMinibossFloor_IsOnlyTheMidFloorOfThreePlus()
    {
        var run = new DungeonRun(); run.Floors.AddRange(new[] { "f0", "f1", "f2" });
        Assert.False(DungeonService.IsMinibossFloor(run, 0));
        Assert.True(DungeonService.IsMinibossFloor(run, 1));
        Assert.False(DungeonService.IsMinibossFloor(run, 2));

        var two = new DungeonRun(); two.Floors.AddRange(new[] { "f0", "f1" });
        Assert.False(DungeonService.IsMinibossFloor(two, 0));
        Assert.False(DungeonService.IsMinibossFloor(two, 1));
    }

    [Fact]
    public void ApplyMiniboss_HardensBlocks_AndAddsAnEliteEnemy()
    {
        var g = Make();
        int countBefore = g.Blocks.Count;
        int hpBefore    = g.Blocks.First(b => b.TypeId == "b").MaxHp;

        DungeonService.ApplyMiniboss(g);

        Assert.True(g.Blocks.Count > countBefore, "an elite enemy block should be added");
        Assert.Contains(g.Blocks, b => b.TypeId == "miniboss" && b.Emitter);
        Assert.True(g.Blocks.First(b => b.TypeId == "b").MaxHp > hpBefore, "blocks should be hardened");
    }
}
