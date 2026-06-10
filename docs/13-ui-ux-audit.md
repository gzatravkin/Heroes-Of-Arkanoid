# 13 — UI/UX Visual Audit (2026-06-10)

> Audited live at the design target **390×844 portrait** (per docs/06) plus desktop widths.
> Verdict: the game is functionally complete but visually unshippable. The root problem is
> systemic — there is **no shared design system**, so every screen invented its own palette,
> button style, and typography, and several screens ship placeholder or broken assets.

## Systemic defects (cause most of the screen-level ones)

| # | Defect | Evidence |
|---|--------|----------|
| S1 | **No design system.** Menu = warm brown + ornate gold/blue pills. Heroes/Skills = purple. Settings = navy + iOS-style purple toggles + teal button. Items = flat dark HTML cards. Four palettes, three button languages, plain `← Menu` text links next to ornate buttons. | every screen |
| S2 | **Placeholder/broken assets in shipping UI.** Menu nav: "Heroes" icon is literally `...`, "Settings" is a dash. Skills screen: corrupted grey image in header, Phoenix icon is a broken-image square. Campaign top bar EXP is grey placeholder squares. Achievements title has a stray grey rectangle behind it. Menu bottom-right: a credit badge renders clipped behind the nav. | menu, skills, campaign, achievements |
| S3 | **Text layout broken.** Campaign node labels wrap mid-phrase up to 4 lines ("Caverns — Collapse Run") and overflow/clip their banner plaques; label text is low-contrast olive-on-brown; battle skill-bar labels are ~8px and clip into the icons. | campaign, battle |
| S4 | **Dead space.** Menu and Heroes leave 50–60% of the screen empty flat gradient. No background art, no composition. | menu, heroes |
| S5 | **No desktop strategy.** Mobile-first 390×844 design, but on desktop the playfield stretches the full window width (bricks tiny, huge empty margins) and the whole canvas re-lays-out live when the window resizes. Needs a letterboxed fixed design space (centered, max-width, decorated gutters) and DPR-aware rendering (Windows 125–150% scaling ⇒ dpr 1.25–1.5). | battle, all scenes |

## Screen-by-screen

### Menu
- 60% of the screen is empty brown gradient between the two CTAs and the bottom nav.
- 2 of 5 nav icons are placeholders (`...`, dash).
- Clipped watermark/credit badge behind the bottom nav, reads as a rendering bug.

### Campaign map
- Labels overflow the banner plaques, wrap up to 4 lines, get clipped at the bottom.
- Banner sprite is stretched; the column connector line runs through the plaques.
- Lock glyphs on locked nodes are blurry/low-res.
- Top bar cramped; EXP bar is placeholder squares; `← Menu` plain link beside the ornate Upgrades button.
- Reads as a rigid 3-column grid, not a map.

### Items (worst offender — "AI-generated page" look)
- Flat HTML cards with thin borders + generic sans type: zero relation to the ornate menu style.
- Item icons are dark silhouettes on dark cards — nearly invisible.
- Equipped slots are empty boxes labeled 1/2/3.
- Buy buttons are muddy brown rectangles that look permanently disabled.

### Heroes
- Purple background (palette mismatch with menu).
- Banner sprite hard-clipped at the right card edge on Fire Mage.
- Bottom half of the screen empty; plain underlined back link.

### Skills
- Corrupted grey image overlapping the header; Phoenix icon broken (white square).
- Skill icons are raw rectangular art crops with hard edges; Ignite and Fireball are near-identical.
- Unidentifiable bracket glyph next to the level number; "+" buttons are grey blobs.

### Awards (Achievements)
- Stray grey rectangle behind the title.
- Medal art reads as gravestones; engraved tier text (Novice/Initiate/Oru) illegible at size.
- Monotone dark cards, no locked/unlocked visual rhythm beyond `???`.

### Settings
- Structurally fine, but three palettes in one screen: navy background, purple iOS toggles, teal Reset, gold Replay.

### Battle (in-game)
- **Brick field renders underneath the top-left HP/lives HUD** — no top playfield inset.
- **Ball is nearly as wide as a brick** (~45px vs ~40px brick at 390w). Classic ratio is ~⅓ brick width. It's also a flat beige orb that ignores the fire theme.
- **Paddle/bar cluster visibly changes scale between frames** with no input (captured in successive screenshots). Suspect a pulse/respawn scale animation leaking into layout, or bar width tied to animated paddle width. User reports it as "constantly changing size".
- Skill icons muddy/dark and illegible at 36px; labels clip into the icons; mana bar is a plain flat rectangle next to ornate gold frames.
- Desktop: playfield stretches the full window width; live reflow on window resize.
- Console: deprecated color setter warning from @pixi/filter-glow at Renderer.ts:120; favicon 404; manifest icon size mismatch.

## Audio

- **Background "music" disabled 2026-06-10** (user: "terrible noise that makes my head hurt").
  The procedural per-biome ambience in `frontend/src/audio/Music.ts` — most notably Hell's
  two detuned sawtooth oscillators at 55/55.6 Hz (a deliberate beat-frequency drone) — reads
  as grating noise, not music. `setMusicBiome()` is now a no-op behind `MUSIC_DISABLED`.
  **Needs improvement:** replace with real composition (authored loops or a far more musical
  generative approach: tonal harmony, melody, tempo, mixing/EQ), per-biome themes, and a
  separate Music toggle in Settings. SFX (`Sfx.ts`) remain enabled and were not flagged.

