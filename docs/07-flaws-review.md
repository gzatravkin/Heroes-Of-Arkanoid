# Flaws Review — Self-Audit (2026-06-09)

Honest review of the current build against (a) the mobile screenshots in
`tests/demo-screenshots/`, (b) the original Unity game's art in `Sprites/`, and
(c) a critical eye for "generic AI-kit" UI. Each item has a **severity** (P1 worst →
P3 polish) and a concrete fix. Nothing here is fixed yet — this is the punch-list.

Reviewed shots: `move1-home-*`, `move3-rift-banner`, `move4-bars-{full,half,empty}`,
`move4-boss-{full,half,empty}`, `move5-{hell-2,caverns-1,village-ghost,heaven-2}`,
`p7-{skills,achievements,settings,tutorial}`.

---

## A. Missing blocks / mechanics from the original game

The original `Sprites/` ships art for whole block families the current build never
uses (`config/blocks.json` references only 16 sprites). The biomes feel same-y
because each is just "colored rectangle + one gimmick" while the original had a
distinct toy per location.

| # | Missing | Where (original art) | Sev | Note |
|---|---------|----------------------|-----|------|
| A1 | **Bomb blocks** (explode + chain to neighbours) | `Location_2_Dungeion/Bomb.png`, `GrateBomb.png`, `*Stand*` | **P1** | Caverns' signature toy. 0 gameplay refs. Biggest single content gap. |
| A2 | **Cauldron blocks** (`Kotelok1/2/3` + death frames) | `Location_3_Village/Blocks/Kotelok*.png` | **P1** | The Witchland signature block. **0 refs** anywhere. Village currently only has plain + ghost blocks. |
| A3 | **Block damage states** (cracked sprites as HP drops) | every block has `*Damaged` / `*Destroyed` | **P1** | Renderer fakes damage with a crude alpha fade (`Renderer.ts:753` `alpha = 0.4+0.6*hp/maxHp`) instead of swapping to the real cracked art. Blocks look like they're *vanishing*, not *breaking*. |
| A4 | **Heaven Columns** (multi-part pillars: Top/Bottom/Damaged) | `Location_4_Heavens/Column*.png` | P2 | Heaven uses only flat statue/standart blocks; the tall column structures (real silhouette variety) are unused. |
| A5 | **Village Portal block** (teleporter) | `Location_3_Village/Blocks/Portal.png` | P2 | Teleporter routing only exists in Hell (red skull). Village had its own portal. |
| A6 | **Colored skulls** (blue/green, + Active frames) | `Location_1_Hell/Skull{Blue,Green}*.png` | P2 | Only `SkullRed` is used (as the teleporter). The blue/green skulls (paired-portal colors / variant hazards) are unused. |
| A7 | **Heaven boss + altar/defender** | `HeavenBoss`, `HeavenBossGlobe`, `HeavenAltarV2`, `HeavenDefender*`, `GraalHaven` | P2 | The campaign just **ends at `heaven-2`** — no 4th boss, no finale. Hell/Caverns/Witchland have bosses; Heaven doesn't. |
| A8 | `Stone` / `StoneLight` caverns variants | `Location_2_Dungeion/Stone*.png` | P3 | Extra rock art unused; caverns rock is one flat `DungeonInvulnerable`. |

**Net effect:** four biomes that should each feel mechanically distinct currently
reduce to palette swaps. A1–A3 are the high-impact fixes.

---

## B. Bar visualization flaws

The 3-slice rebuild fixed the "half bar" bug (caps are correct now), but:

| # | Flaw | Sev | Fix |
|---|------|-----|-----|
| B1 | **Spare-balls bar fill is near-invisible.** The pale blue→white gradient (`move4-bars-*`) has almost no contrast against the light frame interior — you can't read the fill level. | **P2** | Use a saturated colour (cyan/teal or the actual `LifeBall` blue), or render balls as discrete pips. |
| B2 | **Continuous bar is wrong for tiny counts.** Lives/spare-balls are small integers (3, 5). A smooth bar reads worse than the original's discrete heart/ball **orbs** for "how many do I have left." We *lost* at-a-glance counting to gain a bar that doesn't need to be a bar. | P2 | Revert HP/balls to pip/orb rows (original `BattleLifeBall` / heart), keep bars only for the continuous resources (mana, boss). |
| B3 | HP/balls bars are tiny (118px) with a cramped icon+number overlay. | P3 | Slightly larger, or pip row per B2. |
| B4 | Bar middle is the *stretched empty-sprite interior* (border-image `fill`), so it looks faintly soft/smeared vs the crisp caps. | P3 | Acceptable; could tile instead of stretch. |

