using System;
using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Sim;

namespace Arkanoid.Core.Meta;

/// <summary>League tiers, low → high. Promotion climbs, demotion drops; clamped at the ends.</summary>
public enum LeagueTier { Wood = 0, Bronze = 1, Silver = 2, Gold = 3, Platinum = 4, Diamond = 5, Champion = 6 }

/// <summary>A single row in a league cohort view (the player + generated bot rivals, for local play).</summary>
public sealed class CohortEntry
{
    public int    Rank        { get; set; }
    public string PlayerId    { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int    Score       { get; set; }
    public bool   IsMe        { get; set; }
    public bool   IsBot       { get; set; }
}

/// <summary>Result of resolving a week (promotion/demotion + placement rewards). Idempotent per week.</summary>
public sealed class WeekResolution
{
    public bool Resolved      { get; set; }   // false if already resolved or nothing to do
    public int  OldTier       { get; set; }
    public int  NewTier       { get; set; }
    public int  Rank          { get; set; }
    public int  CohortSize    { get; set; }
    public bool Promoted      { get; set; }
    public bool Demoted       { get; set; }
    public int  RewardMedals  { get; set; }
    public int  RewardGems    { get; set; }   // granted as Crystals (the meta soft currency)
    public int  RewardCardDust{ get; set; }
}

/// <summary>
/// Pure league logic over an <see cref="ILeaderboardStore"/> (social plan §A.3 / §0.3). Handles:
/// score submission with anti-cheat shadow-ban, deterministic bot cohorts (so leagues are meaningful in a
/// fully-local single-player build), and weekly promotion/demotion + placement rewards.
/// </summary>
public static class LeaderboardService
{
    public const int CohortSize   = 30;
    public const int PromoteTop   = 7;
    public const int DemoteBottom = 7;
    public const int StrikeShadowThreshold = 2;

    /// <summary>Plausibility ceiling for a board: a score above this is physically impossible to earn
    /// in one run, so it can only be a fabricated/tampered submission (social plan §3.3).</summary>
    public static int MaxPlausibleScore(string boardId) => boardId switch
    {
        "trial"    => 100_000,   // shared-seed weekly gauntlet ceiling
        "prestige" => 1_000_000, // prestige depth board (tier * 1000 + progress)
        _           => 500_000,
    };

    /// <summary>True when a submitted score is within the board's physical bounds. Implausible ⇒ cheat.</summary>
    public static bool IsPlausible(string boardId, int score) =>
        score >= 0 && score <= MaxPlausibleScore(boardId);

    /// <summary>
    /// Submit a score. Server-authoritative scores are always plausible; a tampered client POSTing an
    /// impossible score earns a strike and (past the threshold) a <b>silent shadow-ban</b> — the call still
    /// "succeeds" so the cheater gets no signal. Returns whether the score entered the public board.
    /// </summary>
    public static bool Submit(ILeaderboardStore store, string playerId, string displayName,
        string boardId, string periodId, int score)
    {
        var state = store.GetPlayerState(playerId);

        if (!IsPlausible(boardId, score))
        {
            store.AddStrike(playerId, "implausible_score", $"board={boardId} score={score}");
            state.Strikes++;
            if (state.Strikes >= StrikeShadowThreshold) state.Shadowed = true;
            store.SetPlayerState(state);
            // Record it as shadowed so it never reaches another player's board, but report success.
            store.UpsertScore(boardId, periodId, new ScoreRecord
            { PlayerId = playerId, DisplayName = displayName, Score = System.Math.Min(score, MaxPlausibleScore(boardId)), Shadowed = true });
            return false;
        }

        store.UpsertScore(boardId, periodId, new ScoreRecord
        { PlayerId = playerId, DisplayName = displayName, Score = score, Shadowed = state.Shadowed });
        return !state.Shadowed;
    }

