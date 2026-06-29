# Battle-screen visual feedback — proposal (2026-06-15)

Owner feedback on a live `hell-2` screenshot. Four items. This doc states what I found in
the current code and **what I would change** for each, so we agree on the plan *before* I
touch anything. Nothing here is implemented yet.

Reference screenshot: `frontend/ui-audit2/fb-full.png` (hell-2, fire mage).

---

## 1. "X left" should be a *fillable* thing

**What I found.** The centre of the status bar already has a progress bar
(`Hud.svelte` `.blocks-bar` / `.blocks-fill`), but it's wired backwards from how the label
reads:

- Label says **"19 left"** (blocks remaining).
- The bar `width` = `blocksPct` = *fraction cleared* — so at level start it is **empty**, and
  it fills up only as you destroy blocks.
- Result: "19 left" sitting next to an **empty** bar reads as "0 progress / nothing done",
  which is the confusing mismatch you felt.

**What I'd change.** Make the bar *mean* what the label says — a depleting "remaining" gauge:

- Bar starts **full** at the start of the level (= "19 left" = full) and **drains toward
  empty** as you clear, ending at `Clear!`.
- Keep the count text on top; recolor the fill so a near-clear bar reads as "almost done"
  (e.g. gold → green as it empties, or simply a clean gold drain — your call).
- Pure frontend change in `Hud.svelte`: invert to `width: {100 - blocksPct}%` (or compute a
  `remainingPct`), no backend touch.

*Alternative if you prefer a "completion" bar instead of a "remaining" bar:* keep it filling
up, but change the label to read **"19 / 20"** or **"5% cleared"** so the empty bar matches
the text. I recommend the **depleting "X left"** version — it matches your wording and the
arcade convention.

---

## 2. The ball is too big

**What I found.** Ball size comes from several stacked multipliers, all on a tiny 8-cell board
(cell = 32px):

| Source | Value | Effect |
|---|---|---|
| `SimConfig.BallRadius` (physics) | `8` | = 0.25 cell → 16px collision ball (½ a cell wide) |
| `BallLayer.BALL_RADIUS_FRAC` | `0.25` | render radius matches physics (good) |
| `BallLayer.BALL_SPRITE_SCALE` | `1.15` | sprite drawn 15% bigger than the ball → ~18px |
| `BallLayer.BALL_CONTRAST_MULT` / `_ALPHA` | `1.35` / `0.38` | a dark disc ~22px wide behind the ball |
| `BallTrail` | 7 dots | adds visual mass behind the ball |

So the visible ball is ~**21–22px in a 32px cell** (~⅔ of a cell). The dark contrast ring is
the biggest offender — its code comment claims it's "invisible on dark biomes," but on hell
it is clearly visible (see screenshot) and adds bulk.

**What I'd change** (keep physics and render in sync so the ball doesn't visually lie about
its hitbox):

- Drop physics `BallRadius` **8 → 6** (= 0.1875 cell, ~12px ball) — a real Arkanoid-sized ball
  with more travel room. (`AllLevelsWinnableTests` will re-confirm every level still beatable.)
- Set `BALL_RADIUS_FRAC` to match (`0.1875`) so render = physics.
- Trim `BALL_SPRITE_SCALE` **1.15 → 1.05**.
- Make the dark contrast ring **biome-aware for real**: only draw it on bright biomes
  (heaven), skip it on dark ones (hell/caverns) where it just adds grime.
- Optionally shorten the trail (`TRAIL_LENGTH` 7 → 5) so it doesn't read as a heavy tail.

Verified by screenshot after the change (ball should sit at ~⅓ cell, crisp, no dark blob).

---

## 3. Spells have no icons (they show letters)

