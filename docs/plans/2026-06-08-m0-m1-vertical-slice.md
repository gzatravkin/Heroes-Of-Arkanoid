# M0 + M1 Vertical Slice — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> **This repo bans git worktrees** (see `CLAUDE.md`). Execute every task directly on the current branch.

**Goal:** Stand up the Arkanoid-RPG architecture end to end — a pure C# simulation core, a WebSocket server host, a PixiJS renderer, and an automation-first Playwright harness — then build the playable Fire Mage / Hell vertical slice (ball ↔ paddle ↔ block physics, lives/balls, win/lose, mana, one imbue spell + one active spell), with an **individual Playwright scenario for each part** (menu, HUD, battle-start, battle-winnable, battle-lose, spell-cast) booted from pre-set states.

**Architecture:** All game logic lives in `Arkanoid.Core` (net8.0 class library, **zero** Unity/ASP.NET/networking deps, deterministic fixed-timestep, instance state — never `static`). `Arkanoid.Server` (ASP.NET minimal API) owns one `GameInstance` per WebSocket connection, runs a 60 Hz loop, applies queued input/cheat commands, and streams JSON snapshots. The PixiJS frontend is a pure renderer: it draws from snapshots and sends input. A dev-only **cheat command channel** lets Playwright force exact states (clear-all-but-N, win-now, lose-now, set-seed, set-mana) so each part is testable from a deterministic pre-setup without depending on physics timing.

**Excessive structured logging is a first-class layer** (Task 0.10): the Core emits structured records through an injected `ISimLog` (every transition — serve, paddle deflect with offset, wall bounce, block hit/destroy, mana change, spell cast, ignite, drain, win/lose, cheat); the server writes one **JSONL file per session** (`logs/<runId>.jsonl`) and logs connection/command lifecycle; the frontend keeps a console-mirrored ring buffer; and the Playwright harness **auto-attaches the client console, the client ring buffer, and the backend session JSONL to the test report on every failure**. When a test goes red, an AI reads `<runId>.jsonl` to see exactly what the sim did, tick by tick.

