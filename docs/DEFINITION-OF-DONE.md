# Heroes of Arkanoid II — Definition of Done

> **This is the authoritative quality standard for the game.**
> A feature, fix, or improvement is not done when it compiles. It is not done when it
> "seems to work." It is done when every gate in this document passes. No exceptions.
> No "I'll clean it up later." If a gate can't be verified now, the task is not started yet.

---

## What "Great" Means for This Game

A player who has never heard of this game picks it up on a phone. Within 10 minutes they:

1. Understand what to do without reading a manual
2. Feel in control of the paddle — not fighting the game
3. Notice their own skill improving
4. Experience one moment they didn't expect (a combo, a power-up, a boss pattern)
5. Want to play the next level

If any of those five things don't happen, the game is not great yet.

---

## Part I — Visual & UI

### V1 · Design System Compliance

Every line of CSS written must come from the token system in `frontend/src/ui/theme.ts`.

**Verification checklist:**

- [ ] All colors reference CSS variables (`var(--gold-bright)`, `var(--text-dim)`, etc.) — no raw hex or `rgba()` that duplicates a token
- [ ] All font families reference `var(--font-body)` or `var(--font-display)` — no hardcoded `'Segoe UI'`, `sans-serif`, or `Georgia`
- [ ] All font sizes reference `var(--fs-*)` tokens or a `clamp()` derived from them — no bare `13px` or `0.9rem`
- [ ] All spacing values (gap, padding, margin) use `var(--sp-*)` tokens (4/8/12/16/24px scale) or multiples of them — no arbitrary `6px`, `14px`, `22px` gaps
- [ ] All transition durations reference `var(--dur-fast)` (0.1s), `var(--dur-normal)` (0.15s), or `var(--dur-slow)` (0.35s) — no hardcoded `0.2s ease` scattered across files
- [ ] New semantic colors are added to `theme.ts` first, then referenced — no one-off inline hex that "only appears once"

**What this prevents:** A color change to the gold palette requires editing one file, not grepping through 15. The HUD, which is the most visible screen during play, must comply — it currently does not (zero CSS variables in `Hud.ts`). That is a blocker.

---

### V2 · Touch & Interaction

Every interactive element a player can tap must meet these standards.

**Verification checklist:**

- [ ] All buttons, toggles, and tappable cards have a minimum hit area of **44×44px** (WCAG 2.5.5)
- [ ] All interactive elements have `:hover` feedback (filter brightness or transform)
- [ ] All interactive elements have `:active` feedback (transform scale down)
- [ ] All interactive elements have a `:focus-visible` state — a gold outline or equivalent that is visually distinct from the rest state. Never remove focus outline without replacing it.
- [ ] No element uses `-webkit-tap-highlight-color: transparent` without a visible `:active` replacement
- [ ] Spell slots in the HUD are ≥44×44px in both portrait and landscape modes

**What this prevents:** Keyboard and gamepad users can navigate without guessing. On iOS, focus-visible matters for Switch-style external controllers. Progress dots and achievement cards were found to be borderline — they must be fixed before shipping.

---

### V3 · Typography Hierarchy

Every screen must communicate a clear reading order.

**Verification checklist:**

- [ ] Screen title: `var(--fs-title)` (26px), `var(--font-display)`, `var(--gold-bright)`, text-shadow for lift
- [ ] Section labels / panel headers: `var(--fs-section)` (15px) or smaller, weight 700, `var(--gold)`
- [ ] Body / descriptions: `var(--fs-body)` (13px), `var(--font-body)`, `var(--text-dim)`
- [ ] Metadata / timestamps / kickers: `var(--fs-small)` (11px), `var(--text-faint)`, letter-spacing 0.12em+
- [ ] No two text styles on the same screen share identical size + weight + color — every text element is visually distinct from its neighbors
- [ ] No text is smaller than 10px anywhere in the game

---

### V4 · Art Integrity

**Verification checklist:**

- [ ] All 9-sliced frames render without cap distortion — corner art must not stretch or compress. Verify visually at 390px and 430px viewport widths.
- [ ] All sprites render at their native aspect ratio — no `width: 100%; height: auto` on a fixed-height parent that squashes them
- [ ] No broken image placeholders (`alt` text visible, red borders) in any scene
- [ ] Paddle renders mirrored-symmetrically — left and right edges match
- [ ] Blocks in the playfield render at native aspect — not stretched to fill an arbitrary cell size

---

### V5 · Animation Quality

Animation is informative, not decorative. Every animation must encode game state.

**Verification checklist:**

