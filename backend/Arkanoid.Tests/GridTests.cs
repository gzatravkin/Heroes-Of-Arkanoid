using Arkanoid.Core.Grid;
using Arkanoid.Core.Blocks;
using Xunit;

public class GridTests
{
    private const string BlocksJson = """
    { "types": [
      { "id": "hell_basic", "biome": "hell", "hp": 2, "sprite": "HellStandart", "needToKill": true },
      { "id": "hell_tough", "biome": "hell", "hp": 4, "sprite": "HellStandart2", "needToKill": true }
    ]}
    """;

    private const string LevelJson = """
    { "id": "t1", "biome": "hell", "cols": 4, "rows": 3,
      "rows_data": [ "....", "AB..", "AAAA" ],
      "legend": { "A": "hell_basic", "B": "hell_tough" } }
    """;

    [Fact]
    public void Loader_PlacesBlocksOnIntegerCells()
    {
        var catalog = BlockCatalog.FromJson(BlocksJson);
        var level = LevelLoader.FromJson(LevelJson, catalog);

        Assert.Equal(4, level.Grid.Cols);
        Assert.Equal(3, level.Grid.Rows);
        // 6 blocks total: row1 "AB.." -> 2, row2 "AAAA" -> 4
        Assert.Equal(6, level.Blocks.Count);

        var tough = level.Blocks.Find(b => b.Col == 1 && b.Row == 1);
        Assert.NotNull(tough);
        Assert.Equal(4, tough!.Hp);
        Assert.True(tough.NeedToKill);
    }

    [Fact]
    public void Grid_CellCenter_IsDeterministic()
    {
        var g = new Grid(cols: 4, rows: 3, cellSize: 10, originX: 0, originY: 0);
        var c = g.CellCenter(col: 1, row: 0);
        Assert.Equal(15, c.X, 5); // col1 center = 1*10 + 5
        Assert.Equal(5, c.Y, 5);
    }
}
