# Backend Refactor Roadmap — June 2026

Harsh assessment of what remains after the arc-analysis pass, with task descriptions only (nothing implemented).

**Current shape:** 12,079 C# lines — Core 5,459, Server 886, Tests 5,734.
The arc-analysis pass fixed the *symptoms* (split god classes, dedup helpers, enums over strings). What it did **not** fix is the *generative pattern* that produced them: every piece of game content (spell, relic, ball core, paddle mod, pickup, boss) is hardcoded C# instead of data. The codebase will regrow all the same problems with each new spell or relic. The tasks below attack that root cause.

**Estimated total: −2,500 to −3,300 net lines (~25% of the backend)**, and most future content additions become JSON-only.

---

## T1 — Data-driven spell engine *(biggest win)*

**Problem.** Adding one spell currently touches **six** hardcode sites:
1. A bespoke `Cast*` method (~15–25 lines) in one of four `SpellSystem.*.cs` partials (809 lines combined).
2. 4–6 knobs in `SimConfig` (lines 118–206 are ~90 lines of per-spell constants).
3. A case in the 20-arm `SpellDispatch` switch (`GameInstance.cs:261`).
4. The hardcoded `SpellKits` dictionary (`GameInstance.cs:241`) — whose own comment admits *"Must stay in sync with characters.json"*. `config/characters.json` already contains every kit; the C# copy is pure duplication waiting to drift.
5. The hardcoded 13-entry `SpellLevels` dictionary (`GameInstance.cs:212`).
6. Ad-hoc state fields in `GameInstance` (`_phoenixRemaining`, `_lastDayRemaining`, `_magnetRemaining`, `_penetrationArmed`, …).

**Reality check:** the 20 cast methods are near-identical templates — phase gate → `Spend()` → effect → `RaiseEvent` → `_log.Log`. They collapse into ~5 archetypes:

| Archetype | Spells | Params |
|---|---|---|
| `Projectile` | fireball, spear, rocket, golem, mage, skeleton bullets, turret bullets | speed, damage, radius, count, fanAngleDeg, pierce, homing, aoeRadius |
| `Imbue` (arm next hit/deflect) | ignite, decay, penetration | hits, armSlot |
| `TimedAura` | phoenix, turret, skeleton, drain, magnet, lastday | duration, tickInterval, per-tick behavior id |
| `Placement` | firewall, shield barrier, radiation zone, overload bomb | entity kind, lifetime, geometry |
| `Instant` | lightning, duplicate | damage, chainJumps/copies |

**Task.**
- Define `SpellDef` (id, archetype, manaCost, params, perLevelScaling) loaded from JSON — either a new `config/spells.json` or extending the `spells` arrays already in `characters.json`.
- One generic executor per archetype; truly bespoke behaviors (lightning chain, seraph interactions) keyed by a behavior enum, not a method-per-spell.
- `SpellKits` and `SpellLevels` derive from `CharacterCatalog` — delete both dictionaries and the `SpellDispatch` switch.
- Delete the ~90 per-spell lines from `SimConfig` (values move to JSON).

**Acceptance:** adding a Projectile/Imbue/TimedAura spell requires *zero* C# changes. `SimConfig` < 160 lines. Net **−400 to −500 lines**.

---

## T2 — Generic timed-effect system

**Problem.** The `XRemaining` / `XAccumulator` pattern is copy-pasted **10+ times**: turret, skeleton, drain, phoenix, lastday, magnet, cannons (in `GameInstance` fields) plus widePaddle, slowBall, fireshot (in `PowerupState`). Each has its own decrement-and-expire code scattered across `UpdateTurret`, `UpdateSkeleton`, `UpdateDrain`, `UpdateKitSpells`, `UpdateTempEffects`.

**Task.**
- `ActiveEffect { string Id; double Remaining; double TickInterval; double Accum; }` list on `GameInstance`.
- Single `EffectSystem.Update(g, dt)`: decrements, fires per-tick hooks on cadence, fires on-expire hooks (e.g. slow_ball restores speed, wide_paddle shrinks paddle).
- Replaces 6 separate `Update*` methods and all the ad-hoc fields. `TurretActive`-style accessors become `HasEffect("turret")`.

**Acceptance:** one duration field pattern exists in the codebase, not eleven. Net **−150 lines**. Prerequisite for T9.

---

## T3 — One stat-modifier vocabulary for relics, ball cores, paddle mods