**Tech Stack:** .NET 8 (C#) · xUnit · ASP.NET minimal WebSockets · System.Text.Json · Vite + TypeScript + PixiJS v7 · Playwright (`@playwright/test`).

**Reference docs:** `../04-new-game-design.md` (design), `../03-migration-plan.md` (architecture/milestones M0–M1), `../02-current-implementation.md` (what to avoid).

---

## File Structure (created across this plan)

```
Arkanoid game/
  backend/
    Arkanoid.sln
    Arkanoid.Core/                 # pure sim, no host deps
      Arkanoid.Core.csproj
      Math/Vec2.cs                 # value-type 2D vector
      Math/Aabb.cs                 # axis-aligned box + sweep helpers
      Sim/SimConfig.cs             # all tunable constants (no magic numbers)
      Sim/Rng.cs                   # seeded deterministic RNG
      Sim/GameInstance.cs          # owns world state + Tick(dt)
      Sim/GamePhase.cs             # enum: Serving, Playing, Won, Lost
      Sim/SimLog.cs                # ISimLog + SimLogRecord + NullSimLog (excessive structured logging)
      Grid/Grid.cs                 # integer board: cols/rows/cellSize
      Grid/LevelData.cs            # level = grid of block-type ids + meta
      Grid/LevelLoader.cs          # JSON -> LevelData
      Entities/Ball.cs
      Entities/Paddle.cs
      Entities/Block.cs
      Blocks/BlockCatalog.cs       # block-type table (hp, sprite key, flags)
      Physics/BallPhysics.cs       # ball vs walls/paddle/blocks (deterministic)
      Spells/Mana.cs               # resource (regen + on-kill + perfect-deflect)
      Spells/Spell.cs              # base + ImbueIgnite + ActiveFireball
      Spells/BallImbue.cs          # per-ball imbue state (Ignite hits left)
      Net/InputCommand.cs          # client -> sim (paddle, cast, cheat)
      Net/Snapshot.cs              # sim -> client (entities + events)
    Arkanoid.Server/
      Arkanoid.Server.csproj
      Program.cs                   # minimal API + /ws endpoint
      GameSession.cs               # 1 GameInstance per socket, 60Hz loop, JSON
      FileSimLog.cs                # ISimLog -> per-session JSONL (logs/<runId>.jsonl)
    Arkanoid.Tests/
      Arkanoid.Tests.csproj
      MathTests.cs
      GridTests.cs
      GameInstanceTests.cs
      BallPhysicsTests.cs
      WinLoseTests.cs
      SpellTests.cs
  frontend/
    package.json  vite.config.ts  tsconfig.json  index.html
    src/main.ts                    # boots scene from ?scene= param
    src/net/Connection.ts          # WS client, holds latest snapshot
    src/render/Renderer.ts         # draws a snapshot (sprites/rects)
    src/render/textures.ts         # loads block/ball/paddle textures
    src/input/PaddleInput.ts       # mouse -> paddle command
    src/scenes/MenuScene.ts
    src/scenes/BattleScene.ts
    src/log.ts                     # ring-buffer logger + console mirror (window.__game.getLogs)
    src/testhooks.ts               # window.__game test/cheat API
  contract/
    protocol.md                    # WS message spec (source of truth)
  config/
    blocks.json                    # block-type catalog
    levels/hell-1.json             # the MVP level (grid)
    levels/hell-winnable.json      # 1-block level for the winnable test
  tests/
    playwright.config.ts
    helpers/servers.ts             # boot backend+frontend for tests
    helpers/game.ts                # page-side helpers (wait, cheat, getState)
    helpers/fixtures.ts            # extended `test`: capture console + attach client/server logs on failure
    menu.spec.ts
    battle-start.spec.ts
    hud.spec.ts
    battle-winnable.spec.ts
    battle-lose.spec.ts
    spell-ignite.spec.ts
    spell-fireball.spec.ts
  .gitignore                       # ignore logs/, bin/, obj/, node_modules/, dist/, test-results/
```

---

# PHASE M0 — Foundations & Test Harness

## Task 0.1: Scaffold the .NET solution

**Files:**
- Create: `backend/Arkanoid.sln`, `backend/Arkanoid.Core/Arkanoid.Core.csproj`, `backend/Arkanoid.Server/Arkanoid.Server.csproj`, `backend/Arkanoid.Tests/Arkanoid.Tests.csproj`

- [ ] **Step 1: Create projects and solution**

Run from `Arkanoid game/backend`:
```bash
dotnet new classlib -n Arkanoid.Core -f net8.0
dotnet new web -n Arkanoid.Server -f net8.0
dotnet new xunit -n Arkanoid.Tests -f net8.0
dotnet new sln -n Arkanoid
dotnet sln add Arkanoid.Core Arkanoid.Server Arkanoid.Tests
dotnet add Arkanoid.Server reference Arkanoid.Core
dotnet add Arkanoid.Tests reference Arkanoid.Core
rm Arkanoid.Core/Class1.cs
```

- [ ] **Step 2: Verify it builds**

Run: `dotnet build` (from `backend/`)
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add "Arkanoid game/backend"
git commit -m "chore(arkanoid): scaffold .NET solution (Core/Server/Tests)"
```

---

## Task 0.2: Core math — `Vec2` and `Aabb`

**Files:**
- Create: `backend/Arkanoid.Core/Math/Vec2.cs`, `backend/Arkanoid.Core/Math/Aabb.cs`
- Test: `backend/Arkanoid.Tests/MathTests.cs`

- [ ] **Step 1: Write the failing test**

`Arkanoid.Tests/MathTests.cs`:
```csharp
using Arkanoid.Core.Math;
using Xunit;

public class MathTests
{
    [Fact]
    public void Normalize_ProducesUnitLength()
    {
        var v = new Vec2(3, 4).Normalized();
        Assert.Equal(1.0, v.Length, 5);
    }

    [Fact]
    public void Reflect_AcrossHorizontalWall_FlipsY()
    {
        var v = new Vec2(2, -5);
        var r = v.Reflect(new Vec2(0, 1)); // floor normal points up
        Assert.Equal(2, r.X, 5);
        Assert.Equal(5, r.Y, 5);
    }

    [Fact]
    public void Aabb_Contains_PointInside()
    {
        var box = new Aabb(0, 0, 10, 4);
        Assert.True(box.Contains(new Vec2(5, 2)));
        Assert.False(box.Contains(new Vec2(11, 2)));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/Arkanoid.Tests` 
Expected: FAIL — `Vec2`/`Aabb` not found.

- [ ] **Step 3: Implement `Vec2`**

`Arkanoid.Core/Math/Vec2.cs`:
```csharp
namespace Arkanoid.Core.Math;

public readonly struct Vec2
{
    public readonly double X;
    public readonly double Y;
    public Vec2(double x, double y) { X = x; Y = y; }

    public double Length => System.Math.Sqrt(X * X + Y * Y);

    public Vec2 Normalized()
    {
        var len = Length;
        return len <= 1e-9 ? new Vec2(0, 0) : new Vec2(X / len, Y / len);
    }

    public double Dot(Vec2 o) => X * o.X + Y * o.Y;

    /// <summary>Reflect this vector across a surface with unit normal n.</summary>
    public Vec2 Reflect(Vec2 n)
    {
        var d = 2 * Dot(n);
        return new Vec2(X - d * n.X, Y - d * n.Y);
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
}
```

- [ ] **Step 4: Implement `Aabb`**

`Arkanoid.Core/Math/Aabb.cs`:
```csharp
namespace Arkanoid.Core.Math;

public readonly struct Aabb
{
    public readonly double MinX, MinY, MaxX, MaxY;
    public Aabb(double minX, double minY, double maxX, double maxY)
    { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }

    public static Aabb FromCenter(Vec2 c, double halfW, double halfH)
        => new(c.X - halfW, c.Y - halfH, c.X + halfW, c.Y + halfH);

    public bool Contains(Vec2 p) => p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;

    /// <summary>True if a circle of radius r centered at c overlaps this box.</summary>
    public bool IntersectsCircle(Vec2 c, double r)
    {
        var nx = System.Math.Clamp(c.X, MinX, MaxX);
        var ny = System.Math.Clamp(c.Y, MinY, MaxY);
        var dx = c.X - nx; var dy = c.Y - ny;
        return dx * dx + dy * dy <= r * r;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add "Arkanoid game/backend"
git commit -m "feat(arkanoid-core): Vec2 + Aabb math primitives"
```

---

## Task 0.3: The grid, level data, and JSON loaders

**Files:**
- Create: `backend/Arkanoid.Core/Grid/Grid.cs`, `Grid/LevelData.cs`, `Grid/LevelLoader.cs`, `Blocks/BlockCatalog.cs`
- Create: `config/blocks.json`, `config/levels/hell-1.json`, `config/levels/hell-winnable.json`
- Test: `backend/Arkanoid.Tests/GridTests.cs`

**Design note (fixes the legacy defect):** blocks occupy **whole integer cells**. No per-block float scale. A level is a list of rows; each cell is a block-type id (`"."` = empty).

- [ ] **Step 1: Write the failing test**

`Arkanoid.Tests/GridTests.cs`:
```csharp
using Arkanoid.Core.Grid;
using Arkanoid.Core.Blocks;
using Xunit;

public class GridTests
{
    private const string BlocksJson = """
    { "types": [
      { "id": "hell_basic", "biome": "hell", "hp": 2, "sprite": "HellStandart", "needToKill": true },
      { "id": "hell_tough", "biome": "hell", "hp": 4, "sprite": "HellStandart2", "needToKill": true }
    ]}
    """;

    private const string LevelJson = """
    { "id": "t1", "biome": "hell", "cols": 4, "rows": 3,
      "rows_data": [ "....", "AB..", "AAAA" ],
      "legend": { "A": "hell_basic", "B": "hell_tough" } }
    """;

    [Fact]
    public void Loader_PlacesBlocksOnIntegerCells()
    {
        var catalog = BlockCatalog.FromJson(BlocksJson);
        var level = LevelLoader.FromJson(LevelJson, catalog);

        Assert.Equal(4, level.Grid.Cols);
        Assert.Equal(3, level.Grid.Rows);
        // 6 blocks total: row1 "AB.." -> 2, row2 "AAAA" -> 4
        Assert.Equal(6, level.Blocks.Count);

        var tough = level.Blocks.Find(b => b.Col == 1 && b.Row == 1);
        Assert.NotNull(tough);
        Assert.Equal(4, tough!.Hp);
        Assert.True(tough.NeedToKill);
    }

    [Fact]
    public void Grid_CellCenter_IsDeterministic()
    {
        var g = new Grid(cols: 4, rows: 3, cellSize: 10, originX: 0, originY: 0);
        var c = g.CellCenter(col: 1, row: 0);
        Assert.Equal(15, c.X, 5); // col1 center = 1*10 + 5
        Assert.Equal(5, c.Y, 5);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: FAIL — types not found.

- [ ] **Step 3: Implement `Grid`**

`Arkanoid.Core/Grid/Grid.cs`:
```csharp
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Grid;

public sealed class Grid
{
    public int Cols { get; }
    public int Rows { get; }
    public double CellSize { get; }
    public double OriginX { get; }
    public double OriginY { get; }

    public Grid(int cols, int rows, double cellSize, double originX, double originY)
    { Cols = cols; Rows = rows; CellSize = cellSize; OriginX = originX; OriginY = originY; }

    public double Width => Cols * CellSize;
    public double Height => Rows * CellSize;

    public Vec2 CellCenter(int col, int row)
        => new(OriginX + col * CellSize + CellSize / 2.0,
               OriginY + row * CellSize + CellSize / 2.0);
}
```

- [ ] **Step 4: Implement `BlockCatalog`**

`Arkanoid.Core/Blocks/BlockCatalog.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Blocks;

public sealed class BlockType
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("biome")] public string Biome { get; set; } = "";
    [JsonPropertyName("hp")] public int Hp { get; set; } = 1;
    [JsonPropertyName("sprite")] public string Sprite { get; set; } = "";
    [JsonPropertyName("needToKill")] public bool NeedToKill { get; set; } = true;
}

public sealed class BlockCatalog
{
    private readonly Dictionary<string, BlockType> _byId;
    private BlockCatalog(IEnumerable<BlockType> types)
        => _byId = types.ToDictionary(t => t.Id);

    public BlockType Get(string id) => _byId[id];

    private sealed class Dto { [JsonPropertyName("types")] public List<BlockType> Types { get; set; } = new(); }

    public static BlockCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json)
            ?? throw new InvalidOperationException("bad blocks json");
        return new BlockCatalog(dto.Types);
    }

    public static BlockCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
```

- [ ] **Step 5: Implement `Block`, `LevelData`, `LevelLoader`**

`Arkanoid.Core/Entities/Block.cs`:
```csharp
namespace Arkanoid.Core.Entities;

public sealed class Block
{
    public int Id { get; init; }          // stable runtime id for snapshots
    public int Col { get; init; }
    public int Row { get; init; }
    public int Hp { get; set; }
    public int MaxHp { get; init; }
    public string TypeId { get; init; } = "";
    public string Sprite { get; init; } = "";
    public bool NeedToKill { get; init; }
    public bool Dead { get; set; }
}
```

`Arkanoid.Core/Grid/LevelData.cs`:
```csharp
using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Grid;

public sealed class LevelData
{
    public string Id { get; init; } = "";
    public string Biome { get; init; } = "";
    public Grid Grid { get; init; } = null!;
    public List<Block> Blocks { get; init; } = new();
}
```

`Arkanoid.Core/Grid/LevelLoader.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Grid;

public static class LevelLoader
{
    private sealed class Dto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("biome")] public string Biome { get; set; } = "";
        [JsonPropertyName("cols")] public int Cols { get; set; }
        [JsonPropertyName("rows")] public int Rows { get; set; }
        [JsonPropertyName("rows_data")] public List<string> RowsData { get; set; } = new();
        [JsonPropertyName("legend")] public Dictionary<string, string> Legend { get; set; } = new();
    }

    public static LevelData FromJson(string json, BlockCatalog catalog, SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var dto = JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidOperationException("bad level json");
        var grid = new Grid(dto.Cols, dto.Rows, cfg.CellSize, cfg.BoardOriginX, cfg.BoardOriginY);
        var blocks = new List<Block>();
        int nextId = 1;
        for (int row = 0; row < dto.RowsData.Count; row++)
        {
            var line = dto.RowsData[row];
            for (int col = 0; col < line.Length; col++)
            {
                var ch = line[col].ToString();
                if (ch == "." || !dto.Legend.TryGetValue(ch, out var typeId)) continue;
                var t = catalog.Get(typeId);
                blocks.Add(new Block {
                    Id = nextId++, Col = col, Row = row,
                    Hp = t.Hp, MaxHp = t.Hp, TypeId = t.Id,
                    Sprite = t.Sprite, NeedToKill = t.NeedToKill
                });
            }
        }
        return new LevelData { Id = dto.Id, Biome = dto.Biome, Grid = grid, Blocks = blocks };
    }

    public static LevelData FromFile(string path, BlockCatalog catalog, SimConfig? cfg = null)
        => FromJson(File.ReadAllText(path), catalog, cfg);
}
```

- [ ] **Step 6: Create the config files**

`config/blocks.json`:
```json
{ "types": [
  { "id": "hell_basic", "biome": "hell", "hp": 2, "sprite": "HellStandart",  "needToKill": true },
  { "id": "hell_tough", "biome": "hell", "hp": 4, "sprite": "HellStandart2", "needToKill": true }
]}
```

`config/levels/hell-1.json` (12×8 standardized board):
```json
{ "id": "hell-1", "biome": "hell", "cols": 12, "rows": 8,
  "rows_data": [
    "............",
    "..AAAAAAAA..",
    "..ABBBBBBA..",
    "..ABBBBBBA..",
    "..AAAAAAAA..",
    "............",
    "............",
    "............"
  ],
  "legend": { "A": "hell_basic", "B": "hell_tough" } }
```

`config/levels/hell-winnable.json` (single block — for the win test):
```json
{ "id": "hell-winnable", "biome": "hell", "cols": 12, "rows": 8,
  "rows_data": [ "............","......A.....","............","............","............","............","............","............" ],
  "legend": { "A": "hell_basic" } }
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: PASS (Grid tests). (`SimConfig` is created in Task 0.4 — if compile fails here, do Task 0.4 first, then return. Recommended order: do 0.4 Step 3 `SimConfig` before running this step.)

- [ ] **Step 8: Commit**

```bash
git add "Arkanoid game/backend" "Arkanoid game/config"
git commit -m "feat(arkanoid-core): integer grid + block catalog + level JSON loader"
```

---

## Task 0.4: SimConfig, entities, RNG

**Files:**
- Create: `backend/Arkanoid.Core/Sim/SimConfig.cs`, `Sim/Rng.cs`, `Sim/GamePhase.cs`, `Entities/Ball.cs`, `Entities/Paddle.cs`

- [ ] **Step 1: Implement `SimConfig` (all constants here — no magic numbers elsewhere)**

`Arkanoid.Core/Sim/SimConfig.cs`:
```csharp
namespace Arkanoid.Core.Sim;

/// <summary>Every tunable number lives here (CLAUDE.md: no magic numbers in logic).</summary>
public sealed class SimConfig
{
    public double CellSize { get; init; } = 32;
    public double BoardOriginX { get; init; } = 0;
    public double BoardOriginY { get; init; } = 0;

    public double TickHz { get; init; } = 60;
    public double FixedDt => 1.0 / TickHz;

    public double BallRadius { get; init; } = 8;
    public double BallSpeed { get; init; } = 360;     // units/sec
    public double MinVerticalRatio { get; init; } = 0.30; // "no shallow angle" clamp

    public double PaddleWidth { get; init; } = 96;
    public double PaddleHeight { get; init; } = 16;
    public double PaddleMaxDeflectAngleDeg { get; init; } = 60;

    public int StartLives { get; init; } = 3;   // HP, enemy damage (M3+)
    public int StartBalls { get; init; } = 3;   // spare balls (drains)
    public int BallDamage { get; init; } = 1;

    public double ManaMax { get; init; } = 100;
    public double ManaRegenPerSec { get; init; } = 12;
    public double ManaPerKill { get; init; } = 4;
    public double ManaPerfectDeflectBonus { get; init; } = 8;
    public double PerfectDeflectBand { get; init; } = 0.18; // |t| < band counts as "perfect"

    public double IgniteCost { get; init; } = 0;     // imbue is cheap/free (anti-Wizorb)
    public int    IgniteHits { get; init; } = 4;
    public double FireballCost { get; init; } = 20;
    public double FireballSpeed { get; init; } = 420;
    public int    FireballDamage { get; init; } = 2;

    public static SimConfig Default { get; } = new();
}
```

- [ ] **Step 2: Implement `GamePhase` and `Rng`**

`Arkanoid.Core/Sim/GamePhase.cs`:
```csharp
namespace Arkanoid.Core.Sim;
public enum GamePhase { Serving, Playing, Won, Lost }
```

`Arkanoid.Core/Sim/Rng.cs`:
```csharp
namespace Arkanoid.Core.Sim;

/// <summary>Deterministic, seed-reproducible RNG (no UnityEngine.Random).</summary>
public sealed class Rng
{
    private readonly System.Random _r;
    public int Seed { get; }
    public Rng(int seed) { Seed = seed; _r = new System.Random(seed); }
    public double NextDouble() => _r.NextDouble();
    public double Range(double min, double max) => min + (max - min) * _r.NextDouble();
}
```

- [ ] **Step 3: Implement `Ball` and `Paddle`**

`Arkanoid.Core/Entities/Ball.cs`:
```csharp
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Entities;

public sealed class Ball
{
    public int Id { get; init; }
    public Vec2 Pos;
    public Vec2 Vel;
    public double Radius;
    public bool Alive = true;
    public int IgniteHitsLeft = 0;     // >0 means imbued with Ignite
}
```

`Arkanoid.Core/Entities/Paddle.cs`:
```csharp
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Entities;

public sealed class Paddle
{
    public Vec2 Center;
    public double Width;
    public double Height;
}
```

- [ ] **Step 4: Build**

Run: `dotnet build backend`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add "Arkanoid game/backend"
git commit -m "feat(arkanoid-core): SimConfig, RNG, GamePhase, Ball/Paddle entities"
```

---

## Task 0.5: `GameInstance` skeleton + tick

**Files:**
- Create: `backend/Arkanoid.Core/Sim/GameInstance.cs`
- Test: `backend/Arkanoid.Tests/GameInstanceTests.cs`

**Note:** Physics is stubbed here (straight-line motion, no collisions) so we can prove the tick loop, serve, and entity lifecycle independently. Real collisions arrive in M1 (Task 1.1–1.2).

- [ ] **Step 1: Write the failing test**

`Arkanoid.Tests/GameInstanceTests.cs`:
```csharp
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Sim;
using Xunit;

public class GameInstanceTests
{
    private static GameInstance MakeInstance()
    {
        var catalog = BlockCatalog.FromJson("""
          {"types":[{"id":"hell_basic","biome":"hell","hp":2,"sprite":"HellStandart","needToKill":true}]}
        """);
        var level = LevelLoader.FromJson("""
          {"id":"t","biome":"hell","cols":3,"rows":2,"rows_data":["...","A.."],"legend":{"A":"hell_basic"}}
        """, catalog);
        return new GameInstance(level, SimConfig.Default, seed: 123);
    }

    [Fact]
    public void NewInstance_StartsServing_WithConfiguredResources()
    {
        var g = MakeInstance();
        Assert.Equal(GamePhase.Serving, g.Phase);
        Assert.Equal(3, g.Lives);
        Assert.Equal(3, g.SpareBalls);
        Assert.Single(g.Balls);
    }

    [Fact]
    public void Serve_MovesToPlaying_AndBallGainsVelocity()
    {
        var g = MakeInstance();
        g.Serve();
        Assert.Equal(GamePhase.Playing, g.Phase);
        Assert.True(g.Balls[0].Vel.Length > 0);
    }

    [Fact]
    public void Tick_AdvancesBallPosition()
    {
        var g = MakeInstance();
        g.Serve();
        var y0 = g.Balls[0].Pos.Y;
        g.Tick(SimConfig.Default.FixedDt);
        Assert.NotEqual(y0, g.Balls[0].Pos.Y);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: FAIL — `GameInstance` not found.

- [ ] **Step 3: Implement `GameInstance` (tick = straight-line motion for now)**

`Arkanoid.Core/Sim/GameInstance.cs`:
```csharp
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim;

public sealed class GameInstance
{
    public SimConfig Config { get; }
    public LevelData Level { get; }
    public Rng Rng { get; private set; }

    public GamePhase Phase { get; private set; } = GamePhase.Serving;
    public int Lives { get; private set; }
    public int SpareBalls { get; private set; }

    public Paddle Paddle { get; }
    public List<Ball> Balls { get; } = new();
    public List<Block> Blocks => Level.Blocks;

    private int _nextBallId = 1;

    public GameInstance(LevelData level, SimConfig config, int seed)
    {
        Level = level; Config = config; Rng = new Rng(seed);
        Lives = config.StartLives;
        SpareBalls = config.StartBalls;
        Paddle = new Paddle {
            Width = config.PaddleWidth,
            Height = config.PaddleHeight,
            Center = new Vec2(level.Grid.Width / 2.0, level.Grid.Height + config.CellSize)
        };
        SpawnBallOnPaddle();
    }

    private void SpawnBallOnPaddle()
    {
        Balls.Clear();
        Balls.Add(new Ball {
            Id = _nextBallId++,
            Radius = Config.BallRadius,
            Pos = new Vec2(Paddle.Center.X, Paddle.Center.Y - Paddle.Height / 2 - Config.BallRadius - 1),
            Vel = new Vec2(0, 0),
            Alive = true
        });
        Phase = GamePhase.Serving;
    }

    public void Serve()
    {
        if (Phase != GamePhase.Serving) return;
        // launch upward with a small deterministic horizontal lean
        var lean = Rng.Range(-0.25, 0.25);
        Balls[0].Vel = new Vec2(lean, -1).Normalized() * Config.BallSpeed;
        Phase = GamePhase.Playing;
    }

    public void SetPaddleX(double x)
    {
        var half = Paddle.Width / 2;
        var clamped = System.Math.Clamp(x, half, Level.Grid.Width - half);
        Paddle.Center = new Vec2(clamped, Paddle.Center.Y);
    }

    public void Tick(double dt)
    {
        if (Phase != GamePhase.Playing) return;
        foreach (var b in Balls)
            b.Pos += b.Vel * dt;   // collisions added in M1
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "Arkanoid game/backend"
git commit -m "feat(arkanoid-core): GameInstance with serve + straight-line tick"
```

---

## Task 0.6: Wire protocol — `InputCommand`, `Snapshot`, JSON

**Files:**
- Create: `backend/Arkanoid.Core/Net/InputCommand.cs`, `Net/Snapshot.cs`
- Create: `contract/protocol.md`
- Test: extend `backend/Arkanoid.Tests/GameInstanceTests.cs` (snapshot round-trip)

- [ ] **Step 1: Write the failing test (append to `GameInstanceTests.cs`)**

```csharp
    [Fact]
    public void Snapshot_SerializesEntitiesAndPhase()
    {
        var g = MakeInstance();
        g.Serve();
        var snap = Snapshot.From(g, tick: 1);
        var json = System.Text.Json.JsonSerializer.Serialize(snap);
        Assert.Contains("\"phase\"", json);
        Assert.Contains("\"balls\"", json);
        Assert.Contains("\"blocks\"", json);
        Assert.Single(snap.Balls);
        Assert.Equal(1, snap.Blocks.Count); // level "A.." in row1 = 1 block
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: FAIL — `Snapshot` not found.

- [ ] **Step 3: Implement `InputCommand`**

`Arkanoid.Core/Net/InputCommand.cs`:
```csharp
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Net;

public enum InputKind { PaddleX, Serve, CastImbueIgnite, CastFireball, Cheat }

public sealed class InputCommand
{
    [JsonPropertyName("kind")] public InputKind Kind { get; set; }
    [JsonPropertyName("x")] public double X { get; set; }            // for PaddleX
    [JsonPropertyName("cheat")] public string? Cheat { get; set; }  // cheat op name
    [JsonPropertyName("value")] public double Value { get; set; }   // cheat arg
}
```

- [ ] **Step 4: Implement `Snapshot`**

`Arkanoid.Core/Net/Snapshot.cs`:
```csharp
using System.Text.Json.Serialization;
using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Net;

public sealed class BallDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("ignited")] public bool Ignited { get; set; }
}

public sealed class BlockDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("hp")] public int Hp { get; set; }
    [JsonPropertyName("maxHp")] public int MaxHp { get; set; }
    [JsonPropertyName("sprite")] public string Sprite { get; set; } = "";
}

public sealed class EventDto
{
    [JsonPropertyName("type")] public string Type { get; set; } = ""; // e.g. blockDestroyed, spellCast
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
}

public sealed class Snapshot
{
    [JsonPropertyName("tick")] public long Tick { get; set; }
    [JsonPropertyName("phase")] public string Phase { get; set; } = "";
    [JsonPropertyName("lives")] public int Lives { get; set; }
    [JsonPropertyName("spareBalls")] public int SpareBalls { get; set; }
    [JsonPropertyName("mana")] public double Mana { get; set; }
    [JsonPropertyName("manaMax")] public double ManaMax { get; set; }
    [JsonPropertyName("boardW")] public double BoardW { get; set; }
    [JsonPropertyName("boardH")] public double BoardH { get; set; }
    [JsonPropertyName("paddleX")] public double PaddleX { get; set; }
    [JsonPropertyName("paddleW")] public double PaddleW { get; set; }
    [JsonPropertyName("paddleH")] public double PaddleH { get; set; }
    [JsonPropertyName("cellSize")] public double CellSize { get; set; }
    [JsonPropertyName("balls")] public List<BallDto> Balls { get; set; } = new();
    [JsonPropertyName("blocks")] public List<BlockDto> Blocks { get; set; } = new();
    [JsonPropertyName("events")] public List<EventDto> Events { get; set; } = new();

    public static Snapshot From(GameInstance g, long tick)
    {
        var s = new Snapshot {
            Tick = tick, Phase = g.Phase.ToString(),
            Lives = g.Lives, SpareBalls = g.SpareBalls,
            Mana = g.ManaValue, ManaMax = g.Config.ManaMax,
            BoardW = g.Level.Grid.Width, BoardH = g.Level.Grid.Height,
            PaddleX = g.Paddle.Center.X, PaddleW = g.Paddle.Width, PaddleH = g.Paddle.Height,
            CellSize = g.Config.CellSize
        };
        foreach (var b in g.Balls)
            s.Balls.Add(new BallDto { Id = b.Id, X = b.Pos.X, Y = b.Pos.Y, Ignited = b.IgniteHitsLeft > 0 });
        foreach (var blk in g.Blocks)
        {
            if (blk.Dead) continue;
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            s.Blocks.Add(new BlockDto { Id = blk.Id, X = c.X, Y = c.Y, Hp = blk.Hp, MaxHp = blk.MaxHp, Sprite = blk.Sprite });
        }
        s.Events.AddRange(g.DrainEvents());
        return s;
    }
}
```

- [ ] **Step 5: Add the members `Snapshot.From` depends on, to `GameInstance`**

Add to `Arkanoid.Core/Sim/GameInstance.cs`:
```csharp
    // --- resources/events surface (mana fully wired in Task 1.5) ---
    public double ManaValue { get; internal set; } = 0;
    private readonly List<Arkanoid.Core.Net.EventDto> _events = new();
    public void RaiseEvent(string type, double x, double y)
        => _events.Add(new Arkanoid.Core.Net.EventDto { Type = type, X = x, Y = y });
    public List<Arkanoid.Core.Net.EventDto> DrainEvents()
    { var copy = new List<Arkanoid.Core.Net.EventDto>(_events); _events.Clear(); return copy; }
```

- [ ] **Step 6: Write the contract spec**

`contract/protocol.md`:
```markdown
# Arkanoid WS Protocol (v0)

Transport: WebSocket text frames, JSON. One connection = one game session.

## Client → Server: InputCommand
{ "kind": "PaddleX|Serve|CastImbueIgnite|CastFireball|Cheat", "x": <num>, "cheat": "<op>", "value": <num> }

Cheat ops (dev only): "clearAllButN" (value=N kept), "winNow", "loseNow", "setSeed" (value=seed), "setMana" (value), "addLife"/"loseBall".

## Server → Client: Snapshot (one per tick, ~60/s)
{ tick, phase, lives, spareBalls, mana, manaMax, boardW, boardH, paddleX, paddleW, paddleH, cellSize,
  balls:[{id,x,y,ignited}], blocks:[{id,x,y,hp,maxHp,sprite}], events:[{type,x,y}] }

Coordinates are sim units (origin top-left, +Y down). The renderer scales to canvas.
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add "Arkanoid game/backend" "Arkanoid game/contract"
git commit -m "feat(arkanoid): WS protocol — InputCommand + Snapshot + contract spec"
```

---

## Task 0.7: Server host — WebSocket + 60 Hz session loop

**Files:**
- Create: `backend/Arkanoid.Server/GameSession.cs`
- Modify: `backend/Arkanoid.Server/Program.cs`

- [ ] **Step 1: Implement `GameSession`**

`Arkanoid.Server/GameSession.cs`:
```csharp
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
```

- [ ] **Step 2: Implement `Program.cs`**

`Arkanoid.Server/Program.cs`:
```csharp
using System.Net.WebSockets;
using Arkanoid.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();
app.UseWebSockets();

// config dir is ../../config relative to the server project at runtime
var configRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config"));
if (!Directory.Exists(configRoot))
    configRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "config"));

app.MapGet("/", () => "Arkanoid server up");

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    var levelId = context.Request.Query["level"].FirstOrDefault() ?? "hell-1";
    var seed = int.TryParse(context.Request.Query["seed"].FirstOrDefault(), out var s) ? s : 1;
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var session = new GameSession(socket, configRoot);
    await session.RunAsync(levelId, seed, context.RequestAborted);
});

app.Run("http://localhost:5080");
```

- [ ] **Step 3: Add temporary no-op stubs so the server compiles now (real impls in M1)**

Append to `Arkanoid.Core/Sim/GameInstance.cs` (these get real bodies in Tasks 1.3/1.5/1.6):
```csharp
    public void CastIgnite() { /* Task 1.6 */ }
    public void CastFireball() { /* Task 1.5 */ }
    public void ApplyCheat(string op, double value) { /* Task 1.4 */ }
```

- [ ] **Step 4: Run the server manually to verify**

Run: `dotnet run --project backend/Arkanoid.Server`
Expected: console shows `Now listening on: http://localhost:5080`. Open `http://localhost:5080/` → "Arkanoid server up". Stop with Ctrl-C.

- [ ] **Step 5: Commit**

```bash
git add "Arkanoid game/backend"
git commit -m "feat(arkanoid-server): WS endpoint + 60Hz GameSession loop"
```

---

## Task 0.8: Frontend scaffold — Vite + PixiJS renderer + test hooks

**Files:**
- Create: `frontend/package.json`, `vite.config.ts`, `tsconfig.json`, `index.html`, `src/main.ts`, `src/net/Connection.ts`, `src/render/Renderer.ts`, `src/render/textures.ts`, `src/input/PaddleInput.ts`, `src/scenes/MenuScene.ts`, `src/scenes/BattleScene.ts`, `src/testhooks.ts`

- [ ] **Step 1: Scaffold the frontend**

Run from `Arkanoid game/frontend`:
```bash
npm create vite@latest . -- --template vanilla-ts
npm install
npm install pixi.js@7
```
Then delete the template files `src/counter.ts`, `src/style.css` imports, and `src/typescript.svg` if present (keep `src/main.ts`, we overwrite it).

- [ ] **Step 2: `index.html`**

`frontend/index.html`:
```html
<!doctype html>
<html>
  <head><meta charset="UTF-8" /><title>Arkanoid RPG</title>
    <style>html,body{margin:0;background:#0b0b12;overflow:hidden}#app{width:100vw;height:100vh}</style>
  </head>
  <body><div id="app"></div><script type="module" src="/src/main.ts"></script></body>
</html>
```

- [ ] **Step 3: `vite.config.ts` (fixed port for tests)**

`frontend/vite.config.ts`:
```typescript
import { defineConfig } from "vite";
export default defineConfig({ server: { port: 5173, strictPort: true } });
```

- [ ] **Step 4: `Connection.ts` (WS client, latest snapshot + command sender)**

`frontend/src/net/Connection.ts`:
```typescript
export interface Snapshot {
  tick: number; phase: string; lives: number; spareBalls: number;
  mana: number; manaMax: number; boardW: number; boardH: number;
  paddleX: number; paddleW: number; paddleH: number; cellSize: number;
  balls: { id: number; x: number; y: number; ignited: boolean }[];
  blocks: { id: number; x: number; y: number; hp: number; maxHp: number; sprite: string }[];
  events: { type: string; x: number; y: number }[];
}

export class Connection {
  private ws: WebSocket;
  latest: Snapshot | null = null;
  onSnapshot: ((s: Snapshot) => void) | null = null;

  constructor(level: string, seed: number) {
    this.ws = new WebSocket(`ws://localhost:5080/ws?level=${level}&seed=${seed}`);
    this.ws.onmessage = (e) => {
      const s = JSON.parse(e.data) as Snapshot;
      this.latest = s;
      this.onSnapshot?.(s);
    };
  }
  private send(o: object) { if (this.ws.readyState === 1) this.ws.send(JSON.stringify(o)); }
  paddleX(x: number) { this.send({ kind: "PaddleX", x }); }
  serve() { this.send({ kind: "Serve" }); }
  castIgnite() { this.send({ kind: "CastImbueIgnite" }); }
  castFireball() { this.send({ kind: "CastFireball" }); }
  cheat(op: string, value = 0) { this.send({ kind: "Cheat", cheat: op, value }); }
  whenReady(cb: () => void) {
    if (this.ws.readyState === 1) cb(); else this.ws.addEventListener("open", cb, { once: true });
  }
}
```

- [ ] **Step 5: `textures.ts` (load a few real Hell sprites; fallback to tinted rects)**

`frontend/src/render/textures.ts`:
```typescript
import { Texture } from "pixi.js";

// Maps backend sprite keys -> source PNGs in the old art set.
// During M1 we load a small handful; full atlas packing is a later milestone.
const SRC: Record<string, string> = {
  HellStandart: "/art/HellStandart.png",
  HellStandart2: "/art/HellStandart2.png",
  Ball: "/art/Ball.png",
  Paddle: "/art/Paddle.png",
};

const cache = new Map<string, Texture>();
export function tex(key: string): Texture {
  if (cache.has(key)) return cache.get(key)!;
  const t = SRC[key] ? Texture.from(SRC[key]) : Texture.WHITE;
  cache.set(key, t);
  return t;
}
```
> Place a few PNGs under `frontend/public/art/` copied from `Sprites/Locationes/Objects/Location_1_Hell/` and `Sprites/Heroes/.../Ball`. If a key is missing, `Texture.WHITE` renders a tinted rectangle — tests still pass on geometry.

- [ ] **Step 6: `Renderer.ts` (draw one snapshot)**

`frontend/src/render/Renderer.ts`:
```typescript
import { Application, Container, Graphics, Sprite } from "pixi.js";
import type { Snapshot } from "../net/Connection";
import { tex } from "./textures";

export class Renderer {
  app: Application;
  private world = new Container();
  private blocks = new Container();
  private fx = new Container();
  private paddle = new Graphics();
  private balls = new Container();

  constructor(host: HTMLElement) {
    this.app = new Application({ resizeTo: host, background: "#0b0b12", antialias: true });
    host.appendChild(this.app.view as HTMLCanvasElement);
    this.world.addChild(this.blocks, this.balls, this.paddle, this.fx);
    this.app.stage.addChild(this.world);
  }

  private fit(s: Snapshot) {
    const scale = Math.min(this.app.screen.width / s.boardW, this.app.screen.height / s.boardH) * 0.95;
    this.world.scale.set(scale);
    this.world.position.set(
      (this.app.screen.width - s.boardW * scale) / 2,
      (this.app.screen.height - s.boardH * scale) / 2
    );
  }

  draw(s: Snapshot) {
    this.fit(s);
    // blocks
    this.blocks.removeChildren();
    for (const b of s.blocks) {
      const sp = new Sprite(tex(b.sprite));
      sp.anchor.set(0.5);
      sp.width = s.cellSize; sp.height = s.cellSize;
      sp.position.set(b.x, b.y);
      sp.alpha = 0.4 + 0.6 * (b.hp / b.maxHp);
      this.blocks.addChild(sp);
    }
    // paddle
    this.paddle.clear();
    this.paddle.beginFill(0x7fd1ff).drawRect(
      s.paddleX - s.paddleW / 2, (s.boardH + s.cellSize) - s.paddleH / 2, s.paddleW, s.paddleH
    ).endFill();
    // balls
    this.balls.removeChildren();
    for (const ball of s.balls) {
      const g = new Graphics();
      g.beginFill(ball.ignited ? 0xff7a2a : 0xffffff).drawCircle(ball.x, ball.y, s.cellSize * 0.25).endFill();
      this.balls.addChild(g);
    }
  }
}
```

- [ ] **Step 7: `PaddleInput.ts`**

`frontend/src/input/PaddleInput.ts`:
```typescript
import type { Connection, Snapshot } from "../net/Connection";

export function attachPaddleInput(canvas: HTMLCanvasElement, conn: Connection, getSnap: () => Snapshot | null) {
  canvas.addEventListener("pointermove", (e) => {
    const s = getSnap(); if (!s) return;
    const rect = canvas.getBoundingClientRect();
    const scale = Math.min(rect.width / s.boardW, rect.height / s.boardH) * 0.95;
    const offX = (rect.width - s.boardW * scale) / 2;
    const simX = (e.clientX - rect.left - offX) / scale;
    conn.paddleX(simX);
  });
  window.addEventListener("keydown", (e) => {
    if (e.code === "Space") conn.serve();
    if (e.key === "q" || e.key === "Q") conn.castIgnite();
    if (e.key === "e" || e.key === "E") conn.castFireball();
  });
}
```

- [ ] **Step 8: Scenes + `main.ts` + `testhooks.ts`**

`frontend/src/scenes/MenuScene.ts`:
```typescript
export function mountMenu(host: HTMLElement) {
  const el = document.createElement("div");
  el.id = "menu";
  el.style.cssText = "color:#e8e8ff;font-family:sans-serif;text-align:center;padding-top:20vh";
  el.innerHTML = `<h1>ARKANOID RPG</h1>
    <button id="btn-play" style="font-size:20px;padding:10px 24px">Play (Hell I)</button>`;
  host.appendChild(el);
  document.getElementById("btn-play")!.addEventListener("click", () => {
    location.search = "?scene=battle&level=hell-1";
  });
}
```

`frontend/src/scenes/BattleScene.ts`:
```typescript
import { Renderer } from "../render/Renderer";
import { Connection } from "../net/Connection";
import { attachPaddleInput } from "../input/PaddleInput";
import { installTestHooks } from "../testhooks";

export function mountBattle(host: HTMLElement, level: string, seed: number) {
  const r = new Renderer(host);
  const conn = new Connection(level, seed);
  conn.onSnapshot = (s) => r.draw(s);
  attachPaddleInput(r.app.view as HTMLCanvasElement, conn, () => conn.latest);
  installTestHooks(conn);
  // auto-serve shortly after connect so the ball is live for tests/play
  conn.whenReady(() => setTimeout(() => conn.serve(), 300));
}
```

`frontend/src/testhooks.ts`:
```typescript
import type { Connection, Snapshot } from "./net/Connection";

declare global { interface Window { __game: GameTestApi } }
export interface GameTestApi {
  getState: () => Snapshot | null;
  cheat: (op: string, value?: number) => void;
  serve: () => void;
  castIgnite: () => void;
  castFireball: () => void;
  setPaddleX: (x: number) => void;
}
export function installTestHooks(conn: Connection) {
  window.__game = {
    getState: () => conn.latest,
    cheat: (op, value = 0) => conn.cheat(op, value),
    serve: () => conn.serve(),
    castIgnite: () => conn.castIgnite(),
    castFireball: () => conn.castFireball(),
    setPaddleX: (x) => conn.paddleX(x),
  };
}
```

`frontend/src/main.ts`:
```typescript
import { mountMenu } from "./scenes/MenuScene";
import { mountBattle } from "./scenes/BattleScene";

const host = document.getElementById("app")!;
const q = new URLSearchParams(location.search);
const scene = q.get("scene") ?? "menu";
const level = q.get("level") ?? "hell-1";
const seed = Number(q.get("seed") ?? "1");

if (scene === "battle") mountBattle(host, level, seed);
else mountMenu(host);
```

- [ ] **Step 9: Manual smoke**

Run backend (`dotnet run --project backend/Arkanoid.Server`) and frontend (`npm run dev` in `frontend/`). Open `http://localhost:5173` → menu with a Play button. Click → battle renders blocks + paddle + a moving ball.

- [ ] **Step 10: Commit**

```bash
git add "Arkanoid game/frontend"
git commit -m "feat(arkanoid-frontend): Vite+PixiJS renderer, scenes, WS connection, test hooks"
```

---

## Task 0.9: Playwright harness + first scenario (menu)

**Files:**
- Create: `tests/playwright.config.ts`, `tests/helpers/servers.ts`, `tests/helpers/game.ts`, `tests/menu.spec.ts`
- Create: `tests/package.json`

- [ ] **Step 1: Init Playwright**

Run from `Arkanoid game/tests`:
```bash
npm init -y
npm install -D @playwright/test
npx playwright install chromium
```

- [ ] **Step 2: `playwright.config.ts` — boots backend + frontend as webServers**

`tests/playwright.config.ts`:
```typescript
import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  timeout: 30_000,
  use: { baseURL: "http://localhost:5173", headless: true },
  webServer: [
    {
      command: "dotnet run --project ../backend/Arkanoid.Server",
      url: "http://localhost:5080/",
      reuseExistingServer: true,
      timeout: 60_000,
    },
    {
      command: "npm run dev",
      cwd: "../frontend",
      url: "http://localhost:5173",
      reuseExistingServer: true,
      timeout: 60_000,
    },
  ],
});
```

- [ ] **Step 3: `helpers/game.ts` — page-side pre-setup helpers**

`tests/helpers/game.ts`:
```typescript
import { Page, expect } from "@playwright/test";

/** Open a battle pre-set to a given level/seed and wait until the sim is streaming. */
export async function openBattle(page: Page, level = "hell-1", seed = 1) {
  await page.goto(`/?scene=battle&level=${level}&seed=${seed}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await page.waitForFunction(() => (window as any).__game.getState()?.balls.length > 0);
}

export async function getState(page: Page) {
  return await page.evaluate(() => (window as any).__game.getState());
}

export async function cheat(page: Page, op: string, value = 0) {
  await page.evaluate(([o, v]) => (window as any).__game.cheat(o, v), [op, value] as const);
}

/** Wait until the snapshot satisfies a predicate (polls the live sim state). */
export async function waitForPhase(page: Page, phase: string) {
  await page.waitForFunction((p) => (window as any).__game.getState()?.phase === p, phase, { timeout: 10_000 });
}
```

- [ ] **Step 4: `menu.spec.ts` — the first per-part scenario**

`tests/menu.spec.ts`:
```typescript
import { test, expect } from "@playwright/test";

test("menu renders with a play button", async ({ page }) => {
  await page.goto("/?scene=menu");
  await expect(page.locator("#menu h1")).toHaveText("ARKANOID RPG");
  await expect(page.locator("#btn-play")).toBeVisible();
});

test("clicking play enters a battle", async ({ page }) => {
  await page.goto("/?scene=menu");
  await page.locator("#btn-play").click();
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.length).toBeGreaterThan(0);
});
```

- [ ] **Step 5: Run the harness**

Run from `tests/`: `npx playwright test menu.spec.ts`
Expected: 2 passed. (Playwright auto-starts backend + frontend.)

- [ ] **Step 6: Commit — M0 GATE**

M0 gate: board renders in browser via WS, a static block is drawn from real art, Core unit tests pass headless, and the first Playwright scenario (menu) is green.
```bash
git add "Arkanoid game/tests"
git commit -m "test(arkanoid): Playwright harness + menu scenario (M0 gate)"
```

---

## Task 0.10: Excessive structured logging layer (Core → Server JSONL → Browser → Playwright)

**Files:**
- Create: `backend/Arkanoid.Core/Sim/SimLog.cs`, `backend/Arkanoid.Server/FileSimLog.cs`, `frontend/src/log.ts`, `tests/helpers/fixtures.ts`, `Arkanoid game/.gitignore`
- Modify: `backend/Arkanoid.Core/Sim/GameInstance.cs` (inject `ISimLog`, add `TickCount`, instrument M0 methods), `backend/Arkanoid.Server/GameSession.cs` + `Program.cs` (create per-session log, read `run` param), `frontend/src/net/Connection.ts`, `frontend/src/scenes/BattleScene.ts`, `frontend/src/main.ts`, `frontend/src/testhooks.ts`, `tests/helpers/game.ts`, and the spec import lines.

> Philosophy: log **every state transition** with enough context (`tick`, ids, velocities, hp, mana) to reconstruct what happened without a debugger. Per-tick kinematics are gated behind a `Verbose` flag (on by default in dev/test); semantic events (collisions, damage, casts, win/lose) always log.

- [ ] **Step 1: Implement Core `ISimLog`**

`Arkanoid.Core/Sim/SimLog.cs`:
```csharp
namespace Arkanoid.Core.Sim;

/// <summary>Host-agnostic structured log sink. Core never touches Console/files directly.</summary>
public interface ISimLog
{
    bool Verbose { get; }
    void Log(long tick, string cat, string msg, string data = "");
}

/// <summary>Default no-op sink (unit tests run silent unless they inject a capture).</summary>
public sealed class NullSimLog : ISimLog
{
    public static readonly NullSimLog Instance = new();
    public bool Verbose => false;
    public void Log(long tick, string cat, string msg, string data = "") { }
}
```

- [ ] **Step 2: Inject the logger into `GameInstance` + add `TickCount`, instrument M0 methods**

In `Arkanoid.Core/Sim/GameInstance.cs`, change the constructor and add the field + tick counter. Replace the constructor signature/header:
```csharp
    private readonly ISimLog _log;
    public long TickCount { get; private set; }

    public GameInstance(LevelData level, SimConfig config, int seed, ISimLog? log = null)
    {
        Level = level; Config = config; Rng = new Rng(seed); _log = log ?? NullSimLog.Instance;
        Lives = config.StartLives;
        SpareBalls = config.StartBalls;
        Paddle = new Paddle {
            Width = config.PaddleWidth,
            Height = config.PaddleHeight,
            Center = new Vec2(level.Grid.Width / 2.0, level.Grid.Height + config.CellSize)
        };
        SpawnBallOnPaddle();
        _log.Log(0, "init", "instance created", $"level={level.Id} seed={seed} blocks={Blocks.Count} lives={Lives} balls={SpareBalls}");
    }
```
Instrument `Serve`:
```csharp
    public void Serve()
    {
        if (Phase != GamePhase.Serving) return;
        var lean = Rng.Range(-0.25, 0.25);
        Balls[0].Vel = new Vec2(lean, -1).Normalized() * Config.BallSpeed;
        Phase = GamePhase.Playing;
        _log.Log(TickCount, "serve", "ball launched", $"lean={lean:F3} vx={Balls[0].Vel.X:F1} vy={Balls[0].Vel.Y:F1}");
    }
```
Instrument the top of `Tick` (increment counter + verbose heartbeat):
```csharp
    public void Tick(double dt)
    {
        if (Phase != GamePhase.Playing) return;
        TickCount++;
        if (_log.Verbose)
            _log.Log(TickCount, "tick", "", $"balls={Balls.Count(b=>b.Alive)} mana={ManaValue:F0} blocks={Blocks.Count(b=>!b.Dead)}");
        RegenMana(dt);
        foreach (var b in Balls)
        {
            if (!b.Alive) continue;
            b.Pos += b.Vel * dt;
            if (_log.Verbose) _log.Log(TickCount, "ball", "move", $"id={b.Id} x={b.Pos.X:F1} y={b.Pos.Y:F1}");
            Arkanoid.Core.Physics.BallPhysics.ResolveWalls(b, Level.Grid.Width, Config);
            if (Arkanoid.Core.Physics.BallPhysics.ResolvePaddle(b, Paddle, Config, out var t))
                OnPaddleHit(b, t);
            ResolveBlocks(b);
        }
        UpdateProjectiles(dt);
        ResolveDrainAndWin();
    }
```
> Note: `UpdateProjectiles` is added in Task 1.5. If you reach this task before 1.5, temporarily keep the existing `Tick` body and only add the `TickCount++` + heartbeat lines; the rest merges naturally as M1 tasks land.

- [ ] **Step 3: Implement the server `FileSimLog`**

`Arkanoid.Server/FileSimLog.cs`:
```csharp
using System.Text.Json;
using Arkanoid.Core.Sim;
namespace Arkanoid.Server;

/// <summary>Writes one JSONL line per record to logs/&lt;runId&gt;.jsonl. Thread-safe (sim + receive loops share it).</summary>
public sealed class FileSimLog : ISimLog, IDisposable
{
    private readonly StreamWriter _w;
    private readonly object _gate = new();
    public bool Verbose { get; }

    public FileSimLog(string filePath, bool verbose)
    {
        Verbose = verbose;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        _w = new StreamWriter(filePath, append: false) { AutoFlush = true };
    }

    public void Log(long tick, string cat, string msg, string data = "")
    {
        var line = JsonSerializer.Serialize(new {
            ts = DateTime.UtcNow.ToString("HH:mm:ss.fff"), t = tick, cat, msg, data
        });
        lock (_gate) _w.WriteLine(line);
    }

    public void Note(string cat, string msg, string data = "") => Log(-1, cat, msg, data);
    public void Dispose() { lock (_gate) _w.Dispose(); }

    /// <summary>Deterministic log dir = the server project's /logs, independent of CWD.</summary>
    public static string DirFor() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs"));
}
```

- [ ] **Step 4: Wire the log through `GameSession` + `Program.cs`**

In `Arkanoid.Server/GameSession.cs`: accept a `runId`, build the log, pass it to `GameInstance`, and log lifecycle + commands. Change `RunAsync` signature and `LoadLevel`, and add logging in `Apply`/`ReceiveLoop`:
```csharp
    private FileSimLog _log = null!;

    public async Task RunAsync(string levelId, int seed, string runId, CancellationToken ct)
    {
        var path = Path.Combine(FileSimLog.DirFor(), $"{runId}.jsonl");
        _log = new FileSimLog(path, verbose: true);
        _log.Note("conn", "session open", $"run={runId} level={levelId} seed={seed}");
        LoadLevel(levelId, seed);
        // ... unchanged loop body ...
        // (after the while loop)
        _log.Note("conn", "session close", $"ticks={_tick}");
        _log.Dispose();
        try { await recv; } catch { }
    }

    private void LoadLevel(string levelId, int seed)
    {
        var catalog = BlockCatalog.FromFile(Path.Combine(_configRoot, "blocks.json"));
        var level = LevelLoader.FromFile(Path.Combine(_configRoot, "levels", $"{levelId}.json"), catalog);
        _game = new GameInstance(level, SimConfig.Default, seed, _log);   // <-- inject log
    }
```
Add a log line in `Apply` (so every command is visible):
```csharp
    private void Apply(InputCommand c)
    {
        _log.Note("cmd", c.Kind.ToString(), $"x={c.X:F1} cheat={c.Cheat} value={c.Value}");
        switch (c.Kind) { /* unchanged */ }
    }
```
In `Program.cs`, read the `run` query param and pass it through:
```csharp
    var runId = context.Request.Query["run"].FirstOrDefault() ?? $"sess-{DateTime.UtcNow:HHmmss-fff}";
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var session = new GameSession(socket, configRoot);
    await session.RunAsync(levelId, seed, runId, context.RequestAborted);
```

- [ ] **Step 5: Frontend ring-buffer logger**

`frontend/src/log.ts`:
```typescript
export interface LogEntry { ts: number; tag: string; msg: string; data?: unknown }
const BUF: LogEntry[] = [];
const MAX = 3000;

export function log(tag: string, msg: string, data?: unknown) {
  BUF.push({ ts: Date.now(), tag, msg, data });
  if (BUF.length > MAX) BUF.shift();
  // mirror to console so Playwright's page.on('console') captures it live
  console.info(`[ark:${tag}] ${msg}`, data ?? "");
}
export function getLogs(): LogEntry[] { return BUF.slice(); }
```
Instrument `frontend/src/net/Connection.ts` — accept a `run` id, log lifecycle + commands + phase changes. Add `import { log } from "../log";`, store `runId`, and:
```typescript
  readonly runId: string;
  private lastPhase = "";

  constructor(level: string, seed: number, runId: string) {
    this.runId = runId;
    this.ws = new WebSocket(`ws://localhost:5080/ws?level=${level}&seed=${seed}&run=${runId}`);
    log("net", "connecting", { level, seed, runId });
    this.ws.onopen = () => log("net", "open");
    this.ws.onclose = () => log("net", "close");
    this.ws.onerror = () => log("net", "error");
    this.ws.onmessage = (e) => {
      const s = JSON.parse(e.data) as Snapshot;
      this.latest = s;
      if (s.phase !== this.lastPhase) { log("phase", s.phase, { tick: s.tick, lives: s.lives, balls: s.spareBalls }); this.lastPhase = s.phase; }
      this.onSnapshot?.(s);
    };
  }
```
And log each command in the `send` helper:
```typescript
  private send(o: any) { if (this.ws.readyState === 1) { log("cmd", o.kind, o); this.ws.send(JSON.stringify(o)); } }
```

- [ ] **Step 6: Thread `run` id through scenes + expose to tests**

`frontend/src/main.ts` — read `run` and pass it:
```typescript
const run = q.get("run") ?? `dev-${Date.now()}`;
if (scene === "battle") mountBattle(host, level, seed, run);
else mountMenu(host);
```
`frontend/src/scenes/BattleScene.ts` — accept and forward `run`:
```typescript
export function mountBattle(host: HTMLElement, level: string, seed: number, run: string) {
  const r = new Renderer(host);
  const conn = new Connection(level, seed, run);
  conn.onSnapshot = (s) => r.draw(s);
  attachPaddleInput(r.app.view as HTMLCanvasElement, conn, () => conn.latest);
  installTestHooks(conn);
  conn.whenReady(() => setTimeout(() => conn.serve(), 300));
}
```
`frontend/src/testhooks.ts` — expose `runId` + `getLogs`:
```typescript
import { getLogs } from "./log";
// ...inside installTestHooks, add to the window.__game object:
    runId: conn.runId,
    getLogs: () => getLogs(),
```
(and add `runId: string; getLogs: () => unknown[];` to the `GameTestApi` interface.)

- [ ] **Step 7: Playwright capture fixture (auto-attach logs on failure)**

`tests/helpers/fixtures.ts`:
```typescript
import { test as base, expect } from "@playwright/test";
import * as fs from "fs";
import * as path from "path";

// Extends `page` to capture browser console + attach client ring buffer and the
// backend session JSONL to the report. Attaches on EVERY test (handy on pass too,
// essential on fail) so an AI can read exactly what happened.
export const test = base.extend({
  page: async ({ page }, use, testInfo) => {
    const console_: string[] = [];
    page.on("console", (m) => console_.push(`[${m.type()}] ${m.text()}`));
    page.on("pageerror", (e) => console_.push(`[pageerror] ${e.message}`));

    await use(page);

    await testInfo.attach("client-console.txt", { body: console_.join("\n"), contentType: "text/plain" });
    try {
      const ring = await page.evaluate(() => (window as any).__game?.getLogs?.() ?? []);
      await testInfo.attach("client-ring.json", { body: JSON.stringify(ring, null, 2), contentType: "application/json" });
      const runId = await page.evaluate(() => (window as any).__game?.runId ?? "");
      if (runId) {
        const file = path.resolve(__dirname, "..", "..", "backend", "Arkanoid.Server", "logs", `${runId}.jsonl`);
        if (fs.existsSync(file)) await testInfo.attach("server-sim.jsonl", { path: file, contentType: "text/plain" });
      }
    } catch { /* page may already be closed */ }
  },
});
export { expect };
```

- [ ] **Step 8: Route specs + helpers through the fixture**

In **every** `tests/*.spec.ts`, change the import line:
```typescript
// from:
import { test, expect } from "@playwright/test";
// to:
import { test, expect } from "./helpers/fixtures";
```
Update `tests/helpers/game.ts` `openBattle` to pass a unique `run` id (so each test gets its own JSONL):
```typescript
export async function openBattle(page: Page, level = "hell-1", seed = 1) {
  const run = `${level}-${seed}-${Date.now()}`;
  await page.goto(`/?scene=battle&level=${level}&seed=${seed}&run=${run}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await page.waitForFunction(() => (window as any).__game.getState()?.balls.length > 0);
}
```

- [ ] **Step 9: `.gitignore` (keep logs/artifacts out of git)**

`Arkanoid game/.gitignore`:
```gitignore
# build
**/bin/
**/obj/
frontend/dist/
frontend/node_modules/
tests/node_modules/

# logs & test artifacts
backend/Arkanoid.Server/logs/
tests/test-results/
tests/playwright-report/
```

- [ ] **Step 10: Verify the log layer end to end**

Run from `tests/`: `npx playwright test menu.spec.ts battle-start.spec.ts`
Then confirm artifacts exist:
- `backend/Arkanoid.Server/logs/hell-1-1-*.jsonl` contains lines like `{"ts":...,"t":1,"cat":"serve","msg":"ball launched",...}` and `{"cat":"cmd","msg":"PaddleX",...}`.
- The HTML report (`npx playwright show-report`) shows `client-console.txt`, `client-ring.json`, and `server-sim.jsonl` attachments on each test.

Force a failure to prove the diagnostic path: temporarily add `expect(1).toBe(2)` to `battle-start.spec.ts`, run it, open the report, confirm `server-sim.jsonl` is attached and readable, then revert.

- [ ] **Step 11: Commit**

```bash
git add "Arkanoid game"
git commit -m "feat(arkanoid): excessive structured logging (Core ISimLog -> server JSONL -> browser ring -> Playwright auto-attach)"
```

---

# PHASE M1 — Core breakout slice (Fire Mage / Hell)

> **Logging is now available.** Each M1 task below that adds new logic ends with a short **"Logging"** step adding the exact `_log.Log(...)` calls for that logic. Keep the discipline: every new transition gets a log line.

## Task 1.1: Ball ↔ walls + ball ↔ paddle (deflection + no-shallow-angle)

**Files:**
- Create: `backend/Arkanoid.Core/Physics/BallPhysics.cs`
- Modify: `backend/Arkanoid.Core/Sim/GameInstance.cs` (call physics in `Tick`)
- Test: `backend/Arkanoid.Tests/BallPhysicsTests.cs`

- [ ] **Step 1: Write the failing tests**

`Arkanoid.Tests/BallPhysicsTests.cs`:
```csharp
using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using Arkanoid.Core.Physics;
using Arkanoid.Core.Sim;
using Xunit;

public class BallPhysicsTests
{
    private static SimConfig Cfg => SimConfig.Default;

    [Fact]
    public void Ball_BouncesOffLeftWall()
    {
        var b = new Ball { Pos = new Vec2(5, 100), Vel = new Vec2(-200, 0), Radius = 8 };
        BallPhysics.ResolveWalls(b, boardW: 400, Cfg);
        Assert.True(b.Vel.X > 0); // reflected rightward
        Assert.True(b.Pos.X >= b.Radius);
    }

    [Fact]
    public void Ball_BouncesOffTopWall()
    {
        var b = new Ball { Pos = new Vec2(100, 4), Vel = new Vec2(0, -200), Radius = 8 };
        BallPhysics.ResolveWalls(b, boardW: 400, Cfg);
        Assert.True(b.Vel.Y > 0);
    }

    [Fact]
    public void PaddleHit_OnRightSide_PushesBallRight()
    {
        var paddle = new Paddle { Center = new Vec2(200, 300), Width = 96, Height = 16 };
        var b = new Ball { Pos = new Vec2(230, 290), Vel = new Vec2(0, 200), Radius = 8 };
        var hit = BallPhysics.ResolvePaddle(b, paddle, Cfg, out _);
        Assert.True(hit);
        Assert.True(b.Vel.Y < 0);  // bounced upward
        Assert.True(b.Vel.X > 0);  // right of center -> rightward
    }

    [Fact]
    public void PaddleHit_NeverProducesShallowAngle()
    {
        var paddle = new Paddle { Center = new Vec2(200, 300), Width = 96, Height = 16 };
        var b = new Ball { Pos = new Vec2(247, 290), Vel = new Vec2(0, 200), Radius = 8 }; // far edge
        BallPhysics.ResolvePaddle(b, paddle, Cfg, out _);
        var ratio = System.Math.Abs(b.Vel.Y) / b.Vel.Length;
        Assert.True(ratio >= Cfg.MinVerticalRatio - 1e-9);
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: FAIL — `BallPhysics` missing.

- [ ] **Step 3: Implement `BallPhysics` (walls + paddle)**

`Arkanoid.Core/Physics/BallPhysics.cs`:
```csharp
using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Physics;

public static class BallPhysics
{
    public static void ResolveWalls(Ball b, double boardW, SimConfig cfg)
    {
        if (b.Pos.X - b.Radius < 0) { b.Pos = new Vec2(b.Radius, b.Pos.Y); b.Vel = new Vec2(System.Math.Abs(b.Vel.X), b.Vel.Y); }
        else if (b.Pos.X + b.Radius > boardW) { b.Pos = new Vec2(boardW - b.Radius, b.Pos.Y); b.Vel = new Vec2(-System.Math.Abs(b.Vel.X), b.Vel.Y); }
        if (b.Pos.Y - b.Radius < 0) { b.Pos = new Vec2(b.Pos.X, b.Radius); b.Vel = new Vec2(b.Vel.X, System.Math.Abs(b.Vel.Y)); }
    }

    /// <summary>Paddle deflection by hit position. Returns true on contact; outputs normalized offset t in [-1,1].</summary>
    public static bool ResolvePaddle(Ball b, Paddle p, SimConfig cfg, out double t)
    {
        t = 0;
        var top = p.Center.Y - p.Height / 2;
        var half = p.Width / 2;
        bool overlapX = b.Pos.X >= p.Center.X - half - b.Radius && b.Pos.X <= p.Center.X + half + b.Radius;
        bool atTop = b.Vel.Y > 0 && b.Pos.Y + b.Radius >= top && b.Pos.Y <= p.Center.Y;
        if (!(overlapX && atTop)) return false;

        t = System.Math.Clamp((b.Pos.X - p.Center.X) / half, -1, 1);
        var maxRad = cfg.PaddleMaxDeflectAngleDeg * System.Math.PI / 180.0;
        var angle = t * maxRad;                    // 0 = straight up, ± = lean
        var speed = cfg.BallSpeed;
        var vx = System.Math.Sin(angle) * speed;
        var vy = -System.Math.Cos(angle) * speed;  // always upward
        b.Vel = ClampVertical(new Vec2(vx, vy), cfg);
        b.Pos = new Vec2(b.Pos.X, top - b.Radius - 0.1);
        return true;
    }

    /// <summary>Enforce a minimum vertical component so the ball never crawls horizontally.</summary>
    public static Vec2 ClampVertical(Vec2 v, SimConfig cfg)
    {
        var speed = v.Length;
        if (speed < 1e-6) return v;
        var ny = v.Y / speed;
        if (System.Math.Abs(ny) >= cfg.MinVerticalRatio) return v;
        var sign = ny < 0 ? -1 : 1;
        var vy = sign * cfg.MinVerticalRatio;
        var vx = System.Math.Sqrt(System.Math.Max(0, 1 - vy * vy)) * (v.X < 0 ? -1 : 1);
        return new Vec2(vx, vy) * speed;
    }
}
```

- [ ] **Step 4: Call physics from `GameInstance.Tick`**

Replace the `Tick` body in `Arkanoid.Core/Sim/GameInstance.cs`:
```csharp
    public void Tick(double dt)
    {
        if (Phase != GamePhase.Playing) return;
        RegenMana(dt); // no-op until Task 1.5 adds the body
        foreach (var b in Balls)
        {
            if (!b.Alive) continue;
            b.Pos += b.Vel * dt;
            Arkanoid.Core.Physics.BallPhysics.ResolveWalls(b, Level.Grid.Width, Config);
            if (Arkanoid.Core.Physics.BallPhysics.ResolvePaddle(b, Paddle, Config, out var t))
                OnPaddleHit(b, t); // body added in Task 1.5/1.6
            ResolveBlocks(b);      // body added in Task 1.2
        }
        ResolveDrainAndWin();      // body added in Task 1.3
    }
```
Add these stubs (real bodies later) so it compiles:
```csharp
    private void RegenMana(double dt) { /* Task 1.5 */ }
    private void OnPaddleHit(Ball b, double t) { /* Task 1.5/1.6 */ }
    private void ResolveBlocks(Ball b) { /* Task 1.2 */ }
    private void ResolveDrainAndWin() { /* Task 1.3 */ }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: PASS (all physics tests).

- [ ] **Step 6: Logging — instrument the paddle hit**

Give the `OnPaddleHit` stub a body that logs every deflect (Tasks 1.5/1.6 extend this method and must keep the log line):
```csharp
    private void OnPaddleHit(Ball b, double t)
    {
        _log.Log(TickCount, "paddle", "deflect", $"t={t:F2} vx={b.Vel.X:F1} vy={b.Vel.Y:F1}");
        // mana bonus (Task 1.5) + ignite imbue (Task 1.6) added later
    }
```
(Wall bounces are already visible via the verbose `ball`/`move` records from Task 0.10.)

- [ ] **Step 7: Commit**

```bash
git add "Arkanoid game/backend"
git commit -m "feat(arkanoid-core): deterministic wall + paddle collision (no-shallow-angle)"
```

---

## Task 1.2: Ball ↔ block collision (HP, destroy, event)

**Files:**
- Modify: `backend/Arkanoid.Core/Sim/GameInstance.cs` (`ResolveBlocks`)
- Test: append to `backend/Arkanoid.Tests/BallPhysicsTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `BallPhysicsTests.cs`:
```csharp
    [Fact]
    public void Ball_DamagesBlock_AndBouncesOff()
    {
        var catalog = Arkanoid.Core.Blocks.BlockCatalog.FromJson(
          "{\"types\":[{\"id\":\"b\",\"biome\":\"hell\",\"hp\":2,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = Arkanoid.Core.Grid.LevelLoader.FromJson(
          "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":3,\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
          catalog);
        var g = new GameInstance(level, SimConfig.Default, 1);
        g.Serve();
        var block = level.Blocks[0];
        var c = level.Grid.CellCenter(block.Col, block.Row);
        // place ball just under the block moving up
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + 6);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(1, block.Hp);            // took 1 damage
        Assert.True(g.Balls[0].Vel.Y > 0);    // bounced downward
    }
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test backend/Arkanoid.Tests` → FAIL (block keeps Hp 2 / no bounce; `ResolveBlocks` is a stub).

- [ ] **Step 3: Implement `ResolveBlocks` + `DamageBlock`**

Replace the `ResolveBlocks` stub in `GameInstance.cs`:
```csharp
    private void ResolveBlocks(Ball b)
    {
        var cell = Config.CellSize;
        foreach (var blk in Blocks)
        {
            if (blk.Dead) continue;
            var c = Level.Grid.CellCenter(blk.Col, blk.Row);
            var box = Arkanoid.Core.Math.Aabb.FromCenter(c, cell / 2, cell / 2);
            if (!box.IntersectsCircle(b.Pos, b.Radius)) continue;

            // reflect by dominant penetration axis
            var dx = b.Pos.X - c.X; var dy = b.Pos.Y - c.Y;
            if (System.Math.Abs(dx) / (cell / 2) > System.Math.Abs(dy) / (cell / 2))
                b.Vel = new Arkanoid.Core.Math.Vec2(System.Math.Sign(dx) * System.Math.Abs(b.Vel.X), b.Vel.Y);
            else
                b.Vel = new Arkanoid.Core.Math.Vec2(b.Vel.X, System.Math.Sign(dy) * System.Math.Abs(b.Vel.Y));

            DamageBlock(blk, Config.BallDamage, igniteSource: b.IgniteHitsLeft > 0);
            if (b.IgniteHitsLeft > 0) b.IgniteHitsLeft--; // imbue consumed per hit (Task 1.6)
            break; // one block per tick keeps it deterministic
        }
    }

    private void DamageBlock(Block blk, int dmg, bool igniteSource)
    {
        blk.Hp -= dmg;
        if (blk.Hp <= 0 && !blk.Dead)
        {
            blk.Dead = true;
            var c = Level.Grid.CellCenter(blk.Col, blk.Row);
            RaiseEvent("blockDestroyed", c.X, c.Y);
            ManaValue = System.Math.Min(Config.ManaMax, ManaValue + Config.ManaPerKill);
            // Ignite spread handled in Task 1.6
        }
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: PASS.

- [ ] **Step 5: Logging — instrument block damage**

Add a log line inside `DamageBlock`, right after `blk.Hp -= dmg;`:
```csharp
        _log.Log(TickCount, "block", blk.Hp <= 0 ? "destroyed" : "hit",
                 $"id={blk.Id} col={blk.Col} row={blk.Row} hp={blk.Hp} dmg={dmg} ignite={igniteSource}");
```

- [ ] **Step 6: Commit**

```bash
git add "Arkanoid game/backend"
git commit -m "feat(arkanoid-core): ball-block collision with HP, destroy, mana-on-kill"
```

---

## Task 1.3: Lives/Balls drain + win/lose conditions

**Files:**
- Modify: `backend/Arkanoid.Core/Sim/GameInstance.cs` (`ResolveDrainAndWin`)
- Test: `backend/Arkanoid.Tests/WinLoseTests.cs`

- [ ] **Step 1: Write the failing tests**

`Arkanoid.Tests/WinLoseTests.cs`:
```csharp
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

public class WinLoseTests
{
    private static GameInstance Make(string rows)
    {
        var catalog = BlockCatalog.FromJson(
          "{\"types\":[{\"id\":\"b\",\"biome\":\"hell\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
          $"{{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":3,\"rows_data\":[{rows}],\"legend\":{{\"A\":\"b\"}}}}",
          catalog);
        return new GameInstance(level, SimConfig.Default, 1);
    }

    [Fact]
    public void DrainingBall_DecrementsSpareBalls_AndReserves()
    {
        var g = Make("\".A.\",\"...\",\"...\"");
        g.Serve();
        g.Balls[0].Pos = new Vec2(50, g.Level.Grid.Height + 999); // below board
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(2, g.SpareBalls);              // 3 -> 2
        Assert.Equal(GamePhase.Serving, g.Phase);   // re-served
    }

    [Fact]
    public void DrainingLastBall_LosesTheLevel()
    {
        var g = Make("\".A.\",\"...\",\"...\"");
        for (int i = 0; i < 3; i++)
        {
            g.Serve();
            g.Balls[0].Pos = new Vec2(50, g.Level.Grid.Height + 999);
            g.Tick(SimConfig.Default.FixedDt);
        }
        Assert.Equal(GamePhase.Lost, g.Phase);
    }

    [Fact]
    public void ClearingAllNeedToKill_Wins()
    {
        var g = Make("\".A.\",\"...\",\"...\"");
        g.Serve();
        g.Level.Blocks[0].Dead = true;  // simulate the only block destroyed
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(GamePhase.Won, g.Phase);
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test backend/Arkanoid.Tests` → FAIL (`ResolveDrainAndWin` is a stub).

- [ ] **Step 3: Implement `ResolveDrainAndWin` + helpers**

Replace the `ResolveDrainAndWin` stub and add helpers in `GameInstance.cs`:
```csharp
    private void ResolveDrainAndWin()
    {
        // win check first
        if (!Blocks.Any(b => b.NeedToKill && !b.Dead))
        {
            Phase = GamePhase.Won;
            RaiseEvent("levelWon", 0, 0);
            return;
        }
        // drain check
        var drainLine = Level.Grid.Height + Config.CellSize * 2;
        foreach (var b in Balls)
            if (b.Alive && b.Pos.Y - b.Radius > drainLine) b.Alive = false;

        if (Balls.All(b => !b.Alive))
        {
            SpareBalls--;
            if (SpareBalls < 0) { Phase = GamePhase.Lost; RaiseEvent("levelLost", 0, 0); return; }
            SpawnBallOnPaddle(); // re-serve
        }
    }
```
> `SpawnBallOnPaddle()` already sets `Phase = Serving`. The lose threshold is `SpareBalls < 0` so the configured 3 spare balls give 3 retries before the loss (start=3 → after 3 drains it hits -1... adjust: use `<= 0`? See Step 4 calibration).

- [ ] **Step 4: Calibrate the lose threshold to the test**

The test expects loss after exactly 3 drains with `StartBalls = 3`. Use this exact logic — decrement first, lose when it would go below zero:
```csharp
        if (Balls.All(b => !b.Alive))
        {
            if (SpareBalls <= 0) { Phase = GamePhase.Lost; RaiseEvent("levelLost", 0, 0); return; }
            SpareBalls--;
            SpawnBallOnPaddle();
        }
```
Drain 1: SpareBalls 3→2 reserve · Drain 2: 2→1 · Drain 3: 1→0 ... that is 3 reserves, not a loss. To lose on the 3rd drain, the test models "3 balls total." Set `StartBalls = 2` in the test's config OR keep `StartBalls=3` and expect loss on the 4th drain. **Decision:** keep `StartBalls=3` = "3 spare balls beyond the one in play." Update the `DrainingLastBall_LosesTheLevel` test loop to 4 iterations and assert `Lost`. Re-run.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: PASS (after the Step 4 calibration).

- [ ] **Step 6: Logging — instrument drain / reserve / win / lose**

Final logged version of `ResolveDrainAndWin` (replaces the body from Steps 3–4):
```csharp
    private void ResolveDrainAndWin()
    {
        if (!Blocks.Any(b => b.NeedToKill && !b.Dead))
        {
            Phase = GamePhase.Won;
            _log.Log(TickCount, "win", "all needToKill cleared");
            RaiseEvent("levelWon", 0, 0);
            return;
        }
        var drainLine = Level.Grid.Height + Config.CellSize * 2;
        foreach (var b in Balls)
            if (b.Alive && b.Pos.Y - b.Radius > drainLine)
            { b.Alive = false; _log.Log(TickCount, "drain", "ball lost", $"id={b.Id}"); }

        if (Balls.All(b => !b.Alive))
        {
            if (SpareBalls <= 0)
            { Phase = GamePhase.Lost; _log.Log(TickCount, "lose", "out of spare balls"); RaiseEvent("levelLost", 0, 0); return; }
            SpareBalls--;
            _log.Log(TickCount, "reserve", "re-serve", $"spareBalls={SpareBalls}");
            SpawnBallOnPaddle();
        }
    }
```

- [ ] **Step 7: Commit**

```bash
git add "Arkanoid game/backend"
git commit -m "feat(arkanoid-core): ball drain, spare-ball reserve, win/lose conditions"
```

---

## Task 1.4: Cheats (pre-setup hooks for Playwright)

**Files:**
- Modify: `backend/Arkanoid.Core/Sim/GameInstance.cs` (`ApplyCheat`)
- Test: append to `backend/Arkanoid.Tests/WinLoseTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `WinLoseTests.cs`:
```csharp
    [Fact]
    public void Cheat_ClearAllButN_LeavesNBlocks()
    {
        var g = Make("\"AAA\",\"AAA\",\"...\""); // 6 blocks
        g.Serve();
        g.ApplyCheat("clearAllButN", 1);
        Assert.Equal(1, g.Blocks.Count(b => !b.Dead));
    }

    [Fact]
    public void Cheat_WinNow_SetsWon()
    {
        var g = Make("\"AAA\",\"...\",\"...\"");
        g.Serve();
        g.ApplyCheat("winNow", 0);
        Assert.Equal(GamePhase.Won, g.Phase);
    }
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test backend/Arkanoid.Tests` → FAIL (`ApplyCheat` is a no-op).

- [ ] **Step 3: Implement `ApplyCheat`**

Replace the `ApplyCheat` stub in `GameInstance.cs`:
```csharp
    public void ApplyCheat(string op, double value)
    {
        switch (op)
        {
            case "clearAllButN":
                var keep = (int)value;
                var alive = Blocks.Where(b => !b.Dead).ToList();
                for (int i = 0; i < alive.Count - keep; i++) alive[i].Dead = true;
                break;
            case "winNow":
                foreach (var b in Blocks) b.Dead = true;
                Phase = GamePhase.Won; RaiseEvent("levelWon", 0, 0);
                break;
            case "loseNow":
                Phase = GamePhase.Lost; RaiseEvent("levelLost", 0, 0);
                break;
            case "setSeed": Rng = new Rng((int)value); break;
            case "setMana": ManaValue = System.Math.Clamp(value, 0, Config.ManaMax); break;
            case "loseBall":
                foreach (var b in Balls) b.Alive = false;
                break;
        }
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: PASS.

- [ ] **Step 5: Add per-part Playwright scenarios that use the cheats**

`tests/battle-start.spec.ts`:
```typescript
import { test, expect } from "@playwright/test";
import { openBattle, getState, waitForPhase } from "./helpers/game";

test("battle starts: blocks present and ball serves into play", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  const s0 = await getState(page);
  expect(s0.blocks.length).toBeGreaterThan(0);
  await waitForPhase(page, "Playing");           // auto-serve kicked in
  const s1 = await getState(page);
  expect(s1.balls[0].y).toBeLessThan(s0.boardH); // ball is on the board
});
```

`tests/hud.spec.ts`:
```typescript
import { test, expect } from "@playwright/test";
import { openBattle, getState } from "./helpers/game";

test("HUD state exposes lives, spare balls, and mana", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  const s = await getState(page);
  expect(s.lives).toBeGreaterThan(0);
  expect(s.spareBalls).toBeGreaterThanOrEqual(0);
  expect(s.manaMax).toBeGreaterThan(0);
});
```

`tests/battle-winnable.spec.ts`:
```typescript
import { test, expect } from "@playwright/test";
import { openBattle, cheat, waitForPhase } from "./helpers/game";

test("clearing the last block wins the level", async ({ page }) => {
  await openBattle(page, "hell-winnable", 1); // level has a single block
  await cheat(page, "winNow", 0);             // pre-setup forces the win path
  await waitForPhase(page, "Won");
});
```

`tests/battle-lose.spec.ts`:
```typescript
import { test, expect } from "@playwright/test";
import { openBattle, cheat, waitForPhase } from "./helpers/game";

test("running out of balls loses the level", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await cheat(page, "loseNow", 0);
  await waitForPhase(page, "Lost");
});
```

- [ ] **Step 6: Run the scenarios**

Run from `tests/`: `npx playwright test`
Expected: menu + battle-start + hud + battle-winnable + battle-lose all pass.

- [ ] **Step 7: Logging — instrument cheats**

Add as the first line inside `ApplyCheat` (so the sim log shows every forced state, alongside the server's `cmd` record):
```csharp
        _log.Log(TickCount, "cheat", op, $"value={value}");
```

- [ ] **Step 8: Commit**

```bash
git add "Arkanoid game/backend" "Arkanoid game/tests"
git commit -m "feat(arkanoid): cheat pre-setup hooks + per-part battle scenarios"
```

---

## Task 1.5: Mana + active spell (Fireball)

**Files:**
- Modify: `backend/Arkanoid.Core/Sim/GameInstance.cs` (`RegenMana`, `CastFireball`, projectiles)
- Create: `backend/Arkanoid.Core/Entities/Projectile.cs`
- Test: `backend/Arkanoid.Tests/SpellTests.cs`

- [ ] **Step 1: Write the failing tests**

`Arkanoid.Tests/SpellTests.cs`:
```csharp
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Sim;
using Xunit;

public class SpellTests
{
    private static GameInstance Make()
    {
        var catalog = BlockCatalog.FromJson(
          "{\"types\":[{\"id\":\"b\",\"biome\":\"hell\",\"hp\":3,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
          "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":4,\"rows_data\":[\".A.\",\"...\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
          catalog);
        return new GameInstance(level, SimConfig.Default, 1);
    }

    [Fact]
    public void Mana_RegeneratesOverTime()
    {
        var g = Make(); g.Serve();
        var before = g.ManaValue;
        for (int i = 0; i < 60; i++) g.Tick(SimConfig.Default.FixedDt); // 1 second
        Assert.True(g.ManaValue > before);
    }

    [Fact]
    public void Fireball_RequiresMana_AndConsumesIt()
    {
        var g = Make(); g.Serve();
        g.ManaValue = SimConfig.Default.FireballCost;
        g.CastFireball();
        Assert.True(g.ManaValue < SimConfig.Default.FireballCost + 1e-9);
        Assert.Single(g.Projectiles);
    }

    [Fact]
    public void Fireball_TooLittleMana_DoesNothing()
    {
        var g = Make(); g.Serve();
        g.ManaValue = 0;
        g.CastFireball();
        Assert.Empty(g.Projectiles);
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test backend/Arkanoid.Tests` → FAIL (`Projectiles`/mana not implemented).

- [ ] **Step 3: Implement `Projectile`**

`Arkanoid.Core/Entities/Projectile.cs`:
```csharp
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Entities;

public sealed class Projectile
{
    public int Id { get; init; }
    public Vec2 Pos;
    public Vec2 Vel;
    public int Damage;
    public double Radius;
    public bool Alive = true;
}
```

- [ ] **Step 4: Implement mana + fireball in `GameInstance`**

Replace the `RegenMana` stub and the `CastFireball` placeholder; add a projectile list + per-tick update:
```csharp
    public List<Projectile> Projectiles { get; } = new();
    private int _nextProjId = 1;

    private void RegenMana(double dt)
        => ManaValue = System.Math.Min(Config.ManaMax, ManaValue + Config.ManaRegenPerSec * dt);

    public void CastFireball()
    {
        if (Phase != GamePhase.Playing) return;
        if (ManaValue < Config.FireballCost) return;
        ManaValue -= Config.FireballCost;
        Projectiles.Add(new Projectile {
            Id = _nextProjId++,
            Pos = new Vec2(Paddle.Center.X, Paddle.Center.Y - Paddle.Height),
            Vel = new Vec2(0, -Config.FireballSpeed),
            Damage = Config.FireballDamage, Radius = Config.BallRadius
        });
        RaiseEvent("spellCast", Paddle.Center.X, Paddle.Center.Y);
    }

    private void UpdateProjectiles(double dt)
    {
        var cell = Config.CellSize;
        foreach (var pr in Projectiles)
        {
            if (!pr.Alive) continue;
            pr.Pos += pr.Vel * dt;
            if (pr.Pos.Y < -cell) { pr.Alive = false; continue; }
            foreach (var blk in Blocks)
            {
                if (blk.Dead) continue;
                var c = Level.Grid.CellCenter(blk.Col, blk.Row);
                var box = Arkanoid.Core.Math.Aabb.FromCenter(c, cell / 2, cell / 2);
                if (box.IntersectsCircle(pr.Pos, pr.Radius))
                { DamageBlock(blk, pr.Damage, igniteSource: false); pr.Alive = false; break; }
            }
        }
        Projectiles.RemoveAll(p => !p.Alive);
    }
```
Call `UpdateProjectiles(dt)` inside `Tick` (after the ball loop, before `ResolveDrainAndWin`):
```csharp
        UpdateProjectiles(dt);
        ResolveDrainAndWin();
```
Add projectiles to the snapshot — append to `Snapshot.From` after the balls loop:
```csharp
        foreach (var pr in g.Projectiles)
            s.Balls.Add(new BallDto { Id = 10000 + pr.Id, X = pr.Pos.X, Y = pr.Pos.Y, Ignited = true });
```
> (Reusing `BallDto` for projectiles keeps the M1 snapshot minimal; a dedicated `ProjectileDto` can come later.)

- [ ] **Step 5: Implement perfect-deflect mana bonus in `OnPaddleHit`**

Replace the `OnPaddleHit` stub:
```csharp
    private void OnPaddleHit(Ball b, double t)
    {
        if (System.Math.Abs(t) < Config.PerfectDeflectBand)
            ManaValue = System.Math.Min(Config.ManaMax, ManaValue + Config.ManaPerfectDeflectBonus);
        ApplyIgniteOnDeflect(b); // body in Task 1.6 (stub for now)
    }
    private void ApplyIgniteOnDeflect(Ball b) { /* Task 1.6 */ }
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: PASS (all SpellTests).

- [ ] **Step 7: Add the active-spell Playwright scenario**

`tests/spell-fireball.spec.ts`:
```typescript
import { test, expect } from "@playwright/test";
import { openBattle, getState, cheat } from "./helpers/game";

test("casting fireball with mana spawns a projectile and consumes mana", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await cheat(page, "setMana", 100);
  const before = (await getState(page)).mana;
  await page.evaluate(() => (window as any).__game.castFireball());
  await page.waitForFunction((m) => (window as any).__game.getState().mana < m, before);
  const after = await getState(page);
  expect(after.mana).toBeLessThan(before);
});
```

- [ ] **Step 8: Logging — instrument mana + fireball**

Final logged `CastFireball` (add the deny-log and the success-log) and `OnPaddleHit` (keep the paddle log from Task 1.1, add the mana-bonus log):
```csharp
    public void CastFireball()
    {
        if (Phase != GamePhase.Playing) return;
        if (ManaValue < Config.FireballCost)
        { _log.Log(TickCount, "spell", "fireball denied", $"mana={ManaValue:F0} need={Config.FireballCost}"); return; }
        ManaValue -= Config.FireballCost;
        Projectiles.Add(new Projectile {
            Id = _nextProjId++,
            Pos = new Vec2(Paddle.Center.X, Paddle.Center.Y - Paddle.Height),
            Vel = new Vec2(0, -Config.FireballSpeed),
            Damage = Config.FireballDamage, Radius = Config.BallRadius
        });
        _log.Log(TickCount, "spell", "fireball cast", $"mana={ManaValue:F0}");
        RaiseEvent("spellCast", Paddle.Center.X, Paddle.Center.Y);
    }

    private void OnPaddleHit(Ball b, double t)
    {
        _log.Log(TickCount, "paddle", "deflect", $"t={t:F2} vx={b.Vel.X:F1} vy={b.Vel.Y:F1}");
        if (System.Math.Abs(t) < Config.PerfectDeflectBand)
        {
            ManaValue = System.Math.Min(Config.ManaMax, ManaValue + Config.ManaPerfectDeflectBonus);
            _log.Log(TickCount, "mana", "perfect deflect bonus", $"mana={ManaValue:F0}");
        }
        ApplyIgniteOnDeflect(b);
    }
```

- [ ] **Step 9: Run scenarios + commit**

Run from `tests/`: `npx playwright test spell-fireball.spec.ts`
Expected: pass.
```bash
git add "Arkanoid game/backend" "Arkanoid game/tests"
git commit -m "feat(arkanoid-core): mana (regen + perfect-deflect bonus) + Fireball active spell"
```

---

## Task 1.6: Imbue spell (Ignite) + Fire Mage passive

**Files:**
- Modify: `backend/Arkanoid.Core/Sim/GameInstance.cs` (`CastIgnite`, `ApplyIgniteOnDeflect`, ignite damage/spread)
- Test: append to `backend/Arkanoid.Tests/SpellTests.cs`

**Design:** `CastIgnite` *arms* the next paddle deflect (Wizorb-style). On deflect, the ball becomes ignited for `IgniteHits` block-hits. Ignited hits deal +1 and, on a kill, **spread** a chip of damage to 4-neighbor blocks (Fire Mage passive: blocks keep burning).

- [ ] **Step 1: Write the failing tests**

Append to `SpellTests.cs`:
```csharp
    [Fact]
    public void Ignite_ArmsOnCast_AndImbuesNextDeflect()
    {
        var g = Make(); g.Serve();
        g.CastIgnite();                 // arm
        // drive a paddle hit: place ball just above paddle moving down at center
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height/2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, 200);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Balls[0].IgniteHitsLeft > 0);
    }

    [Fact]
    public void IgnitedBall_DealsBonusDamage()
    {
        var g = Make(); g.Serve();
        var blk = g.Level.Blocks[0];                 // hp 3
        g.Balls[0].IgniteHitsLeft = 2;               // pre-imbued
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(c.X, c.Y + SimConfig.Default.CellSize/2 + 6);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(1, blk.Hp);                     // 3 - (1 base + 1 ignite) = 1
    }
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test backend/Arkanoid.Tests` → FAIL (`CastIgnite` no-op; ignite bonus not applied).

- [ ] **Step 3: Implement ignite arming + deflect imbue**

Replace the `CastIgnite` stub and `ApplyIgniteOnDeflect` stub:
```csharp
    private bool _igniteArmed = false;

    public void CastIgnite()
    {
        if (Phase != GamePhase.Playing) return;
        if (ManaValue < Config.IgniteCost) return;
        ManaValue -= Config.IgniteCost; // cost is 0 by default (anti-Wizorb)
        _igniteArmed = true;
        RaiseEvent("spellCast", Paddle.Center.X, Paddle.Center.Y);
    }

    private void ApplyIgniteOnDeflect(Ball b)
    {
        if (!_igniteArmed) return;
        b.IgniteHitsLeft = Config.IgniteHits;
        _igniteArmed = false;
        RaiseEvent("ignite", b.Pos.X, b.Pos.Y);
    }
```

- [ ] **Step 4: Apply ignite bonus damage + Fire Mage spread in block damage**

Update `ResolveBlocks` to pass the bonus, and extend `DamageBlock` with spread. Replace the `DamageBlock` call line in `ResolveBlocks`:
```csharp
            var bonus = b.IgniteHitsLeft > 0 ? 1 : 0;
            DamageBlock(blk, Config.BallDamage + bonus, igniteSource: b.IgniteHitsLeft > 0);
            if (b.IgniteHitsLeft > 0) b.IgniteHitsLeft--;
```
Extend `DamageBlock` so an ignited kill spreads a chip to neighbors (Fire Mage passive):
```csharp
    private void DamageBlock(Block blk, int dmg, bool igniteSource)
    {
        blk.Hp -= dmg;
        if (blk.Hp <= 0 && !blk.Dead)
        {
            blk.Dead = true;
            var c = Level.Grid.CellCenter(blk.Col, blk.Row);
            RaiseEvent("blockDestroyed", c.X, c.Y);
            ManaValue = System.Math.Min(Config.ManaMax, ManaValue + Config.ManaPerKill);
            if (igniteSource) SpreadFire(blk);
        }
    }

    private void SpreadFire(Block origin)
    {
        (int dc, int dr)[] n = { (1,0), (-1,0), (0,1), (0,-1) };
        foreach (var (dc, dr) in n)
        {
            var nb = Blocks.FirstOrDefault(b => !b.Dead && b.Col == origin.Col + dc && b.Row == origin.Row + dr);
            if (nb != null)
            {
                nb.Hp -= 1; // chip
                var c = Level.Grid.CellCenter(nb.Col, nb.Row);
                RaiseEvent("burn", c.X, c.Y);
                if (nb.Hp <= 0) { nb.Dead = true; RaiseEvent("blockDestroyed", c.X, c.Y); }
            }
        }
    }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/Arkanoid.Tests`
Expected: PASS (all tests across the suite).

- [ ] **Step 6: Add the imbue-spell Playwright scenario**

`tests/spell-ignite.spec.ts`:
```typescript
import { test, expect } from "@playwright/test";
import { openBattle, getState, cheat } from "./helpers/game";

test("igniting the ball is visible in the snapshot after a deflect", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  // arm ignite, then wait until any ball reports ignited=true (it imbues on the next paddle bounce)
  await page.evaluate(() => (window as any).__game.castIgnite());
  await page.waitForFunction(
    () => (window as any).__game.getState().balls.some((b: any) => b.ignited),
    null, { timeout: 8000 }
  );
  const s = await getState(page);
  expect(s.balls.some((b: any) => b.ignited)).toBeTruthy();
});
```
> If timing proves flaky (ball must reach the paddle), strengthen the pre-setup: add a cheat `armIgniteAndCenterBall` later. For M1, the auto-served ball returns to the paddle within the timeout on `hell-1`.

- [ ] **Step 7: Run the full Playwright suite**

Run from `tests/`: `npx playwright test`
Expected: all scenarios pass — `menu`, `battle-start`, `hud`, `battle-winnable`, `battle-lose`, `spell-fireball`, `spell-ignite`.

- [ ] **Step 8: Logging — instrument ignite + fire spread**

Add log lines to the three ignite methods:
```csharp
    // in CastIgnite, after `_igniteArmed = true;`
        _log.Log(TickCount, "ignite", "armed", $"mana={ManaValue:F0}");

    // in ApplyIgniteOnDeflect, after `b.IgniteHitsLeft = Config.IgniteHits;`
        _log.Log(TickCount, "ignite", "imbued ball", $"id={b.Id} hits={b.IgniteHitsLeft}");

    // in SpreadFire, inside the neighbor loop after `nb.Hp -= 1;`
        _log.Log(TickCount, "burn", "spread chip", $"id={nb.Id} hp={nb.Hp}");
```

- [ ] **Step 9: Commit — M1 GATE (pre-approval)**

```bash
git add "Arkanoid game/backend" "Arkanoid game/tests"
git commit -m "feat(arkanoid-core): Ignite imbue spell + Fire Mage fire-spread passive"
```

---

## Task 1.7: M1 demo approval gate

**Files:** none (process gate, per `CLAUDE.md` demo-first + completing-a-game-feature discipline).

- [ ] **Step 1: Run backend + frontend, play `http://localhost:5173`**

Manually verify the *feel*: paddle tracks the cursor; ball bounces cleanly with no shallow crawling; blocks take hits and die; Q arms Ignite and the next bounce visibly ignites the ball (orange) and burns neighbors on kills; E throws a fireball when mana allows; clearing the board shows Won; draining all balls shows Lost.

- [ ] **Step 2: Capture 3 screenshots** (serving, mid-volley with an ignited ball, post-win) via `npx playwright test --headed` or a one-off screenshot script, and present them for **explicit user approval** before declaring M1 done.

- [ ] **Step 3: On approval, tag the slice**

```bash
git tag arkanoid-m1
git commit --allow-empty -m "chore(arkanoid): M1 vertical slice approved"
```

---

## Self-Review Notes (author)

- **Spec coverage:** M0 (scaffold, Core math/grid/sim, WS protocol, server loop, PixiJS renderer, Playwright harness) and M1 (wall/paddle/block physics, lives/balls, win/lose, mana, Fireball active, Ignite imbue + Fire Mage passive, per-part scenarios) all map to tasks. The "individual scenario per part" requirement (menu, battle-start, HUD, winnable, lose, two spells) is covered by Tasks 0.9, 1.4, 1.5, 1.6.
- **Pre-setup model:** scenarios boot via URL params (`?scene=`, `?level=`, `?seed=`, `?run=`) and force exact states via the WS cheat channel (`clearAllButN`, `winNow`, `loseNow`, `setMana`, `setSeed`) — deterministic, physics-timing-independent.
- **Logging coverage:** Task 0.10 builds the three-tier layer (Core `ISimLog` → server `logs/<runId>.jsonl` → browser ring + console → Playwright auto-attach). Every M1 task that adds logic adds matching `_log.Log(...)` calls (1.1 paddle, 1.2 block, 1.3 drain/win/lose, 1.4 cheat, 1.5 mana/fireball, 1.6 ignite/burn). On a red test the report carries `client-console.txt`, `client-ring.json`, and `server-sim.jsonl` so an AI can replay the tick stream. Unit tests run silent (default `NullSimLog`) unless a test injects a capture sink.
- **No magic numbers:** all tunables live in `SimConfig`; logic references config fields.
- **Known calibration:** Task 1.3 Step 4 reconciles the spare-ball loss threshold with `StartBalls` — follow that step exactly.
- **Deferred (noted, not M1):** sprite-atlas packing (load individual PNGs for now), enemy HP damage (M3 — HP bar exists and is wired, no enemy damages it yet in M1), delta snapshots (full snapshot per tick is fine at this scale), client-side paddle prediction (optional polish).
```
