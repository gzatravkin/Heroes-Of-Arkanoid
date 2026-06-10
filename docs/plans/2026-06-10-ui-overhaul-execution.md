# UI/UX Overhaul Execution Plan (P1b + P3 + Audio)

> **For agentic workers:** This plan is executed by a **Sonnet orchestrator** dispatching
> one subagent per task. Each task header declares `Model:` (haiku | sonnet) and
> `Parallel group:`. Tasks in the same wave with different file sets may run in parallel.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring every remaining screen of Heroes of Arkanoid II up to the design system
established in P0–P2 (docs/13-ui-ux-audit.md), so the whole game reads as one
professionally designed product.

**Architecture:** All shell screens are DOM scenes mounted into a letterboxed portrait
`#app` container (CSS size container; use `cqw`/`cqh`, never `vw`/`vh`). The design
system lives in `frontend/src/ui/theme.ts` (CSS custom properties + `.ui-*` component
classes) and `frontend/src/ui/nineSlice.ts` (9-slice helper for the painted art).
The battle scene is PixiJS (`frontend/src/render/*`) with a DOM HUD (`frontend/src/ui/Hud.ts`,
`frontend/src/ui/hud/*`).

**Tech Stack:** TypeScript + Vite (frontend), PixiJS 7, ASP.NET Core backend (don't touch),
Playwright test suite in `tests/`.

---

## 0. The Design Rulebook (verify EVERY task against this)

Distilled from game-UI research (sources at bottom) + this project's audit (docs/13):

1. **One visual language.** Palette anchored to warm-brown + gold + deep navy
   (`--gold #d8a84e`, `--gold-bright #ffe9b0`, `--text #f0e0b8`, `--text-dim #c9b182`,
   `--navy #16243a`, bg gradient `--bg-0/1/2`). Display font `var(--font-display)`
   (Palatino stack) for titles/plaques; `var(--font-body)` for copy. NO purple, NO navy-only
   screens, NO iOS-style widgets, NO plain HTML-looking buttons or underlined links.
   Every surface uses the game's painted art via 9-slice (`NameBlock`, `BarGoods`,
   `Kvadrat`, `Button1`, `InterfaceButton`, `MissionName`) or the `.ui-*` classes.
2. **Visual hierarchy.** One primary action per screen, visually dominant (size +
   contrast + gold). Secondary info smaller/dimmer. The eye must land on the most
   important element first.
3. **Text never breaks.** No mid-phrase wrapping, no clipped labels, no text smaller
   than 10px, contrast ≥ readable on first glance (gold-bright on dark, never
   dark-olive-on-brown).
4. **Correct art rendering.** Painted art is NEVER `image-rendering: pixelated`.
   Frames keep their aspect (9-slice, not `background-size: 100% 100%` stretch).
   No placeholder/broken/corrupt images may render — if an asset is bad, remove it
   and leave a code comment.
5. **Feedback on everything ("juice").** Every interactive element has hover
   (`brightness(1.15)`), active (`scale(0.96)`), and disabled
   (`saturate(.25) brightness(.65)`) states. State changes animate (~150ms), never snap.
6. **No dead space without intent.** Empty regions need composition (background art,
   vignette, centered content) — not flat gradient voids.
7. **Touch-first.** Interactive targets ≥ 44px. The design viewport is 390×844 portrait;
   desktop gets the same column letterboxed.
8. **UI is invisible when playing.** In battle, HUD never overlaps the playfield's
   bricks/ball; icons read at a glance.

## 0.1 Ground rules for every worker (read before starting)

- **Dev servers are usually already running** (backend `:5080`, vite `:5175`). Check with
  `netstat -ano | grep -E ":5080|:5175" | grep LISTENING` before starting any. If missing:
  backend `cd backend/Arkanoid.Server && dotnet run` (background), frontend
  `cd frontend && npm run dev` (background).
- **NEVER `git stash` / `git checkout` / `git reset` the working tree.** Vite HMR ships
  any working-tree change straight into the user's open browser. (This burned us once.)
