# Effects Style — Make Them Match the Game

Owner 2026-06-16: make the effects "more in style with the rest of the game."

## The mismatch (root cause)

The game's *good* visuals are **hand-painted sprite animations** — blocks, the Demon, fire wall, phoenix,
explosions, lightning. The effects that look off are the ones drawn as **procedural `Graphics`** (flat
rectangles, hard lines, plain circles, gradient funnels). They read as "engine debug shapes" on a painted
stage. (Even the spark/funnel telegraphs I just added are still procedural.)

**Still-procedural effects (the offenders):**
| Effect | How it's drawn now |
|---|---|
| Lane telegraph (charging emitter) | Graphics: haze rect + spark dashes + circle target |
| Boss fist telegraph/slam, Paladin judgement | Graphics: gradient funnel + ellipse pool |
| Boss aura | Graphics: plain additive circle |
| Lich's Gaze beam, Twin Soul tether | Graphics: `lineStyle` lines |
| Lance-of-Dawn pillars | Graphics: rects |
| **Necromancer minions (Bonewalker / Bone Golem)** | Graphics: **bone-coloured rectangles** (worst offender) |

**Already painted (good — leave alone):** block-destroy shatter, hit sparks, death-mark spheres
(`DeathSphere`), ignite/phoenix flashes — these use real art.

**Painted art available to build from:** soft glows (`firemage/spell_phonex/PhoenixGlow`,
`engineer/spell_lighting/LightingGlow`, `firemage/spell_fireturret/FireHeroTurretGlow`,
`engineer/spell_rocket/RocketGlow`), fire (`FireBirth`, `FireStandAnnimation1/2`, `FireRing`), impacts
(`effects/Explosion`), lightning frames, `hell/DemonHand1-3`, `village/enemies/DeathSphere`.

## Proposals (pick a direction)

### 1. "Painted glow" — reuse the game's own VFX sprites everywhere *(recommended)*
Swap every procedural shape for the matching painted sprite the game already ships:
- **Telegraph target** → a soft pulsing **glow sprite** (`PhoenixGlow`/`LightingGlow`) at the pit, not a
  Graphics circle.
- **Lane "incoming"** → a small **fire/ember sprite** falling the lane (`FireBirth` frames) instead of dash
  rects.
- **Boss fist** → the **`DemonHand` sprite slams down** the column + an **`Explosion`/`FireBirth`** impact
  at the floor (uses the boss's own art — the most "in style" possible).
- **Boss aura** → a soft **glow sprite** behind the boss instead of a flat circle.
- **Beams (lich/tether)** → a soft glow-sprite line / lightning frames instead of `lineStyle`.
- **Minions** → bone/skeleton **sprite art** instead of rectangles.
One coherent result: the effects layer looks hand-painted like the spells. Most bang-for-buck.

### 2. "Elemental telegraphs" — #1 plus a per-biome danger motif
Everything in #1, and the *telegraph* re-skins per biome so threats read the same but match the world:
Hell = falling embers + scorch ring · Caverns = falling grit + rock crack · Witchland = ghost wisps + rune
circle · Heaven = light motes + halo ring. Most cohesive "world" feel; more art wiring per biome.

### 3. "Minimal & diegetic" — pare effects down
Keep effects tiny, soft, and consistent: one shared soft-glow target + a faint tint for every telegraph,
understated, letting the painted blocks/ball/spells be the show. Safest readability, least flashy — but
doesn't fully solve "looks painted," it just makes the shapes quiet.

## Recommendation
**#1 now** (reuses existing art, fixes the whole effects layer, including the minion rectangles), and adopt
**#2's per-biome telegraph** for the danger columns specifically if you want the extra polish. Verified the
usual way: before/after screenshots per effect, judged against the painted art around it.

## Status — #1 "Painted glow", ALL procedural effects (owner-approved), 2026-06-16

New helper `EffectSprites.SpritePool` (pooled world sprites). Conversions:
- **Lane telegraph target** → soft `PhoenixGlow` sprite (additive, pulsing) instead of a Graphics circle.
  Verified live (`docs/effects-review/lane-painted.png`).
- **Minions** → real `necromancer/spell_skeleton/Skeleton` + `necromancer/spell_lastday/BoneGolem` sprites
  instead of bone-coloured rectangles. Verified by render-graph inspection (sprite visible, valid 125×122
  texture, sized + positioned at the entity). Hard to screenshot because the Bonewalker rides the *highest*
  rooftop (often at/above the top frame).
- **Lance-of-Dawn pillars** → soft additive `effects/RangeArea` glow sprites (glow + core) instead of rects.
- **Boss aura** → soft layered radial glow (3 falloff rings) instead of a hard disc.
- **Lich's Gaze beam / Twin Soul tether** → additive multi-layer glow beams (wide soft + mid + bright core)
  instead of flat `lineStyle` lines.
- (Pillars/beams/tether are class/relic-specific → build-verified, not live-triggered.)
- **Boss fist** stays the soft gradient *funnel* from the telegraph pass (a sprite-based fire-pillar/DemonHand
  slam is a possible future upgrade; the funnel already reads far better than the old flat bar).

`vite build` clean.

## Status — both follow-ups done (owner "do it all"), 2026-06-16

- **#2 Per-biome telegraph:** the lane telegraph re-skins to the world via `TELEGRAPH_THEMES` in `Renderer`
  (haze/dash/glow + glow sprite per biome): Hell embers (orange + `PhoenixGlow`), Caverns grit (dusty gold +
  `RangeArea`), Witchland ghost-wisps (purple + `RangeArea`), Heaven motes (gold-white). Verified live:
  Hell orange (`lane-painted.png`) and Witchland purple (`tele-village-purple.png`).
- **Boss fist → sprite fire-pillar:** `Effects.spawnFirePillar` (wired on `fistSlam`) now drops a **pillar of
  hellfire** (the fire-wall stand animation, stretched down the column) + a **DemonHand claw** that crashes
  down (ease-in slam via the new `_fxSprites` tweened-sprite system) + a floor `Explosion` impact — instead
  of the flat red bar. Verified live by triggering `fistSlam` through the bridge (`fist-painted.png`): a
  dramatic hellfire column. (The pillar is the star; the claw reads as a secondary slam under the fire.)

`vite build` clean. Nothing committed.
