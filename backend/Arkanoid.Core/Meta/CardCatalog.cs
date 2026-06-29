using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

/// <summary>A Card: a persistent passive modifier (Tower-style), equipped into a limited slot count and
/// leveled with Card Dust (social/economy plan §A.1). Cards-as-passive-modifiers IS the system's design.</summary>
public sealed class CardDef
{
    [JsonPropertyName("id")]          public string Id          { get; set; } = "";
    [JsonPropertyName("name")]        public string Name        { get; set; } = "";
    [JsonPropertyName("rarity")]      public string Rarity      { get; set; } = "common";
    [JsonPropertyName("icon")]        public string Icon        { get; set; } = "";
    /// <summary>ball_damage | max_mana | start_mana | kill_mana | crit_tough | crystal_bonus | start_life
    /// | paddle_mod | ball_core.</summary>
    [JsonPropertyName("effect")]      public string Effect      { get; set; } = "";
    /// <summary>Per-level magnitude (units depend on effect). 0 for binary grant cards (mod/core).</summary>
    [JsonPropertyName("magnitude")]   public double Magnitude   { get; set; } = 0;
    /// <summary>For paddle_mod/ball_core: the id of the mod/core to grant.</summary>
    [JsonPropertyName("effectValue")] public string EffectValue { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";

    /// <summary>True for cards that grant a fixed mod/core and don't scale with level.</summary>
    public bool IsBinaryGrant => Effect is "paddle_mod" or "ball_core";
}

/// <summary>A player's ownership of one card: its level + spare duplicate copies.</summary>
public sealed class CardOwn
{
    [JsonPropertyName("level")]  public int Level  { get; set; } = 1;
    [JsonPropertyName("copies")] public int Copies { get; set; } = 0;
}

public sealed class CardCatalog : Catalog<CardDef>
{
    private CardCatalog(IEnumerable<CardDef> defs) : base(defs, d => d.Id) { }
    public IEnumerable<CardDef> Cards => All;

    private sealed class Dto { [JsonPropertyName("cards")] public List<CardDef> Cards { get; set; } = new(); }

    public static CardCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidOperationException("Invalid cards JSON");
        return new CardCatalog(dto.Cards);
    }
    public static CardCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
