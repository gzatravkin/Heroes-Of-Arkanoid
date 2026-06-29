using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

/// <summary>One tier on the season reward track (plan §C): cumulative tokens to unlock + its payout.</summary>
public sealed class SeasonTier
{
    [JsonPropertyName("tier")]              public int Tier              { get; set; }
    [JsonPropertyName("tokens")]            public int Tokens            { get; set; }
    [JsonPropertyName("rewardGems")]        public int RewardGems        { get; set; }
    [JsonPropertyName("rewardCardDust")]    public int RewardCardDust    { get; set; }
    [JsonPropertyName("rewardModuleCores")] public int RewardModuleCores { get; set; }
}

/// <summary>The season config: rotating themes + a shared reward track (plan §C).</summary>
public sealed class SeasonCatalog
{
    [JsonPropertyName("themes")]          public List<string> Themes        { get; set; } = new();
    [JsonPropertyName("tokensPerBattle")] public int           TokensPerBattle { get; set; } = 10;
    [JsonPropertyName("track")]           public List<SeasonTier> Track      { get; set; } = new();

    public string ThemeFor(int seasonId) => Themes.Count == 0 ? "Season" : Themes[((seasonId % Themes.Count) + Themes.Count) % Themes.Count];

    public static SeasonCatalog FromJson(string json) =>
        JsonSerializer.Deserialize<SeasonCatalog>(json) ?? throw new InvalidOperationException("Invalid seasons JSON");
    public static SeasonCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