**Problem.** The codebase has **four** parallel "id → stat tweak" subsystems and only one of them is data-driven:
- **Items** ✅ — `effect` + `magnitudePerTier` in items.json, generic `ItemEffects.ApplyOne`.
- **Relics** ❌ — 18 magnitude knobs squat in `SimConfig` (`ManaBatteryBonus`, `MidasCrystals`, `LeadPaddleWidthMult`…), application is a switch in `GameInstance.AddRelic`, and behavior checks are scattered `HasRelic("lodestone")` / `HasRelic("midas")` / `HasRelic("conductor")` string literals all over the systems.
- **Ball cores** ❌ — if-chains inline in `GameInstance.Serve()` (ghost/ember/split, ~40 lines), plus fusion special cases.
- **Paddle mods** ❌ — switch in `AddPaddleMod`.

**Task.**
- Extend relics.json / characters config with `effect` + `magnitude` fields (the item model).
- Generic stat application for the numeric ones (paddle width, mana max, regen mult, damage bonus…).
- Hook-style effects (lodestone drift, midas pay, split-on-serve) get an enum-keyed registry — one lookup site per hook point instead of `HasRelic("...")` string literals sprinkled through systems.
- Delete the 18 relic knobs from `SimConfig`; delete the `AddRelic`/`AddPaddleMod` switches and the core logic in `Serve()`.

**Acceptance:** a new numeric relic is a JSON edit. No `HasRelic("` string literal outside the registry. Net **−150 to −200 lines**.

---

## T4 — Kill double bookkeeping: derive logging from events

**Problem.** There are **206** `_log.Log` / `_log.Note` / `RaiseEvent` call sites. Nearly every action does *both*, back to back (see `CastShield`: `_log.Log(...)` then `RaiseEvent(...)` with the same coordinates). The hand-written interpolated strings are the single biggest LOC-and-noise sink in the sim, and they restate what the event already says.

**Task.**
- The event stream becomes the single record. `FileSimLog` subscribes to drained events and writes them (it already serializes JSON lines — events are already structured).
- Keep `_log` only for lifecycle notes (session open/close, errors, cheat denials) and genuinely event-less diagnostics.
- Delete the per-action `_log.Log` lines throughout `SpellSystem.*`, `BossSystem`, `BonusSystem`, etc.

**Acceptance:** an action is recorded exactly once. Net **−250 to −300 lines**.

---

## T5 — Typed sim events *(do together with T4)*

**Problem.** Events are stringly typed, and the shape is already being abused: `RaiseEvent("bossPhase", phase, prevPhase)` shoves non-coordinates into the x/y fields. The client must string-match ~30 undocumented type names.

**Task.**
- `SimEventType` enum + `SimEvent(type, x, y, payload?)`. Serialization keeps the current strings (client contract unchanged).
- `bossPhase`-style events get an honest payload field instead of coordinate abuse.

**Acceptance:** no `RaiseEvent` call passes a non-coordinate as x/y; event names are compile-checked. LOC-neutral; removes a typo bug class.

---

## T6 — Test-suite consolidation *(after T1–T3 land)*

**Problem.** Tests are **5,734 lines — bigger than the entire Core library**. `EnemyTests` (756) and `BossTests` (689) each redefine their own private `Make` / `Park` / `BallHit` helpers, and every fact embeds hand-escaped inline JSON for block catalogs and levels. Dozens of facts are the same scenario with one constant changed.

**Task.**
- Shared `SimTestKit`: fluent level builder (`Level(3,3).Row(".E.").Block('E', behavior: Emitter, emitInterval: 1.0)`), `Park`, `BallHit`, `TickFor(seconds)` — one copy, all files.
- Convert same-shape fact families to `[Theory]` tables (boss patterns per biome, pickup effects, relic magnitudes are the obvious ones).
- Sequence this **after** T1–T3: the existing suite is the safety net during those refactors; consolidating first would mean rewriting tests twice.

**Acceptance:** same behavioral coverage (mutation-spot-check a few systems), tests ≤ ~3,800 lines. Net **−1,500 to −2,000 lines**.

---

## T7 — Merge duplicate pickup-effect pairs

**Problem.** `BonusSystem.ApplyEffect` still carries the parallel vocabulary the arc analysis flagged, one level deeper: `extra_ball` vs `powerup_multiball` (same code, ×2), `mana_surge` vs `powerup_manasurge` (partial vs full refill), `wide_paddle` vs `powerup_wide` (different duration). Six switch arms for three behaviors.

