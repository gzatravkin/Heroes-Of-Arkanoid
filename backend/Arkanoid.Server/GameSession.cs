using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Net;
using Arkanoid.Core.Sim;

namespace Arkanoid.Server;

/// <summary>One GameInstance per socket. Real-time 60Hz loop. NOT static — many sessions coexist.</summary>
public sealed class GameSession
{
    private readonly WebSocket _socket;
    private readonly string _configRoot;
    private readonly ConcurrentQueue<InputCommand> _inbox = new();
    private GameInstance _game = null!;
    private long _tick;

    public GameSession(WebSocket socket, string configRoot)
    { _socket = socket; _configRoot = configRoot; }

    public async Task RunAsync(string levelId, int seed, CancellationToken ct)
    {
        LoadLevel(levelId, seed);
        var recv = ReceiveLoop(ct);
        var dtMs = (int)(_game.Config.FixedDt * 1000);
        var sb = new byte[1 << 16];
        while (!ct.IsCancellationRequested && _socket.State == WebSocketState.Open)
        {
            while (_inbox.TryDequeue(out var cmd)) Apply(cmd);
            _game.Tick(_game.Config.FixedDt);
            _tick++;
            var snap = Snapshot.From(_game, _tick);
            var json = JsonSerializer.Serialize(snap);
            var bytes = Encoding.UTF8.GetBytes(json);
            try { await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct); }
            catch { break; }
            await Task.Delay(dtMs, ct);
        }
        try { await recv; } catch { /* socket closed */ }
    }

    private void LoadLevel(string levelId, int seed)
    {
        var catalog = BlockCatalog.FromFile(Path.Combine(_configRoot, "blocks.json"));
        var level = LevelLoader.FromFile(Path.Combine(_configRoot, "levels", $"{levelId}.json"), catalog);
        _game = new GameInstance(level, SimConfig.Default, seed);
    }

    private void Apply(InputCommand c)
    {
        switch (c.Kind)
        {
            case InputKind.PaddleX: _game.SetPaddleX(c.X); break;
            case InputKind.Serve: _game.Serve(); break;
            case InputKind.CastImbueIgnite: _game.CastIgnite(); break;
            case InputKind.CastFireball: _game.CastFireball(); break;
            case InputKind.Cheat: _game.ApplyCheat(c.Cheat ?? "", c.Value); break;
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
