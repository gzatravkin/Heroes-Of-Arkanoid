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

---
---

# Round 2 — Expanded review (verified against Sprites/ + Scripts/ + design docs)

Added shots reviewed: `ui-{characters,inventory,skills,campaign,dungeon-run}`,
`spell-{ignite,fireball,firewall,turret}`, `spell-{paladin-shield,engineer-lightning,necromancer-skeleton}`.

## E. Missing ENEMIES & hazards per location (the big one — verified)

The original was an action-RPG breakout: **each location had multiple moving enemies that
shoot/attack you and environmental hazards**, confirmed by art in `Sprites/Locationes/Objects/`,
controller scripts in `Scripts/`, and `docs/01-current-game-design.md`. The current build has
**none of them** — only the static boss-hazard. This is the single biggest reason it "looks like
a shit / feels empty." Full inventory of what exists in the assets but is **unused**:

**Hell (`Location_1_Hell`)**
- **Hell Ball Spawner** (`HellBallSpawner`, `HellBallLvl1/2/3`, `HellBallMissile`, `HellBallDamage`) — a maw that **spawns homing fireballs at the paddle**. Real enemy. **P1**.
- **Lava Spawner / lava flow** (`LavaSpowner(+Active/Damaged/Destroyed)`, `LavaBegining/MainPart/End`; scripts `BlockEffects_OpenLavaSpawner`, `LavaBlockEater`) — the signature Hell hazard (design doc calls it a stub even in the original, but the art + opener exist). **P2**.
- **Color-paired teleporters** (`Skull` Red/Blue/Green + Active + `SkullAnimation`) — design doc: "color-paired portals warp the ball." Current uses ONLY red as a single teleport. The pairing-by-color mechanic + blue/green skulls are unused. **P2**.
- **Chains** (`ChainHell`, `ChainMainHell(+Damaged/Destroyed)`) — decorative/structural. P3.

**Caverns (`Location_2_Dungeion`)**
- **Stalactites** (`Stalactite`, `Stalactite2`) — dedicated downward-spike art; design doc: the **Goblin boss "drops stalactites"** and there's "stalactite scatter." Completely unused as a hazard/block. **P1** (user explicitly called this out).
- **Bombs** (`Bomb`, `GrateBomb`, `*Stand/Vertical`) — explosive chain blocks. **P1** (= A1).
- **Mine cart** (`DungeonCart`, `DungeonCartWheel`) — a rolling hazard/prop. P3.
- `Stone`/`StoneLight` rock variants — unused. P3.

**Witchland (`Location_3_Village`) — the deepest roster, all unused**
- **Beholders** (`Beholder1/2/3` + `Ghost` variants, `BeholderAttackAnimation`, `BeholderDeathAnimation`, `BeholderMissile(+Ghost)`; script `BeholderController/LookAtBall`) — flying eyes that **track the ball and shoot missiles**. **P1**.
- **Bats** (`BatFlyAnimation*`, `BatSleeping`, `BatGhost*`; scripts `BatController`, `SleepingBatController`) — sleeping bats that wake and fly. **P1**.
- **Necromant / Death** (`VillageDeath(+Ghost)`, `*CastAnimation`, `*DeathAnimation`, `DeathSphere`, `DeathMark.cs`) — design doc: "**Necromant enemy that revives destroyed blocks**" + casts death spheres. **P1**.
- **Cauldrons** (`Kotelok1/2/3` + Death) — signature Witchland block. **0 refs** (= A2). **P1**.
- **Portals** (`Portal`, `VillagePortal`, `VillagePotalLarge`; script `GhostPortalController`) — ghost-layer portals. **P2**.
- Pots/potions/broom/shadow props (`VillagePotion`, `VillageMetla`, `VillageShadow`, `VillageCorrupt`) — set dressing, unused. P3.

**Heaven (`Location_4_Heavens`) — "most developed biome" in the original, now the emptiest**
- **Statue enemies** (`HeavenDefender(+Active)`, `HeavenMeleeStatue(+Active/glowing parts)`, `ShieldStatue.cs`, `MeleeStatue.cs`, `StatueController.cs`, `AbstractStatue.cs`) — statues that **activate and attack**, and (design doc) can be **turned ally / leveled by hitting an Altar/Vase**. **P1**.
- **WindMaster** (`WindMaster2`, `WindMasterV2Circle/FromCircle/Glow`; `WindMasterScript.cs`) — a wind enemy/miniboss. **P2**.
- **Columns** (`Column`, `ColumnTop/Bottom(+Damaged/Destroyed)`; `ColumnPart.cs`) — multi-part vertical pillars (real silhouette variety) (= A4). **P2**.
- **Altar / Vase / Graal** (`HeavenAltarV2(+Active)`, `HeavenVaza(+DeathAnimation)`, `GraalHaven`, `HolyBall`, `Shield`, `Missile`) — the ally/level-up interaction objects. **P2**.
- **Heaven boss** (`HeavenBoss`, `HeavenBossGlobe`) — the campaign currently **just ends at `heaven-2`** with no 4th boss (= A7). **P2**.

