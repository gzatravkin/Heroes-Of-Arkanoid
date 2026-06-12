using System.Text.Json;
using System.Text.Json.Serialization;
using Arkanoid.Core.Meta;
namespace Arkanoid.Core.Bonuses;

public sealed class BonusDef
{
    [JsonPropertyName("id")]       public string Id       { get; set; } = "";
    [JsonPropertyName("name")]     public string Name     { get; set; } = "";
    [JsonPropertyName("icon")]     public string Icon     { get; set; } = "";
    [JsonPropertyName("effect")]   public string Effect   { get; set; } = "";
    [JsonPropertyName("count")]    public int    Count    { get; set; } = 1;
    [JsonPropertyName("duration")] public double Duration { get; set; }
    [JsonPropertyName("full")]     public bool   Full     { get; set; }
}

public sealed class BonusCatalog : Catalog<BonusDef>
{
    private readonly BonusDef[] _arr;
    // Accept BonusDef[] to capture the original insertion-ordered array before the base
    // constructor loads it into a ConcurrentDictionary (which doesn't preserve order).
    private BonusCatalog(BonusDef[] defs) : base(defs, d => d.Id) => _arr = defs;

    /// <summary>Stable index-based pick for random bonus selection.</summary>
    public BonusDef Pick(int index) => _arr[index % _arr.Length];
    public int Count => _arr.Length;

    private sealed class Dto { [JsonPropertyName("bonuses")] public List<BonusDef> Bonuses { get; set; } = new(); }

    public static BonusCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json)
            ?? throw new InvalidOperationException("bad bonuses json");
        return new BonusCatalog(dto.Bonuses.ToArray());
    }

    public static BonusCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