**What I found — confirmed root cause.** The art exists and loads fine
(`/art/SpellIgnite.png`, `SpellFireball.png`, `SpellFirewall.png`, `SpellTurret.png`,
`SpellPhoenix.png` all 256px; plus a curated `/spellicons/{engineer,necromancer,paladin}/…`
set). The hotbar resolver (`hud/spellIcon.ts`) even has a legacy map that turns
`FireBallIco → /art/SpellFireball.png`.

The break is upstream: **the backend sends `icon: ""` (empty) for every loadout slot.**
Live snapshot probe:

```
loadout: [ {id:"ignite", icon:""}, {id:"fireball", icon:""}, {id:"firewall", icon:""} ]
```

`Snapshot.cs` builds each slot as `Icon = g.SpellDisplay(id)?.Icon ?? ""` — and
`SpellDisplay(id).Icon` is coming back empty, even though `config/characters.json` has the
icon keys (`"FireHeroBall"`, `"FireBallIco"`, …). So `spellIcon.ts` hits its
`if (!iconKey) → letter fallback` branch and we see "I", "F", "F".

**What I'd change.**

1. **Fix the source (backend):** make the loadout slot carry the spell's catalog icon — i.e.
   `SpellDisplay(id)` (or the loadout builder) must read `Icon` from `config/characters.json`
   for that spell id. This single fix lights up every hero's hotbar through the existing
   resolver.
2. **Audit the resolver fallthrough so nothing silently letters again:** the long atlas-path
   icons (phoenix `firemage/spell_phonex/ChosePhoenixLargeIco`, and the paladin / engineer /
   necromancer icons) should resolve to the curated `/spellicons/…` and `/art/Spell*.png`
   files. I'll add the missing entries to the legacy/icon map and verify **every spell of
   every hero** renders real art — zero letter fallbacks — by probing each hero's hotbar.
3. Acceptance test: a frontend/Playwright check that asserts each hotbar slot contains an
   `<img>` (not a text fallback) for all heroes.

---

## 4. Corner pieces for a "house-like" feel + use the full block-art set

**Owner decisions (2026-06-15):**
- Corners on **all 4 corners**, produced by **symmetry** (one base sprite, mirrored).
- **Cosmetic only** — a corner block has the **same HP as the basic block** (no gameplay
  difference, no special drop).
- **Not in every level** — use it as an occasional **design element** where a structure
  should read as a building, not on every floor.
- **Use the fuller original block-art set** — there are more block sprites than we wired up
  (the hell demon-rune family in particular).

**What I found — the art is a body+corner set in ~2 styles per biome, and we used a wrong,
inconsistent subset.** The original tree (`frontend/public/Sprites/Locationes/Objects/…`) has,
per biome, **full-body tiles** and **sloped-corner tiles** (a diagonal top edge), in roughly
two visual *styles*. Naming is inconsistent between biomes, so the reliable classifier is the
shape itself, not the filename. Hell, fully classified:

| Sprite | Shape | Style | Used today? |
|---|---|---|---|
| `StandartHell` | full body | cobblestone | ✅ as `hell_basic` (hp 2) |
| `StandartHell2` | **sloped corner** | cobblestone | ⚠️ used as `hell_tough` (hp 4) — **a corner sprite stuffed into flat rows** → the jagged top row you saw |
| `Standart2Hell` | full body | demon-rune (darker, studded) | ❌ **unused** |
| `Standart2Hell2` | **sloped corner** | demon-rune | ❌ **unused** |

So hell ships **two** of four standard sprites, and the one tagged "tough" is actually a
corner cap. The same body+corner pattern repeats in every biome (confirmed by eye):
heaven `StandartHaven`/`StandartHaven2`(corner) + `Standart2Haven`(brick body)/`Standart2Haven2`(corner);
dungeon `DungeonStandart` + `Dungeon2Standart`/`Dungeon2Standart2`(corners);
village `VillageStandart`/`VillageStandart2`/`3` + `Village2Standart`(corner) — village even has
a 3rd body variant. Plenty of unused art to bring back.

**What I'd change.**

