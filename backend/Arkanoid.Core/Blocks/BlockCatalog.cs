using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Blocks;

public sealed class BlockType
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("biome")] public string Biome { get; set; } = "";
    [JsonPropertyName("hp")] public int Hp { get; set; } = 1;
    [JsonPropertyName("sprite")] public string Sprite { get; set; } = "";
    [JsonPropertyName("needToKill")] public bool NeedToKill { get; set; } = true;
    /// <summary>Ball and all damage sources are ignored; ball still bounces.</summary>
    [JsonPropertyName("indestructible")] public bool Indestructible { get; set; } = false;
    /// <summary>Ball passes through with no collision/damage, but projectiles and firewalls still hit it.</summary>
    [JsonPropertyName("ballPhases")] public bool BallPhases { get; set; } = false;
    /// <summary>On ball overlap, warps the ball to the next teleporter cyclically (Hell signature mechanic).</summary>
    [JsonPropertyName("teleporter")] public bool Teleporter { get; set; } = false;
}

public sealed class BlockCatalog
{
    private readonly Dictionary<string, BlockType> _byId;
    private BlockCatalog(IEnumerable<BlockType> types)
        => _byId = types.ToDictionary(t => t.Id);

    public BlockType Get(string id) => _byId[id];

    private sealed class Dto { [JsonPropertyName("types")] public List<BlockType> Types { get; set; } = new(); }

    public static BlockCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json)
            ?? throw new InvalidOperationException("bad blocks json");
        return new BlockCatalog(dto.Types);
    }

    public static BlockCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
