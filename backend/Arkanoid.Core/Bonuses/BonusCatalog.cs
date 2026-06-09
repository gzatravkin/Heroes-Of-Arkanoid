using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Bonuses;

public sealed class BonusDef
{
    [JsonPropertyName("id")]     public string Id     { get; set; } = "";
    [JsonPropertyName("name")]   public string Name   { get; set; } = "";
    [JsonPropertyName("icon")]   public string Icon   { get; set; } = "";
    [JsonPropertyName("effect")] public string Effect { get; set; } = "";
}

public sealed class BonusCatalog
{
    private readonly BonusDef[] _defs;
    private BonusCatalog(IEnumerable<BonusDef> defs) => _defs = defs.ToArray();

    public IReadOnlyList<BonusDef> Defs => _defs;

    public BonusDef Pick(int index) => _defs[index % _defs.Length];

    private sealed class Dto { [JsonPropertyName("bonuses")] public List<BonusDef> Bonuses { get; set; } = new(); }

    public static BonusCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json)
            ?? throw new InvalidOperationException("bad bonuses json");
        return new BonusCatalog(dto.Bonuses);
    }

    public static BonusCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
