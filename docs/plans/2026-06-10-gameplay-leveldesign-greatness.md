# Heroes of Arkanoid II — Gameplay, Level Design & Greatness Plan

> **For agentic workers:** This plan is executed by a **Sonnet orchestrator** dispatching
> one subagent per task. Each task header declares `Model:` and `Parallel group:`.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Take the game from "polished UI on mediocre mechanics" to a product that feels
great to play — responsive controls, purposeful animation, handcrafted levels with real
escalation, and systems (power-ups, combos, boss mechanics) that create emergent moments
worth talking about.

**Architecture:** TypeScript + Vite frontend, PixiJS 7 battle scene, ASP.NET Core backend
game loop. Levels defined in `config/levels/*.json`, bricks defined in `config/blocks.json`,
game state flows as WebSocket snapshots from backend to frontend.

---

## 0. Definition of Quality

> "Quality" is not "it works." It is "a player who has never heard of this game picks it
> up, feels in control, learns through play, and wants to keep going." Every task in this
> plan must meet ALL of the criteria below, not just the ones that feel relevant.

### 0.1 Visual Quality

**Q-V1 — Purposeful animation.** Every animated element encodes game state, not just
decorates. If something moves, the player should be able to read information from HOW it
moves. A paddle frame that changes with mana is informative. A paddle frame that loops on
a timer is noise. Ask: *what does the player learn from this animation?* If the answer is
"nothing," remove it or tie it to state.

**Q-V2 — Motion is smooth and readable at 60fps.** No jarring frame pops at 6fps.
No visual discontinuity between animation states. Transitions between states (low mana →
high mana) feel continuous, not a hard swap.

**Q-V3 — No layout shift during play.** The HUD, paddle, and playfield occupy fixed
screen regions. Nothing outside the playfield changes size or position in response to
game events. Ball hits, spell casts, and HP changes must not reflow the page.

**Q-V4 — The playfield is readable at a glance.** A player who pauses the game and looks
at a screenshot for 2 seconds should be able to identify: where the ball is, where the
paddle is, which bricks are high-HP, which are dangerous, what the objective is. If the
layout looks like noise, the level design has failed.

**Q-V5 — Art is never distorted.** 9-sliced frames never stretch their caps. Sprites
render at native aspect. No pixelation on painted art. No broken/placeholder images in
production builds.

### 0.2 Gameplay Quality

**Q-G1 — Controls feel instant.** Paddle response to input must feel zero-lag. Any
processing in the input path must be imperceptible (<16ms at 60fps).

**Q-G2 — Death is always the player's fault.** After losing a life, the player must be
able to immediately explain why. "The ball was going too fast," "I missed it," "I didn't
break the brick in time" are acceptable. "Something weird happened" is a quality failure.
This means: minimum bounce angles enforced (no flat unplayable shots), ball speed capped
to a playable max, no silent mechanics that affect the ball without visual feedback.

**Q-G3 — Mechanics are taught, not assumed.** Every new brick type should appear first
in a context where the player has space to observe and experiment. Teleporters should
first appear in an open level with few other bricks. Ghost bricks should first appear
alone so the player can discover ball-phasing. A mechanic that surprises the player with
an unfair death is a design failure, not a difficulty feature.

**Q-G4 — Spells feel impactful.** Casting a spell must produce a visible and audible
effect that makes the player feel powerful. If a spell could be removed and the player
wouldn't notice for three levels, the spell design or its visual feedback is broken.

**Q-G5 — Each session has a clear arc.** A level must have a beginning (learning the
layout), a middle (executing the strategy), and an end (closing out remaining bricks
under pressure). Levels that feel like random clicking until bricks run out have failed
this criterion. This is achieved through: decreasing brick count → increasing ball speed,
structured layouts that create routing decisions, and power-ups / boss mechanics that
create memorable moments.

**Q-G6 — Difficulty is honest.** Harder = more complex decisions + less margin for
error, not just higher brick HP or faster projectiles. A level that is "hard" only
because enemies fire more bullets is padding, not design.

### 0.3 Level Design Quality

**Q-L1 — Every level has an identity.** After playing a level, a player should be able
to describe it in one sentence that makes it distinct from every other level. *"The one
where you have to chain the bombs to clear the rock wall"* is an identity.
*"The one with a lot of bricks"* is not.

