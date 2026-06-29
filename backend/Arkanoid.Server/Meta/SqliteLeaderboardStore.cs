using Arkanoid.Core.Meta;
using Microsoft.Data.Sqlite;

namespace Arkanoid.Server.Meta;

/// <summary>
/// Local SQLite adapter for <see cref="ILeaderboardStore"/> — the no-cloud provider. Same contract as the
/// in-memory adapter, so the game/league logic is identical regardless of backend.
/// </summary>
public sealed class SqliteLeaderboardStore : ILeaderboardStore
{
    private readonly SqliteDb _db;
    public SqliteLeaderboardStore(SqliteDb db) => _db = db;

    private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public void UpsertScore(string boardId, string periodId, ScoreRecord record) => _db.Write(c =>
    {
        using var cmd = c.CreateCommand();
        // Keep the MAX score; always refresh name/shadow flag.
        cmd.CommandText = """
            INSERT INTO scores (player_id, board_id, period_id, score, display_name, shadowed, updated_at)
            VALUES ($p, $b, $pe, $s, $n, $sh, $t)
            ON CONFLICT(player_id, board_id, period_id) DO UPDATE SET
                score        = MAX(scores.score, excluded.score),
                display_name = excluded.display_name,
                shadowed     = excluded.shadowed,
                updated_at   = excluded.updated_at;
            """;
        Bind(cmd, "$p", record.PlayerId); Bind(cmd, "$b", boardId); Bind(cmd, "$pe", periodId);
        Bind(cmd, "$s", record.Score); Bind(cmd, "$n", record.DisplayName);
        Bind(cmd, "$sh", record.Shadowed ? 1 : 0); Bind(cmd, "$t", NowUnix());
        cmd.ExecuteNonQuery();
    });

    public ScoreRecord? GetScore(string boardId, string periodId, string playerId) => _db.Read(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT player_id, display_name, score, shadowed FROM scores WHERE board_id=$b AND period_id=$pe AND player_id=$p;";
        Bind(cmd, "$b", boardId); Bind(cmd, "$pe", periodId); Bind(cmd, "$p", playerId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadScore(r) : null;
    });

    public IReadOnlyList<ScoreRecord> TopScores(string boardId, string periodId, int limit, bool includeShadowed) => _db.Read(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT player_id, display_name, score, shadowed FROM scores WHERE board_id=$b AND period_id=$pe {(includeShadowed ? "" : "AND shadowed=0")} ORDER BY score DESC LIMIT $lim;";
        Bind(cmd, "$b", boardId); Bind(cmd, "$pe", periodId); Bind(cmd, "$lim", limit);
        using var r = cmd.ExecuteReader();
        var list = new List<ScoreRecord>();
        while (r.Read()) list.Add(ReadScore(r));
        return (IReadOnlyList<ScoreRecord>)list;
    });

    public PlayerLeagueState GetPlayerState(string playerId) => _db.Read(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT league_tier, shadow_flag, strikes, last_resolved_week FROM player_state WHERE player_id=$p;";
        Bind(cmd, "$p", playerId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return new PlayerLeagueState { PlayerId = playerId };
        return new PlayerLeagueState
        {
            PlayerId = playerId,
            Tier = r.GetInt32(0),
            Shadowed = r.GetInt32(1) != 0,
            Strikes = r.GetInt32(2),
            LastResolvedWeek = r.GetInt32(3),
        };
    });

    public void SetPlayerState(PlayerLeagueState s) => _db.Write(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            INSERT INTO player_state (player_id, league_tier, shadow_flag, strikes, last_resolved_week)
            VALUES ($p, $t, $sh, $st, $w)
            ON CONFLICT(player_id) DO UPDATE SET
                league_tier=excluded.league_tier, shadow_flag=excluded.shadow_flag,
                strikes=excluded.strikes, last_resolved_week=excluded.last_resolved_week;
            """;
        Bind(cmd, "$p", s.PlayerId); Bind(cmd, "$t", s.Tier); Bind(cmd, "$sh", s.Shadowed ? 1 : 0);
        Bind(cmd, "$st", s.Strikes); Bind(cmd, "$w", s.LastResolvedWeek);
        cmd.ExecuteNonQuery();
    });

    public void AddStrike(string playerId, string reason, string detail) => _db.Write(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO audit_strikes (player_id, reason, detail, created_at) VALUES ($p,$r,$d,$t);";
        Bind(cmd, "$p", playerId); Bind(cmd, "$r", reason); Bind(cmd, "$d", detail); Bind(cmd, "$t", NowUnix());
        cmd.ExecuteNonQuery();
    });

    private static ScoreRecord ReadScore(SqliteDataReader r) => new()
    {
        PlayerId = r.GetString(0), DisplayName = r.GetString(1), Score = r.GetInt32(2), Shadowed = r.GetInt32(3) != 0,
    };

    private static void Bind(SqliteCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter(); p.ParameterName = name; p.Value = value; cmd.Parameters.Add(p);
    }
}
