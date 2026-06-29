using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Arkanoid.Core;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Net;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim;

namespace ArkanoidWasm;

public static partial class GameBridge
{
    // Active game session (one per battle).
    private static GameInstance? _game;
    private static long _tick;
    // Block-DTO cache — reused between ticks when the block grid hasn't changed.
    private static List<BlockDto>? _cachedBlocks;
    private static int _lastBlockVersion = -1;
    private static readonly Queue<InputCommand> _inbox = new();

    // Stores (singletons — shared with MetaBridge).
    internal static readonly WasmProfileStore ProfileStore = new();
    internal static readonly WasmDungeonStore DungeonStore = new();

    // Catalogs — loaded once in InitCatalogs(), reused for every session.
    private static BlockCatalog? _blockCatalog;
    private static RelicCatalog? _relicCatalog;
    private static BonusCatalog? _bonusCatalog;
    private static CardCatalog? _cardCatalog;
    private static ModuleCatalog? _moduleCatalog;
    private static CharacterCatalog? _charCatalog;
    private static EventCatalog? _eventCatalog;

    /// <summary>Called once from bridge.ts after dotnet.create() — loads all catalogs from embedded resources.</summary>
    [JSExport]
    public static void InitCatalogs()
    {
        _blockCatalog  = BlockCatalog.FromJson(ResourceLoader.GetJson("config/blocks.json"));
        _relicCatalog  = RelicCatalog.FromJson(ResourceLoader.GetJson("config/relics.json"));
        _bonusCatalog  = ResourceLoader.TryGetJson("config/bonuses.json", out var bj)
                         ? BonusCatalog.FromJson(bj) : null;
        _cardCatalog   = ResourceLoader.TryGetJson("config/cards.json", out var cj)
                         ? CardCatalog.FromJson(cj) : CardCatalog.FromJson("{\"cards\":[]}");
        _moduleCatalog = ResourceLoader.TryGetJson("config/modules.json", out var mj)
                         ? ModuleCatalog.FromJson(mj) : ModuleCatalog.FromJson("{\"modules\":[]}");
        _charCatalog   = CharacterCatalog.FromJson(ResourceLoader.GetJson("config/characters.json"));
        _eventCatalog  = ResourceLoader.TryGetJson("config/events.json", out var ej)
                         ? EventCatalog.FromJson(ej) : EventCatalog.FromJson("{\"events\":[]}");
    }

    /// <summary>Called by WasmConnection constructor — one per battle. Builds a fresh GameInstance.</summary>
    [JSExport]
    public static void InitSession(string levelId, int seed, string runId, string pid, string mode)
    {
        _cachedBlocks = null;
        _lastBlockVersion = -1;
        _inbox.Clear();
        _tick = 0;

        if (mode == "trial")
        {
            var weekId = SeasonClock.Default.WeekId(DateTimeOffset.UtcNow);
            levelId = TrialConfig.LevelId;
            seed    = TrialConfig.SeedFor(weekId);
        }

        _game = GameInitializer.Build(
            levelId, seed,
            _blockCatalog!, _relicCatalog!, _bonusCatalog,
            ProfileStore, DungeonStore, pid,
            NullSimLog.Instance,
            _cardCatalog!, _moduleCatalog!, _charCatalog!, _eventCatalog!,
            id => ResourceLoader.GetJson($"config/levels/{id}.json"));
    }

    /// <summary>Queue an input — called immediately on user interaction, applied on the next Tick.</summary>
    [JSExport]
    public static void EnqueueInput(string inputJson)
    {
        if (string.IsNullOrEmpty(inputJson)) return;
        var cmd = JsonSerializer.Deserialize<InputCommand>(inputJson);
        if (cmd != null) _inbox.Enqueue(cmd);
    }

    /// <summary>
    /// Advance the sim one fixed step and return a snapshot JSON string.
    /// Called once per animation frame by WasmConnection.
    /// </summary>
    [JSExport]
    public static string Tick()
    {
        if (_game == null) return "{}";
        while (_inbox.TryDequeue(out var cmd)) ApplyInput(cmd);
        _game.Tick(_game.Config.FixedDt);
        _tick++;
        // Reuse cached block DTOs when the block grid has not changed (same pattern as GameSession).
        var blockCache = _game.BlockVersion == _lastBlockVersion ? _cachedBlocks : null;
        var snap = Snapshot.From(_game, _tick, blockCache);
        if (blockCache == null) { _cachedBlocks = snap.Blocks; _lastBlockVersion = _game.BlockVersion; }
        return JsonSerializer.Serialize(snap);
    }

    /// <summary>Tear down the active session — called when the battle scene is destroyed.</summary>
    [JSExport]
    public static void CloseSession()
    {
        _game = null;
        _cachedBlocks = null;
        _inbox.Clear();
    }

    private static void ApplyInput(InputCommand c)
    {
        switch (c.Kind)
        {
            case InputKind.PaddleX:         _game!.SetPaddleX(c.X); break;
            case InputKind.Serve:           _game!.Serve(); break;
            case InputKind.CastImbueIgnite: _game!.CastIgnite(); break;
            case InputKind.CastFireball:    _game!.CastFireball(); break;
            case InputKind.CastFireWall:    _game!.CastFireWall(); break;
            case InputKind.CastTurret:      _game!.CastTurret(); break;
            case InputKind.CastPhoenix:     _game!.CastPhoenix(); break;
            case InputKind.CastSlot:        _game!.CastSlot(c.Slot); break;
            case InputKind.Cheat:           _game!.ApplyCheat(c.Cheat ?? "", c.Value); break;
            case InputKind.RiftPick:        _game!.PickRiftModifier(c.RiftMod ?? ""); break;
        }
    }
}
