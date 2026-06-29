using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Net;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server;

/// <summary>One GameInstance per socket. Stopwatch-driven fixed-timestep loop. NOT static — many sessions coexist.</summary>
public sealed class GameSession
{
    private static readonly bool _cheatsEnabled =
        System.Environment.GetEnvironmentVariable("ARKANOID_CHEATS") == "1"
        || System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    private static readonly bool _verboseLogs =
        System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
        || System.Environment.GetEnvironmentVariable("ARKANOID_VERBOSE_LOGS") == "1";

    private readonly WebSocket _socket;
    private readonly string _configRoot;
    private readonly BlockCatalog _blockCatalog;
    private readonly RelicCatalog _relicCatalog;
    private readonly BonusCatalog? _bonusCatalog;
    private readonly IProfileStore _profileStore;
    private readonly IDungeonStore _dungeonStore;
    private readonly MissionCatalog? _missionCatalog;
    private readonly Arkanoid.Core.Meta.ILeaderboardStore? _leaderboard;
    private readonly SeasonCatalog? _seasonCatalog;
    private readonly EventCatalog? _eventCatalog;
    private readonly string _pid;
    private readonly string _mode;
    private bool _dailyRecorded;
    private bool _trialSubmitted;
    private readonly ConcurrentQueue<InputCommand> _inbox = new();
    private GameInstance _game = null!;
    private FileSimLog _log = null!;
    private long _tick;
    private List<BlockDto>? _cachedBlocks;
    private int _lastBlockVersion = -1;

    public GameSession(WebSocket socket, string configRoot,
        BlockCatalog blockCatalog, RelicCatalog relicCatalog, BonusCatalog? bonusCatalog,
        IProfileStore profileStore, IDungeonStore dungeonStore,
        string pid = "default", MissionCatalog? missionCatalog = null,
        Arkanoid.Core.Meta.ILeaderboardStore? leaderboard = null, string mode = "",
        SeasonCatalog? seasonCatalog = null, EventCatalog? eventCatalog = null)
    {
        _socket = socket; _configRoot = configRoot;
        _blockCatalog = blockCatalog; _relicCatalog = relicCatalog; _bonusCatalog = bonusCatalog;
        _profileStore = profileStore; _dungeonStore = dungeonStore;
        _pid = pid; _missionCatalog = missionCatalog;
        _leaderboard = leaderboard; _mode = mode;
        _seasonCatalog = seasonCatalog; _eventCatalog = eventCatalog;
    }