1. **Re-cast the hell block roles so every sprite has an honest job** (and the "2hp block for
   hell" exists like the others):

   | block id | sprite | hp | role |
   |---|---|---|---|
   | `hell_basic` | `StandartHell` (cobble body) | 2 | fill |
   | `hell_basic_corner` ×4 mirrors | `StandartHell2` (cobble corner) | **2** | cosmetic corner |
   | `hell_tough` | `Standart2Hell` (demon body) | 4 | tougher block — now **visually distinct** (was a corner sprite before) |
   | `hell_tough_corner` ×4 | `Standart2Hell2` (demon corner) | 4 | cosmetic corner for demon-rune structures |

   This uses **all four** hell sprites for what they were drawn to be, and fixes the current
   "tough = a jagged corner in a row" look. Same treatment per biome, wiring in the unused
   bodies/corners (and the village 3rd variant) for visual variety.

2. **Mechanism for the 4 mirrored corners.** Flip is currently per block-*type*
   (`BlockType.FlipX/FlipY`, copied by `LevelLoader`), not per-cell — the renderer already
   honors `flipX/flipY`. So I'll add **4 corner block-types per style**, same sprite + baked
   flips (`…_corner_tl/tr/bl/br`), authorable today as 4 legend symbols, **zero engine
   change**. One base sloped sprite → all four corners by symmetry:

   ```
   C1 A A A C2     C1 = tl (base)      C2 = tr (flipX)
   A  A A A A
   A  A A A A
   C3 A A A C4     C3 = bl (flipY)     C4 = br (flipX+flipY)
   ```

   I'll pick the flip mapping by eye so the slopes chamfer the structure's outside corners.

3. **Author it as a design element, not a global rule.** Add building-shaped clusters to a
   *subset* of levels (a few per biome) — corner caps + fill body reading as a house/temple —
   and leave other levels as plain block fields. Cosmetic, so winnability is unchanged.

**Scope.** Phase B = the block-catalog work (role re-cast + corner types per biome, wiring the
unused art) + 1–2 reference levels per biome for your sign-off. Phase C = sprinkle building
clusters into the chosen levels. Judged on whether it *reads as a building*, per the design
rule (fidelity over LOC), not on file count.

---

## Suggested build order

| Phase | Items | Size | Risk |
|---|---|---|---|
| **A — quick wins** | #1 blocks bar, #2 ball size, #3 spell icons | small, mostly frontend (+1 backend icon fix) | low; verified by screenshot + a hotbar test |
| **B — block roles** | #4 role re-cast + 4 mirrored corner types per style per biome, wiring the unused art (hell demon-rune family etc.) + 1–2 reference levels per biome | medium | needs your sign-off on the look |
| **C — level pass** | sprinkle building-shaped clusters into a chosen subset of levels (design element, not all) | content | judged by "reads as a building" + winnable tests, not LOC |

Phase A I can land quickly and show you. For Phase B I'll build the catalog + reference levels
and screenshot a "house" for your sign-off before doing the Phase C pass.

---

## Appendix — block sprites present vs. wired up

Standard destructible block art that exists in `Sprites/Locationes/Objects/…` but is **not**
in `config/blocks.json` today (candidates to bring in during Phase B):

- **Hell:** `Standart2Hell` (demon body), `Standart2Hell2` (demon corner) — both unused.
  `StandartHell2` is used but mis-roled (corner sprite as a flat "tough" block).
- **Dungeon:** `Dungeon2Standart`, `Dungeon2Standart2` (corner sprites) unused.
- **Heaven:** `StandartHaven2`, `Standart2Haven2` (corners) unused; `Standart2Haven` (brick
  body) is used as the tough block.
- **Village:** `VillageStandart3`, `Village2Standart`, `Village2Standart2`, `Village2Standart3`
  unused (plus the ghost variants for ghost levels).

Each will be classified **BODY** or **CORNER** by its shape and given the right role + flips.
