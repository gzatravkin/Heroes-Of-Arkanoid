# Level Format (AI-generatable)

Levels are plain JSON in `config/levels/<id>.json`. The backend (`Grid/LevelLoader.cs`) reads them directly at battle start — no build step. The format is deliberately simple so levels can be authored by hand, by the in-browser editor (`?scene=editor`), or generated programmatically/by an AI.

## Schema
```json
{
  "id": "hell-2",          // must match the filename (^[a-z0-9-]+$)
  "biome": "hell",         // hell | caverns | village | heaven (display/grouping)
  "cols": 12,              // grid width in cells
  "rows": 8,               // grid height in cells
  "rows_data": [           // exactly `rows` strings, each exactly `cols` chars
    "............",
    "AAAAAAAAAAAA",
    "..."
  ],
  "legend": { "A": "hell_basic", "B": "hell_tough" }  // char -> block-type id
}
```
- `.` is always an empty cell (no block). Any char not in `legend` is treated as empty.
- Each string in `rows_data` MUST be exactly `cols` characters; there MUST be exactly `rows` strings.
- A level is **winnable** when all its `needToKill` blocks are destroyed. Include at least one destructible `needToKill` block or the level wins instantly.
- Blocks occupy whole integer cells — there is **no per-block scale** (the fix for the old game's standardization bug). Difficulty comes from block choice, density, and layout, not from resizing.

## Block-type ids (from `config/blocks.json`)
| Biome | Destructible (needToKill) | Special |
|-------|---------------------------|---------|
| hell | `hell_basic` (hp2), `hell_tough` (hp3) | `hell_obsidian` (indestructible), `hell_teleporter` (warps ball, pairs cyclically), `hell_demon_boss` (hp20, fires hazards that damage HP) |
| caverns | `cavern_basic` (hp2), `cavern_tough` (hp3) | `cavern_rock` (indestructible) |
| village | `village_basic` (hp2), `village_tough` (hp3) | `village_ghost` (ball passes THROUGH — must be cleared with spells; needToKill) |
| heaven | `heaven_basic` (hp2), `heaven_tough` (hp3) | `heaven_statue` (indestructible) |

## Design guidance (difficulty curve)
- **Early biome levels:** mostly `*_basic`, open layout, few/no indestructibles.
- **Mid:** mix in `*_tough` cores and a few indestructible obstacles to shape the ball's path.
- **Late / signature:** biome mechanic front-and-center — Hell teleporters/boss, Village ghost cores (force spell use), Heaven statue mazes.
- Keep a clear path for the ball to reach every `needToKill` block (don't fully wall them off with indestructibles).
- Standard board is `12 x 8` (cell size 32). Larger boards (e.g. `14 x 9`) are allowed; the renderer auto-fits.

## Validation
`tests/all-levels.spec.ts` loads every level and asserts it parses (blocks present) and is winnable — so a malformed generated level fails CI immediately.