- **Commit after each task** with a conventional message; never `--no-verify`.
- **Selector freeze.** Playwright tests depend on these — keep ids/classes and data
  attributes working: `#menu`, `#btn-continue`, `#btn-campaign`, `#btn-characters`,
  `#btn-inventory`, `#btn-skills`, `#btn-achievements`, `#btn-settings`,
  `#inventory-root`, `#inv-grid`, `.inv-card`, `.inv-buy-btn`, `.inv-equip-btn`,
  `.inv-tier-badge`, `#inv-crystals`, `#inv-crystal-count`, `#inv-equipped-row`,
  `.inv-equip-slot-filled`, `#skills-scene`, `#sk-tabs`, `#sk-spell-grid`, `#sk-points`,
  `.camp-node`, `[data-level]`, `[data-state]`, `#hud-mana`, `#hud-mana-fill`,
  `#hud-boss-hp`, `.set-toggle`, `#set-toggle-audio`, `#set-toggle-fx`,
  `.ach-card`, `.char-card` (and anything else a failing test points at — check the
  spec before renaming anything).
- **Do not edit `frontend/src/ui/theme.ts` in parallel tasks.** If your screen needs a
  new shared-looking style, define it in your scene's own stylesheet. Theme changes
  go through the orchestrator as a dedicated sequential task.
- **Units:** `cqw`/`cqh` inside scenes, never `vw`/`vh`. Scene-internal overlays are
  `position: absolute` (relative to `#app`), not `fixed`.

### Verification recipe (mandatory, every UI task)

```bash
# 1. Typecheck
cd frontend && npx tsc --noEmit          # expected: no output

# 2. Screenshot at BOTH viewports (CLI waits 4s for atlas + fade-in)
cd ../tests
npx playwright screenshot --viewport-size=390,844  --wait-for-timeout=4000 "http://localhost:5175/?scene=<SCENE>" shot-mobile.png
npx playwright screenshot --viewport-size=1280,800 --wait-for-timeout=4000 "http://localhost:5175/?scene=<SCENE>" shot-desktop.png
```

Then **Read both PNGs and judge them against the Rulebook §0** — text fits, palette
matches, art unbroken, hierarchy obvious, letterbox frame intact on desktop. A
screenshot that "renders" but violates a rule = task NOT done. Fix and re-shoot.

```bash
# 3. Run the task's mapped spec (listed per task)
cd tests && npx playwright test <spec>.spec.ts --reporter=line   # expected: all pass
```

Known pre-existing failure (NOT yours to fix unless the task says so):
`touch-controls.spec.ts` "tapping spell slot casts spell and consumes mana".
Suites are flaky under parallel load — run specs for your own task only, sequentially.

### Canon reference

The **Items screen** (`frontend/src/scenes/inventory/inventoryStyles.ts`) is the approved
reference implementation: warm bg gradient, `NameBlock` section plaques, `BarGoods`
card panels, `Kvadrat` slots, `Button1` action pills, `.ui-back` chip + `.ui-title`
header. When in doubt, open it and copy its patterns. The shared classes available
from `theme.ts`: `.ui-screen`, `.ui-screen-bg`, `.ui-content`, `.ui-topbar`,
`.ui-topbar-spacer`, `.ui-title`, `.ui-back`, `.ui-plaque`, `.ui-panel`, `.ui-slot`,
`.ui-btn`, `.ui-btn--primary`, `.ui-btn--small`, `.ui-gem`, `.ui-link`.

---

## WAVE A — independent screen tasks (all parallelizable with each other)

### Task A1: Settings screen restyle — **Model: haiku** — Parallel group: A

**Files:** Modify `frontend/src/scenes/SettingsScene.ts` (styles are injected at the
bottom of the file; DOM built in `mountSettings` / `buildToggle`).

Problems (docs/13): navy background, purple iOS toggles, teal Reset button — three
palettes on one screen, none of them the game's.

- [ ] Replace the `.set-bg` gradient with the standard warm one (copy from
      `inventoryStyles.ts` `.inv-root` background).
