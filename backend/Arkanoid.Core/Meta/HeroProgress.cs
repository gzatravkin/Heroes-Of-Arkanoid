using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

/// <summary>Per-hero progression (design §5.3/§5.4): play-driven Level (1→30) with XP toward the next,
/// and collection-driven ★ Stars (0→6). Persisted per hero id in <see cref="Profile.HeroProgress"/>.</summary>
public sealed class HeroProgress
{
    [JsonPropertyName("level")] public int Level { get; set; } = 1;
    [JsonPropertyName("exp")]   public int Exp   { get; set; } = 0;
    [JsonPropertyName("stars")] public int Stars { get; set; } = 0;
    /// <summary>Duplicate-hero pips collected from hero rolls; consumed to raise ★ per the §5.4 cost
    /// curve (economy rework — replaces the spent-Hero-Token ascension).</summary>
    [JsonPropertyName("ascendPips")] public int AscendPips { get; set; } = 0;
}
