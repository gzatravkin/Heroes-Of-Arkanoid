# Shell & Flow Overhaul

> Game-feel pass on the *shell* (menu, navigation, HUD bars) and on level-design quality.
> Mobile-first (390×844 portrait), real art only, keep the layered architecture
> (pure Core sim / Meta / Server endpoints / PixiJS scenes), no magic numbers.

---

## Why

Two concrete problems, both visible on a phone:

**The HUD bars render as "half a bar."** They aren't built as bars — they're single
sprites stretched to fit. The boss bar is a flat CSS gradient; the HP/balls pill uses
`BattleHeroBar.png` as `center/contain` (floats at native ratio, ignores box width); mana
uses `BattleMPFull.png` stretched `100% 100%`. None respect that the source art is
**9-slice**: fixed cap/corner pieces that belong *only at the ends*, and an edge/fill
piece that stretches across the *middle*. Caps get scaled into the middle and the fill
only covers half → the lopsided look.

**The menu is a redundant stack of identical pills.** Eight near-identical blue-gold
buttons (`Play`, `Campaign`, `Characters`, `Dungeons`, `Items`, `Level Editor`,
`Achievements`, `Settings`) plus a loose grid of level chips with no sense of progression.
Play and Campaign are the same thing. Editor doesn't belong in a player build. Dungeons
shouldn't be a menu — the locked design said they should appear *during* the campaign as
an opt-in banner.

---

## The five moves

### 1. Collapse the menu into one journey
Delete the stacked button list in `MenuScene.ts` and the loose level-chip grid.

- Primary action = **Continue** → resume the furthest-unlocked campaign node.
- The campaign map *is* the navigation.
- Characters / Items / Skills / Achievements / Settings become small framed icons docked
  on one edge.
- Remove the **Level Editor** from the player-facing menu (reachable only via
  `?scene=editor` or a Settings developer toggle).

### 2. Classic buttons (NOT a playable board)
Keep a clean, **classic button** menu — the live ball+paddle "menu-as-mechanic" idea is
parked as too risky. Polish what's there: a clear visual hierarchy (one big primary
**Continue/Play** CTA, secondary actions as smaller docked icons), consistent framing,
real art, mobile-sized touch targets (≥44px). No live simulation on the home screen.

### 3. Dungeons become Rifts inside the campaign
No Dungeons menu. After clearing a campaign node, a tunable `riftChance` (config) can spawn
a **Rift** node on the path with a slide-in banner:

> ⚡ *A rift opens — descend? 2–5 floors, permadeath, one reward per floor.*

Accept → existing dungeon run; decline/fail → back on the campaign path with campaign
progress untouched. Route the existing dungeon system through this entry point only.

### 4. Fix the HUD bars (symmetrical 9-slice)
The HP, spare-balls, mana, and boss bars must be rebuilt as proper **3-slice**: fixed-size
cap pieces pinned to the ends ONLY, the edge/fill piece stretched/tiled across the middle,
fully symmetrical left↔right. Verify each bar at **0% / 50% / 100%** fill on mobile.

### 5. Levels that mean something
Redesign every shipped level so it reads as a designed space with a distinct silhouette and
a biome-tied gimmick that has a purpose:

- **Hell** — indestructible magma channels that funnel the ball + teleporter routing.
- **Caverns** — crumbling stalactite columns hiding treasure blocks.
- **Witchland** — ghost blocks that phase on a timer + cursed multipliers.
- **Heaven** — mirror-symmetry light-beam puzzles.

No two levels share a layout. All levels stay winnable (`all-levels.spec.ts` green) and
AI-generatable (`docs/level-format.md`).

---

## Operating Rules (how each subpoint is executed & proven)

Work autonomously, **one move at a time, in order (1→5)**. Do not start a move until the
previous move's checkbox is ticked. Code that compiles is **not** proof.

For each subpoint:

1. **Update automatic tests + logs first.** Write/extend xUnit and Playwright (@390×844)
   tests that assert the new behavior. Add the structured log lines needed so a failure is
   diagnosable from the JSONL, not guesswork.
2. **Run them** and capture the passing output (test counts + the relevant log lines) into
   the per-move report.
3. **Capture mobile screenshots** of the actual rendered result, saved under
   `tests/demo-screenshots/`.
4. **Verify with a clean-context subagent.** Dispatch a fresh subagent given ONLY the
   requirement text (from this doc) + the screenshot/log artifact, and ask a narrow
   question like *"Does this screenshot/log output match this requirement?"*
   **Never pass the subagent any justification of decisions already taken** — it judges the
   artifact against the requirement cold. If it says no, fix and re-shoot.
5. **Tick the checkbox** in this file and **commit** (inner Arkanoid repo).

**Final gate:** full unit + Playwright suite green at 390×844, **run twice**; a final report
listing, per move, the proving test(s), the log evidence, and the screenshot filenames —
with an honest note on anything only partially met.

If a rule is missing, **add it here** rather than asking.

## Definition of Done (all required)

- [x] Classic-button home menu; legacy redundant button list gone; Editor not player-visible.
- [x] **Continue** resumes the campaign; secondary screens reachable via docked icons.
- [ ] Rift/dungeon entry is probabilistic from the campaign via banner; Dungeons menu removed.
- [ ] All HUD bars are symmetrical 9-slice, screenshot-verified at 0/50/100% fill on mobile.
- [ ] Every level has a distinct, purposeful layout; `all-levels.spec.ts` green; asset usage ≥ current ~86%.
- [ ] All unit tests + all Playwright specs green at 390×844 (update specs that asserted the old menu/buttons).
- [ ] Before/after mobile screenshots attached for: home menu, a rift banner, the HUD bars at 3 fills, and 3 redesigned levels.