- [ ] Title: `font-family: var(--font-display); color: var(--gold-bright);
      font-size: 26px; text-shadow: 0 2px 4px rgba(0,0,0,.9), 0 0 18px rgba(255,180,60,.25);`
- [ ] `← Menu` link → keep element/behavior, restyle with class `ui-back` (icon chip)
      OR `.ui-link` styling if it's an `<a>`; ensure ≥44px hit area.
- [ ] Each settings row: wrap-style as a `BarGoods` panel —
      `${nineSlice("/ui/BarGoods.png", "26 30 26 30", "12px 14px")}` (import
      `nineSlice` from `../ui/nineSlice`), row label `color: var(--gold-bright)`,
      description `color: var(--text-dim); font-size: 12px`.
- [ ] Toggle restyle (keep `.set-toggle` class + behavior): track
      `background: #241a0d; border: 1px solid var(--gold-dim); border-radius: 999px;`
      knob (`.set-toggle-slider`): `background: radial-gradient(circle at 38% 32%, #ffe9b0, #d8a84e 70%);`
      checked track: `background: #3a2a10; box-shadow: inset 0 0 8px rgba(255,190,80,.5);`
      150ms transitions.
- [ ] Buttons: Replay + Reset Progress → `${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 18px")}`,
      text gold; Reset text color `#f3b8a8` (danger accent on the same gold pill — no teal).
- [ ] Footer "Heroes of Arkanoid II" → `color: var(--text-faint); font-family: var(--font-display);`
- [ ] Verify per §0.1 recipe with `?scene=settings`; spec: `p7b-new-screens.spec.ts`.
- [ ] Commit: `style(settings): conform to design system — warm palette, gold toggles, Button1 pills`

### Task A2: Heroes (character select) restyle — **Model: sonnet** — Parallel group: A

**Files:** Modify `frontend/src/scenes/CharacterScene.ts`.

Problems: purple palette; banner art (`InterfaceKnightGlory`-style strips) hard-clipped
at card right edge; plain underlined back link; bottom half of screen empty.

