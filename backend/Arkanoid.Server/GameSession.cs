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
    private readonly ItemCatalog? _itemCatalog;
    private readonly string _pid;
    private readonly ConcurrentQueue<InputCommand> _inbox = new();
    private GameInstance _game = null!;
    private FileSimLog _log = null!;
    private long _tick;
    private List<BlockDto>? _cachedBlocks;
    private int _lastBlockVersion = -1;
    private bool _winGranted;

    public GameSession(WebSocket socket, string configRoot,
        BlockCatalog blockCatalog, RelicCatalog relicCatalog, BonusCatalog? bonusCatalog,
        IProfileStore profileStore, IDungeonStore dungeonStore, ItemCatalog? itemCatalog = null, string pid = "default")
    {
        _socket = socket; _configRoot = configRoot;
        _blockCatalog = blockCatalog; _relicCatalog = relicCatalog; _bonusCatalog = bonusCatalog;
        _profileStore = profileStore; _dungeonStore = dungeonStore;
        _itemCatalog = itemCatalog; _pid = pid;
    }

    public async Task RunAsync(string levelId, int seed, string runId, CancellationToken ct)
    {
        var path = System.IO.Path.Combine(FileSimLog.DirFor(), $"{runId}.jsonl");
        _log = new FileSimLog(path, verbose: _verboseLogs);
        _log.Note("conn", "session open", $"run={runId} level={levelId} seed={seed}");
        _game = GameInitializer.Build(levelId, seed, _configRoot,
            _blockCatalog, _relicCatalog, _bonusCatalog,
            _itemCatalog, _profileStore, _dungeonStore, _pid, _log);
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
            if (_game.Phase == GamePhase.Won && !_winGranted)
            {
                _winGranted = true;
                GrantWinReward(levelId);
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

    private void GrantWinReward(string levelId)
    {
        try
        {
            var profile = _profileStore.Load(_pid);
            var treasureBonus = _itemCatalog != null
                ? ItemEffects.ComputeTreasureBonus(profile.EquippedItems, profile.OwnedItems, _itemCatalog)
                : 0;
            Rewards.GrantLevelCompletion(profile, levelId, ProgressionConfig.Default, treasureBonus);
            _profileStore.Save(profile, _pid);
            _log.Note("meta", "win-reward granted", $"pid={_pid} level={levelId}");
        }
        catch (Exception ex)
        {
            _log.Note("meta", "win-reward failed", ex.Message);
        }
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
            case InputKind.CastSlot:  _game.CastSlot(c.Slot);  break;
            case InputKind.Cheat:
                if (_cheatsEnabled) _game.ApplyCheat(c.Cheat ?? "", c.Value);
                else _log.Note("cheat", "denied", c.Cheat ?? "");
                break;
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
