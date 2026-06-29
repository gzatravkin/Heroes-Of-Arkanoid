using System.Text.Json;
using System.Text.Json.Serialization;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Meta;
namespace Arkanoid.Core.Blocks;

public sealed class BlockType
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("biome")] public string Biome { get; set; } = "";
    [JsonPropertyName("hp")] public int Hp { get; set; } = 1;
    [JsonPropertyName("sprite")] public string Sprite { get; set; } = "";
    [JsonPropertyName("needToKill")] public bool NeedToKill { get; set; } = true;
    [JsonPropertyName("indestructible")] public bool Indestructible { get; set; } = false;
    [JsonPropertyName("ballPhases")] public bool BallPhases { get; set; } = false;
    /// <summary>Caverns union-of-sticks: adjacent union blocks collapse together when one dies.</summary>
    [JsonPropertyName("union")] public bool Union { get; set; } = false;
    [JsonPropertyName("behavior")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BlockBehavior Behavior { get; set; } = BlockBehavior.None;
    [JsonPropertyName("teleportColor")] public int TeleportColor { get; set; } = 0;
    [JsonPropertyName("emitInterval")] public double EmitInterval { get; set; } = 2.5;
    [JsonPropertyName("emitAim")]      public string EmitAim      { get; set; } = "down";
    [JsonPropertyName("explodeRadius")] public int  ExplodeRadius { get; set; } = 1;
    [JsonPropertyName("missileKind")]  public string MissileKind  { get; set; } = "";
    [JsonPropertyName("flipX")] public bool FlipX { get; set; } = false;
    [JsonPropertyName("flipY")] public bool FlipY { get; set; } = false;
    /// <summary>5-HP "elite" tier (2026-06-16): renders with a distinct cold tint so the player reads it as extra-tough.</summary>
    [JsonPropertyName("elite")] public bool Elite { get; set; } = false;
    /// <summary>Effect tag this block guarantees on death (e.g. "powerup_wide"). Null = normal drop rules.</summary>
    [JsonPropertyName("forcedDropEffect")] public string? ForcedDropEffect { get; set; }
}

public sealed class BlockCatalog : Catalog<BlockType>
{
    private BlockCatalog(IEnumerable<BlockType> types) : base(types, t => t.Id) { }

    private sealed class Dto { [JsonPropertyName("types")] public List<BlockType> Types { get; set; } = new(); }

    public static BlockCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidOperationException("bad blocks json");
        return new BlockCatalog(dto.Types);
    }

    public static BlockCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
