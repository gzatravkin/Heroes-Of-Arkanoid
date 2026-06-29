# Difficulty tuning — diagnosis + 3 proposals (2026-06-16)

Owner: "game is VERY easy." This finds every balance value, compares to reference games, and
proposes **three difficulty profiles** (Super Hardcore → Medium → Easy-but-not-broken).

## Why it's trivially easy right now (ranked by impact)

1. **Post-hit i-frames = 3.0 s** (`SimConfig.DamageImmunity`). After any hit you're invulnerable
   for 3 s. With 5 HP that's ~15 s of total grace — you can **face-tank** hazard levels. This is
   the single biggest culprit. (It was added to stop one hazard double-hitting; 3 s is way past that.)
2. **Mana regen = 14/s** (`ManaRegenPerSec`) → a full 100 bar every **~7 s**. Spells are spammable,
   not a resource.
3. **Ignite is free + huge.** `manaCost 0`, burns `1 dmg × 8 ticks = 8 dmg` per block over 2 s and
   **spreads 3 generations** (`FireConfig`). A free, board-clearing signature.
4. **5 HP** (`StartHp`, was 3) on top of the i-frames.
5. **Ball is slowish for the arena.** `BallSpeed 360` on a 256×448 board ≈ 1.3 s top-to-paddle;
   only ramps +5%/20 bricks, capped +40%. Lots of reaction time.

Paddle width (64 = 25% of the board) is actually *fine/slightly hard* — not a cause of easiness;
the **wide power-up (→112 = 44%)** is the generous part.

## Current values (the knobs)

| Knob | Where | Current |
|---|---|---|
| Paddle width (default) | `SimConfig.PaddleWidth` | **64** (2 cells) |
| Paddle width (wide pickup) | `Pickups.WidePaddleBonus +48`, 15 s | **112** |
| Paddle mods (cards) | `mod_wide ×1.2`, rift `×1.25` | stacks |
| Ball radius | `SimConfig.BallRadius` | **6** |
| Ball speed (base / cap) | `BallSpeed`, ramp +5%/20 bricks ≤+40% | **360 / 504** |
| Board size | level `cols`×`rows` @ `CellSize 32` | **8×14** (256×448 px) |
| Mana max | `ManaMax` | **100** |
| Mana regen | `ManaRegenPerSec` | **14/s** (full ≈7 s) |
| Mana per kill / perfect deflect | `ManaPerKill` / `…Bonus` | **4 / +8** |
| Ignite cost / burn / spread | `characters.json` / `FireConfig` | **0 mp / 8 dmg over 2 s / 3 gens** |
| Other spell costs | `characters.json` | fireball 25 · firewall 35 · turret 25 · phoenix 30 · ashfall 30 |
| Block HP (basic / tough) | `config/blocks.json` | hell 2/4 · cavern 2/3 · village 2/3 · heaven 3/5 |
| Block HP ramp over campaign | *(none today)* | **flat — a basic brick is 2 HP on level 1 and level 47** |
| Ball damage | `BallDamage` (+ hero Power/crit) | **1** (scales up via upgrades) |
| Start HP | `StartHp` | **5** |
| Post-hit i-frames | `DamageImmunity` | **3.0 s** |
| Start spare balls | `StartBalls` | **3** |

## Reference values (researched 2026-06-16 — sources at the bottom)

