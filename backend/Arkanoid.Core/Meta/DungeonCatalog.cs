using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

public sealed class DungeonDef
{
    [JsonPropertyName("id")]             public string      Id             { get; set; } = "";
    [JsonPropertyName("name")]           public string      Name           { get; set; } = "";
    [JsonPropertyName("floors")]         public List<string> Floors        { get; set; } = new();
    [JsonPropertyName("rewardRelic")]    public string      RewardRelic    { get; set; } = "";
    [JsonPropertyName("rewardCrystals")] public int         RewardCrystals { get; set; }
    [JsonPropertyName("tier")]           public int         Tier           { get; set; }
    /// <summary>§7: a Rift gauntlet (10 biome levels, one HP/ball pool, §8 modifier picks, depth rewards,
    /// no permanent relic draft) rather than a legacy dungeon.</summary>
    [JsonPropertyName("isRift")]         public bool        IsRift         { get; set; }
}

public sealed class DungeonCatalog : Catalog<DungeonDef>
{
    private DungeonCatalog(IEnumerable<DungeonDef> defs) : base(defs, d => d.Id) { }

    private sealed class Dto { [JsonPropertyName("dungeons")] public List<DungeonDef> Dungeons { get; set; } = new(); }

    public static DungeonCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidOperationException("Invalid dungeons JSON");
        return new DungeonCatalog(dto.Dungeons);
    }

    public static DungeonCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
