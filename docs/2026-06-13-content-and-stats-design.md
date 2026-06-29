# Content & Stats Design — Cards, Modules, Spells, and the Stat Engine (2026-06-13)

> **⚠️ ACQUISITION/ECONOMY SUPERSEDED (2026-06-14).** The *content behaviors* here (each card/module/spell/perk) remain the spec, but how they are **acquired, leveled, and gated** is replaced by `docs/2026-06-14-economy-rework-proposal.md`: Skill Points → **Insight** (mastery) + **rolls**; cards/modules level by **duplicate rolls** (not Card Dust / Module Cores); spells are a **global pool** rolled with **Souls** (not per-hero Skill-Point upgrades); §5.6/§5.10 (Points-funded spells+mastery) and the per-hero kit assumptions no longer hold. Read both together — this doc for *what each item does*, the rework for *how you get it*.

The build's four power layers, kept deliberately distinct:
1. **Heroes / Bars** — *who you are*: your base **stats** + identity (the new system, §4).
2. **Spells** — *what you cast*: active abilities, per hero, upgraded with Skill Points (§3).
3. **Cards** — *how you bend the rules*: cross-hero rule-breaking passives, leveled by **duplicates** (§1).
4. **Modules** — *your gear's mechanics*: slot-bound single passives, leveled by Module Cores (§2).

Every Card/Module is **passive/auto-triggering**, **distinct** from all 23 spells + 18 relics + 6 ball-cores
+ 3 paddle-mods, and **balanced** by an explicit lever (a %, a hard trigger, or a tradeoff). No filler stats.

---

## 1. Cards (20) — rule-breaking passives, duplicate-leveled

| # | Card | Rarity | Trigger | Balance lever | Impl |
|---|---|---|---|---|---|
| 1 | **Headhunter** | common | block destroyed in **top row** | position-gated +dmg (top only) | reuse |
| 2 | **Underdog** | common | block destroyed in **bottom 2 rows** | position-gated +dmg | reuse |
| 3 | **Opening Gambit** | common | **first** kill each level | once/level cap → AoE | new |
| 4 | **Dead Center** | common | **perfect deflect** | first-block-only burst (skill) | reuse |
| 5 | **Bank Shot** | rare | block hit after **wall bounces** | banks dmg, resets on any hit | new |
| 6 | **Avalanche** | rare | kill at **combo ≥ 8** | % chance; dead block falls (gravity dmg) | new |
| 7 | **Martyr's Brand** | rare | **HP loss** | must be hit; short buff | new |
| 8 | **Channeling** | rare | mana regen state | regen pauses in flight, doubles while caught (tradeoff) | new |
| 9 | **Sleight of Hand** | rare | **center-catch a pickup** | precision-gated → duplicate pickup | new |
| 10 | **Executioner's Edge** | rare | **crit** vs block ≤25% HP | double-gated (crit + low HP) | new |
| 11 | **Keystone** | rare | kill a block **with a stack above it** | % to crack the load-bearing base | new |
| 12 | **Metronome** | epic | consecutive **perfect deflects** | stacks; one miss = full reset | new |
| 13 | **Hot Hand** | epic | **combo** milestones | ball grows; drop/serve resets size | new |
| 14 | **Domino** | epic | **3+ blocks die within 1s** | tight window → next death explodes | new |
| 15 | **Redline** | epic | time since last **paddle touch** | ball faster+stronger but harder to catch (tradeoff) | new |
| 16 | **Ricochet** | rare | **side-wall bounce** | **% chance** → single horizontal bolt across the row | new |
| 17 | **Erosion** | **mythic** | hit on an **indestructible block** | slow: ~16 hits to crack one (commitment) | new |
| 18 | **Cleanup Crew** | rare | **≤6 blocks remain** | only at the tail (no main-fight help) | new |
| 19 | **Overkill** | epic | hit dealing **>2× a block's HP** | only the *excess* carries to the block behind | new |
| 20 | **Phase Window** | epic | **combo ≥ 15** | brief full-pierce window; any miss resets combo | new |

---

## 2. Modules (12) — one strong slot-bound passive each, no sub-stats

