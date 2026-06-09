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
    /// <summary>Teleporter colour group (0 red / 1 blue / 2 green) — warps pair within a colour.</summary>
    [JsonPropertyName("teleportColor")] public int TeleportColor { get; set; } = 0;
    /// <summary>Boss block: periodically fires falling hazards that damage player HP on paddle contact.</summary>
    [JsonPropertyName("boss")] public bool Boss { get; set; } = false;

    /// <summary>Enemy emitter — periodically fires a hazard (Hell spawner / Beholder / Melee statue).</summary>
    [JsonPropertyName("emitter")]      public bool   Emitter      { get; set; } = false;
    [JsonPropertyName("emitInterval")] public double EmitInterval { get; set; } = 2.5;
    /// <summary>"down" | "paddle" | "ball".</summary>
    [JsonPropertyName("emitAim")]      public string EmitAim      { get; set; } = "down";

    /// <summary>Explodes on death, damaging blocks within explodeRadius cells (chains into other bombs).</summary>
    [JsonPropertyName("bomb")]          public bool Bomb          { get; set; } = false;
    [JsonPropertyName("explodeRadius")] public int  ExplodeRadius { get; set; } = 1;

    /// <summary>Mirror the sprite so asymmetric/corner art can sit at any corner/side.</summary>
    [JsonPropertyName("flipX")] public bool FlipX { get; set; } = false;
    [JsonPropertyName("flipY")] public bool FlipY { get; set; } = false;
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