**Q-L2 — The first 3 bricks teach the whole level.** The top-left region of the grid
(where the ball typically arrives first) should introduce the level's key mechanic. If
the interesting mechanic is hidden in the center, the player may clear most of the level
without encountering it.

**Q-L3 — Indestructible terrain creates routing, not frustration.** Walls and
indestructible bricks should channel the ball toward interesting decisions. A layout
where indestructible bricks trap the ball in a corner is a failure. The player should
always feel that the indestructibles are helping define the puzzle, not cheating.

**Q-L4 — Layouts read well in screenshot.** Take a screenshot of the level at start.
The grid should look like an intentional design — symmetrical, or deliberately
asymmetrical for a reason, with clear focal points. A random scatter of bricks is not
a layout. Use the visual gestalt: the player's eye should travel a path.

**Q-L5 — Escalation is felt within a biome.** Level 1 of a biome should be completable
in under 90 seconds by an average player. The boss should be the hardest encounter in
that biome and take 3–5 minutes. If levels 2–6 feel identical in difficulty to level 7,
the crescendo linter rule is not being enforced.

**Q-L6 — Biome identity is mechanical, not just cosmetic.** Hell levels feel different
from Heaven levels because the MECHANICS differ (descending pressure vs. escalating
enemies vs. phasing bricks), not just because the colors are different.

### 0.4 Audio Quality

**Q-A1 — No sound is ever worse than silence.** If a sound effect or music cue makes
the player want to mute the game, it must not ship. The bar for "acceptable" is: the
player notices when it is missing, but is not bothered while it plays. The bar for
"great" is: the sound reinforces the game feel and the player would miss it.

**Q-A2 — Audio communicates.** Brick destruction sounds different from spell cast
sounds different from ball-paddle contact different from taking damage. The player
should be able to recognize an event from sound alone.

**Q-A3 — Music does not compete.** Music must sit behind SFX in the mix at all times.
A spell cast should be clearly audible over the ambient track. This is enforced by the
DynamicsCompressor + master gain ceiling already in place.

---

## 1. Definition of Done

> A task is NOT done when the code is written. A task is NOT done when "it seems to
> work." A task is done when ALL of the following gates pass. No exceptions, no
> "I'll fix it in the next task."

### Gate 1 — Code health

- [ ] `cd frontend && npx tsc --noEmit` exits 0 (zero TypeScript errors)
- [ ] `cd backend/Arkanoid.Server && dotnet build` exits 0
- [ ] No `console.error` or unhandled promise rejections appear in the browser console
      during normal play. Test by opening DevTools and playing through the changed feature.
- [ ] No `TODO`, `FIXME`, or `HACK` comments introduced by this task
      (carry-overs in untouched code are acceptable)

### Gate 2 — Visual verification

- [ ] Screenshot at 390×844 (mobile portrait) — the primary design target
- [ ] Screenshot at 1280×800 (desktop letterboxed) — must show correct letterbox frame
- [ ] Read both screenshots and verify against **all 8 rules of §0** in the UI rulebook
      (`docs/plans/2026-06-10-ui-overhaul-execution.md`). A screenshot that "renders"
      but violates a rule = Gate 2 FAILED.
- [ ] If the task involves animation: capture a second screenshot 500ms after the first
      with the game in a different state. Verify the visual difference matches the
      expected state change (e.g., paddle looks different at 10% mana vs. 90% mana).

### Gate 3 — Gameplay verification (for any task touching game mechanics)

This gate requires live interaction, not screenshots.

- [ ] Open the game at `http://localhost:5175/?scene=battle&level=<affected-level>&seed=1`
- [ ] Play for at least 60 seconds without touching developer tools
- [ ] Verify the changed mechanic works in a real play session
- [ ] Verify that losing a life always feels explainable (Quality criterion Q-G2)
- [ ] If a new mechanic was introduced: verify it is legible within the first 10 seconds
      of encountering it (Q-G3)
- [ ] For level design tasks: complete the level. If you cannot complete it within
      5 minutes, the difficulty requires re-evaluation.