| Slot | Module | Main passive | Balance lever |
|---|---|---|---|
| **Core** | **Tidal Core** | alternates heavy/swift each deflect | half-time in each mode |
| **Core** | **Twin Soul Core** | two tethered balls; the tether slices between them | each twin weaker; lose one → tether dies |
| **Core** | **Fission Core** | splits every Nth block; **re-fuses on a catch** into a bigger ball | scattered/fragile while split |
| **Paddle** | **Gyro Paddle** | deflect angle driven by paddle **velocity** | still = predictable, jerky = wild (double-edged) |
| **Paddle** | **Drumhead Paddle** | perfect-center deflect → **shockwave up the column** | perfect-deflect only; one column |
| **Paddle** | **Riposte Paddle** | **parry** an enemy attack back as damage | timing window; enemy attacks only |
| **Ball** | **Spin-Loaded** | edge hits impart a **curving hook** | spin decays; erratic edge play |
| **Ball** | **Brittle Glass Ball** | huge per-hit dmg; **shatters** on un-breakable hits | durability economy (lose ball unless caught) |
| **Ball** | **Hollow Ball** | big/light: **wide coverage**, erratic, low per-hit dmg | coverage vs precision/damage |
| **Field** | **Gravity Well** | arena pulls the ball to **dense clusters** | edges hard to clear; drain risk |
| **Field** | **Toll Roads** | gold only from **crit / perfect-deflect** kills | skill-gated economy (no flat payout) |
| **Field** | **Pressure Cooker** | the **block field descends**; each kill pushes it back | HP-pressure if you clear too slow |

---

## 3. Spells — creative reworks + new

**Reworks** (the bland projectile/turret/zone clones, rethought):
- **Conflagration** (Fire Mage) — detonates the board's existing **ignite stacks** (no projectile).
- **Lance of Dawn** (Paladin) — drops a temporary **solid pillar** to bank shots off.
- **Concussion Charge** (Engineer) — blast = **knockback** (rescue balls, yank pickups), not damage.
- **Bonewalker** (Necro) — minion that **walks the rooftops**, meleeing blocks it stands on.
- **Lich's Gaze** (Necro) — slow **lighthouse beam** that *curses* lit blocks (+dmg from your ball).
- **Containment Field** (Engineer) — zone that **suppresses emitter enemies** + melts blocks (HP-axis).
- **Rot & Collapse** (Necro) — imbue that **lowers max HP permanently**; rotted blocks **fall** (gravity chain).
- **Bone Golem** (Necro, fix) — climbing **bodyguard** that bulldozes a column and **tanks enemy fire**.

**New:**
- **Ashfall** (Fire Mage) — ignite-kills rain **vertical embers** down a column.
- **Reckoning** (Paladin) — a meter charged by **HP you lose**, auto-smites the board.
- **Tesla Grid** (Engineer) — charge **both side walls** via wall-bounces → horizontal lightning curtain.

---

## 4. The Stat Engine — "Heroes are your stats" (PROPOSAL)

### The core stats (the dials everything tunes)
**Power** (ball damage) · **Vitality** (max HP) · **Crit Chance** (%) · **Crit Damage** (×) ·
**Multiball** (extra starting balls — rare, capped) · **Tempo** (paddle/ball control & mana feel).

### The model: stats live on the **Hero / Bar**, on two axes
This is a *roguelite RPG* called *Heroes of Arkanoid* — the bar **is** the hero. So the base stats live on
the hero, and each hero is a **different stat platform** (the "bars with different parameters" idea):

| Hero | Identity | High in | Low in |
|---|---|---|---|
| **Fire Mage** | glass-cannon burst | Crit Chance, Power | Vitality |
| **Paladin** | bruiser big-hits | Vitality, Crit Damage | Crit Chance, Multiball |
| **Engineer** | swarm | Multiball, Tempo | per-hit Power |
| **Necromancer** | attrition / sustain | Crit Damage, sustain | raw Power |

Two progression axes per hero:
1. **Hero Level (play-driven, free):** every battle with a hero earns hero-XP → levels → **steady automatic
   stat growth** weighted to that hero's profile. You always progress just by playing — no wall.
2. **Hero Stars / Ascension (collection-driven):** collect **duplicate Hero Tokens** (from league, season
   track, events, a hero shop) → ascend a **★** → a **big stat jump** + unlock/upgrade a **Hero Perk** (a
   passive identity boost, e.g. Mage ★3 "+8% crit chance"; Paladin ★3 "save the first ball-drain each level";
   Engineer ★3 "+1 starting ball"; Necro ★3 "heal 1 HP per 50 kills"). This is the "find duplicates → more
   stats" loop you wanted — consistent with how Cards level.