Boss bar (`move4-boss-*`) is good — name + colour-tier fill read clearly.

---

## C. Level design flaws

| # | Flaw | Sev |
|---|------|-----|
| C1 | **Hell "channels" are red demon-face skull blocks** (`HellInvulnerable`). They read as *enemies/faces*, not walls/channels (`move5-hell-2`). The funnel intent is lost. Use a plain obsidian/rock-looking indestructible, reserve faces for hazards. | P2 |
| C2 | **Sparse playfield.** Blocks occupy only the top ~40% of the tall portrait board; the lower 60% is empty (`all move5-*`). Necessary for paddle travel, but it reads as "half-finished level." Push layouts taller / add a second cluster. | P2 |
| C3 | **Biomes differ only by palette + 1 gimmick.** Without A1–A6 (bombs/cauldrons/portals/columns) every level is "rectangles in a shape." | P2 (rolls up to A) |
| C4 | **Ghost blocks too faint** (`move5-village-ghost`) — the translucent interior is so low-contrast it looks like empty space, not phaseable blocks. Add an outline/shimmer. | P3 |
| C5 | Caverns "tough" rock blocks are sparse/thin; stalactite columns read but feel skeletal. | P3 |

---

## D. UI quality — "generic AI-kit" tells

The shell leans on dark navy/black gradients + gold-stroke text everywhere, which is
exactly the generic-fantasy-mobile-kit look. Specific offenders:

| # | Screen | Flaw | Sev |
|---|--------|------|-----|
| D1 | **Skills** (`p7-skills`) | A **blank grey box** sits top-left next to "Skill Upgrades" — looks like a broken/placeholder image. Class tabs are flat pills; "+" buttons are flat grey. | **P1** (broken-looking) |
| D2 | **Achievements** (`p7-achievements`) | Badge labels are in **Russian** (`Новичок`/`Берсерк`/`Мастер`/`Эксперт`) and every badge is the **same trophy icon**. Reads as untranslated placeholder. | **P1** |
| D3 | **Settings** (`p7-settings`) | Dev watermark **"Arkanoid RPG — P7b"** shipped on screen; bottom ⅔ is empty black void; toggles are generic purple. | P2 |
| D4 | **Home menu** (`move1-home-*`) | Big empty black gap between the two buttons and the dock; only two CTAs on a tall screen feels barren. Docked icons are tiny and a couple read as blank squares. | P2 |
| D5 | **Campaign map** (`move3-rift-banner` bg) | Every node is the **same glassy orb with the same lock icon** — nodes don't convey biome or progress at a glance; labels are hard to read on the dark bg. | P2 |
| D6 | **Tutorial** (`p7-tutorial`) | "Your hero paddle" is illustrated as a **green orb**, not the paddle sprite — misleading first impression. | P3 |
| D7 | Global | Heavy reliance on the same dark-gradient + gold-stroke title treatment on every screen makes them blur together. Vary per-biome accent / texture. | P3 |

---

## Suggested fix order (highest ROI first)

1. **D1 + D2 + D3** — kill the broken-looking grey box, the Russian/duplicate badges, and the dev watermark. These are the loudest "unfinished" tells and are cheap.
2. **A3** — wire real `*Damaged`/`*Destroyed` sprites (swap by `hp/maxHp` bucket) instead of alpha-fade. Instantly makes every block feel tactile.
3. **B1 + B2** — fix the invisible balls bar; consider reverting HP/balls to orb pips.
4. **A1 + A2** — implement Bomb (caverns) and Cauldron (village) blocks — the two signature toys that make biomes feel distinct.
5. **C1 + C2** — non-face Hell channels; denser/taller layouts.
6. **A7** — a Heaven boss to actually end the campaign.
7. Remaining P2/P3 polish (A4–A6, C4–C5, D4–D7).