### Gate 4 — Animation & motion verification (for any task touching animated elements)

- [ ] Use `page.evaluate(() => window.__gameState)` or equivalent to read the actual
      numeric state (mana, HP, combo) from the live game
- [ ] Verify programmatically that the visual frame/state matches the numeric state:
      e.g., `paddleLayer._animFrame === 3` when `mana/maxMana > 0.75`
- [ ] If the task adds a new animation: verify it plays to completion without
      visual discontinuities by capturing 3 frames 200ms apart during the animation

### Gate 5 — Test suite

```bash
# Run the task's mapped spec(s) — listed in each task below
cd tests && npx playwright test <spec>.spec.ts --reporter=line --workers=1
```

- [ ] All tests in the mapped spec pass
- [ ] If no spec exists for the feature: add one as part of the task
- [ ] Known pre-existing failures (not introduced by this task) are documented
      inline in the test with `// pre-existing: <reason>`

### Gate 6 — Level design specific (for Wave 2 tasks only)

- [ ] Run `node tools/gen-levels.mjs --lint config/levels/<new-level>.json`
      Expected: PASS (all 5 linter rules satisfied)
- [ ] View the level layout as ASCII (print the `rows_data` array). It must look like
      an intentional design, not a random scatter. Apply Q-L4: does the eye travel
      a path?
- [ ] Name the level's identity in one sentence (Q-L1). If you cannot, redesign.
- [ ] Verify the first three rows teach the key mechanic (Q-L2)

### Gate 7 — Commit

- [ ] Conventional commit message: `type(scope): description`
- [ ] Message body explains the WHY, not just the WHAT
- [ ] Never `--no-verify`
- [ ] If this task touched `config/levels/`: include the level identity sentence
      in the commit body

---

## 2. Ground Rules (read before every task)

All rules from the UI overhaul plan (`docs/plans/2026-06-10-ui-overhaul-execution.md §0.1`)
apply here. Additional rules for this plan:

- **NEVER `git stash` / `git checkout` / `git reset` the working tree.** Vite HMR ships
  any change straight to the user's open browser. This has burned us before.
- **Gameplay changes require live play verification** (Gate 3). Screenshots are
  insufficient for mechanics.
- **A self-report of "it works" fails Gate 3.** The worker must describe what they
  observed during live play — what the mechanic looked like, felt like, what happened
  when they triggered it.
- **Static screenshots cannot verify animation.** Any task that changes animated
  elements must use Gate 4 (programmatic frame inspection or multi-frame capture).
- **Do not change `cols`/`rows` on existing levels.** Only new levels may use
  non-standard grid sizes. Existing levels are in the campaign and changing their
  dimensions would break campaign progression.

---

## Wave 0 — Critical bugs (must ship first, visible every second of play)

### Task 0.1: Paddle bar tied to mana level — **Model: Sonnet**

**The bug:** `PaddleLayer.ts` cycles through the 4 bar frames on a 6fps timer,
completely ignoring mana. But the frames are clearly state art: frame 0 is a compact
simple paddle, frame 3 is the wide elaborate dragon-head form. Cycling them randomly
makes the paddle look like it is constantly charging and discharging at random.
The squash/stretch animation is correct and must NOT be touched.

**Why it wasn't caught:** All verification passes use static screenshots. A single
screenshot freezes one frame of the cycle. Motion-sensitive bugs require
multi-frame or programmatic checks. Gate 4 exists to close this gap going forward.

**Files:** `frontend/src/render/PaddleLayer.ts`, `frontend/src/render/Renderer.ts`,
`tests/paddle-anim.spec.ts` (create)

- [ ] In `PaddleLayer.ts`: remove `_animElapsed` and the timer-based frame cycling
      from `updateAnim()`. Add a `setMana(ratio: number): void` method that maps
      `ratio` (0.0–1.0) to frame index: `Math.min(3, Math.floor(ratio * 4))`.
      Call `setMana` to update `_animFrame` and immediately swap the texture on both
      halves. The squash/stretch block in `updateAnim()` stays exactly as-is.
- [ ] In `Renderer.ts`: find where `paddleLayer.update()` is called in the snapshot
      update path. Pass `snapshot.mana / snapshot.maxMana` (or equivalent field names —
      check the snapshot type). Call `paddleLayer.setMana(manaRatio)` there.
