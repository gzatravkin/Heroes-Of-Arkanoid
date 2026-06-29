using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

/// <summary>Per-profile season + event progress (plan §C).</summary>
public sealed class SeasonState
{
    [JsonPropertyName("seasonId")]     public int SeasonId     { get; set; } = -1;
    /// <summary>Cumulative Season Tokens this season — drives both the track and the season leaderboard.</summary>
    [JsonPropertyName("tokens")]       public int Tokens       { get; set; } = 0;
    [JsonPropertyName("claimedTiers")] public List<int> ClaimedTiers { get; set; } = new();
    // Event (rotates weekly)
    [JsonPropertyName("eventWeek")]    public int  EventWeek    { get; set; } = -1;
    [JsonPropertyName("eventTokens")]  public int  EventTokens  { get; set; } = 0;
    [JsonPropertyName("eventClaimed")] public bool EventClaimed { get; set; } = false;
}

public sealed class SeasonClaimResult
{
    public bool Ok { get; set; }
    public int Gems { get; set; }
    public int CardDust { get; set; }
    public int ModuleCores { get; set; }
}

/// <summary>
/// Season reward track (plan §C): cumulative Season Tokens unlock tier rewards (battle-pass shape, free
/// lane). Tokens also feed the season leaderboard. Resets when the season rolls over. Pure.
/// </summary>
public static class SeasonService
{
    public const string BoardId = "season";

    public static void EnsureSeason(Profile p, int seasonId)
    {
        if (p.Season.SeasonId == seasonId) return;
        p.Season.SeasonId = seasonId;
        p.Season.Tokens = 0;
        p.Season.ClaimedTiers = new List<int>();
    }

    public static void AddTokens(Profile p, int seasonId, int amount)
    {
        if (amount <= 0) return;
        EnsureSeason(p, seasonId);
        p.Season.Tokens += amount;
    }

    /// <summary>Claim a track tier once its cumulative-token threshold is reached (no token deduction —
    /// battle-pass style). Idempotent per tier.</summary>
    public static SeasonClaimResult ClaimTier(Profile p, SeasonCatalog catalog, int seasonId, int tier)
    {
        EnsureSeason(p, seasonId);
        var def = catalog.Track.FirstOrDefault(t => t.Tier == tier);
        if (def == null || p.Season.ClaimedTiers.Contains(tier) || p.Season.Tokens < def.Tokens)
            return new SeasonClaimResult { Ok = false };
        p.Season.ClaimedTiers.Add(tier);
        Wallet.Add(p, Currency.Souls,    def.RewardGems);
        Wallet.Add(p, Currency.Sparks,    def.RewardCardDust);
        Wallet.Add(p, Currency.Sparks, def.RewardModuleCores);
        return new SeasonClaimResult { Ok = true, Gems = def.RewardGems, CardDust = def.RewardCardDust, ModuleCores = def.RewardModuleCores };
    }

    /// <summary>The season leaderboard score = cumulative tokens earned this season.</summary>
    public static int SeasonScore(Profile p) => p.Season.Tokens;
}
