using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

public sealed class CharacterDef
{
    [JsonPropertyName("id")]      public string Id      { get; set; } = "";
    [JsonPropertyName("name")]    public string Name    { get; set; } = "";
    [JsonPropertyName("passive")] public string Passive { get; set; } = "";
    [JsonPropertyName("icon")]    public string Icon    { get; set; } = "";
}

public sealed class CharacterCatalog
{
    private readonly List<CharacterDef> _characters;
    private CharacterCatalog(IEnumerable<CharacterDef> characters) => _characters = new List<CharacterDef>(characters);

    public IEnumerable<CharacterDef> All => _characters;

    public CharacterDef Get(string id)
    {
        var def = _characters.FirstOrDefault(c => c.Id == id);
        if (def is null) throw new KeyNotFoundException($"Character '{id}' not found");
        return def;
    }

    private sealed class Dto
    {
        [JsonPropertyName("characters")] public List<CharacterDef> Characters { get; set; } = new();
    }

    public static CharacterCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json)
            ?? throw new InvalidOperationException("Invalid characters JSON");
        return new CharacterCatalog(dto.Characters);
    }

    public static CharacterCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