- [ ] In `tests/paddle-anim.spec.ts`: write two tests:
      - `"paddle bar shows frame 0 at low mana"`: open a battle, use
        `cheat(page, "setMana", 5)` pattern (see `hud-live.spec.ts`), then
        `const frame = await page.evaluate(() => window.__renderer?.paddleLayer?._animFrame)`
        and assert `frame === 0`.
      - `"paddle bar shows frame 3 at full mana"`: same with mana = 95, assert `frame === 3`.
      Export `__renderer` on `window` if not already present (check `BattleScene.ts`).
- [ ] TypeScript check, screenshots at both viewports, live play verification:
      cast a spell to drain mana, watch the paddle simplify; regenerate mana, watch it
      rebuild. Describe what you saw.
- [ ] Spec: `paddle-anim.spec.ts` (new), `hud-bars.spec.ts` (regression check)
- [ ] Commit: `fix(render): paddle bar driven by mana ratio — removes nonsensical timer loop`

### Task 0.2: Remove dev/test levels from production — **Model: Haiku**

**The bug:** `hell-teleport`, `hell-winnable`, and `test-editor-auto` are in
`config/levels/` and accessible in production. `hell-3` and `village-3` are missing
from the numbered sequence, creating gaps in the campaign.

**Files:** `config/levels/`, `config/campaign.json`,
`config/levels/hell-3.json` (create), `config/levels/village-3.json` (create)

- [ ] Read `config/campaign.json` to understand the level sequence format
- [ ] Move `hell-teleport`, `hell-winnable`, `test-editor-auto` to a new
      `config/levels/dev/` subfolder. Do NOT delete them (they may be useful for testing).
      Verify campaign.json does not reference them; if it does, remove those references.
- [ ] Create `hell-3.json` — a simple level whose ONLY new mechanic is the green
      teleporter (first teleporter introduction). Layout: open grid with 4 basic
      bricks and one green teleporter pair in the center two columns, rows 3–4.
      Teach the mechanic in isolation. Use standard 8×14 grid.
      Apply Q-L1: *"The one where you discover that teleporters warp the ball across the grid."*
- [ ] Create `village-3.json` — first necromancer encounter. Layout: sparse grid,
      a few basic bricks arranged around a single necromancer. Enough space to observe
      the resurrection mechanic without being overwhelmed. Apply Q-L1.
- [ ] Run the level linter on both new files
- [ ] Add both new levels to `config/campaign.json` in the correct sequence position
- [ ] Commit: `fix(levels): remove dev levels from prod, fill hell-3 + village-3 gaps`

---

## Wave 1 — Gameplay feel (run in parallel after Wave 0)

### Task 1.1: Ball physics escalation — **Model: Sonnet**

**Problem:** Ball speed is flat for the entire level. The last few bricks of a level
(especially if they are in a corner) become a tedious wait rather than a tense finish.
There are no guardrails on bounce angle, so rare flat-horizontal shots are unplayable.

**Files:** `backend/Arkanoid.Server/Game/Ball.cs` (or equivalent),
`frontend/src/render/BallLayer.ts`, `backend/Arkanoid.Server/Game/Board.cs`

- [ ] **Speed escalation:** Every time 20 bricks are destroyed in the current level,
      increase ball speed by 5% (compound). Cap at 1.4× start speed. Reset on new level.
      The start speed itself stays unchanged — only accumulation within a level increases it.
- [ ] **Minimum bounce angle:** After any collision, if the resulting velocity vector
      makes an angle < 20° from horizontal (i.e., `|vy/vx| < tan(20°) ≈ 0.364`),
      nudge the angle to exactly 20° from horizontal (preserve the direction sign
      of vy, just raise its magnitude). This prevents unplayable flat shots.
- [ ] **Last-brick outline:** When ≤ 3 destructible bricks remain on the board, add
      a pulsing gold outline to each. In PixiJS: add a `Graphics` overlay in `BallLayer`
      or a new `DangerLayer`; draw a gold rectangle (lineStyle 2px, `0xd8a84e`, alpha
      `0.5 + 0.4 * sin(time)`) around each remaining destructible brick position.
      Remove the overlay when count rises above 3 (e.g., necromancer resurrection).
