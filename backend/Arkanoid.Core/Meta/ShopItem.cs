using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

/// <summary>
/// One item for sale on a dungeon shop floor (docs/04 §6.2 shop pick, §5 "spent at shops on spells,
/// relics, heals"). <see cref="Kind"/> discriminates how <see cref="DungeonService.TryBuy"/> applies it;
/// <see cref="Id"/> is the raw spell/relic/core/mod id (no SpellPrefix — Kind already distinguishes).
/// </summary>
public sealed class ShopItem
{
    [JsonPropertyName("id")]    public string Id    { get; set; } = "";
    /// <summary>"relic" | "core" | "paddleMod" | "spell" | "heal".</summary>
    [JsonPropertyName("kind")]  public string Kind  { get; set; } = "";
    [JsonPropertyName("price")] public int    Price { get; set; }
}
