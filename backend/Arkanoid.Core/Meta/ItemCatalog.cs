using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

/// <summary>
/// Descriptor for one item in the persistent equip catalog.
/// Each item has up to maxTier tiers, each with its own crystal cost.
/// </summary>
public sealed class ItemDef
{
    [JsonPropertyName("id")]          public string      Id          { get; set; } = "";
    [JsonPropertyName("name")]        public string      Name        { get; set; } = "";
    [JsonPropertyName("icon")]        public string      Icon        { get; set; } = "";
    [JsonPropertyName("maxTier")]     public int         MaxTier     { get; set; } = 3;
    /// <summary>Crystal cost to buy/upgrade to each tier (index 0 = tier 1, index 1 = tier 2, …).</summary>
    [JsonPropertyName("cost")]        public List<int>   Cost        { get; set; } = new();
    [JsonPropertyName("effect")]      public string      Effect      { get; set; } = "";
    [JsonPropertyName("description")] public string      Description { get; set; } = "";

    /// <summary>Cost to purchase/upgrade to <paramref name="tier"/> (1-based).</summary>
    public int CostForTier(int tier)
    {
        var idx = tier - 1;
        return (idx >= 0 && idx < Cost.Count) ? Cost[idx] : int.MaxValue;
    }
}

/// <summary>Loaded-once registry of all items defined in items.json.</summary>
public sealed class ItemCatalog
{
    private readonly Dictionary<string, ItemDef> _byId;
    private ItemCatalog(IEnumerable<ItemDef> defs) => _byId = defs.ToDictionary(d => d.Id);

    public IEnumerable<ItemDef> All => _byId.Values;

    public ItemDef Get(string id) => _byId.TryGetValue(id, out var d) ? d
        : throw new KeyNotFoundException($"Item '{id}' not found.");

    public bool TryGet(string id, out ItemDef def)
    {
        if (_byId.TryGetValue(id, out var found)) { def = found; return true; }
        def = null!;
        return false;
    }

    private sealed class Dto { [JsonPropertyName("items")] public List<ItemDef> Items { get; set; } = new(); }

    public static ItemCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json)
            ?? throw new InvalidOperationException("Invalid items JSON");
        return new ItemCatalog(dto.Items);
    }

    public static ItemCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