- [ ] Paddle bar frame is driven by `mana / manaMax` ratio — frame 0 = 0–25%, frame 1 = 25–50%, frame 2 = 50–75%, frame 3 = 75–100%. Verified by `tests/paddle-anim.spec.ts`.
- [ ] Paddle squash fires on ball-paddle contact and resolves within 250ms. Verified by `tests/paddle-anim.spec.ts`.
- [ ] Combo badge animates in (`combo-pop`, scale 0.7→1.1→1.0) at ×2 and above. Visible to naked eye in a 1-second window.
- [ ] No animation runs on a free timer disconnected from game state. Check: pause the game. Every animation should freeze or become static.
- [ ] All animations respect `prefers-reduced-motion: reduce` — decorative animations (ember particles, idle pulses) are disabled; state-communicating animations (paddle frame, mana fill) remain.
- [ ] Frame rate: no animation drops below 30fps on a mid-tier Android device (or throttled 4× CPU in Chrome DevTools).

---

### V6 · Screen Readability

**Verification checklist:**

- [ ] The playfield is readable at a glance. Take a screenshot at any point mid-level. Within 2 seconds you can identify: ball position, paddle position, high-HP bricks (visually distinct), objective.
- [ ] The HUD does not cover more than 20% of the playfield height. Player's view of the game is primary.
- [ ] Last-brick highlight (gold pulse on ≤3 remaining bricks) is visible without prior explanation.
- [ ] Win/loss banner is legible — large enough to read in peripheral vision. Text does not overflow the screen at any viewport width.
- [ ] No layout shift occurs during play. HUD dimensions are fixed. Spell cast, HP change, and combo events must not reflow any DOM outside the PixiJS canvas.

---

## Part II — Accessibility

Accessibility is not optional. It is part of "great." A game that is inaccessible to a non-trivial group of players is not great — it is incomplete.

### A1 · Keyboard Navigation

**Verification checklist:**

- [ ] Every screen can be navigated top-to-bottom using Tab/Shift-Tab
- [ ] Every action that can be tapped can also be triggered with Enter or Space
- [ ] Focus order follows reading order (top-left to bottom-right) — no focus jumps backwards or out of visual sequence
- [ ] No focus trap exists outside of modal dialogs (Tutorial overlay, confirmation dialogs)
- [ ] Modal dialogs MUST trap focus — Tab must cycle only within the modal while it is open

### A2 · Screen Reader Support

**Verification checklist:**

- [ ] Every `<button>` and `<a>` has a human-readable accessible name — either visible text or `aria-label`
- [ ] Icon-only buttons have `aria-label` (e.g., spell slots: `aria-label="Fireball — cost 30 mana"`)
- [ ] Modal dialogs have `role="dialog"` and `aria-labelledby` pointing to their title
- [ ] Tab controls have `role="tablist"` / `role="tab"` / `aria-selected` — not bare `<button>` siblings styled to look like tabs
- [ ] Locked content is communicated semantically — not only by grayscale filter. Use `aria-disabled="true"` or a visually-hidden text span.
- [ ] Emoji used as semantic content (e.g., 🔒 for locked) must have `aria-label` or `role="img"` with a title

### A3 · Color Contrast

**Verification checklist:**