**Task.**
- One effect id each with params in `bonuses.json` (`count`, `duration`, `amount`); legacy ids map to parameterized entries during load.
- Switch shrinks to one arm per behavior.

**Acceptance:** no `powerup_*` duplicate of an existing effect in C#. Net **−60 lines**.

---

## T8 — GameInstance diet: < 250 lines

**Problem.** Still 484 lines and still a god object — it just hides it better now. It owns: spell kit table, spell dispatch, relic application, ball-core logic, paddle-mod logic, 10+ ad-hoc effect fields, the tick pipeline, cheat surface, and event buffer.

**Task** (mostly fallout collection from T1–T3):
- Spell kits/dispatch → catalog (T1). Relic/core/mod switches → modifier registry (T3). Ad-hoc timers → effect list (T2).
- The 22-call `Tick()` pipeline becomes an ordered `static readonly` array of system delegates — one registration point, trivially reorderable.
- What remains: state, `Tick`, command surface (`Serve`, `SetPaddleX`, `CastSlot`), constructor.

**Acceptance:** `GameInstance.cs` < 250 lines; no content-specific string literal (spell/relic/core id) appears in it. Net **−200 lines** here.

---

## T9 — Snapshot generic effects + JSON contract cleanup *(coordinated client change — one PR with frontend)*

**Problem.** Every effect adds a bool+timer **pair** to `Snapshot`: `widePaddleActive/Timer`, `slowBallActive/Timer`, `fireshotActive/Timer`, `turretActive`, `skeletonActive`, `spellDrainActive`, `shieldActive` — 10+ fields and growing by two per feature, each requiring a server schema edit *and* a client reader edit. Plus the two ⚠️ leftovers from the arc analysis: `lives` JSON key actually carries `Hp`, and `treasureBonus` carries `CrystalBonus`.

**Task.**
- Replace the per-effect fields with `activeEffects: [{id, timeLeft}]` (depends on T2).
- Rename JSON keys honestly: `lives` → `hp`, `treasureBonus` → `crystalBonus`.
- Single coordinated PR updating frontend reader + HUD bindings; this clears both remaining ⚠️ items in `arc-analysis-success-criteria.md`.

**Acceptance:** adding a timed effect changes zero snapshot schema. Net **−40 server lines**, larger saving in future churn.

---

## T10 — Boss definitions to data *(optional — only if more bosses are planned)*

**Problem.** `BossSystem` (343 lines) hardcodes per-biome signature moves (Hell fist, Village witch-grab, Heaven seraph summon, Caverns goblin hop) and the per-phase pattern weights live in a nested switch in `ChoosePattern`.

**Task.** `BossDef` table (JSON or static): hazard kind, phase pattern weights, signature-move enum per `BossKind`. Generic patterns (aimed/rain/spread/summon) stay shared code; signatures stay code but are selected by data.

**Acceptance:** a new boss with standard patterns + one signature = data entry + one signature method. LOC-neutral; pure extensibility — **skip if boss roster is final**.

---

## Explicit won't-fix (with reasons, so nobody relitigates)

- **`Projectile` doing double duty** (friendly projectiles + hostile hazards in two lists of the same type, with `Homing`/`PiercingHitsLeft`/`AoeRadius`/`GrabbedBallId` as a union of fields). The `HazardBehavior` enum already disambiguates dispatch; splitting the type duplicates movement/collision code for no behavioral gain.
- **Binary WebSocket framing** — client-decoder rewrite; delta block caching already removed the dominant allocation. Revisit only if profiling shows serialization as a real cost.

## Sequencing

1. **T5 + T4** (typed events, then log dedup) — small, immediate −300 lines, makes everything after cleaner to verify.
2. **T2** (effect system) — unlocks T1's TimedAura archetype and T9.
3. **T1** (spell engine) — the big one; existing tests are the safety net.
4. **T3** (modifier vocabulary), **T7** (pickup merge) — same pattern, smaller scope.
5. **T8** (GameInstance diet) — fallout collection.
6. **T6** (test consolidation) — once APIs are stable, rewrite tests once, not twice.
7. **T9** (snapshot/contract) — coordinated frontend PR, last because it's the only externally visible break.
8. **T10** — only on demand.