- [ ] Gate 3 (live play): complete hell-1 and describe whether the final phase
      feels more tense than before.
- [ ] Commit: `feat(physics): speed escalation, min-angle guard, last-brick highlight`

### Task 1.2: Power-ups system — **Model: Sonnet** (design + plumbing)

**Problem:** Currently nothing drops from bricks. Every level is pure attrition.
Power-ups create emergent moments, give the player agency, and make each run feel
slightly different.

**Files:** `backend/Arkanoid.Server/Game/PowerUp.cs` (create),
`backend/Arkanoid.Server/Game/Board.cs`, `frontend/src/render/PowerUpLayer.ts` (create),
`frontend/src/ui/Hud.ts`, `config/blocks.json`

**Five power-ups (implement all five):**

| ID | Name | Effect | Duration | Drop source |
|----|------|--------|----------|-------------|
| `wide` | Wide Paddle | Paddle 40% wider | 15s | hell_tough, cavern_tough |
| `multiball` | Multi-ball | Splits ball into 3 | Instant | cavern_bomb (on destroy) |
| `fireshot` | Fire Shot | Ball destroys indestructibles | 10s | village_cauldron |
| `manasurge` | Mana Surge | Fills mana to 100% | Instant | heaven_vase |
| `shield` | Shield | One auto-save from falling | One-touch | heaven_tough |

**Drop mechanic:** When a brick in the "drop source" list is destroyed, spawn a
power-up item at that brick's center with a 25% chance. The item falls at a constant
rate (same as `descendInterval` projectiles). Paddle collision picks it up.

**Visual:** Each power-up is a 24×24px icon (colored circle with a symbol is acceptable
— these can be placeholder geometry in PixiJS until art ships). The icon falls smoothly.
On pickup: play the existing "spell cast" SFX, flash the HUD entry.

**HUD:** Add a row of up to 3 small active-power-up icons in the top-right corner
of the HUD (inside the `topLeft` panel or a new `topRight` panel). Each icon shows
a countdown arc (CSS conic-gradient or PixiJS arc). Expired icons disappear.

- [ ] Backend: `PowerUp.cs` with state machine (Falling → Active → Expired).
      Add to game snapshot.
- [ ] Frontend: `PowerUpLayer.ts` renders falling and active indicators.
- [ ] Verify Wide Paddle: paddle visibly wider in screenshot after pickup.
- [ ] Verify Multi-ball: 3 balls visible in screenshot after pickup.
- [ ] Gate 3: play a cavern level, trigger a bomb chain, observe multiball drop.
- [ ] Spec: `powerup.spec.ts` — at minimum test that a power-up item appears in the
      snapshot when triggered via cheat, and disappears after its duration.
- [ ] Commit: `feat(gameplay): power-ups system — wide/multiball/fireshot/manasurge/shield`

### Task 1.3: Combo system — **Model: Haiku**

**Problem:** No feedback for consecutive hits. Clearing a row is the same as clearing
one brick at a time. No reason to play skillfully beyond survival.

**Files:** `backend/Arkanoid.Server/Game/ComboTracker.cs` (create),
`frontend/src/ui/Hud.ts`, `frontend/src/ui/hud/bars.ts`

- [ ] **Combo counter:** Consecutive brick destructions without the ball touching the
      paddle increment the combo: ×1 → ×2 → ×3 → ×4 (cap). Ball-paddle contact resets
      to ×1. Multiplier applies to crystal rewards from brick destruction.
- [ ] **Score popup:** When a brick is destroyed at multiplier > 1, a floating text
      (`+N ×M`) rises from the brick position over 0.8s then fades. PixiJS Text.
- [ ] **HUD display:** A small combo badge in the top-right: text `×2` / `×3` / `×4`
      in gold, hidden at ×1. Animate in with a brief scale bounce (0.7→1.0 over 150ms)
      when the multiplier increases.
- [ ] Gate 3: play a level and deliberately keep a multi-hit streak; verify the
      multiplier displays and resets correctly.
- [ ] Spec: `combo.spec.ts` — verify multiplier appears in snapshot after 3 consecutive
      hits without paddle contact.