    public async Task RunAsync(string levelId, int seed, string runId, CancellationToken ct)
    {
        // Weekly Trial (plan §A.3): the server owns the level + seed so everyone faces the same gauntlet.
        if (_mode == "trial")
        {
            int weekId = SeasonClock.Default.WeekId(DateTimeOffset.UtcNow);
            levelId = Arkanoid.Core.Meta.TrialConfig.LevelId;
            seed    = Arkanoid.Core.Meta.TrialConfig.SeedFor(weekId);
        }
        var path = System.IO.Path.Combine(FileSimLog.DirFor(), $"{runId}.jsonl");
        _log = new FileSimLog(path, verbose: _verboseLogs);
        _log.Note("conn", "session open", $"run={runId} level={levelId} seed={seed}");
        _game = GameInitializer.Build(levelId, seed, _configRoot,
            _blockCatalog, _relicCatalog, _bonusCatalog,
            _profileStore, _dungeonStore, _pid, _log);
        var recv = ReceiveLoop(ct);

        // Fixed-timestep loop: Stopwatch drives simulation so wall-clock drift (send latency,
        // scheduler jitter) doesn't accumulate into game-time drift.
        var dt = _game.Config.FixedDt;
        var sw = Stopwatch.StartNew();
        var nextTickAt = sw.Elapsed.TotalSeconds;

        while (!ct.IsCancellationRequested && _socket.State == WebSocketState.Open)
        {
            var now = sw.Elapsed.TotalSeconds;
            // Advance simulation for all elapsed fixed steps (capped to avoid spiral-of-death).
            int maxCatchUp = 4;
            while (nextTickAt <= now && maxCatchUp-- > 0)
            {
                while (_inbox.TryDequeue(out var cmd)) Apply(cmd);
                _game.Tick(dt);
                _tick++;
                nextTickAt += dt;
            }
            // NOTE: the win reward is granted by the CLIENT's context-aware flow on Won
            // (campaign → POST /complete, dungeon floor → POST /dungeon/floor-cleared).
            // A blanket server grant here double-granted with the client (so /complete saw
            // the level already complete and the reward overlay showed "+0"), and wrongly
            // granted campaign completion for dungeon floors. So the server does not grant.
            // Daily-mission progress (plan §A.2): recorded server-authoritatively at battle end, once.
            if (!_dailyRecorded && _missionCatalog != null
                && (_game.Phase == GamePhase.Won || _game.Phase == GamePhase.Lost))
            {
                _dailyRecorded = true;
                RecordDaily(_game.Phase == GamePhase.Won);
            }
            // Weekly Trial: submit the server-authoritative score once, at battle end (plan §A.3).
            if (!_trialSubmitted && _mode == "trial" && _leaderboard != null
                && (_game.Phase == GamePhase.Won || _game.Phase == GamePhase.Lost))
            {
                _trialSubmitted = true;
                SubmitTrial(_game.Phase == GamePhase.Won);
            }
            var blockCache = _game.BlockVersion == _lastBlockVersion ? _cachedBlocks : null;
            var snap  = Snapshot.From(_game, _tick, blockCache);
            if (blockCache == null) { _cachedBlocks = snap.Blocks; _lastBlockVersion = _game.BlockVersion; }
            if (_verboseLogs)
                foreach (var ev in snap.Events)
                    _log.Log(_tick, "ev", ev.Type, $"x={ev.X:F1} y={ev.Y:F1}{(ev.Extra != 0 ? $" extra={ev.Extra}" : "")}");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(snap);
            try { await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct); }
            catch { break; }
            var sleepMs = (int)((nextTickAt - sw.Elapsed.TotalSeconds) * 1000);
            if (sleepMs > 0) await Task.Delay(sleepMs, ct);
        }
        _log.Note("conn", "session close", $"ticks={_tick}");
        _log.Dispose();
        try { await recv; } catch { /* socket closed */ }
    }

    /// <summary>Record daily-mission progress + season/event tokens from the just-finished battle
    /// (server-authoritative stats), and update the season/event leaderboards (plan §A.2/§C).</summary>
    private void RecordDaily(bool won)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            int dayId = SeasonClock.Default.DayId(now), weekId = SeasonClock.Default.WeekId(now), seasonId = SeasonClock.Default.SeasonId(now);
            var profile = _profileStore.Load(_pid);
            DailyService.Record(profile, _missionCatalog!, dayId, weekId, "blocks_destroyed", _game.BricksDestroyedThisLevel);
            DailyService.Record(profile, _missionCatalog!, dayId, weekId, "battles_played", 1);
            if (won) DailyService.Record(profile, _missionCatalog!, dayId, weekId, "levels_won", 1);

            // Season Festival (plan §C): every battle feeds the season track + the live event.
            if (_seasonCatalog != null) SeasonService.AddTokens(profile, seasonId, _seasonCatalog.TokensPerBattle);
            var ev = _eventCatalog?.Current(weekId);
            if (ev != null) EventService.AddTokens(profile, weekId, ev.TokenPerBattle);

            _profileStore.Save(profile, _pid);

            if (_leaderboard != null && _seasonCatalog != null)
                LeaderboardService.Submit(_leaderboard, _pid, _pid, SeasonService.BoardId, seasonId.ToString(), SeasonService.SeasonScore(profile));
            if (_leaderboard != null && ev != null)
                LeaderboardService.Submit(_leaderboard, _pid, _pid, EventService.BoardPrefix + ev.Id, weekId.ToString(), profile.Season.EventTokens);
        }
        catch { /* daily tracking must never break a battle */ }
    }

    /// <summary>Submit the Weekly Trial score (server-authoritative; the client never sends a number).</summary>
    private void SubmitTrial(bool won)
    {
        try
        {
            int weekId = SeasonClock.Default.WeekId(DateTimeOffset.UtcNow);
            int score = Arkanoid.Core.Meta.TrialConfig.Score(_game.BricksDestroyedThisLevel, won);
            Arkanoid.Core.Meta.LeaderboardService.Submit(
                _leaderboard!, _pid, _pid, Arkanoid.Core.Meta.TrialConfig.BoardId, weekId.ToString(), score);
        }
        catch { /* leaderboard must never break a battle */ }
    }

    private void Apply(InputCommand c)
    {
        _log.Note("cmd", c.Kind.ToString(), $"x={c.X:F1} cheat={c.Cheat} value={c.Value}");
        switch (c.Kind)
        {
            case InputKind.PaddleX: _game.SetPaddleX(c.X); break;
            case InputKind.Serve: _game.Serve(); break;
            case InputKind.CastImbueIgnite: _game.CastIgnite(); break;
            case InputKind.CastFireball: _game.CastFireball(); break;
            case InputKind.CastFireWall: _game.CastFireWall(); break;
            case InputKind.CastTurret: _game.CastTurret(); break;
            case InputKind.CastPhoenix: _game.CastPhoenix(); break;
            case InputKind.CastSlot:  _game.CastSlot(c.Slot);  break;
            case InputKind.Cheat:
                if (_cheatsEnabled) _game.ApplyCheat(c.Cheat ?? "", c.Value);
                else _log.Note("cheat", "denied", c.Cheat ?? "");
                break;
            case InputKind.RiftPick: _game.PickRiftModifier(c.RiftMod ?? ""); break;
        }
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var buf = new byte[4096];
        while (_socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var res = await _socket.ReceiveAsync(buf, ct);
            if (res.MessageType == WebSocketMessageType.Close) break;
            var json = Encoding.UTF8.GetString(buf, 0, res.Count);
            var cmd = JsonSerializer.Deserialize<InputCommand>(json);
            if (cmd != null) _inbox.Enqueue(cmd);
        }
    }
}
