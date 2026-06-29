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

    // ── G3a: generated rifts (curated-shuffle over the biome's matrix levels) ──

    private static CampaignCatalog Campaign() => CampaignCatalog.FromJson("""
      { "nodes": [
        { "id": "hell-1",    "label": "H1", "biome": "hell", "x": 0, "y": 0, "requires": [] },
        { "id": "hell-2",    "label": "H2", "biome": "hell", "x": 1, "y": 0, "requires": ["hell-1"] },
        { "id": "hell-4",    "label": "H4", "biome": "hell", "x": 2, "y": 0, "requires": ["hell-2"] },
        { "id": "hell-5",    "label": "H5", "biome": "hell", "x": 3, "y": 0, "requires": ["hell-4"] },
        { "id": "hell-boss", "label": "HB", "biome": "hell", "x": 4, "y": 0, "requires": ["hell-5"] }
      ]}
    """);

    [Fact]
    public void GeneratedRift_IsA10LevelBiomeGauntlet_BossLast_NoRelic_DeterministicBySeed()
    {
        var catalog = Catalog();
        var rift = RiftService.GenerateRift("hell-2", seed: 7, catalog, Campaign(), tier: 0, riftLevels: 10);
        Assert.NotNull(rift);
        Assert.Equal("rift-hell", rift!.Id);

        // §7: a 10-level escalating biome gauntlet, all hell, boss the FINAL (jackpot) level.
        Assert.Equal(10, rift.Floors.Count);
        Assert.All(rift.Floors, f => Assert.StartsWith("hell", f));
        Assert.Equal("hell-boss", rift.Floors[^1]);
        Assert.True(rift.IsRift);
        Assert.Equal("", rift.RewardRelic);   // §7: no permanent relic draft
        Assert.Equal(0, rift.RewardCrystals);  // §7: reward is depth-scaled, not a fixed grant

        // Deterministic by seed.
        var again = RiftService.GenerateRift("hell-2", seed: 7, Catalog(), Campaign(), tier: 0, riftLevels: 10);
        Assert.Equal(rift.Floors, again!.Floors);
    }

    [Fact]
    public void Roll_WithCampaign_OffersThe10LevelRift()
    {
        var rift = RiftService.Roll("force", 0.0, "hell-2", seed: 3, Catalog(), Campaign());
        Assert.NotNull(rift);
        Assert.Equal("rift-hell", rift!.DungeonId);
        Assert.Equal(10, rift.Floors);
    }

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
