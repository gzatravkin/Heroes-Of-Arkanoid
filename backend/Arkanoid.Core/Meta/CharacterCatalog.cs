using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

public sealed class SpellSlotDef
{
    [JsonPropertyName("id")]       public string Id       { get; set; } = "";
    [JsonPropertyName("name")]     public string Name     { get; set; } = "";
    [JsonPropertyName("icon")]     public string Icon     { get; set; } = "";
    [JsonPropertyName("manaCost")] public int    ManaCost { get; set; }
}

public sealed class CharacterDef
{
    [JsonPropertyName("id")]      public string Id      { get; set; } = "";
    [JsonPropertyName("name")]    public string Name    { get; set; } = "";
    [JsonPropertyName("passive")] public string Passive { get; set; } = "";
    [JsonPropertyName("icon")]    public string Icon    { get; set; } = "";
    [JsonPropertyName("spells")]  public List<SpellSlotDef> Spells { get; set; } = new();
}

public sealed class CharacterCatalog : Catalog<CharacterDef>
{
    private CharacterCatalog(IEnumerable<CharacterDef> defs) : base(defs, d => d.Id) { }

    private sealed class Dto { [JsonPropertyName("characters")] public List<CharacterDef> Characters { get; set; } = new(); }

    public static CharacterCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidOperationException("Invalid characters JSON");
        return new CharacterCatalog(dto.Characters);
    }

    public static CharacterCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