- **Arkanoid (Taito, 1986):** **silver/grey bricks take 2 hits, and the hit-count rises by +1 every
  8 stages** (2 hits stages 1–8, 3 hits 9–16, 4 hits 17–24, …); **gold bricks are indestructible**
  (don't count toward clearing). The ball **steadily speeds up** over time and after enough
  bounces it both speeds up and shifts angle. **No i-frames** — a hit/drop simply costs a life.
  *This is the strongest precedent for the owner's "increase block HP, or ramp it up soon" ask:
  classic Arkanoid ramps brick HP on a fixed stage cadence.*
- **Shatter (Sidhe, 2009):** brick variety/HP escalates per world; the signature power (gravity
  Suck/Blow) is a **metered resource**, not free; each world ends in a boss with a weak point.
  Difficulty comes from brick behavior + ball control, not from gating player damage.
- **i-frames norm:** action games use **~0.5 s**, e.g. **Terraria = 0.67 s** after a hit. Our **3.0 s
  is ~4–5× the standard** — that's why face-tanking works. Even the "Easy" profile below (1.5 s) is
  generous by genre norms.
- **Mana/energy precedent (Wizorb-style & roguelites):** magic is a **scarce, clutch** resource —
  meaningful per-cast cost, regen slow / pickup-driven, not a bar that refills every few seconds.

Takeaways applied below: **short i-frames (~0.5–1.5 s)**, **mana as a real resource** (slower regen +
ignite costs something), **a faster ball that ramps**, paddle ~15–25% of board, and a **brick-HP ramp
that kicks in early** (Arkanoid cadence ≈ +1 every 8 levels; we go steeper for the harder profiles).

## The three profiles

> Knobs map to: `SimConfig` (paddle/ball/HP/iframes/mana), `FireConfig` (ignite burn/spread),
> `characters.json` (spell mp costs). Board **width** changes need the 48 levels re-generated
> (I have the generator — cheap to redo at a new width); board **height** is free (add rows).

| Knob | Current | **S — Super Hardcore** | **M — Medium Hardcore** ⭐ | **E — Easy, not broken** |
|---|---|---|---|---|
| Paddle default | 64 | **52** | **60** | **64** |
| Paddle wide (pickup) | 112 | **76** (+24) | **92** (+32) | **108** (+44) |
| Ball radius | 6 | **5** | **6** | **6** |
| Ball speed base / cap | 360 / 504 | **440 / 660** | **400 / 580** | **375 / 525** |
| Board (W×H) | 8×14 | **10×16** ¹ | **9×15** ¹ | **8×14** |
| Mana max | 100 | **120** | **110** | **100** |
| Mana regen | 14/s | **5/s** (~24 s) | **8/s** (~14 s) | **11/s** (~9 s) |
| Mana per kill | 4 | **3** | **4** | **4** |
| Ignite cost | 0 | **12** | **8** | **5** |
| Ignite burn (dmg/dur) | 8 / 2 s | **3 / 1 s** | **5 / 1.5 s** | **6 / 1.5 s** |
| Ignite spread gens | 3 | **1** | **2** | **3** |
| Spell costs ² | base | **+60%** | **+30%** | **base** |
| Block HP base (basic/tough) ³ | 2/4… | **+1 / +1** | **+0 / +1** | **base** |
| Block HP ramp (campaign) ⁴ | none | **+1 every 3 lvls, cap +4** | **+1 every 5 lvls, cap +3** | **+1 every 8 lvls, cap +2** |
| Start HP | 5 | **3** | **4** | **5** |
| Post-hit i-frames | 3.0 s | **0.6 s** | **1.2 s** | **1.5 s** |
| Start spare balls | 3 | **2** | **3** | **3** |

¹ Board-width change ⇒ I regenerate all 48 levels at the new width (≈ the Phase-C pass again). If you'd
rather not re-author, keep **8×14** for that profile and the rest of the knobs still apply.
² Spell costs = fireball/firewall/turret/phoenix/ashfall and the other heroes' spells, scaled.
³ Block-HP **base** bump = edit the per-type HP in `config/blocks.json` (e.g. S: hell_basic 2→3,
hell_tough 4→5; all biomes' basic +1, tough +1). Free — no code, no re-author.
⁴ Block-HP **ramp** = brick HP rises as you go deeper, Arkanoid-style. Needs a small one-time code
add: `GameInitializer` derives the campaign index from the level id and adds
`bonus = min(cap, floor(index / period))` to every **destructible** block's HP at load (indestructible
walls/teleporters/bosses excluded). "Increase them very soon" → the harder profiles ramp every 3–5
levels so bricks are already +1/+2 by the first few nodes. Player damage also grows (hero Power/crit),
so the ramp mostly bites in the un-upgraded early game where the easiness is worst.

> **Why a ramp, not just big flat HP:** flat-high HP makes *every* level a slog (many hits to clear);
> a ramp keeps level 1 snappy while late levels demand real board control + upgrades — matching
> Arkanoid's "+1 every 8 stages" cadence, steeper for the hardcore profiles.

### What each profile *feels* like
- **S — Super Hardcore:** arcade-brutal. Bigger arena, small fast ball, tiny paddle, 3 HP, i-frames
  only long enough to not double-hit on one hazard. Mana is precious (~24 s to fill); ignite is a
  small, costed nudge, not a board-clear. Spells are clutch. Expect to lose.
- **M — Medium Hardcore (recommended):** tense but fair. Mistakes hurt but aren't instant death;
  mana is a real budget; ignite still useful but costed and 2-gen; ball quick and ramps. This is the
  "skill matters, not broken" target.
- **E — Easy, not broken:** keeps today's friendly paddle/HP/board, but fixes the three brokenness
  bugs: i-frames 3.0→1.5 s, regen 14→11/s, ignite no longer free (5 mp) — so you can't face-tank or
  free-clear, while staying accessible.

## Biggest levers (if you want to mix your own)
i-frames (3.0 s → ≤1.5 s), mana regen (14/s → ≤11/s), and ignite cost (0 → ≥5) fix ~80% of the
"broken easy" by themselves, with **no level re-authoring**. **Block-HP base bumps** (`blocks.json`)
are also free and add immediate bite. Ball speed + paddle + the block-HP **ramp** widen the gap
further (ramp = a few lines of code). **Board-width is the only expensive knob** (re-author 48 levels).

## Implementation cost summary
- **Free / config-only:** i-frames, HP, mana max/regen/per-kill, ball radius/speed, paddle widths,
  spell mp costs (`characters.json`), ignite burn (`FireConfig`), **block-HP base** (`blocks.json`).
- **Small code add:** block-HP **campaign ramp** (~10 lines in `GameInitializer`).
- **Expensive:** board **width** change ⇒ regenerate all 48 levels (I have the generator).
- All gated by `AllLevelsWinnableTests` — I'll confirm every level still clears after tuning.

## Sources
- Arkanoid brick HP (silver = 2 hits, +1 every 8 stages; gold indestructible) + ball acceleration:
  [StrategyWiki – Arkanoid/Gameplay](https://strategywiki.org/wiki/Arkanoid/Gameplay),
  [TV Tropes – YMMV/Arkanoid](https://tvtropes.org/pmwiki/pmwiki.php/YMMV/Arkanoid),
  [Arcade-Museum – Arkanoid](https://www.arcade-museum.com/Videogame/arkanoid)
- Shatter (metered Suck/Blow, escalating brick types, bosses):
  [Wikipedia – Shatter](https://en.wikipedia.org/wiki/Shatter_(video_game)),
  [TV Tropes – Shatter](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/Shatter)
- i-frame durations (~0.5 s norm; Terraria 0.67 s):
  [Terraria Wiki – Invincibility frame](https://terraria.wiki.gg/wiki/Invincibility_frame),
  [G2A – I-frames explained](https://www.g2a.com/news/glossary/what-are-invincibility-frames-in-gaming-i-frames-explained/)
- Brick-breaker difficulty progression (multi-hit bricks, denser grids + faster ball per level,
  paddle scaling): [AWS Builder – Brick Breaker design](https://builder.aws.com/content/2ydqqJWLi1YZDq98pa4lOQzAq1f/brick-breaker-game-built-with-amazon-q-cli)