- [ ] All text on dark backgrounds achieves WCAG AA contrast ratio (4.5:1 for normal text, 3:1 for large text ≥18px or bold ≥14px)
- [ ] Game state is never communicated by color alone — always paired with shape, text, or position
- [ ] The win banner (#44ff88 green) and loss banner (#ff3333 red) are legible for red-green colorblind users — they differ in text content ("VICTORY" / "DEFEAT"), which satisfies this

### A4 · Motion Safety

**Verification checklist:**

- [ ] Ember particles and any looping idle animations are gated on `@media (prefers-reduced-motion: no-preference)`
- [ ] Screen shake effects are gated on the same media query or on the FX toggle
- [ ] No content flashes more than 3 times per second (WCAG 2.3.1)

---

## Part III — Gameplay Feel

### G1 · Control Responsiveness

**Verification checklist:**

- [ ] Paddle input-to-movement latency is imperceptible at 60fps. Test: move paddle while recording at 120fps (or use Chrome DevTools frame timing). Input must register within 1 frame (16.7ms).
- [ ] Paddle does not jitter, overshoot, or lag behind pointer position
- [ ] Ball serve responds immediately to tap — no "warming up" period before the game is interactive

### G2 · Ball Physics Integrity

**Verification checklist:**

- [ ] Minimum bounce angle of 20° from horizontal is enforced — no flat unplayable shots. Verified by `backend/Arkanoid.Core/Physics/BallPhysics.cs` `EnforceMinAngle`.
- [ ] Ball speed escalates by +5% per 20 bricks destroyed, capped at 1.4× base speed. Speed escalation is perceptible — the final 10 bricks feel faster than the first 10.
- [ ] Ball does not clip through the paddle or walls at any speed tier — test at 1.4× speed.
- [ ] After losing a life, the player can immediately identify what happened. Watch a death in slow motion. If the cause is unclear, the physics has failed G-2.

### G3 · Feedback Loops

**Verification checklist:**

- [ ] Every brick hit produces a visible impact flash AND an audible crack
- [ ] Every spell cast produces a visible effect AND a distinct audio cue different from brick hits
- [ ] Combo multiplier badge (×2/×3/×4) is visible and updates within 1 frame of the qualifying hit
- [ ] Power-up collection produces a distinct visual/audio moment — player should feel a "yes!" not "did something happen?"
- [ ] Last-brick highlight (≤3 bricks) is clearly visible and creates a measurable tension moment

### G4 · Power-Up Balance

Each power-up must be worth collecting. Verify by playing without collecting any power-ups, then with. The experience with power-ups must feel meaningfully different.

**Verification checklist:**

- [ ] Wide paddle: noticeably wider — not just 10% wider. Player should misread their hit box once in the first 5 seconds.
- [ ] Fireshot: visually distinct projectile, destroys bricks in one hit, has audible distinct sound
- [ ] Multiball: second ball spawns at a visible angle, not overlapping the first. Catching both balls simultaneously is a genuine challenge.
- [ ] Mana surge: mana fills visibly and rapidly. Player should feel a spell window open.
- [ ] Shield: barrier is clearly visible at the bottom of the screen. Does not feel like nothing happened.

### G5 · Session Arc

**Verification checklist:**

- [ ] Standard level (non-boss): completable in 60–180 seconds by an average player
- [ ] Boss level: takes 3–5 minutes. Has at least 2 distinct phases or mechanical shifts.
- [ ] The last 20% of a level feels different from the first 20% — ball is faster, bricks are fewer, tension is higher
- [ ] A player who completes a level should feel they earned it, not that they randomly ground through it

---

## Part IV — Level Design

### L1 · Every Level Has an Identity

After completing a level, the player must be able to name it in one sentence.

**Gate:** Write a one-sentence description of every level before calling it done. If you can't distinguish it from the adjacent level in one sentence, the identity needs work.

**Examples of good identities:**
- "The teleporter introduction — two bricks swap sides when you least expect it"
- "The ghost labyrinth — half the bricks only exist when you're not looking at them"
- "The barricade — bomb chains are the only way through the rock vault"

**Examples of failed identities:**
- "A level with lots of tough bricks"
- "Another hell level"
- "The one before the boss"

### L2 · Visual Gestalt

**Verification checklist:**

- [ ] Take a screenshot of the level at its start state. The layout should look intentional — structured, with visual rhythm (rows, columns, diagonals, clusters). Not a random scatter.
- [ ] The level's key mechanic is visible in the top third of the grid — where the ball arrives first
- [ ] The layout has a clear focal point (the boss, the bomb cluster, the locked block) that draws the eye

### L3 · Mechanical Escalation Within Biome

**Verification checklist:**

- [ ] Level 1 of each biome introduces exactly one new mechanic beyond the base rules (or none, for level 1-1)
- [ ] Each subsequent level introduces at most one additional mechanic or meaningfully escalates an existing one
- [ ] The crescendo linter rule passes: `tools/gen-levels.mjs` exits 0 on all levels in `config/levels/`
- [ ] Boss levels are harder than every non-boss level in their biome — if any non-boss level is harder, redesign

### L4 · Teaching Moment

**Verification checklist:**

- [ ] Teleporters first appear in a sparse level (≤30% fill) with no other special bricks, so the player can observe their behavior
- [ ] Ghost bricks first appear in a small cluster with normal bricks nearby, so the player discovers phasing vs. impact behavior
- [ ] Necromant/wave-summoner mechanics are introduced before the boss that uses them heavily
- [ ] No boss mechanic appears for the first time IN the boss fight. Players must have seen a reduced version earlier.

### L5 · Linter Clean

**Gate:** `node tools/gen-levels.mjs` exits 0 on every level file. All 5 rules pass:
1. Marker rule (≤1 start marker)
2. Exclusivity rule (legend entries are disjoint)
3. Block set rule (all symbols defined in `config/blocks.json`)
4. Depth rule (grid dimensions match declared cols×rows)
5. Crescendo rule (escalation score increases across a biome)

---

## Part V — Audio

### AU1 · No Sound Makes It Worse

Play the game for 5 minutes with sound on. Then play for 5 minutes with sound off. The sound-on session should feel more alive, not more irritating.

**Verification checklist:**

- [ ] Music is opt-in (default OFF) — players who find it irritating are not punished. The toggle is in Settings.
- [ ] SFX are on by default. Test: play without SFX. The game should feel noticeably flatter.
- [ ] No audio artifact that is clearly unintentional (click, pop, distortion) ships in any SFX
- [ ] Peak audio level measured via `OfflineAudioContext` does not exceed 0.25 normalized. Current measurement: 0.21 ✓

### AU2 · Distinct Events = Distinct Sounds

A blindfolded player should be able to narrate what is happening.

**Verification checklist:**

- [ ] Brick hit vs. paddle hit vs. wall hit are audibly distinct
- [ ] Spell cast sounds differ per spell — fireball ≠ ice ≠ lightning
- [ ] Power-up collection sound differs from brick destruction sound
- [ ] Boss death has a distinct, unmistakable climactic cue
- [ ] Low HP warning (if any) is distinct from all other sounds

### AU3 · Music Does Not Compete

**Verification checklist:**

- [ ] During a spell cast, the SFX is clearly audible through the music
- [ ] During a boss fight, the increased brick density and speed does not bury the SFX under music
- [ ] Music transitions between biomes do not produce clicks or discontinuities
- [ ] Music volume is controlled by the Music toggle independently of the SFX Audio toggle

---

## Part VI — Technical Health

### T1 · Build Clean

**Gate:** Every task ends with these passing:

```
cd frontend && npx tsc --noEmit       # Exit 0
cd backend/Arkanoid.Server && dotnet build  # Exit 0
```

No TypeScript errors. No `@ts-ignore` added without an explanatory comment. No `any` cast without a comment explaining why the type cannot be inferred.

### T2 · Test Suite Green

**Gate:** `cd tests && npx playwright test --workers=1` exits 0. Currently 17 tests:

| Suite | Tests | What they guard |
|---|---|---|
| `paddle-anim.spec.ts` | 5 | Paddle frame = mana ratio, no timer-based cycling |
| `combo.spec.ts` | 3 | Combo multiplier builds and resets, floater renders |
| `hud-live.spec.ts` | 4 | HUD elements render, mana tracks setMana cheat, banners |
| `powerup.spec.ts` | 5 | All 4 power-up types spawn, collect, and activate |

No test may be skipped, `test.skip`'d, or `xfail`'d without a dated comment explaining when it will be re-enabled.

### T3 · No Console Errors in Production

**Gate:** Open the browser console during any scene transition and during a full level play. Zero `console.error`, zero unhandled promise rejections, zero 404s on asset URLs.

### T4 · No Stale Cheat Artifacts

**Gate:** After a play session with all cheats used, verify:

- Cheats do not persist across page reloads
- `freezeMana` state resets on new level load
- `setMana` cheat value is constrained to [0, manaMax] — no negative mana

### T5 · Performance

**Gate:** Chrome DevTools Performance tab, throttled to 4× CPU slowdown (simulating mid-tier Android):

- [ ] 60fps during standard gameplay (no dropped frames visible in the flame graph)
- [ ] No memory leak over a 5-minute session (heap size at t=5min ≤ 1.5× heap size at t=0)
- [ ] Level load (from level select tap to first frame rendered): ≤1.5 seconds on localhost

---

## Part VII — Commit & Delivery

### D1 · Every Task Ends with a Commit

No work sits uncommitted. A commit proves the task is done. A task with no commit is a task in progress, regardless of what the code looks like.

**Commit message format:**
```
type(scope): short description

Optional body. Required if the change is non-obvious.
```

Types: `feat`, `fix`, `refactor`, `test`, `content`, `docs`

### D2 · No Dead Code in the Commit

The commit must not include:
- Commented-out code blocks
- `console.log` statements added during debugging
- Unused imports
- Files created and then abandoned (`.bak`, `_old`, `copy_of_*`)

### D3 · The Feature Was Played, Not Just Written

Before closing any gameplay or UI task:

- [ ] Open the browser at `localhost:5175`
- [ ] Navigate to the affected screen or play the affected level
- [ ] Execute the golden path (the normal use case)
- [ ] Execute one edge case (empty state, max value, error condition)
- [ ] Watch for visual regressions in adjacent screens

"I ran the tests" is not a substitute for "I played the game." Tests verify code behavior. Playing verifies the player experience.

### D4 · Screenshots on UI Changes

Any commit that changes a UI scene must include a screenshot comparison:
- Before state: what it looked like before the change
- After state: what it looks like now
- Explicit answer to: *does this look better, or just different?*

If you cannot answer "better," the change is not ready.

---

## Quick Reference — The 7 Blockers

These are hard stops. Nothing ships if any of these are true.

| # | Blocker | Gate |
|---|---|---|
| B1 | TypeScript build fails | T1 |
| B2 | Any Playwright test fails | T2 |
| B3 | Console errors in browser | T3 |
| B4 | Paddle frame driven by anything other than mana ratio | V5 |
| B5 | Any interactive element smaller than 44×44px | V2 |
| B6 | HUD uses hardcoded colors not in theme.ts | V1 |
| B7 | A level fails the gen-levels.mjs linter | L5 |

---

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-06-10 | Initial version — synthesized from UI audit + gameplay plan | Claude Code |