- [ ] Background → standard warm gradient (`.char-bg`); body font/colors → tokens.
- [ ] Header → `.ui-topbar` pattern: `.ui-back` chip (keep the back element's id/handler),
      centered `.ui-title` "Choose Character", `.ui-topbar-spacer`.
- [ ] Cards → `BarGoods` 9-slice panels. The class banner sprite must not clip: give the
      banner img `width: 100%; object-fit: contain` inside the card instead of a hard
      `overflow: hidden` crop, or mask its right edge with a gradient fade
      (`-webkit-mask-image: linear-gradient(90deg, #000 85%, transparent)`). Judge from
      the screenshot which reads cleaner.
- [ ] Selected state: gold glow `filter: drop-shadow(0 0 8px rgba(255,190,80,.55))` +
      a small "SELECTED" `.ui-plaque`-style chip; non-selected slightly dimmed (`brightness(.92)`).
- [ ] Locked classes (if any render): desaturate ~50%, never black-out; lock badge bottom-right.
- [ ] Fill the dead bottom: center the card stack vertically
      (`justify-content: center; min-height: 100cqh`) and enlarge cards/portraits so the
      4 cards + header compose the full screen. Portraits ≥ 64px.
- [ ] Passive description: `var(--text-dim)`, 12–13px, one line if possible.
- [ ] Verify with `?scene=characters` (§0.1); spec: `characters.spec.ts`.
- [ ] Commit: `style(heroes): conform to design system — warm palette, BarGoods cards, full-height composition`

### Task A3: Awards (achievements) restyle — **Model: sonnet** — Parallel group: A

**Files:** Modify `frontend/src/scenes/AchievementsScene.ts`.

Problems: stray grey rectangle behind the title (the `.ach-title` area uses
`/achievements/AchievmentPanel.png` at line ~221 — **inspect that PNG first**; if it's
another grey placeholder like `InterfaceMainPalet.png` was, remove it); medals read as
gravestones with illegible engraved tier text; monotone dark cards.

- [ ] Read `/achievements/AchievmentPanel.png` (the Read tool renders images). If it's a
      flat/placeholder image, delete that background rule and use a `.ui-plaque`-style
      `NameBlock` bar behind the title instead.
- [ ] Background → standard warm gradient; header → back chip + `.ui-title` "Achievements"
      + counter line `0 / 13 unlocked` in `var(--text-dim)`.
- [ ] Cards → `BarGoods` panels (2-col grid stays). Unlocked: full-color medal +
      gold name + description; locked: medal at `saturate(.45) brightness(.8)`,
      name `var(--text-dim)`, `???` description — there must be an obvious
      locked↔unlocked visual rhythm (Rulebook §5/§6).
- [ ] Medal legibility: render the medal img at ≥ 56px. The engraved tier word on the art
      is unreadable at that size — add a small text chip under the medal
      (`Novice/Initiate/Oru/Expert` from existing data if available in the scene's model;
      if the tier string isn't in the data, skip the chip — do NOT parse it from filenames).
- [ ] Toast (`.ach-toast`): restyle to `BarGoods` panel + gold text (it's body-appended
      and `position: fixed` — that's intentional, leave positioning).
- [ ] Verify with `?scene=achievements` (§0.1); spec: `p7b-new-screens.spec.ts`.
- [ ] Commit: `style(awards): conform to design system — plaque title, BarGoods cards, locked/unlocked rhythm`

### Task A4: Skills screen restyle — **Model: sonnet** — Parallel group: A

**Files:** Modify `frontend/src/scenes/SkillsScene.ts` (styles injected at bottom).

Problems: purple background; skill icons are raw rects with hard edges; the level
indicator renders `/levelskill/Lvl{n}Skill.png` (a 183×188 ornate square FRAME with
transparent center) as a tiny image NEXT to the number — it must frame the number;
"+" buttons are grey blobs.

- [ ] Background → standard warm gradient; fonts/colors → tokens; back link → `.ui-back` chip
      (keep href/behavior); title `.ui-title`; "Skill Points: N" → gold chip under title.
- [ ] Class tabs: keep structure; active tab = current gold/blue pill at full brightness,
      inactive tabs `filter: saturate(.4) brightness(.75)`.
- [ ] Spell cards → `BarGoods` panels. Spell icon inside a `Kvadrat` slot
      (`${nineSlice("/ui/Kvadrat.png", "14 14 14 14", "7px 7px")}`), icon
      `width: 56px; height: 56px; object-fit: contain; border-radius: 6px;`.
- [ ] **Level badge fix:** make a 34×34 wrapper with the `Lvl{n}Skill.png` frame as
      `background: url(...) no-repeat center / contain` and the level number centered
      INSIDE it (`display:flex; align-items:center; justify-content:center;
      font-weight:700; color: var(--gold-bright); font-size: 13px;`). Remove the
      side-by-side img+number layout.
- [ ] "+" upgrade buttons → `Button1` 9-slice, label `+ Upgrade`, gold text; disabled
      state per Rulebook §5. Keep all click handlers and any `data-*` attributes.
- [ ] Verify with `?scene=skills` (§0.1) — check ALL FOUR class tabs by clicking each
      (use `npx playwright screenshot` per tab is not possible — instead run one
      `playwright test` snippet or screenshot the default tab and visually inspect the
      icon map covers paladin/engineer/necromancer paths in `SPELL_ICON_MAP`); spec:
      `upgrade.spec.ts` + `p7b-new-screens.spec.ts`.
- [ ] Commit: `style(skills): conform to design system — warm palette, framed icons, level badge inside its frame`

### Task A5: Campaign top bar (profile strip) — **Model: haiku** — Parallel group: A

**Files:** Modify `frontend/src/scenes/campaign/campaignStyles.ts` (`.camp-profile-*`
rules) and `frontend/src/scenes/CampaignScene.ts` (where `.camp-exp-outer/.camp-exp-fill`
backgrounds get set — search for `camp-exp`).

Problems: EXP bar renders as placeholder grey squares; cramped layout; blue/cyan
accent colors off-palette.

- [ ] EXP bar: use the dedicated menu bar art. Outer:
      `background: url('/ui/ExpBarEmptyMainMenu.png') no-repeat center / 100% 100%;`
      → replace with proper 3-slice to avoid cap distortion:
      `border-style: solid; border-width: 7px 18px; border-image: url('/ui/ExpBarEmptyMainMenu.png') 26 70 26 70 fill stretch;`
      width `110px`, height `16px`. Inner fill div: keep width-% behavior, style
      `background: linear-gradient(180deg, #ffe06a, #d89a2e); border-radius: 2px;`
      (If `ExpBarEmptyMainMenu.png` renders badly in the screenshot — it may be another
      placeholder — Read the PNG first; fall back to `/ui/BattleMPEmpty.png` art with the
      same 3-slice numbers used in `frontend/src/ui/hud/bars.ts` `buildBar()`.)
- [ ] Colors: `Lv N` stays gold; `EXP` label and `Pts:` → `var(--text-dim)`; crystals
      counter → gold-bright with the existing Gem img.
- [ ] `← Menu` link → `.ui-link` style (gold on hover), vertically centered, ≥44px hit area.
- [ ] Spacing: `gap: 12px;` on `.camp-profile-bar`, single row at 390px without wrapping
      (test at the mobile screenshot — if it wraps, reduce EXP bar to 90px).
- [ ] Verify with `?scene=campaign` (§0.1); spec: `campaign.spec.ts` (it's slow; one run).
- [ ] Commit: `style(campaign): real EXP bar art + palette-conformant profile strip`

### Task A6: Dungeons screens conformance — **Model: haiku** — Parallel group: A

**Files:** Modify `frontend/src/scenes/DungeonsScene.ts` and `frontend/src/scenes/DungeonScene.ts`.

These are secondary screens (`?scene=dungeons`, `?scene=dungeon`) that still carry ad-hoc
styling.

- [ ] Apply the standard warm background gradient to each scene root (copy the
      `.inv-root` background block).
- [ ] Headers → `.ui-title` typography; back links → `.ui-link` (keep hrefs/ids).
- [ ] Any cards/buttons → `BarGoods` panel / `Button1` pill via `nineSlice` import
      (same numbers as canon: panels `"26 30 26 30", "12px 14px"`, buttons
      `"24 60 24 60", "8px 18px"`).
- [ ] Do NOT redesign layout — palette/typography/components conformance only.
- [ ] Verify with `?scene=dungeons` and `?scene=dungeon` (§0.1); spec: `dungeon.spec.ts`.
- [ ] Commit: `style(dungeons): palette + component conformance`

### Task A7: PWA icons + naming — **Model: haiku** — Parallel group: A

**Files:** Create `frontend/public/icons/icon-192.png`, `frontend/public/icons/icon-512.png`
(overwrite — the current "192" is actually 38×37). Modify `frontend/index.html` (title).

- [ ] Generate proper square icons from the round hero emblem (PowerShell):

```powershell
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Image]::FromFile((Resolve-Path "frontend/public/ui/FireHeroIco.png"))
foreach ($size in 192, 512) {
  $bmp = New-Object System.Drawing.Bitmap($size, $size)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear([System.Drawing.Color]::FromArgb(255, 13, 10, 8))   # --ink backdrop
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $pad = [int]($size * 0.08)
  $g.DrawImage($src, $pad, $pad, $size - 2*$pad, $size - 2*$pad)
  $g.Dispose()
  $bmp.Save((Join-Path (Resolve-Path "frontend/public/icons") "icon-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}
$src.Dispose()
```

- [ ] Verify each file's pixel size by reading the PNG header:
      `node -e "const fs=require('fs');for(const s of[192,512]){const b=fs.readFileSync('frontend/public/icons/icon-'+s+'.png');console.log(s, b.readUInt32BE(16)+'x'+b.readUInt32BE(20))}"`
      Expected: `192 192x192`, `512 512x512`.
- [ ] `frontend/index.html`: `<title>Arkanoid RPG</title>` → `<title>Heroes of Arkanoid II</title>`.
      Also update `name`/`short_name` in `frontend/public/manifest.webmanifest` to
      `"Heroes of Arkanoid II"` / `"Arkanoid II"`.
      **Check first:** `grep -rn "Arkanoid RPG" tests/` — if any spec asserts the page
      title, update that assertion in the same commit.
- [ ] Reload `http://localhost:5175/` and confirm the console manifest warning is gone.
- [ ] Commit: `chore(pwa): real 192/512 icons, unify product name`

### Task A8: Battle DOM HUD polish — **Model: sonnet** — Parallel group: A

**Files:** Modify `frontend/src/ui/Hud.ts` and `frontend/src/ui/hud/*` (bars.ts already
does proper 3-slice value bars — extend that pattern, don't replace it).

Problems: bottom mana bar is a plain flat rectangle next to ornate gold frames; hotbar
slot labels ~8px and clip into icons; active/affordable spell state hard to read.

- [ ] Hotbar slots: frame each slot with `Kvadrat` 9-slice; spell name label ≥ 10px,
      positioned BELOW the slot (no overlap), `var(--text-dim)`; keybind letter chip
      top-left in gold. Castable (enough mana): full brightness + subtle gold glow
      `drop-shadow(0 0 6px rgba(255,190,80,.45))`; not castable: `saturate(.3) brightness(.6)`.
      Active/selected: stronger glow + 1.06 scale (150ms).
- [ ] Mana bar: it already uses `/ui/BattleMPEmpty.png` via `buildBar` — verify in the
      screenshot that caps render symmetric; raise bar height to 22px if the frame looks
      crushed; the `NN / 100` label ≥ 10px with `text-shadow: 0 1px 2px #000`.
- [ ] Top-left HP/lives bars: confirm they sit on a translucent backing strip
      (`background: rgba(10,7,5,.55); border-radius: 8px; padding: 6px 8px;`) so they
      read over bright biomes (Rulebook §8).
- [ ] **Do not** rename `#hud-*` ids (tests). Do not touch the Pixi renderer.
- [ ] Verify in a real battle: navigate
      `http://localhost:5175/?scene=battle&level=hell-1&seed=1&run=hud-check`
      (auto-serves under webdriver), screenshot both viewports per §0.1.
      Specs: `hud-bars.spec.ts`, `hud-live.spec.ts` (run sequentially, NOT in parallel
      with other suites).
- [ ] Commit: `style(hud): framed hotbar slots, legible labels, castable-state affordances`

---

## WAVE B — after Wave A merges (sequential unless noted)

### Task B1: Main menu composition — **Model: sonnet** — Parallel group: B (after A)

**Files:** Modify `frontend/src/scenes/MenuScene.ts`.

Problem: ~50% dead gradient between CTAs and the dock (Rulebook §6). No key-art asset
exists yet (user may commission one later — design for its future slot).

- [ ] Composition: logo at ~12% from top; CTA block vertically centered in the
      remaining space above the dock (`margin-top: auto; margin-bottom: auto` on a
      wrapper), so the layout breathes instead of stacking everything at the top.
- [ ] Add atmosphere to the void: a large, very dim radial ember glow behind the CTA
      block plus 8–12 slowly drifting ember particles (CSS-only: absolutely positioned
      3–5px blurred gold dots animating upward over 8–14s, `opacity ≤ .35`,
      `animation-timing-function: linear; animation-iteration-count: infinite`).
      Honor reduced motion: wrap the animation in
      `@media (prefers-reduced-motion: no-preference) { ... }`.
- [ ] Reserve the key-art slot: an (empty for now) `div.menu-keyart` behind the column,
      `position:absolute; inset:0; z-index:1; pointer-events:none;` with a code comment
      `/* future: commissioned hero illustration (docs/13 asset gap #1) */`.
- [ ] Dock: keep as-is (icons fixed in P2); just ensure 12px breathing room above it.
- [ ] Verify with `?scene=menu` at both viewports (§0.1); spec: `menu.spec.ts`.
- [ ] Commit: `style(menu): composition pass — centered CTA block, ember atmosphere, key-art slot`

### Task B2: Global states & consistency sweep — **Model: haiku** — Parallel group: B (after A, parallel with B1 OK — different files)

**Files:** Read-only sweep + small fixes in any `frontend/src/scenes/*.ts` EXCEPT
`MenuScene.ts` (B1 owns it).

- [ ] For every scene (campaign, inventory, skills, characters, achievements, settings,
      dungeons): confirm each interactive element has hover/active/disabled styles
      (Rulebook §5). Where missing, add the standard trio:
      `:hover { filter: brightness(1.15); }`,
      `:active { transform: scale(.96); }`,
      `:disabled { filter: saturate(.25) brightness(.65); cursor: default; }`.
- [ ] Confirm every screen has exactly one back affordance, top-left, ≥44px.
- [ ] Confirm no remaining `vw`/`vh` units or scene-internal `position: fixed`:
      `grep -rnE "[0-9](vw|vh)\b" frontend/src/scenes frontend/src/ui` → expect empty;
      `grep -rn "position: fixed" frontend/src/scenes` → only `.rift-banner`,
      `.camp-upgrade-panel`, `.ach-toast` (intentional) may remain.
- [ ] Verify: `cd frontend && npx tsc --noEmit`; spot-screenshot any scene you changed (§0.1).
- [ ] Commit: `style: interaction-state and unit-consistency sweep`

### Task B3: Final visual re-audit — **Model: sonnet** — Parallel group: B (LAST — after B1/B2)

**Files:** Modify `docs/13-ui-ux-audit.md` (append status section). No code changes
unless a defect is a one-line fix; bigger findings become new tasks for the orchestrator.

- [ ] Screenshot EVERY scene at 390×844 AND 1280×800 (menu, campaign, battle hell-1,
      inventory, skills, characters, achievements, settings, dungeons) per §0.1.
- [ ] Read each screenshot and grade against Rulebook §0, item by item.
- [ ] Append `## Post-overhaul status (date)` to docs/13 with a per-screen verdict table
      (pass / issues found) and file the issues list.
- [ ] Run the broader suite sequentially:
      `cd tests && npx playwright test menu.spec.ts inventory.spec.ts campaign.spec.ts characters.spec.ts p7b-new-screens.spec.ts upgrade.spec.ts hud-bars.spec.ts battle-start.spec.ts --workers=1 --reporter=line`
      Expected: all pass except the known `touch-controls` case (not in this list).
- [ ] Commit: `docs: post-overhaul visual audit status`

---

## WAVE C — independent of A/B (can run anytime, parallel group C)

### Task C1: Musical rework of the background ambience — **Model: sonnet** — Parallel group: C

**Files:** Modify `frontend/src/audio/Music.ts`, `frontend/src/scenes/SettingsScene.ts`
(new toggle row — coordinate with A1: run AFTER A1 merges or rebase on it).

Context: `setMusicBiome()` is currently a no-op behind `MUSIC_DISABLED = true`
(user: "terrible noise that makes my head hurt" — it was raw detuned sawtooth drones).
Replace the synthesis with something actually musical, but **ship it OFF by default**.

Hard requirements (violating any = task failed):
- [ ] New localStorage key `arkanoid_music` (default `"0"` = off). `MUSIC_DISABLED`
      becomes `localStorage.getItem("arkanoid_music") !== "1"` checked at call time.
- [ ] Settings gains a "Music" toggle row (same `buildToggle` pattern as Audio/FX,
      id `set-toggle-music`) wired to that key, with description
      "Per-biome ambient music (experimental)".
- [ ] Synthesis rules (NO raw sawtooth, NO beat-frequency detuning, NO clipping):
      master gain ≤ 0.05 with a `DynamicsCompressorNode`; every voice through a
      lowpass filter ≤ 2kHz; only sine/triangle oscillators; attack ≥ 50ms,
      release ≥ 300ms (no clicks).
      - hell: Am pads (A2–E3–A3 triad notes, one chord change per 8s), sparse low
        timpani-like sine thumps every 4–6s, 50–60 BPM feel.
      - caverns: D minor; long sine pad + echoing pentatonic plucks (triangle,
        feedback delay ~0.4s, ≤ 3 repeats).
      - village: A minor pentatonic chimes (triangle, 2–4s apart, randomized ±3s),
        soft pad underneath.
      - heaven: Fmaj7/Am alternating pads with slow 8s crossfades, occasional bell
        (triangle + short decay) every ~10s.
- [ ] Self-check: render 10 seconds with an `OfflineAudioContext` in a quick node-free
      browser check (evaluate in the page console via a Playwright `page.evaluate`,
      or simpler: assert programmatically that `Math.max(...channelData) < 0.5`).
      Document the measured peak in the commit message.
- [ ] Final listen test belongs to the USER: leave the toggle off, tell the
      orchestrator to ask the user to flip Music on in Settings and judge it.
- [ ] Commit: `feat(audio): musical per-biome ambience behind opt-in Music toggle (default off)`

### Task C2: Fix pre-existing touch-controls spell-cast failure — **Model: sonnet** — Parallel group: C

**Files:** Investigate `tests/touch-controls.spec.ts` ("tapping spell slot casts spell
and consumes mana") — failure predates the UI overhaul (verified by stash-bisect
2026-06-10). Likely candidates: hotbar tap handler vs. `isMobile` synthetic touch
events, or mana race (see commit aa02959 "freezeMana cheat kills the two chronic
mana-race flakes" for prior art on these).

- [ ] Reproduce in isolation: `cd tests && npx playwright test touch-controls.spec.ts --reporter=line`.
- [ ] Use `superpowers:systematic-debugging`: read the spec, trace the tap → `castSlot`
      path through `frontend/src/ui/Hud.ts` (spell slot wiring) and
      `frontend/src/net/Connection.ts`, find the root cause before changing anything.
- [ ] Fix the product code OR (if it's a test race) fix the test using the existing
      `cheat(page, "freezeMana", …)` pattern from `hud-live.spec.ts` / commit aa02959.
- [ ] All of `touch-controls.spec.ts` passes 3 consecutive runs.
- [ ] Commit: `fix(hud|tests): <root cause> — touch spell-cast`

---

## Orchestrator notes

- **Dispatch order:** Wave A all in parallel (8 tasks, zero file overlap — A1 and C1
  both touch `SettingsScene.ts`, so hold C1 until A1 lands). Then B1+B2 in parallel,
  then B3. C2 anytime.
- **Review gate between waves:** before starting Wave B, Read the screenshots each
  Wave-A worker produced and reject any task that violates Rulebook §0 (workers
  self-certify, you verify — the audit taught us self-certification fails).
- **If a worker reports a test failure it didn't cause:** check it against the known
  flake list (§0.1) and pre-existing failures before bouncing the task.
- **The user's quality bar is HIGH** (see docs/13 origin story). "It renders" is not
  done. When a judgment call is ambiguous, match the Items screen.

## Sources (design rulebook research)

- [Justinmind — Game UI: design principles, best practices, and examples](https://www.justinmind.com/ui-design/game)
- [Procreator — 5 Best Practices for Game UI Design](https://procreator.design/blog/best-practices-for-game-ui-design/)
- [Absorb Studios — 10 Essential UI Design Principles for Game Developers](https://www.absorbstudios.org/blog/10-essential-ui-design-principles-for-game-developers)
- [Sunday — 7 Crucial Mobile Game UI/UX Principles](https://sunday.gg/7-crucial-mobile-game-ui-ux-principles-to-follow/)
- [AAA Game Art Studio — Best Mobile Game UI/UX Design](https://aaagameartstudio.com/blog/mobile-games-ui-ux)
- [Wayline — Game UI/UX Design: Best Practices and Examples](https://www.wayline.io/blog/game-ui-ux-design-best-practices-and-examples)
- [Jonasson & Purho — Juice It or Lose It (GDC, via cobble.games summary)](https://www.cobble.games/wise-inspiring-smart/game-design/juice-it-or-lose-it)
- [abagames — Making Games 'Juicy'](https://abagames.github.io/joys-of-small-game-development-en/make_game_juicy.html)
