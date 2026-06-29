using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

/// <summary>A daily-mission definition (plan §A.2): a metric goal that pays Gems + Card Dust.</summary>
public sealed class MissionDef
{
    [JsonPropertyName("id")]             public string Id             { get; set; } = "";
    [JsonPropertyName("name")]           public string Name           { get; set; } = "";
    /// <summary>blocks_destroyed | levels_won | battles_played.</summary>
    [JsonPropertyName("metric")]         public string Metric         { get; set; } = "";
    [JsonPropertyName("target")]         public int    Target         { get; set; }
    [JsonPropertyName("rewardGems")]     public int    RewardGems     { get; set; }
    [JsonPropertyName("rewardCardDust")] public int    RewardCardDust { get; set; }
}

public sealed class MissionCatalog : Catalog<MissionDef>
{
    private MissionCatalog(IEnumerable<MissionDef> defs) : base(defs, d => d.Id) { }
    public IReadOnlyList<MissionDef> Missions => System.Linq.Enumerable.ToList(All);

    private sealed class Dto { [JsonPropertyName("missions")] public List<MissionDef> Missions { get; set; } = new(); }

    public static MissionCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidOperationException("Invalid missions JSON");
        return new MissionCatalog(dto.Missions);
    }
    public static MissionCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
