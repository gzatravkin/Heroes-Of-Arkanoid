using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

public sealed class Profile
{
    [JsonPropertyName("level")]    public int Level    { get; set; } = 1;
    [JsonPropertyName("exp")]      public int Exp      { get; set; } = 0;
    [JsonPropertyName("points")]   public int Points   { get; set; } = 0;
    [JsonPropertyName("crystals")] public int Crystals { get; set; } = 0;

    [JsonPropertyName("completedLevels")]
    public List<string> CompletedLevels { get; set; } = new();

    [JsonPropertyName("unlockedRelics")]
    public List<string> UnlockedRelics { get; set; } = new();

    [JsonPropertyName("spellLevels")]
    public Dictionary<string, int> SpellLevels { get; set; } = new();

    [JsonPropertyName("selectedCharacter")]
    public string SelectedCharacter { get; set; } = "fire_mage";

    [JsonPropertyName("unlockedCharacters")]
    public List<string> UnlockedCharacters { get; set; } = new();

    /// <summary>Persistent item ownership. Key = item id, value = owned tier (1–maxTier). Missing key ≡ not owned.</summary>
    [JsonPropertyName("ownedItems")]
    public Dictionary<string, int> OwnedItems { get; set; } = new();

    /// <summary>Up to 3 equipped item ids. Must be a subset of OwnedItems keys.</summary>
    [JsonPropertyName("equippedItems")]
    public List<string> EquippedItems { get; set; } = new();

    public static Profile NewDefault()
    {
        return new Profile
        {
            SpellLevels = new Dictionary<string, int>
            {
                ["ignite"]   = 1,
                ["fireball"] = 1,
                ["firewall"] = 1,
                ["turret"]   = 1,
            },
            SelectedCharacter  = "fire_mage",
            UnlockedCharacters = new List<string> { "fire_mage", "paladin", "engineer", "necromancer" },
            OwnedItems         = new Dictionary<string, int>(),
            EquippedItems      = new List<string>(),
        };
    }
}
