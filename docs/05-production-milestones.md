# Production Milestones — From Prototype to Shippable Mobile Game

> The M1–M7 milestones built a **working architecture with a thin functional slice of every system**. They are done and tested, but the result is a prototype: flat-rectangle paddle, no biome backgrounds, plain-div menus, no animation, desktop-only, ~30 of 755 sprites used. These **P-milestones** turn it into a real product.
>
> **Decisions (2026-06-08):** Mobile-first (touch/portrait, the original was Android) · keep the browser(PixiJS)+.NET architecture · "complete" = real backgrounds + full UI art + animations/juice + full content depth · audio deferred (no audio assets exist).

---

## The Strict Definition of Done (applies to EVERY P-milestone)

A milestone is **not done** until ALL of these hold. "Tests pass" is necessary, not sufficient.

1. **Mobile-first, verified on a phone viewport.** Every screen/feature is captured and reviewed at **390×844 portrait** (iPhone) AND checked at a small Android size (360×640) and one landscape size. Touch is the primary input. UI targets are ≥44px. No horizontal scroll, no clipped content, no desktop-only assumptions.
2. **Real assets only — zero placeholders.** No flat-colored rectangles, no plain-`<div>` chrome on shipped surfaces. Every paddle/ball/block/boss/background/menu/button/icon uses real art from `Sprites/`. If a needed asset is missing, that's a flagged blocker, not a rectangle.
3. **It moves.** Anything that should animate, animates (per the asset set): ball/spell/block-break/explosion/paddle/boss/hero. Impacts have juice (feedback, particles, squash/shake).
4. **Rendering is ON and performant on mobile.** The automation render-skip band-aid from M7 is replaced: the renderer runs in tests on a mobile viewport and the integration suite stays green WITH rendering, at mobile-representative resolution. (If perf is the blocker, fix perf — don't skip render.)
5. **Tests green + no regressions.** Unit (Core) + Playwright (mobile viewport) all pass; new behavior gets new tests; prior milestones still work.
6. **Screenshot proof committed.** A `demo-screenshots/p<N>-*.png` set on the mobile viewport that visibly demonstrates the bar was met. I review them and state honestly whether they look shippable.

> If a milestone's screenshots still look like a prototype, the milestone is **not done** — regardless of green tests.

---

## P0 — Asset audit & pipeline *(foundation — do first)*
**Goal:** Know and load the whole art set, so later milestones can actually use it.
- Full inventory of all 755 sprites → a documented manifest categorizing: biome **backgrounds** + transitions; **paddles/bars** (per class, size tiers); **balls**; **hero** sprites/animation frames; **spell** effect frames; **block sets** (per biome, per HP state, specials); **boss rigs** (multi-part: Demon/Goblin/Witch/Beholder/Bats); **items** (20 × 3 tiers); **bonus** pickups; full **UI** sets (Menu_Main / MissionSelect / Rewards / Achievements / Battle Interface / Buttons / Inventory); **HintSystem**; **icons**.
- Build a real **asset pipeline**: a packed texture **atlas** (not 30 loose PNGs) + an **animation-frame manifest** (which sprites are multi-frame animations and their frame order/fps) + a mapping doc from game concept → asset.
- Identify multi-part **boss rig** assembly (the old bosses are composed of body/head/hands/etc.).
**DoD:** atlas + manifest loaded in the frontend; a `docs/asset-manifest.md` mapping every game concept to its art; a coverage report (what % of assets are now reachable). Screenshots N/A but the manifest is the artifact.

## P1 — Mobile-first shell & touch controls
**Goal:** It's a phone game you control with your thumb.
- Responsive **portrait layout** that fits any phone (play field + HUD + on-screen controls, no scroll); graceful landscape; safe-area insets.
- **Touch controls:** drag-anywhere paddle control, tap-to-serve, thumb-reachable on-screen **spell buttons**; haptic-style feedback where possible. Mouse/keyboard still work on desktop.
- Viewport/meta setup, no pinch-zoom, **PWA-installable** (manifest + icon from the Android assets), fullscreen.
- **Replace the M7 render-skip:** make the Pixi render mobile-performant and run Playwright at a mobile viewport WITH rendering on.
**DoD:** a full game loop is playable **by touch** at 390×844 portrait (serve, move paddle by finger, cast spells, win/lose); mobile screenshots; the integration suite runs on a mobile viewport with rendering on and is green.

## P2 — Battlefield art (backgrounds, paddle, ball)
**Goal:** The battle looks like a real level, not a void.
- Each biome battle renders its **real background** (`Fons/`: Hell/Dungeon/Village/Heaven) with subtle parallax/atmosphere; biome transitions where appropriate.
- **Sprite paddle** using the real bar art (per active class, with the size-tier sprites reflecting paddle width); **sprite ball** with the proper art + states (ignited etc.).
- Real **frame/wall** art around the play field if available; bottom "drain" zone styled.
**DoD:** every biome's battle shows its real backdrop + sprite paddle + sprite ball on mobile; per-biome screenshots; no flat void, no rectangle paddle.

## P3 — Full UI & screens art
**Goal:** Every screen looks like a finished mobile game.
- Real art + mobile layout for: **main menu** (Menu_Main), **campaign/mission-select map** (MissionSelect art over a map), **character select**, **dungeon** screens, **reward** screens (Rewards art), **upgrade/skill** screen (Menu_Skill), and the **battle HUD** (Battle Interface art — real hearts/mana/spell frames, not plain bars).
- Buttons from `Buttons/`; consistent typography; transitions between screens.
**DoD:** every screen uses real art and is laid out for portrait phone; screenshot of each screen on mobile; an outside eye would call it "a game," not "a tool."

## P4 — Animation system & juice
**Goal:** It feels alive.
- A sprite-**animation system** (frame sequences from the manifest) driving: ball trail/spin, spell cast + projectile animations, **block-break** animations (per biome), explosions, paddle **squash/stretch** + bounce, hero/idle animation if shown.
- **Juice:** hit-stop on big hits, particle bursts, combo/score popups, screen feedback, satisfying destruction.
**DoD:** the listed elements animate from real frames; every impact has feedback; mobile screenshots + a short captured frame sequence showing motion.

## P5 — Real boss fights
**Goal:** Bosses are fights, not high-HP bricks.
- Reassemble + animate the **multi-part boss rigs** (Demon, Goblin, Witch) with: intro, **telegraphed attack patterns** (hazards the player dodges — building on the HP/two-resource combat), phases, hit reactions, and a **defeat animation**.
- A boss encounter per biome finale (Hell→Heaven), wired into the campaign + dungeons.
**DoD:** at least 3 animated boss fights with readable patterns, beatable by skill, on mobile; boss screenshots + the fight verified end-to-end.

## P6 — Content depth: items, bonuses, classes, levels
**Goal:** Enough game to be worth playing repeatedly.
- **Items system** (the old 20 items × 3 tiers) with real item art, equip slots, effects — alongside relics (or reconciled into one coherent loot model).
- **Falling bonus pickups** (`Bonus/` art): catch with the paddle for temporary boosts.
- **All 4 classes fully playable** with distinct, art-backed kits: finish **Necromancer** from its existing art; give each class a signature active spell (not just a passive); Paladin/Engineer kits fleshed out.
- **Level count** that supports the campaign curve + dungeon variety (target a real number, e.g. 20+ authored/standardized levels across biomes).
**DoD:** items + bonuses live with art; 4 classes each play distinctly; content counts hit the targets; all validated by the all-levels test + new content tests; mobile screenshots.

## P7 — Polish, balance & release-readiness
**Goal:** Shippable.
- **Onboarding/tutorial** using `HintSystem` art; **settings** (controls, etc.); **achievements** (`Achivements/` art) if in scope.
- **Standardized balance pass** across the full curve (block HP/density, spell costs/mana, rewards, dungeon scaling) — the standardization the old game lacked, now with the content to tune.
- **Mobile performance** budget met (stable frame rate on a mid phone); **PWA/Android** packaging (icon, banner, install); save robustness.
- A **full playthrough on a phone** from new-game to a biome boss.
**DoD:** a complete, balanced, installable mobile game; recorded full mobile playthrough; final screenshot set; honest "this is shippable" sign-off.

---

## Deferred / out of scope (unless you say otherwise)
- **Audio (SFX + music)** — no audio assets exist in the copied folders. Add a P8 only if you source/approve royalty-free audio or provide the originals.

## Process per milestone
Feature-by-feature via subagents, but **each milestone ends with me personally reviewing the mobile screenshots against the Definition of Done and telling you honestly whether it clears the bar** — not just "tests pass." If it looks like a prototype, it's not done.
