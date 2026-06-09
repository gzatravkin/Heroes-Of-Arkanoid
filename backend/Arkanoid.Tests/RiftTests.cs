using Arkanoid.Core.Meta;
using Xunit;

/// <summary>
/// Unit tests for the Rift roll — the opt-in dungeon entry that can open after a
/// campaign node clear. Pure-Core, deterministic given the seed.
/// </summary>
public class RiftTests
{
    private static DungeonCatalog Catalog() => DungeonCatalog.FromJson("""
    { "dungeons": [
      { "id": "ember-depths", "name": "Ember Depths", "floors": ["hell-1","hell-teleport","caverns-1"], "rewardRelic": "pyroclasm", "rewardCrystals": 50 },
      { "id": "ghost-spire",  "name": "Ghost Spire",  "floors": ["village-1","village-ghost","heaven-1"], "rewardRelic": "mana_battery", "rewardCrystals": 50 }
    ]}
    """);

    [Fact]
    public void Force_AlwaysOpens_WithFloorCount()
    {
        var rift = RiftService.Roll("force", riftChance: 0.0, "hell-1", seed: 1, Catalog());
        Assert.NotNull(rift);
        Assert.True(rift!.Opened);
        Assert.Equal("ember-depths", rift.DungeonId);
        Assert.Equal(3, rift.Floors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("none")]
    public void NoneOrAbsent_NeverOpens(string? mode)
    {
        var rift = RiftService.Roll(mode, riftChance: 1.0, "hell-1", seed: 1, Catalog());
        Assert.Null(rift);
    }

    [Fact]
    public void Roll_Chance1_AlwaysOpens()
    {
        var rift = RiftService.Roll("roll", riftChance: 1.0, "hell-1", seed: 12345, Catalog());
        Assert.NotNull(rift);
    }

    [Fact]
    public void Roll_Chance0_NeverOpens()
    {
        var rift = RiftService.Roll("roll", riftChance: 0.0, "hell-1", seed: 12345, Catalog());
        Assert.Null(rift);
    }

    [Fact]
    public void Roll_Deterministic_SameSeed()
    {
        var a = RiftService.Roll("roll", 0.5, "hell-1", seed: 999, Catalog());
        var b = RiftService.Roll("roll", 0.5, "hell-1", seed: 999, Catalog());
        Assert.Equal(a?.DungeonId, b?.DungeonId);
    }

    [Theory]
    [InlineData("hell-1",        "ember-depths")]
    [InlineData("caverns-2",     "ember-depths")]
    [InlineData("village-ghost", "ghost-spire")]
    [InlineData("heaven-1",      "ghost-spire")]
    public void PicksBiomeAppropriateDungeon(string clearedLevel, string expectedDungeon)
    {
        var rift = RiftService.Roll("force", 0.0, clearedLevel, seed: 1, Catalog());
        Assert.NotNull(rift);
        Assert.Equal(expectedDungeon, rift!.DungeonId);
    }
}
