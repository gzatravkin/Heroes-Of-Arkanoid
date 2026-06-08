# Current Implementation — Old Unity Arkanoid RPG

> Code-architecture audit of `Arkanoid game/Scripts/` (246 C# files, ~9,000 lines).
> Focus: **how it's built** and **how tightly it's fused to Unity**, to scope a migration to a PixiJS-renders / .NET-simulates split. For player-facing design see `01-current-game-design.md`. For the plan see `03-migration-plan.md`.

**Bottom line up front:** This is a classic Unity "scripts-on-GameObjects" architecture with **no logic/render separation**. Game state lives inside the GameObjects/components themselves; Unity's `Update()`/`FixedUpdate()` *is* the game loop; physics is 100% Unity 2D. The **save model and the skill-scaling/cost rules are liftable**; the **physics, level/board representation, and all UI are heavy rewrites**, not ports.

---

## 1. Architecture Overview

No central simulation loop the game owns — Unity ticks ~54 files' `Update`/`FixedUpdate`. Folders (no real namespaces):

- `GameClases/` (+`Abstract/`) — loose domain types: `GameField` (block registry / spatial queries), enums, parameter classes. Mostly plain C# but pervasively reference `Vector2`/`Transform`.
- `GameField/` — gameplay: `Blocks/`, `Player/Player_Bar/` (ball + paddle), `Skills/`, `Spell Controller/`, `Bosses/`.
- `Services/` — `BattleController` (static), `BattleEventsManager`, `TimeManager`, `LevelLoader`, `ServiceLocator`, `GameIniciacion`.
- `Effects/` — `BlockEffects/`, `PhysicEffects/`, `GraficEffects/` (all MonoBehaviours).
- `Events/`, `SaveSystem/` (+`SaveWriter/`), `Interface/` (uGUI), `LocationLogic/`, `Heroes/`, `Administrator/CreatorTools/`, `Configuraciones/`, `Tests/`.

**A battle frame flows:**
1. `GameIniciacion.Start()` → `LevelLoader.Initialization()` → hero init → `BattleController.Initialization()` → camera/walls/time init → per-item init.
2. `LevelLoader` instantiates a **Level prefab**, walks `GetComponentsInChildren<Transform>`, and registers blocks into a plain-C# `GameField`.
3. Each frame Unity ticks all MonoBehaviours. `BattleEventsManager.Update()` fans out a single routed `UpdateEvent.Invoke()` that most controllers subscribe to (instead of their own `Update`). `TimeManager` maintains `battleTime`/`battleDeltaTime` and even rewrites `Time.fixedDeltaTime`.
4. Physics runs in Unity 2D on `FixedUpdate`. Ball velocity is clamped each tick.
5. Collisions fire `OnCollision/TriggerEnter2D` → `BlockScript.GetHit()` → events.
6. Death routes through `BattleController.DestroyBlockWithCallback()` → `Destroy(go)` + `BlockDestroyed` event.

A thin manager layer exists (`BattleController`, `GameField`, `LifeManager`) but **state lives in the GameObjects**, not a model these managers own.

---

## 2. Unity Coupling Assessment *(critical — this is a rewrite, not a port)*

Quantified:
- **115 / 246 files derive from `MonoBehaviour`.** 37 derive from `ScriptableObject` (skills, heroes, items, config). Almost nothing is engine-free.
- **180 `GetComponent` calls; 58 `Instantiate` calls** — wiring and spawning are Unity-mediated.
- **Physics is 100% Unity 2D, not custom math.** Ball is a `Rigidbody2D`; paddle bounce reads `col.contacts[0].point` and sets `col.rigidbody.velocity`; block hits via `OnTriggerEnter2D`; walls via Unity tag comparisons.
  - One hand-rolled integrator exists (`MyRigidBody2D`: gravity −10, manual `Velocity*Time.deltaTime`) but still mutates a `Transform`.
  - `BallBehavior.CorrectDir` is the one piece of genuinely portable gameplay math (the Arkanoid "no-shallow-angle" velocity clamp + jitter) — but operates on `rigi.velocity`.
- **54 files implement `Update`/`FixedUpdate`/`LateUpdate`.** Timing is split three ways (true `Update`, routed `UpdateEvent`, `TimeManager.battleTime`). Migration must re-centralize into one authoritative tick.
- **`Transform`/`Vector2/3` *are* the data model.** `GameField` spatial queries read `block.transform.position` directly. Block position/rotation/**scale** live only on the Transform.
- **Config anchored to the Unity asset DB** via `ScriptableObject` + `Resources.Load`.
- **Coroutines are light** (~6 files) — easily replaced by timers.
- **UI is Unity uGUI** (`Image`, `Animator`, `RadialLayout`, panels) — the largest single subsystem, and **thrown away** for a PixiJS front-end.

The *rules* (damage math, skill curves, costs, win conditions) are extractable but inlined into MonoBehaviours; the *physics and rendering* must be reimplemented.

---

## 3. Core Systems

- **Player / paddle** — `AbstractPlayerController : MonoBehaviour` → `Player_BarController` composes sibling components (`BarLogic`, `BarMove`, `Bar_GraficController`) via `GetComponent`. Bonuses are a timed `List<AbstractBonus>`. Notably opts out of its own `Update` and subscribes to the routed `UpdateEvent`.
- **Ball** — split across tiny MonoBehaviours on one GameObject: `BallController` (collision dispatch + events), `BallBehavior` (velocity normalization in `FixedUpdate`), `BallVelocityController` (copies paddle speed into the ball each frame), `BallShower`. Multi-ball is a `List<BallController>`.
- **Blocks** — `BlockScript : MonoBehaviour` holds `HP`, `Killed`, and an `AbstractBlockEffect[]` discovered via `GetComponents`. `GetHit(damage, type)` runs a clean 3-phase pipeline (`Hitting(ref damage)` → apply HP → `Hitted` → maybe `Die`). **This effect-component pattern is the cleanest design in the codebase.** Special blocks (Cave/Heavens/Hell/Village/Necromant) are bespoke MonoBehaviours.
- **Skills / Spell controller** — ScriptableObject-based (the RPG core):
  - `SO_AbstractSkill` → `SO_InvisibleSkill` (passive) / `SO_VisibleSkill` (castable, `Activate_Recourse` cost). Levels, upgrade pricing, localized descriptions.
  - `SkillNumberParameter` — per-level numeric scaling, `line`/`curve`/`grid` modes incl. a Unity `AnimationCurve` (**must be exported to plain keyframes**).
  - Uses **reflection** (`GetFields`) to auto-discover its parameter fields for description templating — clever but fragile and editor-coupled (`OnValidate`/`OnEnable`).
  - `SO_SpellController` (mana vs "soulce" subclasses) gates casting: `CastSpell` → `CanCast` → spend → `Spell.Cast()` → `SpellCasted` event.
  - `SO_Hero` bundles a class (skills, spells, spell system, paddle prefab, `HeroRoad`).
  - Concrete skills reach across singletons (e.g. `GetActualBar()` cast to `Player_BarController`).
- **Bosses** — plain MonoBehaviours; several are stubs (`Demon.cs` is entirely no-op collision handlers).
- **Effects** — the 3-folder split (`BlockEffects` rules / `PhysicEffects` Unity-physics / `GraficEffects` rendering) *suggests* a model/view split but it's superficial: all three are sibling components on the same GameObjects and freely call each other.

---

## 4. Data & Configuration — *and the root of the "no standardization" problem*

Content is **ScriptableObject assets + Unity prefab/scene hierarchies** — almost nothing is JSON or plain data.

- **Global config:** one master `SO_Configuraciones` ScriptableObject loaded via `Resources.Load`. Holds heroes, items, locations, block collectors, UI prefabs, palettes — the de-facto game database living in a `.asset`.
- **Skills/heroes/items:** ScriptableObject assets with serialized fields + `SkillNumberParameter` grids/curves.
- **Levels:** **Unity prefabs, not data.** A `Level` MonoBehaviour's *children* are blocks. Optional `BlockBaker` flattens children into a `List<BlockData>` storing per block: `localPos` (Vector2), **`localScale` (Vector3)**, `localRotation`, prefab ref.

**Where the per-block-scale problem lives (evidence):**
- `BlockBaker.BlockData.localScale` is a free `Vector3`, captured from and restored to each block — every block remembers whatever arbitrary scale the designer left it at.
- `BlockLoader.LoadBlock(... Vector3 localScale)` applies an arbitrary per-instance scale on instantiate.
- `LevelFitter.x,y` sizes the **camera/field** per level (e.g. 17×7) — **not** the blocks. There is no cell size, no grid, nothing normalizing block dimensions.
- `Level.GetAutoLocation()` infers biome from block-majority.

**Consequence:** there is no tile/grid model to lift. The redesign must *introduce* a canonical grid the original never had.

---

## 5. Events & Services

- **Message bus exists**, built on `UnityEngine.Events.UnityEvent` (Unity-coupled but conceptually portable). `BattleEvents` is a bag of ~25 events (`SpellCasted`, `BattleStarted/Ended/Won/Lost`, `BlockDestroyed`, `HpLoosed`, ball/wall collisions…). `BattleEventsManager` re-creates them all on battle restart and drives the routed `UpdateEvent`. `GlobalEvents` holds meta-game `static` events.
- **Service access is a mix of patterns (a smell):** a near-empty `ServiceLocator` with `public static` fields, a `static class BattleController`, plus **ad-hoc singletons everywhere** (`static obj/objRef` on `LifeManager`, `MissionWinController`, `SO_SpellController`, `TimeManager`, `GameIniciacion`, `SO_Configuraciones`, …). **All global state — will fight a server-side model with isolated game instances.**

---

## 6. Save / Serialization *(the most liftable part)*

- `Saves.SaveSystem` — static, loads once at startup (`[RuntimeInitializeOnLoadMethod]`).
- Backed by `SaveWriter` — **XML serialization** of a generic key→object dictionary to a `.uml` file. Explicitly **refuses to serialize anything deriving from `Component`**, so save DTOs are plain POCOs: `PlayerData`, `CharactaerData` (sic), `SpellData`, `ItemsData`, `ResourceData`, `CrystalResource`, `GameStatisticData`.
- **Good news:** a clean POCO graph with no MonoBehaviour references — ports to .NET DTOs + JSON with little friction. (Caveat: storage path is platform-branched and writes into StreamingAssets — a smell.)

---

## 7. Editor / MapCreator Tooling

Substantial editor tooling (`#if UNITY_EDITOR`, ties to `UnityEditor`): `LevelCreator`, `RoadEditor`, `MapCreator/` (`BlockBaker`, `BlockDuplicator`, `EditorGizmo`, `LevelFitter`), custom inspectors (`SkillEditor`, `BlockLoader_Editor`, `ScriptableObjectCreator`). **None ports** — it authors the Unity content pipeline. The migration replaces it with an importer/authoring tool that targets the *baked output* (block lists, SO values), not these editors. The `BlockBaker` bake format is the de-facto level-export format.

---

## 8. Tests

**Effectively none.** `Scripts/Tests/` has one 16-line MonoBehaviour that pops a UI confirmation and `Debug.Log`s a click. No NUnit, no EditMode/PlayMode assemblies, no asserts. **Zero regression coverage** — migration has no safety net; collision/skill-scaling parity must be established by writing fresh tests against observed behavior.

---

## 9. Tech Debt & Migration Hazards (concrete)

1. **Unity 2D physics IS the gameplay** — ball bounce, paddle deflection, block/wall hits all engine-driven. A .NET backend needs a fresh deterministic circle-vs-AABB/OBB collision system. **Highest-risk item — feel parity is subjective and untested.**
2. **No logic/render separation** — no model object to lift; must be reconstructed.
3. **Per-instance block scale & per-level board size** — the "no standardization" problem is structural; no grid exists.
4. **`AnimationCurve` in skill data** — Unity-only type in balance data; export to keyframe arrays.
5. **Reflection-driven skill descriptions** — fragile, editor-callback-coupled.
6. **Global static singletons everywhere** — prevents multiple concurrent game instances (a problem for a server simulating sessions).
7. **Config locked in Unity assets** — needs an export pass to JSON/data.
8. **Three timing systems** — collapse into one authoritative server tick.
9. **No tests.**
10. **Naming chaos** — Spanish/English/Russian + typos, some load-bearing: `Configuraciones`, `GameIniciacion`, `Recource`/`AddRecource` (misspelled "resource", used in `SO_SpellController`), `Premy` (reward), `ClaseChoise`, `CharactaerData` (typo, referenced by save system), `MovimientSpeed`. Russian string literals appear in user-facing flow.
11. **Dead/stub code** — empty boss handlers, commented-out blocks, one-off scaffolding.
12. **Large files to budget for** (runtime, carry real logic): `MissionWinController`, win conditions, `FireWallController`, `LevelLoader`, `GameField`. (Editor files are discardable.)

> **Important availability note:** the actual ScriptableObject *instances* and **level prefabs are NOT present** in the `Arkanoid game` folder (only `Scripts/` + `Sprites/`). We have the **data model (schema) and the art**, but not the populated levels/heroes/items. The redesign therefore **authors levels fresh on a clean grid** rather than migrating old layouts — which sidesteps hazard #3 entirely.
