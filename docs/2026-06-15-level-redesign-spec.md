# Level Redesign Spec — 12 levels/biome, teach→reinforce→combine (2026-06-15)

Authoritative spec for recreating ALL campaign levels. **4 biomes × 12 nodes = 48 levels** (11 build-up + boss each).
Follows the level-design research: introduce one mechanic in a safe context, reinforce it, then combine
("teach, test, twist") with a **difficulty saw** (density dips on the level that introduces a new element,
trends up overall). Tuned for the new arena (framed 8-wide board, 64px paddle, fair straight-down enemy lanes).

## JSON schema (every file)
```json
{
  "id": "<biome>-<n>",          // e.g. "hell-1"; boss is "<biome>-boss"
  "biome": "<hell|caverns|village|heaven>",
  "cols": 8,
  "rows": 14,
  "rows_data": [ /* exactly 14 strings, each exactly 8 chars; '.' = empty */ ],
  "legend": { "<char>": "<block id>", ... },   // only chars actually used
  "descendInterval": 14         // BOSS LEVELS ONLY (omit on normal levels)
}
```

## Hard rules (winnability — the `AllLevelsWinnableTests` auto-player must clear every level)
1. **8 columns, 14 rows. Every `rows_data` string is exactly 8 chars.** Legend covers every non-`.` char.
2. **Never fully enclose destructible (needToKill) blocks behind indestructibles.** The ball must be able to
   reach every needToKill block. Keep walls/indestructibles as borders/pillars, not sealed boxes.
3. **Bottom ~4 rows (rows 11–14) stay empty** (`........`) — that's the ball/paddle dodge zone.
4. **Top-heavy:** put blocks in rows 0–9. Leave the very top row(s) reachable.
5. **Emitters/hazard blocks**: place in their own COLUMN with empty cells below (so the telegraphed straight-down
   lane is readable). Max 1 emitter in the teach level, 2–3 later. Never stack emitters in the same column.
6. **Indestructible blocks are `needToKill:false`** so they don't block a win — but rule 2 still applies (don't trap).
7. **Boss levels:** place the boss as a **2×2 block** of its boss char near top-center (e.g. rows 1–2, cols 3–4),
   framed by a few support blocks (basic/tough/wall). Add `"descendInterval": 14`. Keep the rest sparse.
8. **Variety:** vary the SHAPE per level (cluster, checkerboard, columns, fortress, arch, diamond, side-towers) —
   not just "denser rectangle." Symmetry (left-right mirror) reads well.

## Density targets (approx. count of destructible blocks)
teach ≈ 10–16 · reinforce ≈ 16–24 · combine ≈ 24–34 · gauntlet ≈ 34–48 · boss = boss + ~10 support.

## The 12-level arc (same skeleton every biome; fill {SIG}/{B}/{C} with the biome's mechanics)
| # | id | Role | New element (introduce safely) | Blocks to use | Shape theme | Density |
|---|------|------|------|------|------|------|
| 1 | `<b>-1` | Teach basics | ball/paddle, basic block | basic | small centered cluster | teach (low) |
| 2 | `<b>-2` | Tough tier | tough (multi-hit) | basic, tough | mirrored rows | teach→reinforce |
| 3 | `<b>-3` | Walls | indestructible WALL as obstacle | basic, tough, WALL | side towers / pillars | reinforce (saw: fewer destructibles) |
| 4 | `<b>-4` | Teach SIGNATURE | the biome's emitter/hazard {SIG}, **1 only**, safe lane | basic, 1×{SIG} | open, {SIG} top-centre with clear lane | teach (saw) |
| 5 | `<b>-5` | Reinforce {SIG} | 2–3×{SIG} | basic, tough, {SIG} | two/three lanes | reinforce |
| 6 | `<b>-6` | Teach {B} | biome mechanic {B} in isolation | basic, {B} | themed to {B} | reinforce (saw) |
| 7 | `<b>-7` | Combine SIG+{B} | {SIG}+{B} together | basic, tough, {SIG}, {B} | mixed | combine |
| 8 | `<b>-8` | Teach {C} | biome mechanic {C} in isolation | basic, {C} | themed to {C} | combine (saw) |
| 9 | `<b>-9` | Reinforce {C}+walls | {C}+WALL+tough | basic, tough, WALL, {C} | fortress | combine→high |
| 10 | `<b>-10` | Twist (all, lighter) | ALL biome mechanics, medium | all | creative mix | combine (saw before finale) |
| 11 | `<b>-11` | Mastery gauntlet | ALL, densest, hardest non-boss | all | packed symmetric | gauntlet |
| 12 | `<b>-boss` | Boss | biome boss (2×2) + support | boss, basic, tough, WALL | boss arena | boss |

