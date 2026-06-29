using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Sim;
using Arkanoid.Core.Sim.Systems;
using Xunit;

/// <summary>
/// Caverns "union-of-sticks" (docs/04 §8): adjacent bridge blocks flood-fill into one group at load
/// and collapse together when any single block is destroyed.
/// </summary>
public class UnionOfSticksTests
{
    private static GameInstance MakeBridge()
    {
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"u\",\"biome\":\"cavern\",\"hp\":3,\"sprite\":\"s\",\"needToKill\":true,\"union\":true}," +
            "{\"id\":\"n\",\"biome\":\"cavern\",\"hp\":3,\"sprite\":\"s\",\"needToKill\":true}]}");
        // Row 0: a 4-stick bridge (UUUU). Row 1: a lone normal block (n) that must NOT collapse.
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"cavern\",\"cols\":5,\"rows\":4," +
            "\"rows_data\":[\"UUUU.\",\"n....\",\".....\",\".....\"],\"legend\":{\"U\":\"u\",\"n\":\"n\"}}",
            catalog);
        return new GameInstance(level, SimConfig.Default, 1);
    }

    [Fact]
    public void AdjacentUnionBlocks_ShareOneGroup()
    {
        var g = MakeBridge();
        var union = g.Blocks.Where(b => b.IsUnion).ToList();
        Assert.Equal(4, union.Count);
        Assert.True(union.All(b => b.UnionGroup > 0 && b.UnionGroup == union[0].UnionGroup),
            "all four connected sticks should share one union group");
        // The lone normal block has no group.
        Assert.All(g.Blocks.Where(b => !b.IsUnion), b => Assert.Equal(0, b.UnionGroup));
    }

    [Fact]
    public void DestroyingOneStick_CollapsesTheWholeBridge_ButNotOtherBlocks()
    {
        var g = MakeBridge();
        var normal = g.Blocks.First(b => !b.IsUnion);
        // Lethal hit on a single stick.
        BlockDamage.DamageBlock(g, g.Blocks.First(b => b.IsUnion), 99, igniteSource: false);

        Assert.True(g.Blocks.Where(b => b.IsUnion).All(b => b.Dead),
            "the whole bridge should collapse when one stick breaks");
        Assert.False(normal.Dead, "an unconnected normal block must NOT collapse");
    }
}
