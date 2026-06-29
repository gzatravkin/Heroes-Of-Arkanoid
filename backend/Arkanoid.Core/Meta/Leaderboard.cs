using System.Collections.Generic;
using System.Linq;

namespace Arkanoid.Core.Meta;

/// <summary>A stored score row (best score for a player on a board+period).</summary>
public sealed class ScoreRecord
{
    public string PlayerId    { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int    Score       { get; set; }
    public bool   Shadowed    { get; set; }
}

/// <summary>Per-player competitive state (persisted, independent of any single week's score).</summary>
public sealed class PlayerLeagueState
{
    public string PlayerId         { get; set; } = "";
    public int    Tier             { get; set; } = 0;   // 0 = Wood … 6 = Champion
    public bool   Shadowed         { get; set; } = false;
    public int    Strikes          { get; set; } = 0;
    public int    LastResolvedWeek { get; set; } = -1;
}

/// <summary>
/// The swappable leaderboard storage <b>port</b> (social plan §0.3). The game/server depend only on this;
/// adapters provide the backing store — <c>InMemoryLeaderboardStore</c> (tests/offline),
/// <c>SqliteLeaderboardStore</c> (local, no cloud), or any future provider. No infra leaks past this seam.
/// </summary>
public interface ILeaderboardStore
{
    /// <summary>Upsert a player's score for a board+period, keeping the MAX.</summary>
    void UpsertScore(string boardId, string periodId, ScoreRecord record);
    ScoreRecord? GetScore(string boardId, string periodId, string playerId);
    /// <summary>Top scores for a board+period. <paramref name="includeShadowed"/> is false for public reads.</summary>
    IReadOnlyList<ScoreRecord> TopScores(string boardId, string periodId, int limit, bool includeShadowed);

    PlayerLeagueState GetPlayerState(string playerId);
    void SetPlayerState(PlayerLeagueState state);
    void AddStrike(string playerId, string reason, string detail);
}

/// <summary>Reference in-memory adapter — used by tests and offline/dev. Thread-safe enough for tests.</summary>
public sealed class InMemoryLeaderboardStore : ILeaderboardStore
{
    private readonly Dictionary<string, ScoreRecord> _scores = new();   // key: board|period|player
    private readonly Dictionary<string, PlayerLeagueState> _state = new();
    private readonly List<(string player, string reason, string detail)> _strikes = new();
    private readonly object _gate = new();

    private static string Key(string b, string p, string pl) => $"{b}|{p}|{pl}";

    public void UpsertScore(string boardId, string periodId, ScoreRecord record)
    {
        lock (_gate)
        {
            var k = Key(boardId, periodId, record.PlayerId);
            if (!_scores.TryGetValue(k, out var existing) || record.Score > existing.Score)
                _scores[k] = new ScoreRecord { PlayerId = record.PlayerId, DisplayName = record.DisplayName, Score = record.Score, Shadowed = record.Shadowed };
            else
            {
                existing.DisplayName = record.DisplayName;
                existing.Shadowed = record.Shadowed;
            }
        }
    }

    public ScoreRecord? GetScore(string boardId, string periodId, string playerId)
    {
        lock (_gate) { return _scores.TryGetValue(Key(boardId, periodId, playerId), out var r) ? Clone(r) : null; }
    }

    public IReadOnlyList<ScoreRecord> TopScores(string boardId, string periodId, int limit, bool includeShadowed)
    {
        lock (_gate)
        {
            var prefix = $"{boardId}|{periodId}|";
            return _scores
                .Where(kv => kv.Key.StartsWith(prefix))
                .Select(kv => kv.Value)
                .Where(r => includeShadowed || !r.Shadowed)
                .OrderByDescending(r => r.Score)
                .Take(limit)
                .Select(Clone)
                .ToList();
        }
    }

    public PlayerLeagueState GetPlayerState(string playerId)
    {
        lock (_gate)
        {
            if (_state.TryGetValue(playerId, out var s))
                return new PlayerLeagueState { PlayerId = s.PlayerId, Tier = s.Tier, Shadowed = s.Shadowed, Strikes = s.Strikes, LastResolvedWeek = s.LastResolvedWeek };
            return new PlayerLeagueState { PlayerId = playerId };
        }
    }

    public void SetPlayerState(PlayerLeagueState state)
    {
        lock (_gate)
        {
            _state[state.PlayerId] = new PlayerLeagueState { PlayerId = state.PlayerId, Tier = state.Tier, Shadowed = state.Shadowed, Strikes = state.Strikes, LastResolvedWeek = state.LastResolvedWeek };
        }
    }

    public void AddStrike(string playerId, string reason, string detail)
    {
        lock (_gate) { _strikes.Add((playerId, reason, detail)); }
    }

    private static ScoreRecord Clone(ScoreRecord r) =>
        new() { PlayerId = r.PlayerId, DisplayName = r.DisplayName, Score = r.Score, Shadowed = r.Shadowed };
}