    /// <summary>
    /// Build the player's league cohort for display: their real score + deterministic bot rivals scaled to
    /// the player's tier (so promotion/demotion has stakes in a local single-player game). Other real
    /// players' shadowed scores are excluded; the requesting player always sees their own row.
    /// </summary>
    public static List<CohortEntry> GenerateCohort(ILeaderboardStore store, string playerId, string displayName,
        string boardId, string periodId)
    {
        var state = store.GetPlayerState(playerId);
        var mine = store.GetScore(boardId, periodId, playerId);
        int myScore = mine?.Score ?? 0;

        var entries = new List<CohortEntry>
        {
            new() { PlayerId = playerId, DisplayName = string.IsNullOrEmpty(displayName) ? "You" : displayName,
                    Score = myScore, IsMe = true, IsBot = false },
        };

        // Local single-player: the cohort is YOU + deterministic bots (no other real profiles leak in).
        // The shadow-ban invariant is enforced at the store level (TopScores excludes shadowed scores),
        // which is what a real multiplayer board would read.
        var rng = new Rng(StableSeed($"{periodId}|{state.Tier}|{playerId}"));
        int mean = 400 + state.Tier * 450;
        for (int i = entries.Count; i < CohortSize; i++)
        {
            int spread = (int)(mean * 0.45);
            int s = System.Math.Max(0, mean - spread + rng.Range(spread * 2 + 1));
            entries.Add(new CohortEntry { PlayerId = $"bot:{state.Tier}:{i}", DisplayName = BotName(rng), Score = s, IsBot = true });
        }

        var ranked = entries.OrderByDescending(e => e.Score).ToList();
        for (int i = 0; i < ranked.Count; i++) ranked[i].Rank = i + 1;
        return ranked;
    }

    /// <summary>Resolve a finished week: rank the player in their cohort, apply promotion/demotion, grant
    /// placement rewards. Idempotent — only resolves a given week once (via LastResolvedWeek).</summary>
    public static WeekResolution ResolveWeek(ILeaderboardStore store, string playerId, string displayName,
        string boardId, int weekId)
    {
        var state = store.GetPlayerState(playerId);
        if (state.LastResolvedWeek >= weekId)
            return new WeekResolution { Resolved = false, OldTier = state.Tier, NewTier = state.Tier };

        var cohort = GenerateCohort(store, playerId, displayName, boardId, weekId.ToString());
        int rank = cohort.First(e => e.IsMe).Rank;
        int oldTier = state.Tier;

        bool promoted = rank <= PromoteTop && state.Tier < (int)LeagueTier.Champion;
        bool demoted  = rank > CohortSize - DemoteBottom && state.Tier > (int)LeagueTier.Wood;
        if (promoted) state.Tier++;
        else if (demoted) state.Tier--;

        // Placement rewards scale with finishing position (better rank → more).
        int medals   = System.Math.Max(2, (CohortSize - rank + 1) / 2);
        int gems     = promoted ? 50 : 20;
        int cardDust = System.Math.Max(5, (CohortSize - rank + 1));

        state.LastResolvedWeek = weekId;
        store.SetPlayerState(state);

        return new WeekResolution
        {
            Resolved = true, OldTier = oldTier, NewTier = state.Tier, Rank = rank, CohortSize = CohortSize,
            Promoted = promoted, Demoted = demoted,
            RewardMedals = medals, RewardGems = gems, RewardCardDust = cardDust,
        };
    }

    private static int StableSeed(string s)
    {
        int h = 0;
        foreach (var c in s) h = unchecked(h * 31 + c);
        return h;
    }

    private static readonly string[] BotFirst = { "Vex", "Mira", "Krell", "Sable", "Onyx", "Wren", "Dax", "Lyra", "Borin", "Ash", "Pyra", "Gale", "Nox", "Rune", "Tam" };
    private static readonly string[] BotLast  = { "theSwift", "Emberhand", "Stonebreak", "ofAsh", "Nightfall", "theBold", "Ironwill", "Dawnward" };
    private static string BotName(Rng rng) => BotFirst[rng.Range(BotFirst.Length)].Trim() + " " + BotLast[rng.Range(BotLast.Length)];
}