- [ ] Commit: `feat(gameplay): combo multiplier — consecutive-hit streak × crystal bonus`

---

## Wave 2 — Level design (run in parallel after Wave 0)

### Task 2.1: Variable grid size support — **Model: Sonnet**

**Problem:** Every level is 8×14. This means all levels have the same spatial rhythm.
Tight levels and gauntlet levels are impossible to express.

**Files:** `backend/Arkanoid.Server/Game/Board.cs`, `frontend/src/render/Renderer.ts`,
`tools/gen-levels.mjs`

**Permitted grid sizes (add to linter allowlist):**

| Name | Cols × Rows | Use case |
|------|-------------|----------|
| Tight | 6 × 12 | Short levels, fast ball dynamics, close walls |
| Standard | 8 × 14 | Current default |
| Wide | 10 × 16 | Complex patterns, dense late-game levels |
| Tall | 8 × 20 | Vertical gauntlets, descend/survive mechanics |

- [ ] Backend: ensure `Board.cs` reads `cols`/`rows` from the level JSON and does
      not hardcode 8 or 14 anywhere. Check for any hardcoded dimension constants.
- [ ] Frontend: `Renderer.ts` already scales the playfield to available space —
      verify it reads `snapshot.cols` / `snapshot.rows` (not constants). Add a
      regression test screenshot for a 6×12 level (use hell-winnable or a dev level
      resized — don't touch campaign levels).
- [ ] Linter: `tools/gen-levels.mjs` Block-set rule currently checks `cols === 8`.
      Update to accept any of the four sizes above. Add a test in the linter for
      a non-standard size.
- [ ] Do NOT change any existing campaign level dimensions.
- [ ] Commit: `feat(engine): variable grid size — tight/standard/wide/tall vocabulary`

### Task 2.2: New Hell levels — **Model: Sonnet**

**Files:** `config/levels/hell-8.json` (create),
`config/campaign.json`

*(hell-3 is handled in Task 0.2)*

- [ ] **hell-8 (pre-boss climax):** The hardest non-boss Hell level. All Hell mechanics
      combined: teleporters, ball-spawners, lava, obsidian routing walls. Wide grid
      (10×16). Dense layout — no empty rows in the top 12 rows. The teleporters connect
      non-obvious positions to create routing puzzles. One ball-spawner at the midpoint
      guards the path to the core.
      Identity: *"The gauntlet — survive the spawner and navigate the teleport maze."*
- [ ] Apply all 7 Definition of Done gates.
- [ ] Commit: `feat(levels): hell-8 — teleport maze gauntlet (wide 10×16)`

### Task 2.3: New Village & Heaven levels — **Model: Sonnet**

**Files:** `config/levels/village-7.json` (create), `config/levels/heaven-7.json` (create),
`config/campaign.json`

*(village-3 is handled in Task 0.2)*

- [ ] **village-7 (ghost labyrinth):** Dense ghost-brick grid in a deliberate pattern
      that forces the player to plan shot angles through the phasing bricks to reach
      the necromancers behind them. Identity: *"The labyrinth — ghost bricks hide the
      necromancers; find the angles that matter."*
- [ ] **heaven-7 (escalating-statue gauntlet):** Tall grid (8×20). Statues strengthen
      every 8s (`escalateInterval`). Columns of shield statues create protected zones
      that require melee-statue clearance first. The survive-time mechanic applies:
      player wins by surviving 90s, not by clearing all bricks.
      Identity: *"The ascension — survive the statues' escalation for 90 seconds."*
- [ ] Apply all 7 Definition of Done gates for each level.
- [ ] Commit: `feat(levels): village-7 ghost labyrinth + heaven-7 escalating gauntlet`

### Task 2.4: Boss level redesign — **Model: Opus**

**The problem:** Current boss levels are static diamond brick layouts — just high-HP
targets. They feel like a regular level with a different arrangement. A boss should have
a MECHANICAL IDENTITY that creates a distinct challenge requiring specific strategy.

**Files:** `config/levels/hell-boss.json`, `config/levels/caverns-boss.json`,
`config/levels/village-boss.json`, `config/levels/heaven-boss.json`,
possibly `backend/Arkanoid.Server/Game/Entities/BossEntity.cs` (new behaviors)

**Boss mechanical identities (implement all four):**

**Hell Demon — "The Advance"**
The demon starts in row 2 (top). Every 30 seconds, it descends one row (or triggers
a `descendEvent`). If it reaches row 12 (paddle row), the player loses a life
automatically. Player must clear a path through the surrounding bricks and destroy
the demon before it descends too far. The pressure is time, not HP alone.
Layout: demon in row 2, obsidian routing walls creating two corridors to reach it,
dense tough/basic bricks filling the corridors. The player has ~90s before the demon
reaches the danger zone.

**Cavern Goblin — "The Barricade"**
The goblin is surrounded by a 2-brick-thick wall of rock (indestructible). Bomb bricks
are placed on the perimeter of the rock wall. The player cannot damage the goblin
directly — they must chain-explode the bombs to remove the rock perimeter, then
clear the exposed interior. Layout reveals itself progressively as bombs detonate.

**Village Witch — "The Wave Summoner"**
When the witch drops below 75%, 50%, and 25% HP, she summons a full row of ghost bricks
in row 10 (just above the paddle zone). This requires immediate diversion to prevent
the ghost wall from interfering with the ball path. The player is constantly juggling
the boss fight and the summoned barriers.

**Heaven Angel — "The Guardian"**
The angel heals 2 HP every 8 seconds (`escalateInterval`) while any Melee Statue is
alive. Three melee statues are distributed on the field. The player must kill all three
statues first to stop the healing, then focus the angel. If the player tries to rush
the angel directly, the healing outraces the damage.

- [ ] Redesign all four boss JSON layouts according to the mechanical identities above
- [ ] Add any required backend entity behavior (descend event, wave summon, guardian heal)
- [ ] Gate 3 (live play): complete each boss fight. Verify the identity is felt —
      describe the decisive moment in each fight.
- [ ] Spec: `boss.spec.ts` — at minimum verify each boss's special behavior triggers
      (descend event fires, ghost row appears on HP threshold, guardian heals)
- [ ] Commit: `feat(bosses): mechanical identities — advance/barricade/wave-summoner/guardian`

---

## Wave 3 — Testing infrastructure (after Wave 1 + Wave 2)

### Task 3.1: Motion and animation regression suite — **Model: Sonnet**

**Problem:** The paddle bar bug existed for the full session and was never caught because
our entire test suite is static-screenshot-based. We need programmatic motion tests.

**Files:** `tests/paddle-anim.spec.ts` (extend from Task 0.1),
`tests/powerup.spec.ts` (extend from Task 1.2)

- [ ] **Multi-frame diff helper:** Write a helper `captureFrames(page, url, n, intervalMs)`
      that returns N screenshots taken `intervalMs` apart. Used to assert that something
      changes or doesn't change over time.
- [ ] **Paddle continuity test:** Play a battle, capture 5 frames at 200ms intervals
      during normal play. Assert that consecutive frames do NOT show frame-index jumps
      larger than 1 (no teleporting from frame 0 to frame 3). The transition between
      mana states should be gradual.
- [ ] **Paddle-mana correlation matrix:** Test all four mana tiers (5%, 30%, 60%, 95%),
      assert correct frame for each.
- [ ] **Squash trigger test:** Wait for a ball hit, capture 3 frames at 60ms intervals,
      assert that `scaleY` decreases below 0.8 in at least one intermediate frame.
- [ ] Commit: `test(anim): motion regression suite — paddle frame-mana correlation + squash timing`

---

## Wave 4 — Content & polish (independent, run anytime)

### Task 4.1: Achievement descriptions — **Model: Haiku**

**Problem:** All 13 achievements display `???` as description. This reads as placeholder
content shipped to production.

**Files:** wherever achievement data is defined (search for `???` in `config/` or `backend/`)

Tone guide: dark fantastical, matching the biome. Short (one sentence). Never ironic
or meta. Examples:
- *First Victory* → *"The first brick falls. Hell noticed."*
- *Hell Survivor* → *"Three floors of fire, and you came back changed."*
- *Boss Slayer* → *"Demons have names. You took one."*
- *Dungeon Crawler* → *"The descent is voluntary. The return is not guaranteed."*

- [ ] Find the achievement data source
- [ ] Write descriptions for all 13 achievements in the established tone
- [ ] Verify they display in the Achievements screen (screenshot)
- [ ] Commit: `content(achievements): flavor text for all 13 — dark fantastical tone`

### Task 4.2: Menu key-art integration — **Model: Haiku** (when asset exists)

**This task is blocked on a human deliverable:** a commissioned hero portrait.
Spec for the artist: Fire Mage character, portrait format, 390×600px, transparent PNG,
warm lighting from below (lava glow), painted/semi-realistic style consistent with
the existing paddle bar art.

When the asset exists:
- [ ] Place it at `frontend/public/art/MenuKeyArt.png`
- [ ] In `MenuScene.ts`: set `background-image: url('/art/MenuKeyArt.png')` on
      `.menu-keyart`, `background-size: cover`, `background-position: center top`,
      `opacity: 0.6` (the CTA must remain readable over it)
- [ ] Screenshot at both viewports, verify the CTA buttons are legible over the art
- [ ] Commit: `feat(menu): hero key-art behind CTA column`

---

## Execution order

```
Wave 0 ─────────────────────────────────┐
  0.1 Paddle bar (Sonnet)               │ Both parallel
  0.2 Dev levels cleanup (Haiku)        │
                                        ▼
Wave 1 + Wave 2 + Boss ─────────────────┬──────────────────────────┐
  1.1 Ball physics (Sonnet)             │                          │
  1.2 Power-ups (Sonnet)                │ All parallel with        │
  1.3 Combo system (Haiku)              │ each other               │
  2.1 Grid size support (Sonnet)        │ (zero file overlap       │
  2.2 Hell levels (Sonnet)              │  between tasks)          │
  2.3 Village+Heaven levels (Sonnet)    │                          │
  2.4 Boss redesign (Opus)              │                          │
                                        ▼                          │
Wave 3 ─────────────────────────────────┘                          │
  3.1 Motion test suite (Sonnet)        after 1+2 merge            │
                                                                   │
Wave 4 (anytime, truly independent) ───────────────────────────────┘
  4.1 Achievement descriptions (Haiku)
  4.2 Key-art integration (Haiku, blocked on artist)
```

---

## Model assignment rationale

| Task | Model | Reason |
|------|-------|--------|
| 0.1 Paddle bar | Sonnet | Requires threading state through Pixi renderer — judgment about where to hook |
| 0.2 Dev level cleanup | Haiku | Mechanical, clear spec, no ambiguity |
| 1.1 Ball physics | Sonnet | Backend game loop math + PixiJS overlay — multi-file with logic |
| 1.2 Power-ups | Sonnet | New entity type, cross-layer (backend + frontend) — architect role |
| 1.3 Combo system | Haiku | Simple counter + display, well-scoped |
| 2.1 Grid sizes | Sonnet | Needs to understand renderer + backend sync, subtle bugs possible |
| 2.2 Hell levels | Sonnet | Requires reading existing layouts for context + design judgment |
| 2.3 Village/Heaven levels | Sonnet | Same |
| 2.4 Boss redesign | **Opus** | Creative game design + new entity behaviors. The mechanical identities require design intuition, not just execution. A boss that "feels like a boss" requires judgment a smaller model will flatten into mediocrity. |
| 3.1 Motion tests | Sonnet | Novel Playwright patterns (multi-frame), requires test design judgment |
| 4.1 Achievement text | Haiku | Pure creative writing within a tight tone brief |
| 4.2 Key-art wiring | Haiku | One CSS rule once the asset exists |

---

## Sources (level design research)

- Schell, Jesse — *The Art of Game Design*, Chapter 16: The World of the Game
- Mark Brown / Game Maker's Toolkit — "How Super Mario Odyssey's Levels Were Designed"
  (introduce → develop → twist → conclude per zone)
- Sirlin, David — "Playing to Win" (game balance and mechanical fairness)
- Juul, Jesper — *Half-Real* (failure must be explainable, not arbitrary)
- Extra Credits — "Juice It or Lose It" (feedback density and game feel)