## Per-biome mechanic mapping + legend

### HELL — `biome: "hell"` (boss: hell_demon_boss)
- {SIG} = **hell_ballspawner** (emitter, shoots a straight-down lane) · {B} = **hell_teleporter** (ball teleports between same-colour pads) · {C} = **hell_lava / hell_lava_spawner** (creeping lava hazard)
- Legend: `A` hell_basic · `B` hell_tough · `W` hell_obsidian · `E` hell_ballspawner · `T` hell_teleporter · `U` hell_teleporter_blue · `L` hell_lava · `S` hell_lava_spawner · `D` hell_demon_boss
- Teleporters come in colour PAIRS (place two `T` so the ball has an entry+exit; same for `U`).

### CAVERNS — `biome: "caverns"` (boss: cavern_goblin_boss)
- {SIG} = **cavern_stalactite** (falls when ball passes under) + **cavern_cart** (rolls across the paddle row) · {B} = **cavern_bomb** (explodes neighbours on death) · {C} = **cavern_union** (linked blocks)
- Legend: `A` cavern_basic · `B` cavern_tough · `W` cavern_rock · `U` cavern_union · `M` cavern_bomb · `K` cavern_stalactite · `C` cavern_cart · `G` cavern_goblin_boss
- Caverns theme = vertical channels (columns of blocks with empty gaps between).

### VILLAGE (Witchland) — `biome: "village"` (boss: village_witch_boss)
- {SIG} = **village_beholder** (emitter) · {B} = **village_ghost** (phases) + **village_bat** (carries the ball) · {C} = **village_necromant** (revives dead neighbours) + **village_portal** + **village_cauldron**
- Legend: `A` village_basic · `B` village_tough · `E` village_beholder · `H` village_ghost · `T` village_bat · `N` village_necromant · `P` village_portal · `C` village_cauldron · `X` village_witch_boss
- No indestructible wall in this biome → use `B` (tough) as the obstacle in the "walls" level (#3).

### HEAVEN — `biome: "heaven"` (boss: heaven_angel_boss)
- {SIG} = **heaven_melee_statue** (emitter) · {B} = **heaven_windmaster** (pushes the ball with wind) · {C} = **heaven_shield_statue** (shields neighbours) countered by **heaven_altar** (pacifies statues) + **heaven_vase** (level-up risk)
- Legend: `A` heaven_basic · `B` heaven_tough · `W` heaven_statue · `E` heaven_melee_statue · `M` heaven_windmaster · `S` heaven_shield_statue · `T` heaven_column_top · `I` heaven_column_mid · `J` heaven_column_bottom · `L` heaven_altar · `V` heaven_vase · `N` heaven_angel_boss
- Columns stack vertically: `T` on top of `I` on top of `J` (top→mid→bottom) to form temple pillars.

## Reference examples
- Normal: `config/levels/hell-1.json`. Boss: `config/levels/hell-boss.json`.

## After authoring
Campaign chain becomes 48 nodes in order (hell-1..11, hell-boss, caverns-1..11, caverns-boss, …). `config/campaign.json`
is regenerated separately. Validation gate: `dotnet test` (AllLevelsWinnableTests must stay green).
