# Village / Ghost-Phase Rework — Spec

**Owner-approved 2026-06-16.** Scope: make Witchland's ghost-phase mechanic actually work as designed.
Decisions taken before writing this (owner): **separate-cell layer model** (no overlapping cells),
**necromancers revive killed blocks as ghosts**, **spec doc first then implement+screenshot-verify**.

Design source of truth: `docs/12-biome-identity.md` (Witchland rows). This spec extends it; it does **not**
override it. Fidelity is judged by *trigger + on-screen identity*, not LOC.

---

## 1. The design (what it's supposed to be)

Witchland's verb is **PHASE**. There are two block "layers" sharing one board:

- **Physical** blocks — the normal/solid layer.
- **Ghost** blocks (`ballPhases`) — the spectral layer.

Rules (owner's words, restated):

- A **normal** ball hits **physical** blocks and **passes through ghost** blocks.
- A **ghost** ball hits **ghost** blocks and **passes through physical** blocks.
- A **portal** block toggles the ball between normal ⇄ ghost (and the ball passes through the portal).

Doc idioms this must deliver:

- **The double board** (`docs/12` idiom A) — a ghost-layer layout *interleaved* with the solid layout;
  portals are the keys between the two worlds.
- **The guarded heart** (`docs/12` idiom B) — a necromant/cauldron protected *behind ghost blocks*; you
  must **phase in** to silence it.
- **Necromancy is layered/symmetric** (owner 2026-06-16, refined): a corpse keeps its *nature*.
  - A **regular** block leaves a **regular corpse**; a **regular necromant** (`village_necromant`) raises it
    back as a **regular** block.
  - A **ghost** block leaves a **ghost corpse**; a **ghost necromant** (`village_necromant_ghost`,
    `ballPhases`) raises it back as a **ghost**.
  - Cross-layer is inert: a regular necromant never touches a ghost corpse and vice-versa. A ghost necromant
    lives on the ghost layer, so it can **only be killed by a phased ball** — this is the literal
    "guarded heart: phase in to silence it."

**Layer model = SEPARATE CELLS** (owner choice). Ghost and physical blocks never share a grid cell. The
"two layers" read is produced by *layout* (checkerboards, ghost pockets, ghost walls in front of a heart)
and by *rendering* — not by stacking two blocks in one cell. This keeps the existing one-block-per-cell
grid (`GameInstance.State.cs` `_blockGrid`) untouched.

---

## 2. Current state — the drift (why it "isn't working")

| # | Defect | Evidence |
|---|--------|----------|
| D1 | **village-6 & village-7 have ghost blocks but NO portal.** A normal ball phases through them and can never become ghost → those blocks are unclearable by play. | `config/levels/village-6.json`, `village-7.json` (no `village_portal`) + `BallSystem.cs:221` |
| D2 | **The winnable test hides D1.** Its "chip-assist" cheat damages *every* destructible regardless of phase when progress stalls, so unwinnable ghost levels still pass. | `AllLevelsWinnableTests.cs:139` (`chipBlocks`) |
| D3 | **village-8 has a portal but no ghost blocks** → the portal toggles a mode that interacts with nothing. | `config/levels/village-8.json` |
| D4 | **No level actually builds the doc idioms.** village-10/11 have both pieces but the ghost blocks sit in their own top rows; "double board" and "guarded heart" (necromant behind ghost) are absent. | `village-10.json`, `village-11.json` |
| D5 | **Rendering doesn't communicate the active layer, and the ghost art is unused.** Ghost blocks always look ghostly and physical always solid regardless of the ball's phase; the ball only gets a faint purple tint. `BallGhost.png` and the ghost block variants/damaged states are not wired. | `BlockLayer.ts:305`, `BallLayer.ts:109/151` |
| D6 | **Necromant revives blocks as their PHYSICAL selves, not ghosts.** `ReviverSystem` does `Dead=false; Hp=MaxHp` in place — no layer change. | `ReviverSystem.cs:38-40` vs `Scripts/.../Necromant/NecromantController.cs` |

What is already correct and will be **kept**: the per-ball phase flag (`Ball.Ghost`), the portal toggle +
cooldown (`BallSystem.cs:158-167`), the ball-vs-block phase filter for separate cells
(`BallSystem.cs:212-222`), and the snapshot `ghost`/`ballPhases` fields.

---

## 3. The fixes

### 3.1 Sim / rules (`Arkanoid.Core`)

1. **Layered necromancy (D6).** `ReviverSystem` matches the corpse's layer to the necromant's layer:
   - `AnyReviverAlive(g, ghost)` → a non-dead Reviver whose `BallPhases == ghost`.
   - `OnBlockDestroyed`/`Update` only queue/raise a corpse if a **same-layer** necromant lives; the block
     returns on the **same layer it died on** (no nature change — `Dead=false; Hp=MaxHp;`).
   - New block type `village_necromant_ghost` (`ballPhases:true`, `behavior:"Reviver"`, sprite
     `VillageDeathGhost`). A regular necromant (`village_necromant`) is unchanged.
   - **Consequence/constraint:** a level with **ghost blocks or a ghost necromant** must contain a portal,
     or the ghost layer is unclearable → soft-lock. Enforced by the structural test in §3.4. (A regular
     necromant needs no portal.)

2. **Phase gating stays ball-only; spells/projectiles remain phase-agnostic.** Decision: spell projectiles
   can damage either layer (current behavior). Rationale: the ball-phase loop is the puzzle; spells are a
   limited, deliberate "skip". (If playtest shows a Fire Mage trivializes ghost levels, a follow-up can gate
   projectiles by the caster ball's phase — logged, not done now.)

3. **No grid change.** Separate-cell model needs none.

### 3.2 Levels (`config/levels/village-*.json`)

Bake one hard rule and the two idioms across the arc:

- **Invariant:** any village level containing **ghost blocks (`village_ghost`) OR a necromant
  (`village_necromant`)** must contain **≥1 `village_portal`** (≥2 placed for routing where space allows).
- **village-6, village-7 (D1):** add portals + reshape so the ghost cells form a real phase pocket the
  player must route into. Keep difficulty in line with the §2026-06-16 difficulty tuning.
- **village-8 (D3):** add ghost blocks so the existing portal matters (intro "double board" lite), OR drop
  the portal. Prefer adding ghost content (this is the mechanic's tutorial level).
- **village-10/11 (D4):** rebuild into the actual idioms —
  - **Double board:** interleave ghost and physical cells (e.g. checkerboard / alternating columns) so the
    player clears the physical pass, portals, then clears the ghost pass.
  - **Guarded heart:** wall the necromant (and/or cauldron) behind `village_ghost` cells so it can only be
    reached/killed after phasing in — directly realizing `docs/12` idiom B and §3.1's revive-as-ghost.
- Progression of the mechanic across the village arc (suggested): introduce phasing (8) → phase pocket
  (6/7) → double board (10) → guarded heart + revive-as-ghost (11) → boss.
- Re-verify every village level with the passability suite **after** the §3.4 test fixes (so D2 can't hide
  a regression).

### 3.3 Rendering / art (`frontend` — the "find the sprites" part)

Sprites exist in `Sprites/Locationes/Objects/Location_3_Village/` (and are bundled under
`frontend/public/...` / atlas). Wire them:

- **Ghost ball:** when `ball.ghost`, draw the dedicated **`BallGhost.png`** (with a spectral aura), not just
  the `0xaa88ff` tint. (`BallLayer.ts`)
- **Phase-reactive blocks (D5):** blocks always show their layer identity (ghost = translucent/spectral,
  via `VillageStandart2Ghost` + a pulsing alpha as today), **and** the layer matching the controlling
  ball's current phase gets an "active" emphasis (full alpha + soft glow) while the off-phase layer dims.
  Controlling phase = phase of the alive ball nearest the paddle (single source; with mixed-phase
  multiball, emphasize both — never hide a hittable block). Expose this read via the existing per-ball
  `ghost` flag (renderer derives it; no new required snapshot field, but a convenience `activePhase` on the
  snapshot is acceptable).
- **Sprite map for ghost variants** (use the real damaged states instead of generic tint-only):
  - `village_ghost` (hp1) → `VillageStandart2Ghost` + `VillageStandart2GhostDamaged` for the chipped state.
  - revived-as-ghost tough (hp3) → `Village2Standart2Ghost` (+ damaged) so a revived `village_tough` reads
    as a tough ghost.
  - portal → `Portal` / `VillagePortal` (animate a slow spin/pulse so it reads as a gate).
- **Portal flip VFX:** on the `ghostPortal` event, a brief phase-swap flash on the ball so the toggle is
  legible.

### 3.4 Tests (design-fidelity; red-before-green)

Write these to FAIL against current code first, then make them pass:

1. `Ghost block identity`: a **normal** ball overlapping a `ballPhases` block deals **0** damage and passes
   through; a **ghost** ball overlapping it **damages/kills** it. (Park balls in empty cells per the known
   ResolveBlocks gotcha.)
2. `Physical block identity`: a **ghost** ball passes through a normal block; a **normal** ball damages it.
3. `Portal toggles phase`: hitting a `village_portal` flips `ball.Ghost`, respects `TeleportCooldown`, and
   the ball passes through the portal cell.
4. `Necromant revives as ghost`: kill a **physical** block while a necromant lives → after `ReviveDelay`
   the block is alive **and `BallPhases == true`** (and ghost sprite); killing the necromant first cancels.
5. `Level invariant (structural)`: for every `config/levels/village-*.json`, if it contains `village_ghost`
   or `village_necromant`, it contains `village_portal`. (This is the test that would have caught D1; it
   replaces relying on the chip-assist suite.)

### 3.5 Verification (screenshots, critically reviewed — not "it rendered")

Playwright runs on the reworked village levels capturing:
- ball **normal** → ghost blocks dimmed/spectral, physical solid;
- after touching a portal → ball uses `BallGhost` art, ghost blocks emphasized, physical dimmed;
- a necromant **revive producing a ghost block** (physical kill → spectral revival);
- the guarded-heart level: necromant unreachable until phased in.
Reviewed against this spec before "done" (per the visual-quality bar).

---

## 4. Build order / status

1. ✅ Sim: layered necromancy in `ReviverSystem` (same-layer revive) + new `village_necromant_ghost`
   block type + tests (ghost/physical identity, portal toggle, both necromancers). 645 backend tests pass.
2. ✅ Structural test `VillagePhaseInvariantTests` + fixed `village-6` (ghost intro), `village-7` (double
   board), `village-8` (regular-necromant regen), `village-9` (ghost necromant). All 48 levels winnable.
3. ✅ `village-10` grand double board; `village-11` dual-necromant climax (race the regular roof + phase
   the ghost heart).
4. ✅ Rendering/art: `BallGhost` sprite + spectral glow when phased; phase-reactive block emphasis (active
   layer solid, off-phase dim); `VillageDeathGhost` ghost necromant wired; `VillagePortal` art; spectral
   portal flash on `ghostPortal`. `vite build` clean.
5. ✅ Verification: backend suite (645) ✅, `vite build` ✅. Phase screenshots reviewed (village-9 & -11):
   normal phase → ghost blocks faint/physical solid; ghost phase → ball gets the spectral BallGhost glow,
   ghost blocks solidify, physical dims; ghost necromant (`VillageDeathGhost`) + regular necromant
   (`VillageDeath`) both render in the climax. NOTE: local headless WebGL (SwiftShader) fails to init on
   this machine, so the Playwright render specs (incl. `village-phase.spec.ts`) can't capture here — every
   render spec fails identically locally; verified instead via the Playwright **MCP** browser (real
   WebGL) against the dev server, using a fresh `ark_pid` to avoid the `default` profile's active-run
   level override. The specs are valid for CI.

## 5. Out of scope / logged

- True overlapping double-board (two blocks per cell) — owner chose separate-cell; not doing.
- Gating spell projectiles by phase — deferred pending playtest (§3.1.2).
- Ghost variants of chest/potion/enemies (`VillageChestGhost`, `VillagePotionGhost`, `BatGhost*`,
  `Beholder*Ghost`) — available art; wire opportunistically if a reworked level uses those specials on the
  ghost layer, else logged for later.
