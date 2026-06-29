using Microsoft.Data.Sqlite;

namespace Arkanoid.Server.Meta;

/// <summary>
/// Local SQLite database for the social systems (leaderboard, leagues, seasons) — fully on-disk, no cloud.
/// One file under the saves dir. Schema is created idempotently on first open. Connections are short-lived
/// (SQLite handles concurrency via its own lock); a process-wide gate serialises writes for safety.
/// </summary>
public sealed class SqliteDb
{
    private readonly string _connString;
    private readonly object _writeGate = new();

    public SqliteDb(string savesDir)
    {
        Directory.CreateDirectory(savesDir);
        var path = Path.Combine(savesDir, "arkanoid.db");
        _connString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
        Init();
    }

    public SqliteConnection Open()
    {
        var c = new SqliteConnection(_connString);
        c.Open();
        using var pragma = c.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=4000;";
        pragma.ExecuteNonQuery();
        return c;
    }

    /// <summary>Serialise writes through one gate (cheap; the social tables are low-traffic).</summary>
    public T Write<T>(Func<SqliteConnection, T> work)
    {
        lock (_writeGate)
        {
            using var c = Open();
            return work(c);
        }
    }

    public void Write(Action<SqliteConnection> work) => Write<object?>(c => { work(c); return null; });

    public T Read<T>(Func<SqliteConnection, T> work)
    {
        using var c = Open();
        return work(c);
    }

    private void Init() => Write(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS scores (
                player_id    TEXT NOT NULL,
                board_id     TEXT NOT NULL,
                period_id    TEXT NOT NULL,
                score        INTEGER NOT NULL,
                display_name TEXT NOT NULL DEFAULT '',
                shadowed     INTEGER NOT NULL DEFAULT 0,
                updated_at   INTEGER NOT NULL,
                PRIMARY KEY (player_id, board_id, period_id)
            );
            CREATE INDEX IF NOT EXISTS idx_scores_board ON scores (board_id, period_id, score DESC);

            CREATE TABLE IF NOT EXISTS player_state (
                player_id         TEXT PRIMARY KEY,
                league_tier       INTEGER NOT NULL DEFAULT 0,
                shadow_flag       INTEGER NOT NULL DEFAULT 0,
                strikes           INTEGER NOT NULL DEFAULT 0,
                last_resolved_week INTEGER NOT NULL DEFAULT -1
            );

            CREATE TABLE IF NOT EXISTS audit_strikes (
                player_id  TEXT NOT NULL,
                reason     TEXT NOT NULL,
                detail     TEXT NOT NULL DEFAULT '',
                created_at INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    });
}
