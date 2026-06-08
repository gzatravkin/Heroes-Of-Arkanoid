# Migration & Redesign Plan — Arkanoid RPG (Browser + .NET)

> How we rebuild the old Unity Arkanoid RPG as a **browser-rendered, C#-simulated** game, following the conventions of the sibling TerrariaSim project, with **automatic tests** and a **clean logic↔render split so the simulation can later move into Unity**.
> Companion to `01-current-game-design.md` (what the old game is) and `02-current-implementation.md` (how it's built / what to avoid).

---

## 0. Decisions (locked)

| Decision | Choice | Rationale |
|---|---|---|
| **Fidelity** | **Inspiration-only redesign** | Keep theme + art + identities; rebuild mechanics clean. |
| **Execution model** | **`Arkanoid.Core` (pure C#) → .NET WS server → PixiJS renderer** | Mirrors TerrariaSim. Headless .NET for tests. Core lifts into Unity later. |
| **Run structure** | **Persistent branching campaign + opt-in Dungeon roguelike runs** | Campaign = familiar, no permadeath, retryable. Dungeons = "you unlocked a dungeon, wanna try?" banner → 2–5 levels, minibosses, **permadeath in-run**, **pick-a-bonus after each level**. |
| **First class (MVP)** | **Fire Mage** | Most complete old art (93 sprites) + full kit + simplest resource model (mana regen). |
| **Levels** | **Authored fresh on a standardized grid (JSON)** | Old level prefabs are **not in this folder** and were non-standard anyway. This *sidesteps* the core legacy problem instead of importing it. |
| **Docs/code location** | **Inside `Arkanoid game/`** | Per owner. Old `Scripts/`+`Sprites/` stay as frozen reference + art source. |

---

## 1. Goals & Non-Negotiables

1. **All game logic in engine-agnostic C#** (`Arkanoid.Core`) — no Unity, no ASP.NET, no `System.Drawing`, no networking, no static global state. Pure math, deterministic, instantiable many times.
2. **Browser renders only** — PixiJS is a dumb view. No physics, no game state client-side (the TerrariaSim rule).
3. **Automatic tests first-class** — the same Core runs headless in xUnit; every mechanic gets a test. This is the safety net the old game never had (it had **zero** tests).
4. **Unity-portable** — because Core is plain C# with no host dependencies, it drops into a future Unity project as a managed library. The *only* thing Unity replaces is the render/input host — exactly what the browser is doing now.
5. **Standardized levels** — a fixed grid with integer cell sizes. **No per-block float scale, ever** — that was the #1 legacy defect (`02-current-implementation.md` §4).

---

## 2. Target Architecture

```
                 input (paddle, cast)                snapshots + events
  [ PixiJS renderer ] ───────────ws──────────►  [ Arkanoid.Server (.NET 8) ]
        ▲  draws from snapshots                         │ owns 1 sim per session
        └─────────────────ws─────────────────◄─────────┘
                                                         │ hosts
                                              [ Arkanoid.Core ]  (pure C# sim)
                                                         ▲
                                       same library, no host deps
                          ┌──────────────────────────────┼─────────────────────┐
                  [ Arkanoid.Tests ]              [ future Unity host ]
                  headless xUnit                  (render/input only)
```

### 2.1 `Arkanoid.Core` (the simulation — the asset that outlives every host)
Engine-agnostic C# library (`net8.0`, no Unity/ASP.NET refs). Modules:

- **`Sim/`** — `GameInstance` (one per session — *instance state, never `static`*, fixing legacy hazard #6), a deterministic **fixed-timestep loop** (e.g. 120 Hz internal), seeded RNG (`System.Random` wrapper, seed stored for replay/test determinism). Single authoritative tick (collapses the old 3 timing systems).
- **`Math/`** — own `Vec2`, AABB/OBB helpers. No `UnityEngine.Vector2`.
- **`Physics/`** — **custom deterministic collision**: ball (circle) vs block (AABB, integer-cell), vs paddle (segment/OBB with positional deflection), vs walls. Port the one genuinely good piece of legacy math: the **"no-shallow-angle" velocity clamp + jitter** (`BallBehavior.CorrectDir`). Continuous collision to avoid tunneling at high ball speed. **Highest-risk module — see §7.**
- **`Entities/`** — `Ball`, `Paddle`, `Block`, `Projectile`, `Enemy`, `Boss`. Plain data + behavior, identified by stable `int` ids for the wire.
- **`Blocks/`** — block types + the **3-phase effect pipeline** ported from the old `BlockScript` (`Hitting(ref dmg)` → apply HP → `Hitted` → `Die`) — this was the *cleanest* legacy pattern, keep it. Effects: ChangeSpriteByHP, CreateObjectOnDie, etc.
- **`Skills/`** — skill/spell defs + the **`LeveledParameter`** model ported from `SkillNumberParameter` (line/grid/curve). **Unity `AnimationCurve` → exported keyframe arrays** (legacy hazard #4). Resource controller (mana-regen / souls-per-kill).
- **`Items/`** — item effects as event subscribers applying leveled coefficients. **Actually implemented this time** (the old game shipped 3 empty stubs).
- **`Progression/`** — campaign node graph, dungeon-run state machine, rewards, XP/level/points.
- **`Events/`** — plain C# `event`/delegate bus (replacing `UnityEvent`). Battle events (BlockDestroyed, SpellCast, HpChanged, …) drive both gameplay and the snapshot's "fire-and-forget" VFX/SFX cue list.
- **`Data/`** — loads JSON content (§4) into runtime defs. No `Resources.Load`.
- **`Snapshot/`** — serializable per-tick state (entities: id/type/cell-or-pos/spriteState) + an event cue list, for the wire.

### 2.2 `Arkanoid.Server` (host #1 — play)
ASP.NET .NET 8. One `GameInstance` per connection (**session-scoped, not static** — the legacy global-singleton trap is the thing to avoid). WebSocket endpoint: receives input, ticks the sim, sends snapshots (~60 Hz) / deltas. Reuse TerrariaSim's `Network/` patterns and `contract/` discipline.

### 2.3 `Arkanoid.Tests` (host #2 — verification)
xUnit. Instantiates `Arkanoid.Core` directly, advances ticks, asserts behavior with no mocking: collision angles, no-tunneling at max ball speed, skill scaling per level, resource spend/regen, win/lose conditions, item coefficients, dungeon permadeath/reward flow. Deterministic via seeded RNG.

### 2.4 Frontend (PixiJS — the view)
Vite + PixiJS v7, structured like TerrariaSim's `frontend/src/` (`input/`, `network/`, `renderer/`, `ui/`). Receives snapshots, draws blocks/ball/paddle/projectiles from the **sprite atlas built from old art** (§4.4), plays event-cued VFX/SFX. Sends paddle target + cast commands. **Optional client-side paddle prediction** so paddle tracks the cursor with zero perceived latency while the server stays authoritative (reconcile on snapshot).

### 2.5 The logic↔render contract (`contract/`)
A versioned spec (like TerrariaSim):
- **Client→Server:** `Input{paddleX | paddleDelta, castSpellId, pause}`.
- **Server→Client:** `Snapshot{tick, entities[], events[]}` where `events[]` are transient cues (block destroyed at cell, spell cast, hp changed) the renderer turns into juice. Entities reference **grid cells or sim units + a sprite key + sprite-state** — never a raw Unity scale.

---

## 3. The Standardized Grid (fixing the #1 legacy defect)

The old game stored block size as a free `transform.localScale` (Vector3) per instance, with no grid — so no two levels shared a coordinate convention and difficulty was whatever a designer eyeballed (`02-current-implementation.md` §4).

**New rule:** the board is a **grid of integer cells**.
- A small set of **standard board dimensions** (e.g. `cols × rows`) chosen from config — not arbitrary per level.
- **Block size = whole cells.** A block occupies 1 cell, or an integer `w×h` for large blocks. **No float scale.**
- Ball radius, paddle width, speeds all expressed in **cell units** so balance transfers across levels.
- A **level is a JSON grid** (2D array / rows of block-type codes) + biome + objects + win condition + rewards.
- A **standardized difficulty model**: block HP and density derive from a per-biome/per-depth curve in config — the global progression curve the old game lacked.

This single change is what makes levels authorable, testable, balanceable, and procedurally generatable for dungeons.

---

## 4. Content as Data (JSON)

Everything the old game buried in ScriptableObjects/prefabs becomes JSON in `config/` (manual-edit, read by Core at startup — the TerrariaSim model):

| File | Contents |
|---|---|
| `blocks.json` | block types per biome: id, biome, hp, spriteKey, effects[] |
| `biomes.json` | biome name, background, transition images, difficulty curve |
| `classes/<class>.json` | hero def: skills[], spells[] (with `LeveledParameter`s), resource model, startLives/startBalls |
| `items.json` | item defs: event hook + leveled coefficient + sprite tiers (maxLevel 3) |
| `levels/<id>.json` | **grid layout** (cells), biome, placed objects, win condition, rewards |
| `campaign/<class>.json` | road node graph: nodes (level ref, map pos), prerequisite edges, forks |
| `dungeons.json` | dungeon defs: length 2–5, miniboss pool, reward/bonus pool |

### 4.4 Art pipeline (`art-pipeline/`)
The 755 old PNGs are **kept** — they are the theme. Build a packed **sprite atlas** from them (the biome blocks already carry HP/destroy-state variants and consistent naming, e.g. `{Biome}Standart`/`…Damaged`/`…Destroyed`). Replace the old **baked-text localized panels** (`InterfaceFireMage{ENG,RUS,SP}.png`) with **live text** in the PixiJS UI.

---

## 5. Keep / Drop / Redesign (old → new)

**Keep (identity & assets):** all biome art + the 4-biome progression (hell → caverns → witchland → heaven); class identities (Fire Mage, Paladin, Engineer); spell *concepts*; item *concepts*; the branching campaign; the block-effect pipeline pattern; the leveled-parameter scaling model; the POCO save model.

**Drop:** old Unity prefab levels (unavailable + non-standard); all `#if UNITY_EDITOR` tooling; `UnityEvent` bus + global singletons; broken spell timing (static timers, cached dead balls, mixed time bases); dead stubs (Demon no-op, lava block, `StarWarrior` half-class); **baked-text UI images**; the crystal currency-with-no-sink (**repurpose as dungeon currency** or cut).

**Redesign:** physics (deterministic Core); levels (grid + JSON); spells (clean, tunable, **tested**); items (**actually implemented** — fix or cut the 3 stubs); progression (campaign + dungeon mode); UI (PixiJS, live text); difficulty (standardized curve).

**Class roster decision (deferred to M6):** the old art has a **Necromancer** kit (47 sprites + Skeleton prefabs) with **no code** — a candidate real 4th class. **StarWarrior** has code but **no art** and was never finished — likely cut or folded into a shared utility skill. Ship Fire Mage → Paladin → Engineer, then evaluate finishing Necromancer from its existing art.

---

## 6. The MVP Vertical Slice (defines M0–M1)

**Fire Mage, Hell biome, one hand-authored grid level.** Proves the whole pipe end-to-end:
- Deterministic ball ↔ paddle ↔ block ↔ wall physics with the no-shallow-angle clamp.
- HP blocks with sprite-by-HP, lives + spare balls, win = clear all `NeedToKill`, lose = lives 0.
- Paddle input over WS (+ optional paddle prediction). Mana bar.
- **Fire Mage Passive** (ignite ball on center-paddle hit → small explosion) + **Ring** (fireball projectile).
- Renders from the atlas; block-destroy/spell-cast event cues drive VFX.

If this *feels* good and the physics tests pass, the architecture is proven.

---

## 7. Phased Roadmap (milestones with gates)

Each milestone follows the existing project discipline: **feature-critic** before building, **demo-scene + explicit user approval** before commit, **completing-a-game-feature** gate (smoke + scenario), and **automatic tests** for every mechanic.

**M0 — Foundations & pipeline**
Scaffold `backend/Arkanoid.{Core,Server,Tests}`, `frontend/` (Vite+PixiJS), `contract/`, `config/`. Build the sprite atlas from old PNGs. Define the grid + JSON schemas.
*Gate:* empty board renders in browser via WS; one static block drawn from atlas; a Core unit test ticks the sim headless and passes.

**M1 — Core breakout slice (the "feel" milestone)** — Fire Mage, Hell
Deterministic physics; HP blocks; lives/balls; win/lose; paddle input (+optional prediction); mana; Fire Mage Passive + Ring.
*Gate:* playable in browser and **feels right** (demo approval); physics parity tests pass (deflection angles, no tunneling at max speed); win/lose verified.

**M2 — Spells, items & the tuning model**
Full Fire Mage kit (Wall, Turret, Phoenix); skill leveling + upgrade UI; mana costs/regen via ported `LeveledParameter`; item system **actually implemented** (3 slots, ~6 working items, framework for event-hook effects).
*Gate:* leveling measurably changes behavior (tests); items apply effects (tests); demo approval.

**M3 — Biomes & signature block mechanics**
All 4 biomes' blocks on the grid + signature mechanics: Hell teleporters, Cavern union-sticks, Witchland ghost-phasing + necromant-revive, Heaven statue-allies; plus descending-blocks & multi-floor layout modes.
*Gate:* each biome's signature mechanic has a test + a demo level.

**M4 — Campaign & persistence**
Branching campaign node graph (Fire Mage first), mission-select map, **save model ported POCO→JSON**, rewards, XP/level/points, full respec.
*Gate:* complete a multi-level campaign branch end to end; save/load persists across sessions.

**M5 — Dungeon roguelike mode** *(the owner's hybrid)*
"You unlocked a dungeon" banner; a dungeon = **2–5 levels, minibosses, permadeath within the run, pick-a-bonus after each level**; run-scoped buffs + a reward/bonus pool; dungeon currency (repurpose crystals).
*Gate:* full dungeon run start → clear/permadeath; bonus-pick works; run buffs don't leak into the campaign; demo approval.

**M6 — More classes & bosses**
Paladin + Engineer kits (clean ports). Bosses: Goblin & Witch as references, **Demon redesigned properly** (old one was a no-op). Evaluate finishing **Necromancer** as the 4th class from existing art; decide StarWarrior's fate.
*Gate:* each class playable through the MVP campaign slice; bosses have real AI + tests.

**M7 — Authoring, balance & polish**
In-browser **grid level editor** to scale content (replacing the discarded Unity MapCreator). **Standardized difficulty-curve balance pass** (the thing the old game lacked). Audio, VFX juice, death/win screens.
*Gate:* balance targets met; content count; full playthrough.

---

## 8. Risks & Open Decisions

**Risks**
- **Physics feel parity (highest).** There's no runnable reference build to compare against and "feel" is subjective → lean on demo approval + Playwright visual tests + explicit numeric physics tests (deflection, energy, no-tunnel).
- **Paddle latency over WS.** Mitigated by localhost (sub-ms) + optional client-side paddle prediction. The server model also future-proofs remote play for free.
- **Atlas from 755 loose PNGs.** Some are baked-text/platform assets to discard; budget time for slicing/packing and part-based boss rigs (assembled at runtime in the old game).
- **Determinism.** Fixed timestep + seeded RNG must be respected everywhere in Core or tests/replays drift.

**Open decisions (deferred, not blocking M0–M4)**
- Necromancer: finish as 4th class (art exists) vs cut? → M6.
- StarWarrior: cut vs fold into shared utility? → M6.
- Crystal currency: repurpose as dungeon currency vs remove? → M5.
- Procedural dungeon generation depth (curated-shuffle vs fully procedural) → M5.

---

## 9. Proposed Repo Layout (inside `Arkanoid game/`)

```
Arkanoid game/
  Scripts/            # OLD Unity — frozen, reference only
  Sprites/            # OLD art — source for the atlas
  docs/
    01-current-game-design.md
    02-current-implementation.md
    03-migration-plan.md          # this file
  backend/                        # added in M0
    Arkanoid.Core/                # pure C# sim (Unity-portable)
    Arkanoid.Server/              # ASP.NET WS host
    Arkanoid.Tests/               # xUnit, headless Core tests
  frontend/                       # Vite + PixiJS renderer
  contract/                       # WS protocol spec
  config/                         # JSON content (blocks, levels, classes, items, biomes, campaign, dungeons)
  art-pipeline/                   # atlas builder from Sprites/
  tests/                          # Playwright visual scenarios
```

---

## 10. Immediate Next Step

After this plan is approved, the natural follow-up is an **implementation plan for M0 + M1** (the vertical slice) via the writing-plans skill: scaffold the solution, define the grid + snapshot contract, build the atlas, and stand up deterministic ball/paddle/block physics with the Fire Mage Passive + Ring — the milestone that proves the architecture and the feel.
