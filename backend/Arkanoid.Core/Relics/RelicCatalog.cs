using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Relics;

public sealed class RelicDef
{
    [JsonPropertyName("id")]          public string Id          { get; set; } = "";
    [JsonPropertyName("name")]        public string Name        { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("icon")]        public string Icon        { get; set; } = "";
}

public sealed class RelicCatalog
{
    private readonly Dictionary<string, RelicDef> _byId;
    private RelicCatalog(IEnumerable<RelicDef> defs)
        => _byId = defs.ToDictionary(d => d.Id);

    public RelicDef Get(string id) => _byId[id];

    public bool TryGet(string id, out RelicDef def)
    {
        if (_byId.TryGetValue(id, out var found)) { def = found; return true; }
        def = null!;
        return false;
    }

    private sealed class Dto { [JsonPropertyName("relics")] public List<RelicDef> Relics { get; set; } = new(); }

    public static RelicCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json)
            ?? throw new InvalidOperationException("bad relics json");
        return new RelicCatalog(dto.Relics);
    }

    public static RelicCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