> Net: the original had ~12 distinct enemy/hazard types across the biomes (beholders, bats,
> necromant, hell-ball-spawner, lava, stalactites, bombs, melee/shield statues, windmaster, …).
> The current build shipped **zero** of them. This is why locations feel like recoloured walls.

## F. No mirrored / oriented block variants ("corner block with no sense")

The renderer draws every cell as one upright sprite — there is **no flipX/flipY or
corner/edge tile system**. Consequences:
- **Asymmetric block art breaks when repeated.** `HellInvulnerable` is a left/right-asymmetric demon face; using it as a vertical "channel" puts the same face on both walls, so one side faces the wrong way (visible in `move5-hell-2`). It reads as random faces, not a wall.
- **Multi-part structures are impossible.** `ColumnTop` (an ornate capital) needs a matching `ColumnBottom` and orientation; without flip/rotate there's no way to build a proper framed column or a 4-corner border.
- **Fix (user's suggestion):** add mirrored block-ids (e.g. `_l`/`_r`, `_tl/_tr/_bl/_br`) **or** a per-cell `flipX`/`flipY` flag in the level format + renderer. Cheapest: a `flip` field on the legend mapping; or duplicate block defs that point at the same sprite with a mirror flag. This unlocks corners, channels, and symmetric structures that actually look intentional. **P2**.

## G. Emojis used as UI icons (replace with real art)

Emoji glyphs are shipped in place of sprites — looks cheap/inconsistent on a pixel-art game:
- `CampaignScene.ts:379` — **⚡** in the rift banner (use a real rift/portal sprite).
- `InventoryScene.ts` — **💎** as the crystals icon (×3) and **🔒** as the locked-item icon. Real `Gem.png` and a lock sprite exist.
- `Hud.ts` — **✨ 🔥 🛡 ⚡ 💀** as spell/effect fallback icons (lines ~390, 755–769).
**Fix:** swap every emoji for the corresponding sprite (gem, lock, spell icons, rift). **P2** (D-class "AI-kit" tell the user explicitly hates).

## H. Stretched art instead of 9-slice (the "weirdly stretched" art)

~31 single-sprite backgrounds are stretched with `background-size: 100% 100%` / `cover` across
12 files (buttons `InterfaceButton`/`Button1`, panels `LvlUpInterfacePanel`, node art, hero
banners, etc.). A fixed-aspect sprite stretched to an arbitrary box distorts — exactly what the
original avoided with **9-slice** (which we now use for the HUD bars). **Fix:** convert framed
buttons/panels to CSS `border-image` 9-slice (same technique as `Hud.buildBar`). Files: `MenuScene`,
`CampaignScene`, `CharacterScene`, `DungeonsScene`, `DungeonScene`, `SkillsScene`, `SettingsScene`,
`AchievementsScene`, `TutorialOverlay`, `battle/overlays`, `Hud`. **P2**.

## I. More UI flaws (from the new gallery shots)

| # | Screen | Flaw | Sev |
|---|--------|------|-----|
| I1 | **Inventory** (`ui-inventory`) | **Every item icon is an identical grey padlock** — the real item art (`/items/…`) is hidden behind the "unowned" lock, so the whole shop reads as broken placeholders. Show the (greyed) item art + a small lock badge instead. Also uses 💎 emoji. | **P1** |
| I2 | **Battle hotbar** (`spell-fireball/turret/ignite`) | **Fire Mage spell slots render as blank white boxes** (no icon) while Engineer/Paladin/Necromancer slots show icons — the default class's hotbar looks empty/broken. | **P1** |
| I3 | **Dungeon run** (`ui-dungeon-run`) | ~70% empty black void; "Active Run / Floor 1/3 / None yet / Enter Floor" floats in a vacuum. Looks unfinished. | P2 |
| I4 | **Campaign map** (`ui-campaign`) | Every node is the **same glassy orb with the same generic picture icon**, only tinted by tier; nodes don't show biome/level identity. Connectors are thin; labels tiny. | P2 |
| I5 | **Character select** (`ui-characters`) | Each row has a **decorative gold arrow** pointing right that does nothing (implies "next/forward" but is inert). | P3 |
| I6 | **Spell VFX are weak** (`spell-fireball/turret`) | Fireball = a tiny orange ring; turret = barely visible. Only Firewall and Necro skeleton read clearly. Effects need more scale/punch (the original had big missile/explosion frames). | P2 |

## Revised top priorities (after round 2)

The "looks like shit / empty" verdict is mostly **E (no enemies)** + the broken-looking
placeholders. Recommended order:

1. **I1 + I2 + D1 + D2 + D3** — fix the broken-looking placeholders (inventory padlocks, blank Fire-Mage hotbar, Skills grey box, Russian badges, dev watermark). Cheap, kills the "unfinished" read.
2. **E (enemies)** — bring back at least one signature enemy per biome: Hell-Ball-Spawner, Stalactite drops (Caverns), Beholder (Witchland), Melee Statue (Heaven). This is the real gameplay gap.
3. **A1/A2/A3** — bombs, cauldrons, and real block damage states.
4. **F** — mirrored/oriented block variants (unblocks good-looking structures).
5. **G + H** — replace emojis with sprites; convert stretched buttons/panels to 9-slice.
6. **B/C/D residue** — balls-bar contrast, denser levels, Heaven boss, generic-node polish.