## Why this happened (process gaps)

1. **No UI spec / style guide** — docs cover game design and milestones, not visual language. Every scene was built ad hoc.
2. **No fixed design-viewport contract** — scenes were verified at whatever window size was open.
3. **No asset QA gate** — uncropped art, broken references, and placeholders shipped silently.
4. **Verification culture checked "does it run", not "does it look designed"** — screenshots were treated as launch evidence, not design review artifacts.

## Fix plan (passes)

- **P0 — Foundation.** Single design-token sheet (palette anchored to the menu's warm-brown + gold/blue, type scale, spacing) and shared UI components (panel, button, icon frame, bar). Fixed 390×844 design space, letterboxed/centered on desktop with decorated gutters. DPR-correct canvas. Kill the resize reflow.
- **P1 — Shell screens.** Rebuild Items, Skills, Heroes, Awards, Settings on the shared components. Campaign: plaques sized to text with no mid-phrase wrap, contrast fix, real EXP bar, replace lock glyphs, route connector around labels. Replace all placeholder icons; remove the clipped badge; fix favicon/manifest.
- **P2 — Battle.** Playfield top inset below HUD; ball scaled to ~⅓ brick and themed; stable bar/paddle sizing (find and fix the scale animation leak); redrawn skill iconography in consistent frames; styled mana bar.
- **P3 — Polish.** Pressed/hover/disabled states, screen transitions, locked/unlocked rhythm on campaign + awards, empty-space art passes on menu/heroes.

Every fix must be verified by re-screenshot at 390×844 **and** a desktop width, compared against this doc.

---

## Post-overhaul status (2026-06-10)

| Scene | §1 Palette | §2 Hierarchy | §3 Text | §4 Art | §5 Feedback | §6 Space | §7 Touch | §8 HUD |
|-------|-----------|-------------|---------|--------|------------|---------|---------|--------|
| Menu | PASS | PASS | PASS | PASS | PASS | PASS | PASS | n/a |
| Campaign | PASS | PASS | MINOR — node labels small (~12px) | PASS | PASS | PASS | PASS | n/a |
| Battle | PASS | PASS | PASS | PASS (mobile) / WARN (desktop — bricks not visible at 1280×800) | n/a | n/a | PASS | PASS — bars top-left, no brick overlap |
| Inventory | PASS | PASS | PASS | PASS | PASS | PASS | PASS | n/a |
| Skills | PASS | PASS | PASS | PASS | PASS | PASS | PASS | n/a |
| Characters | PASS | PASS | PASS | PASS | PASS | PASS | PASS | n/a |
| Achievements | PASS | n/a | PASS | PASS | PASS | PASS | PASS | n/a |
| Settings | PASS | PASS | PASS | PASS | PASS | WARN — large empty bottom half below 4 rows | PASS | n/a |
| Dungeons | PASS | PASS | PASS | PASS | PASS | WARN — ~50% dead space below 2 dungeon entries | PASS | n/a |

### Issues found

1. **Battle desktop §4/§6 (1280×800)** — At desktop viewport, the portrait game canvas is letterboxed into a narrow center column (~390px wide). The 4 s screenshot shows the HUD bars and spell hotbar but no visible brick field. Either the bricks are above the clipped portion of the column, or the portrait canvas does not fit within 800px height when letterboxed, pushing bricks off-screen. The dark side gutters consume ~40% of total width with no decorative treatment (§6 dead space on all desktop scenes).

2. **Settings §6** — Only 4 settings rows are present; the lower half of the 844px mobile canvas is empty dark space. Low priority, could be addressed with richer empty-state art or a footer.

3. **Dungeons §6** — Two dungeon entries with generous padding leave the bottom ~50% of the screen blank. Intentional for future content but reads as unfinished at current content count.

4. **Campaign §3** — Node label text under circular buttons reads at approximately 12px. Passes legibility at normal contrast but is near the §3 minimum threshold; warrants monitoring when label strings lengthen.

5. **Test regression fixed: `p7b-new-screens.spec.ts`** — Selector `.sk-lvl-badge` was stale after the Wave B1 style overhaul renamed the level badge wrapper to `.sk-lvl-wrap`. Updated in `tests/p7b-new-screens.spec.ts:131`.

### Test suite results

| Spec | Result |
|------|--------|
| menu.spec.ts | 6/6 PASS |
| inventory.spec.ts | 8/8 PASS |
| campaign.spec.ts | 2/2 PASS |
| characters.spec.ts | 1/1 PASS |
| p7b-new-screens.spec.ts | 13/13 PASS (after selector fix) |
| upgrade.spec.ts | 1/1 PASS |
| hud-bars.spec.ts | 4/4 PASS |
| battle-start.spec.ts | **0/1 FAIL** — `waitForPhase("Playing")` times out; `conn.serve()` auto-serve (triggered by `navigator.webdriver`) does not appear to reach the backend within the 10 s window. No `[cmd] Serve` entry visible in console. Pre-existing intermittent failure (see `faeda8c` de-flake commit); worsened or consistent post-overhaul. Not related to Wave A/B UI changes (BattleScene.ts untouched since p7c). |

**Overall:** 35 / 36 pass. One persistent failure in `battle-start.spec.ts` requiring dedicated investigation of the auto-serve path.
