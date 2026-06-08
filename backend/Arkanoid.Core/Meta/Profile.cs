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
            }
        };
    }
}