### Why this (vs the alternatives)
- **On-theme & cohesive** — heroes are already the spine; this makes choosing a bar a real build decision
  (pick the stat platform that fits your card/module/spell plan).
- **Crit is first-class** — chance + damage are hero stats, amplified by Lucky-Star-style cards and the
  Executioner's Edge / Toll Roads synergies.
- **Duplicate-collection loop** you like, applied to heroes — same satisfaction as card dupes.
- **No new screen** — it lives on the existing Heroes/Character screen, enriched with the stat profile, a
  level bar, ★ ascension, and perks.
- **Smooth curve** — play → levels (steady); dupes → stars (spikes). Free players still climb.

### Alternatives considered
- **Pure numeric "Workshop/Lab" tree** (spend a currency on flat +Damage/+HP/+Crit nodes): clean and very
  legible, but "just numbers," not thematic, and risks a grind wall. → **Optional thin add-on**, not the core.
- **Paddle-as-gear** (the bar is an item with rarity + rolled stats): overlaps Modules (paddle slot) +
  the item concept. → **Rejected** (too much overlap).
- **Combination (recommended add-on):** Heroes as the engine **+** a small **Masteries** tab on the
  existing Skill-Points economy for a few **account-wide** stats (e.g. global +Crit Damage, +Multiball cap),
  so points have a second sink and there's a tiny cross-hero layer. Lean — one tab, ~5 nodes.

### Economy hookup
- **Hero-XP** from playing that hero (free axis).
- **Hero Tokens** (per-hero shards) from league placement, the season track, events, and a hero shop —
  the social systems already pay out; they'd grant Hero Tokens. (Reuse/extend the existing `Shards`.)
- No new "stat currency" beyond Hero Tokens; the Masteries add-on spends existing **Points**.

### Recommendation
Ship **Heroes-as-stat-engine (Level + ★ Ascension)** as the core, with crit baked in, **plus** the lean
**Masteries** tab if you want an account-wide layer. It delivers damage/HP/crit/crit-dmg/ball-count growth,
the duplicate loop, distinct bars, and the crit system — without adding a sprawling new screen.

---

## 5. Stat Engine — detailed spec (chosen: Heroes engine + Masteries; for review/tuning)

> All numbers are **starting values to tune in playtest**, not final. The aim is a coherent curve, not a
> balanced one yet.

### 5.1 Core stats — units, base, cap
| Stat | Unit | What it does | Soft cap |
|---|---|---|---|
| **Power** | flat | damage per ball hit (before crit) | ~40 |
| **Vitality** | flat | max HP (lives) | ~15 |
| **Crit Chance** | % | chance a hit crits | 75% |
| **Crit Damage** | × | crit multiplier | ×4.0 |
| **Multiball** | flat | extra balls served | +3 |
| **Tempo** | × | paddle speed + mana-regen mult | ×1.6 |

### 5.2 Per-hero base profile (Lvl 1, ★0)
| Stat | Fire Mage | Paladin | Engineer | Necromancer |
|---|---|---|---|---|
| Power | 3 | 3 | 2 | 2 |
| Vitality | 3 | 6 | 4 | 5 |
| Crit Chance | 12% | 4% | 6% | 8% |
| Crit Damage | ×1.7 | ×2.2 | ×1.5 | ×2.0 |
| Multiball | 0 | 0 | **+1** | 0 |
| Tempo | ×1.1 | ×0.9 | ×1.2 | ×1.0 |

