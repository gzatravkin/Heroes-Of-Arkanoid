using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

/// <summary>A Module definition (§2): slotted gear — ONE strong slot-bound passive each (no sub-stats,
/// economy rework). Slots map onto our build axes (core/paddle/ball/field). Owned/leveled like cards.</summary>
public sealed class ModuleDef
{
    [JsonPropertyName("id")]          public string   Id          { get; set; } = "";
    [JsonPropertyName("name")]        public string   Name        { get; set; } = "";
    /// <summary>core | paddle | ball | field.</summary>
    [JsonPropertyName("slot")]        public string   Slot        { get; set; } = "";
    [JsonPropertyName("rarity")]      public string   Rarity      { get; set; } = "rare";
    [JsonPropertyName("effect")]      public string   Effect      { get; set; } = "";
    [JsonPropertyName("magnitude")]   public double   Magnitude   { get; set; } = 0;
    [JsonPropertyName("effectValue")] public string   EffectValue { get; set; } = "";
    /// <summary>Human-readable effect, shown in the Modules collection so the player knows what it does.</summary>
    [JsonPropertyName("description")] public string   Description { get; set; } = "";
}

public sealed class ModuleCatalog : Catalog<ModuleDef>
{
    private ModuleCatalog(IEnumerable<ModuleDef> defs) : base(defs, d => d.Id) { }
    public IEnumerable<ModuleDef> Modules => All;

    private sealed class Dto { [JsonPropertyName("modules")] public List<ModuleDef> Modules { get; set; } = new(); }

    public static ModuleCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidOperationException("Invalid modules JSON");
        return new ModuleCatalog(dto.Modules);
    }
    public static ModuleCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
