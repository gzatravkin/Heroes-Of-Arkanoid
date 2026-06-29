# Battle Readability — "Follow the Ball" Proposal

Owner feedback 2026-06-16: *the battle is overwhelmed with effects and it's really hard to follow the ball.*
Screenshots: `docs/effects-review/` (hell-2 burning field, hell-5 max-spell, hell-5 early play).

## What the screenshots show (the actual problems)

1. **The ball is never the most salient thing on screen.** It's small, and its colour/glow competes with
   everything around it — on Hell it's an orange-ish orb amid orange fire and orange embers (orange-on-orange).
   (In the test profile it even fell back to a white square — a separate fresh-profile bug — but the point
   stands for the real FireHeroBall too: low, inconsistent contrast.)
2. **Constant ambient ember particles** (orange dots drifting across the whole field) are the #1 thing
   competing with the ball for the eye.
3. **Burning blocks dissolve into one undifferentiated fire blob** — you can't see the blocks underneath,
   so the *playfield itself* becomes noise.
4. **Everything is additive-blended at once** (ball aura + fire wall + phoenix + burn overlays + hit sparks
   + glows) with no hierarchy → a washed, busy frame where nothing reads.
5. **Dark, low-contrast background with big moving shapes** (the demon-hand ambient) adds murk behind it all.

The root cause is the absence of a **visual hierarchy**. Nothing is protected as "most important," so the
ball — the one thing the player must track every frame — drowns.

## The principle (the drastic shift)

**Establish and enforce a strict salience order, every frame:**

> **BALL  ›  blocks  ›  spell/impact FX  ›  ambient atmosphere**

The ball is *always* the brightest, highest-contrast, most stable object. Atmosphere is always the quietest.
Effects are allowed to be loud only briefly and only when they don't occlude the ball or the blocks.

## Concrete changes (the package)

### A. Make the ball unmistakable & STABLE (highest impact)
- **Hue-lock the ball.** The core is always the same bright, non-orange colour (white-hot / pale cyan) with
  a **thin dark outline ring on every biome** (today the dark contrast disc is skipped on Hell/Caverns —
  exactly the biomes where it's needed). State (ignite / ghost / decay) is shown by a **thin coloured ring
  or short trail tint only — never by recolouring or enlarging the whole ball.**
- **Kill the giant fire aura on the ball.** The looping phoenix-fire aura (~2.8× the ball) merges the ball
  into the field fire. Replace with a tight, short, ball-coloured trail (the trail = the ball's path, in the
  ball's own bright hue, *not* orange).
- Net effect: a crisp bright dot with a dark edge that you can always find, instantly.

### B. Cull ambient particles (cheap, huge win)
- Cut the constant drifting embers drastically (or gate them to the screen edges at low alpha). Atmosphere
  should live in the **background layer**, never in the **play layer** where the ball is.

### C. Burning blocks must still read as blocks
- Replace the full-cell additive fire with a **subtle ember tint on the block sprite + one small flame at its
  top edge**, so the block stays visible. **Cap concurrent flame sprites** (e.g. only the most-recently-lit
  N show animated flame; the rest just tint) so a fully-ignited field never becomes a solid fire blob.

### D. Dim the stage during play
- Drop a subtle dark/desaturate scrim over the **background layer** (the demon-hand etc.) during Playing, so
  blocks + ball pop off a calmer backdrop. Background art stays, just quieter.

### E. Effects budget (hierarchy enforcement)
- Cap concurrent additive-blend effects; lower the alpha/scale on glows, hit-sparks (smaller, fewer,
  shorter), and spell auras so a flurry of casts can't white-out the field. Spell FX get **one** loud beat,
  then fade fast.

## How drastic — pick the scope

- **Minimal:** A only (ball identity) — biggest single readability win, low risk.
- **Recommended package:** A + B + C — fix the ball *and* the two worst field-noise sources.
- **Full overhaul:** A–E — strict hierarchy everywhere, including background dimmer + global effects budget.

All of it is verified the same way: side-by-side before/after screenshots on a burning Hell field, judged on
"can you find the ball in <0.5s."

## Status — IMPLEMENTED (full overhaul A–E), 2026-06-16

Owner chose the **full overhaul**. All shipped + verified via the Playwright MCP browser (before/after in
`docs/effects-review/`); `vite build` clean.

- **A** `BallLayer` rewritten: dark crisp outline ring on **every** biome + a bright **hue-locked** white
  core (the ball never recolours with state) + a thin state ring; the giant 2.8× looping fire aura is gone.
- **B** `BackgroundLayer`: ambient particle counts slashed (Hell embers 18→6, etc.) and `ambientContainer`
  alpha 0.22→0.12.
- **C** `BlockLayer`: burning shows the block sprite + a small **top-edge** flame, capped at
  `BURN_FLAME_BUDGET=8` concurrent flames (rest read via burn tint) — no more fire blob.
- **D** `BackgroundLayer.setPlayDim` (called from `Renderer`): the backdrop dims to a cool dark tint during
  `Playing` so blocks + ball pop.
- **E** `AnimSystem` one-shot cap **48→16**; `Effects` additive-particle budget (14, recycle oldest) +
  smaller block-destroy / perfect-deflect sparks.

Before/after on the same burning Hell field: the field went from a solid orange fire blob with embers
everywhere to legible bricks on a calm backdrop with the ball a clear bright dot.

RESOLVED (owner agreed): the ball now renders at **1.4× its physics radius** (`BALL_VISUAL_MULT` in
`BallLayer`) — a clear bright dot that's easy to track, still smaller than a block. Verified on a burning
Hell field (`docs/effects-review/after-hell2-ballbig.png`): the ball is the obvious focal point.

## §F — Telegraph "lines" replaced (owner: "super big red lines"), 2026-06-16

The flat full-height `drawRect` bars were the eyesore. Replaced with art-fitting, soft, fiery telegraphs:

- **Lane telegraph** (`Renderer._laneGfx`, under each charging emitter/beholder): was a solid `0xff5a1e`
  full-height column + a hard bright centre line (a "laser pole" — `docs/effects-review/lane-before.png`).
  Now: a faint warm haze + a **descending stream of warm spark dashes** (animated, reads as "incoming")
  + a **pulsing scorch target at the pit** (the unmistakable dodge spot). Calmer but still dodge-readable
  (`docs/effects-review/lane-after2.png`).
- **Boss fist column** (`Effects.spawnColumnFlash`, Demon fist telegraph/slam + Paladin judgement): was a
  flat full-height bar. Now a **soft fiery funnel** that widens + brightens toward a **glowing impact pool
  at the floor** (where the fist lands), additive-blended. (Applied; matches the lane treatment — a live
  boss-fist frame wasn't captured because the attack has a long cooldown.)

STILL FLAT (not touched — separate, larger art pass if wanted): the §2/§3 class/enemy line effects drawn as
`drawRect`/`lineStyle` placeholders — Lich's Gaze beam, Twin Soul tether, Lance-of-Dawn pillars, and the
Necromancer **minions drawn as bone-coloured rectangles**. These are class-specific (not seen as Fire Mage
in Hell/Witchland) so they're lower priority.