### 5.3 Hero Level (play-driven, free) — Lvl 1→30
- **XP:** ~ (blocks destroyed) + win bonus, per battle with that hero. Curve: `xpToNext = 80 × 1.12^(lvl-1)`.
- **Per-level growth** (weighted to the hero's *highs*; low stats grow slower):
  - Power +0.25/lvl (high-power heroes) or +0.15 (low) → ~+5–7 over 30 levels
  - Vitality +0.15/lvl → ~+4
  - Crit Chance +0.3%/lvl (crit heroes) or +0.1% → up to ~+9%
  - Crit Damage +0.01/lvl → +0.3
  - Multiball/Tempo: **not** from levels (from ★ perks / innate)

### 5.4 ★ Ascension (collection-driven) — ★0→★6
Collect **Hero Tokens** (per hero). Each star = a **+8% multiplier to that hero's whole stat block**
(compounding → ★6 ≈ ×1.47) **and** a Hero Perk at ★1 / ★3 / ★5.

| Star | Token cost | Cumulative stat mult |
|---|---|---|
| ★1 | 10 | ×1.08 |
| ★2 | 20 | ×1.17 |
| ★3 | 40 | ×1.26 |
| ★4 | 70 | ×1.36 |
| ★5 | 110 | ×1.47 |
| ★6 | 160 | ×1.59 |

### 5.5 Hero Perks (passive identity boosts — NOT spells/cards)
| Hero | ★1 | ★3 | ★5 |
|---|---|---|---|
| Fire Mage | +5% Crit Chance | ignited blocks take +15% from **crits** | a **crit kill** ignites a nearby block |
| Paladin | +0.2 Crit Damage | the **first ball-drain each level is saved** | below 50% HP, **+25% Crit Damage** |
| Engineer | +1 Tempo step | **+1 starting ball** | extra balls deal **full** damage (not reduced) |
| Necromancer | heal 1 HP / 60 kills | **crits drain mana** to you | full-combo kill may **raise a helper-ball** |

### 5.6 Masteries (account-wide, spend **Skill Points**; Points also still upgrade spells)
| Node | Effect / level | Max lvl |
|---|---|---|
| **Sharpshooter** | +1% Crit Chance | 5 (+5%) |
| **Brutality** | +0.05 Crit Damage | 5 (+0.25) |
| **Conditioning** | +1 max HP | 3 (+3) |
| **Juggler** | +1 Multiball cap & a small +start-ball chance | 2 |
| **Momentum** | +2% Tempo | 5 (+10%) |

### 5.7 Crit system
- Per hit: `roll < CritChance → damage = round(Power × CritDamage)` (else normal). Cap CritChance 75%.
- **Sources** stack: hero base + level + ★ + perks + Masteries(Sharpshooter/Brutality) + Lucky-Star card.
- **Juice:** crits show a bigger number + a sharper hit-flash/shake (feel, per the visual bar).
- **Synergies already designed:** Executioner's Edge (crit vs low-HP → finish), Toll Roads (crit kills → gold), Fire Mage ★3/★5 (crit↔ignite), Paladin ★5 (low-HP crit-dmg).

### 5.8 How a final hit is computed (composition order)
```
base   = heroStat(level)                         // §5.2 + §5.3
×star  = × ascensionMult                         // §5.4
+masteries (account-wide flats)                  // §5.6
+module/card flats and ×mults (situational)      // §1, §2
→ Power, then crit roll (CritChance/CritDamage)  // §5.7
```
Heroes set the **base + identity**, Masteries nudge **account-wide**, cards/modules add **situational**
spice. One clear chain, no double-dipping.

### 5.9 Scale note (pairs with the gameplay tuning you floated)
Crit + Power growth only feels good if blocks can *take* it: keep base blocks ~3–8 HP, add **tough variants
~20–60 HP** and bosses 500+, so big crits "pop." This is a coordinated balance pass with the taller-levels /
smaller-farther-paddle changes — done together, by playtest, after the engine exists.

### 5.10 Decisions — LOCKED (approved 2026-06-13, "go with defaults")
1. **Star count:** ★6 max.
2. **Crit cap:** 75% chance / ×4 damage.
3. **Multiball cap:** **+2** total extra balls (tightened from +3).
4. **Skill Points:** **one shared pool** funds both spell levels and Masteries.
5. **Vitality scale:** keep HP **small (single digits)** for now; revisit only if the tough-block pass needs it.

---

## 6. Spell scaling — what each level-up changes (Skill Points → Lvl 1→10)

Spells level with Skill Points (the shared pool, §5.6). Each level adds a **fixed per-level increment** to
the spell's key parameter, by archetype. (`value = base + (level − 1) × perLevel`.)

| Archetype | Spells | What scales per level | Feel of leveling |
|---|---|---|---|
| **Projectile** | Fireball→Conflagration, Rocket→Concussion, Lightning, Tesla Grid | **+Damage** / blast scale per level | hits harder |
| **Imbue (on-hit)** | Ignite, Penetration, Decay→Rot | **+imbued Hits** per level (affects more blocks per cast) + damage | spreads/lasts across more hits |
| **Timed Aura / Placement** | Fire Wall, Phoenix, Shield/Barrier, Skeleton→Bonewalker, Drain, Magnet, Last Day, Recall, Slow Time, Containment, Lich's Gaze, **Ashfall**, **Spear→Lance of Dawn** | **+Duration** per level (lives longer / more ticks) | stays out longer |
| **Instant / AoE** | Overload | **+AoE radius** per level | bigger blast |
| **Multi-spawn** | Duplicate (+copies/lvl), Raise (+helper-balls/lvl) | **+ExtraCopies** per level | more balls/minions |
| **Charge / meter** | Reckoning | **−charge threshold** per level (fires sooner) + smite damage | triggers more readily |

Notes:
- **Mana cost does NOT rise** with level — leveling is pure upside (you earned the points).
- Spell **identity/trigger never changes** with level — only the magnitude/duration/coverage. A Turret still
  fires on ball-catch at level 10; it just hits harder.
- New/reworked spells inherit the archetype scaling above (e.g., **Conflagration** scales blast per ignite
  consumed *and* +base damage/lvl; **Bonewalker** scales how many rooftops it walks via +duration/lvl).
- Cap: **Lvl 10**. The Masteries tab does **not** touch spells (spells scale only via their own levels).

---

## 7. Rifts — redefined (the hard biome gauntlet)

> This **supersedes** the old rift→dungeon flow. Rifts become a rare, hard, skill gauntlet whose
> between-level picks are **run modifiers** (§8), not permanent content drafts. The existing dungeon/rift
> code (floor loop, HP-carry, pick overlay) is the foundation to adapt.

- **Trigger:** **~10% chance** after completing any campaign level (down from 34%). A banner offers to enter
  ("A Rift tears open…"); you may **Skip** with no penalty.
- **Biome-locked:** the rift uses the **biome of the level you just cleared** — its blocks, hazards, enemies.
- **Structure:** up to **10 consecutive levels**, escalating difficulty.
- **One life pool (the hard part):** your **HP *and* ball count carry across all 10 levels** — no reset
  between levels. Lose all HP → the run ends where you fell. This is what makes finishing genuinely hard.
- **Between levels:** after each cleared rift level, **pick 1 of 3** from the modifier pool (§8). The choice
  applies for the **rest of the rift**. (Same overlay tech as the dungeon pick, new pool.)
- **Depth = reward:** the reward scales with **how many levels you cleared**. Each level banked raises the
  payout (currency + Hero Tokens + a content-drop chance); **reaching level 10 is the jackpot**. Bailing or
  dying early still pays out by depth (so an attempt is never wasted) — but far less.
- **No permanent draft:** rifts don't grant relics/cores to a run anymore; the run power comes from your
  permanent build (heroes/cards/modules/spells) **+** the modifiers you pick along the way.

Config knobs: `riftChance` (0.10), `riftLevels` (10), per-depth reward curve, biome→level-pool mapping.

---

## 8. Rift modifier pool — 10 options (APPROVED 2026-06-13)

Presented 1-of-3 after each rift level; applies for the rest of the run. Mix of heal / offense / defense /
economy / scaling / risk-reward, so "could a smart player pick differently?" always holds.

| # | Modifier | Effect | Type |
|---|---|---|---|
| 1 | **Field Medic** | Restore HP to full | heal |
| 2 | **Berserker** | +50% Power, but **−1 max HP** | risk offense |
| 3 | **Ironclad** | +2 max HP (and heal 2 now) | defense |
| 4 | **Keen Edge** | +15% Crit Chance | offense |
| 5 | **Cruelty** | +50% Crit Damage | offense |
| 6 | **Twin Serve** | +1 ball for the rest of the rift | tempo |
| 7 | **Prospector** | +30% rift end-reward (stacks each time taken) | economy |
| 8 | **Wide Gait** | +25% paddle width | defense/control |
| 9 | **Snowball** | +5% Power for **every rift level already cleared** (keeps growing) | scaling |
| 10 | **Cursed Bounty** | **+40% reward**, but **+1 enemy emitter each remaining level** | risk/reward |
