# 12 — Biome Identity: Every Level Unmistakably Belongs Somewhere (2026-06-09)

> Companion to `11-enemy-design-proposals.md` (which gave each biome an enemy *verb*).
> This doc extends biome identity to the **levels themselves**: layout idioms, pacing,
> objectives, and atmosphere — so a screenshot with the palette stripped out would
> still tell you which biome you're in. Directly addresses flaw C3 ("biomes differ
> only by palette + 1 gimmick") at the design level instead of the asset level.

---

## 0. The test

**The Greyscale Test:** render any level in greyscale, hide the background. If you
can't name the biome from the *shapes and behaviour* alone, the level fails. Today
all 14 levels fail it (symmetric stamps in the top half of the board, different
colors). The rules below make levels pass it.

## 1. The identity matrix (one row per biome — nothing shared between rows)

Each biome owns, *exclusively*: a fantasy, a verb (from doc 11), **two layout
idioms**, **one pacing mode**, **one objective flavor**, and an atmosphere kit.
Exclusivity is the point — if Caverns levels can also descend, descent stops meaning
"Hell."

| | **HELL — The Furnace** | **CAVERNS — The Mine** | **WITCHLAND — The Haunt** | **HEAVEN — The Trial** |
|---|---|---|---|---|
| **Nature** | The board is a machine that routes the ball. Claustrophobic, channeled, ballistic. | The board is unstable. Everything falls, rolls, or detonates. | The board cheats. Nothing stays dead, nothing is solid. | The board judges. Ordered, symmetric, conditional — everything can be friend or foe. |
| **Verb** (doc 11) | ROUTE | CHAIN | RACE / PHASE | CONVERT |
| **Layout idiom A** | **Channels & funnels** — obsidian walls that force the ball along paths (hell-2 has the seed of this; make it the rule, with flipX-mirrored corners so walls read as walls) | **Veins** — destructible seams threaded through indestructible rock, primed with bombs so one good hit unzips a vein | **The double board** — a ghost-layer layout interleaved with the solid layout; portals are the keys between the two worlds | **The colonnade** — column stacks forming a temple façade; symmetric, vertical, stately |
| **Layout idiom B** | **Teleporter circuits** — colour-paired skull loops that *reward* deliberate routing (exit above a rich pocket); finally uses `hell_teleporter_green` | **Ceiling threat** — stalactite fields over enemy nests; trigger them as weapons (doc 11) | **The guarded heart** — necromant/cauldron protected behind ghost blocks; you must phase in to silence it | **The sanctum choice** — altar and vase placed at *opposite* reachable spots; the level's first decision is which one you route to |
| **Pacing mode** (exclusive) | **Descending blocks** (StrechedLevel port) — the furnace presses down | **Multi-floor collapse** (port) — clear a floor, the mine shaft drops you deeper | **Revive pressure** — necromant cadence sets the level's clock (no new system: enemy IS the pacing) | **Statue escalation** — uncleared statues slowly self-level over time; dawdling makes the trial harder |
| **Objective flavor** (exclusive) | *Breach*: destroy the core blocks behind the moat/channels (subset-kill win) | *Demolition*: clear via N chain reactions / collapse all veins before the timer | *Exorcism*: kill the named thing (necromant/witch) — survival until then, blocks optional | *Judgement*: protect-the-altar escort (lose if the boss/statues destroy it) or convert-and-conquer |
| **Atmosphere kit** (all art exists) | Ember particles, heat shimmer; `LavaBegining/MainPart/End` runs as level *borders*; `HellChest` treasure | Dust motes, pebble falls; `Stone/StoneLight` texture variety; `DungeonCart` rail props; `ChestDungeon` | Fog layer, `VillageShadow` drifting silhouettes, `DeathSphere` wisps; pots/broom set dressing | `Cloud/HeavenClouds` parallax drift, light rays, `GraalHaven` shrine prop; `HolyBall` motes |
| **Music/SFX brief** (G1) | Low drone, metallic impacts | Percussive, echoing knocks, rumbles | Sparse, whispery, detuned chimes | Choral pads, bell impacts |

**Campaign teaching order falls out for free:** Hell teaches aiming along routes →
Caverns adds planning (chains) → Witchland adds time pressure (races) → Heaven
composes everything and adds choice (convert). Difficulty comes from *composition*,
not just HP (which is currently the only ramp — `balance.md`).

## 2. Consistency rules (enforceable, not vibes)

Add to `tools/gen-levels.mjs` validation (it currently checks only 8×14 + winnability):

1. **Marker rule:** every level must contain ≥1 instance of one of its biome's two
   idioms (machine-checkable: hell → ≥4 connected obsidian cells or ≥2 same-colour
   teleporters; caverns → ≥2 bombs or ≥3 stalactites; village → ≥4 ghost cells or a
   necromant/cauldron; heaven → ≥1 column stack or altar+vase pair).
2. **Exclusivity rule:** pacing modes and objective flavors may not cross biomes.
3. **Block-set rule:** a level uses only its biome's block ids (already true — keep it).
4. **Depth rule:** ≥60% of rows 0–9 occupied (kills the "top-half stamp" look, flaw C2);
   asymmetric layouts allowed and encouraged everywhere except Heaven (symmetry IS
   Heaven's identity — its asymmetry budget is zero, which itself is a tell).
5. **Crescendo rule:** within a biome, level N+1 must add one element absent from
   level N (new enemy, new idiom, or the pacing mode) — the biome's nature *unfolds*
   instead of repeating.

## 3. What this changes in practice

- **Now (with doc 11 slice 1):** atmosphere kits are pure renderer work — ember/fog/
  cloud ambient layers per biome + the border/prop art. Cheap, transforms the
  greyscale test immediately.
- **G3 (30+ levels, docs/09):** author levels *against the matrix* — each biome gets
  ~7 levels = 2 per idiom + 1 pacing showcase + 1 composite + boss. The two pacing
  ports (descending, multi-floor) land here.
- **Objective flavors** need small win-condition extensions (subset-kill exists via
  `needToKill`; add timer and protect-target — both are listed in docs/09 G3 already,
  this doc assigns them owners).
- **Rifts (G3):** a generated rift inherits the floor's biome matrix row — so even
  procedural floors keep biome nature (pick idiom template + enemy mix from the row).

## 4. Restraint

- No fifth biome, no biome remixes ("corrupted heaven") until all four pass the
  greyscale test across their full level sets.
- Atmosphere layers are background-only — nothing ambient may move in the play
  field's contrast range (readability of ball/hazards wins every conflict).
- The matrix is a *budget*, not a checklist: a level showcases its row, it doesn't
  cram the whole row in.
