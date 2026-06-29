using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

/// <summary>A rotating limited-time event (plan §C, We-Are-Warriors-style): a themed run modifier that
/// "changes the board", its own token + a milestone reward, and an event leaderboard board.</summary>
public sealed class EventDef
{
    [JsonPropertyName("id")]              public string Id              { get; set; } = "";
    [JsonPropertyName("name")]            public string Name            { get; set; } = "";
    /// <summary>A <see cref="RunModifier"/> effect applied to every battle while the event is live.</summary>
    [JsonPropertyName("effect")]          public string Effect          { get; set; } = "";
    [JsonPropertyName("magnitude")]       public double Magnitude       { get; set; }
    [JsonPropertyName("tokenPerBattle")]  public int    TokenPerBattle  { get; set; } = 10;
    [JsonPropertyName("milestoneTokens")] public int    MilestoneTokens { get; set; } = 60;
    [JsonPropertyName("rewardModuleCores")]public int   RewardModuleCores { get; set; }
    [JsonPropertyName("rewardGems")]      public int    RewardGems      { get; set; }
}

public sealed class EventCatalog
{
    [JsonPropertyName("events")] public List<EventDef> Events { get; set; } = new();

    /// <summary>The live event rotates weekly.</summary>
    public EventDef? Current(int weekId) =>
        Events.Count == 0 ? null : Events[((weekId % Events.Count) + Events.Count) % Events.Count];

    public static EventCatalog FromJson(string json) =>
        JsonSerializer.Deserialize<EventCatalog>(json) ?? throw new InvalidOperationException("Invalid events JSON");
    public static EventCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
