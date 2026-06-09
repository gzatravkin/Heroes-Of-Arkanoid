using System.Text.Json;
using System.Text.Json.Serialization;
using Arkanoid.Core.Entities;
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

    /// <summary>
    /// The block's single special behaviour: "boss" | "teleporter" | "emitter" | "bomb" |
    /// "stalactite" | "necromant" | "windMaster" | "shieldStatue" | "portal" | "bat" | "none".
    /// </summary>
    [JsonPropertyName("behavior")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BlockBehavior Behavior { get; set; } = BlockBehavior.None;

    // --- Parametric fields for the relevant behaviour ---
    /// <summary>Teleporter colour group (0 red / 1 blue / 2 green) — warps pair within a colour.</summary>
    [JsonPropertyName("teleportColor")] public int TeleportColor { get; set; } = 0;
    /// <summary>Emitter: seconds between emitted hazards.</summary>
    [JsonPropertyName("emitInterval")] public double EmitInterval { get; set; } = 2.5;
    /// <summary>Emitter aim: "down" | "paddle" | "ball".</summary>
    [JsonPropertyName("emitAim")]      public string EmitAim      { get; set; } = "down";
    /// <summary>Bomb: explosion radius in cells (chains into other bombs).</summary>
    [JsonPropertyName("explodeRadius")] public int  ExplodeRadius { get; set; } = 1;
    /// <summary>Emitter: hazard kind tag the renderer maps to missile art ("hellball" | "beholdermissile" | "heavenmissile").</summary>
    [JsonPropertyName("missileKind")]  public string MissileKind  { get; set; } = "";

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
