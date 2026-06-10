using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

public sealed class DungeonDef
{
    [JsonPropertyName("id")]             public string Id             { get; set; } = "";
    [JsonPropertyName("name")]           public string Name           { get; set; } = "";
    [JsonPropertyName("floors")]         public List<string> Floors   { get; set; } = new();
    [JsonPropertyName("rewardRelic")]    public string RewardRelic    { get; set; } = "";
    [JsonPropertyName("rewardCrystals")] public int    RewardCrystals { get; set; }
}

public sealed class DungeonCatalog
{
    private readonly Dictionary<string, DungeonDef> _byId;

    private DungeonCatalog(IEnumerable<DungeonDef> defs)
        => _byId = defs.ToDictionary(d => d.Id);

    /// <summary>All dungeon definitions.</summary>
    public IEnumerable<DungeonDef> All => _byId.Values;

    /// <summary>Returns the dungeon with the given id, or throws if not found.</summary>
    public DungeonDef Get(string id) => _byId.TryGetValue(id, out var d) ? d
        : throw new KeyNotFoundException($"Dungeon '{id}' not found.");

    /// <summary>Upsert a generated rift (one slot per id — regenerated offers replace the old one).</summary>
    public void Register(DungeonDef def) => _byId[def.Id] = def;

    private sealed class Dto
    {
        [JsonPropertyName("dungeons")] public List<DungeonDef> Dungeons { get; set; } = new();
    }

    public static DungeonCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json)
            ?? throw new InvalidOperationException("Invalid dungeons JSON");
        return new DungeonCatalog(dto.Dungeons);
    }

    public static DungeonCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
