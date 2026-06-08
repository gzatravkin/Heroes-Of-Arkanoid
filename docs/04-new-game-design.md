# New Game Design — Arkanoid RPG (Redesign)

> The design of the **new** game: characters, spells, build systems, runs, and progression. This is the "what we're building" doc.
> Companions: `01-current-game-design.md` (old game) · `02-current-implementation.md` (old code) · `03-migration-plan.md` (architecture + milestones). Decisions here feed the M-milestones in doc 03.
>
> Grounded in genre research — Wizorb, Peglin, Rogue Patterns, Ball x Pit, Slay the Spire, Hades, Binding of Isaac (sources at the end).

---

## 0. Elevator Pitch

A **roguelite RPG brick-breaker**. You bounce an imbued ball through hand-built biome levels (hell → caverns → witchland → heaven), casting spells *as you deflect* and *on cooldown*, assembling a synergy build from spells, balls, paddles and relics. A **persistent branching campaign** is your main progression; **opt-in dungeon runs** are tense permadeath gauntlets that pay out permanent unlocks. **Unlockable characters** are the engine of "play it again, differently."

---

## 1. Design Pillars

1. **The deflect is the skill moment.** Timing *when and how* you bounce the ball — and *what you imbue it with* — is the core expression of skill. (Wizorb's best idea; we lean in.)
2. **Build variety is the replay engine.** Synergies across spells + balls + paddles + relics create theorycraft and "broken combo" highs. (Peglin/Isaac.) Relics range from *good alone* to *build-defining-only*.
3. **Generous, frequent casting.** Mana is plentiful and some spells are cheap/free, so magic flows constantly. (Wizorb's fatal flaw was a stingy meter that made levels drag — we explicitly avoid it.)
4. **Standardized levels, readable difficulty.** Everything sits on an integer grid with a per-biome difficulty curve. (Direct fix for the old game's "levels were weird, blocks scaled individually.")
5. **Two pressure modes.** Campaign = safe, persistent, learn-and-grow. Dungeon = permadeath, high-variance, build-defining. The same skills, two stakes.
6. **Reuse the soul, rebuild the body.** Keep the art, biomes, and class fantasies; rebuild every mechanic clean and tested.

---

## 2. Core Fantasy & Moment-to-Moment

You control a **paddle**; a **ball** (or several) ricochets upward into destructible **blocks**.

**Two independent survival resources (kept distinct, as in the old game's `LifeManager`):**

- **Spare Balls** — the *breakout* fail vector. When a ball **drains past the paddle**, you lose a spare ball and re-serve. Run out → level/run failed. This is your *skill-at-the-paddle* meter.
- **HP (the life bar)** — the *combat* fail vector, **drained by enemies hitting you**, never by losing a ball. The field is actively hostile: boss **fists/melee**, **bat dives**, falling **stalactites**, **lava**, and enemy **projectiles** strike the paddle/player and chip HP. HP hits 0 → you die (level fail in campaign; **run over** in a dungeon).

Keeping them separate is a *design feature*: it gives the paddle a **dual job** — *aim the ball* (offense) **and** *dodge/defend against attacks* (survival). It's why defensive tools matter (Paladin's **Aegis** shield, the bottom-wall save, **Recall** to rescue a draining ball) and why later biomes that add aggressive enemies raise the stakes without just making blocks tankier. Some hazards threaten *both* at once (a bat can dive at you for HP while also knocking your ball offline).

Beyond the breakout base, two casting modes layer on:

- **Imbue spells** — triggered *at the moment of deflection*. The next volley of *that ball* gains a property for a few hits: **Ignite** (burns), **Pierce** (passes through blocks), **Frost** (freezes/shatters), **Chain** (arcs lightning), **Heavy** (smashes armored blocks, ignores angle clamp), **Homing** (curves to targets), **Ghost** (phases through ghost-layer blocks). Skill-timed and tactile.
- **Active spells** — triggered anytime from **mana**, for zone control and utility: **Fire Wall**, **Turret**, **Magnet**, **Rocket**, **Duplication** (split a ball), **Last Day** (nuke line), **Aegis** (reflect shield), **Recall** (steer a ball home), **Slow Time**.

Mana regenerates over time **and** on kills; perfect-center deflects grant bonus mana (rewarding skill). Casting is meant to feel constant, not rationed.

---

## 3. Characters — *Archetype + Unlock model*

**A character is an unlockable identity lens, not a fixed spellbook.** Each provides:

- **A signature spell** — exclusive to that character (can't roll from the shared pool).
- **A passive identity** — a defining always-on rule.
- **A starting bias** — opening loadout + which shared picks it synergizes with.
- **A mastery track** — per-character meta-progression (unlocks, cosmetics, new starting options) that drives long-tail reruns.

Moment-to-moment build variety still comes from the **shared pools** (spells/balls/paddles/relics, §4) — so two Fire Mage runs play very differently, while two *different* characters feel like different games. This is the Slay the Spire / Hades / Isaac pattern: characters are the primary "different way to play," reruns earn new ones.

**Roster** (reusing the old class art):

| Character | Signature spell | Passive | Synergy bias | Status |
|---|---|---|---|---|
| **Fire Mage** | **Ignite** (imbue: ball burns + spreads DoT) | Damaged blocks keep burning | Fire / AoE / mana-regen | **MVP / starter** |
| **Paladin** | **Aegis** (active: reflect shield; next ball gains Pierce) | One free bottom-wall save per level | Multi-ball / pierce / defense | M6 |
| **Engineer** | **Overload** (active: place a bomb-block that chain-detonates) | +1 relic slot; relics cheaper | Chain / explosive / relic-stacking | M6 |
| **Necromancer** | **Raise** (killed blocks may spawn a friendly skeleton helper-ball) | Souls economy — mana from kills | Summons / kill-fueled casting | M6 (art exists, was never coded) |

**StarWarrior** (old code-only, no art) is **not** a character — its ball-steering (Recall/Inverse) becomes **shared ball/paddle tech** in the pools.

Old class kits **seed the shared spell pool**: Lightning/Magnet/Rocket (Engineer), Fire Wall/Turret/Phoenix (Fire Mage), Duplication/Spear/Last Day (Paladin) all become draftable by anyone — your character just *starts* biased toward its theme.

---

## 4. The Four Build Axes (sequenced)

Build variety lives across four axes. All are designed now; built in order (see §9 / doc 03 milestones).

### 4.1 Spells loadout *(MVP)*
Mix-and-match imbue + active spells into your run's kit. Spells **level up** (ported `LeveledParameter` scaling) via campaign points or dungeon picks. Loadout size grows with progression.

### 4.2 Relics / Items with synergies *(MVP — the core engine)*
Collectible passives that **combo**. The spread, per Peglin's lesson, must range from standalone-good to combo-only:
- **Flint Core** — crit vs stone/armored blocks.
- **Pyroclasm** — blocks that die *while ignited* ignite neighbors *(fire build enabler)*.
- **Conductor** — Chain jumps +1 target *(engineer build)*.
- **Overcharge** — bonus mana on perfect-center deflect.
- **Split Shot** — every 3rd block hit briefly splits the ball.
- **Glass Cannon** — +50% ball damage, −1 max life *(tradeoff pick)*.
- **Souljar** — gain meta-shards per block destroyed over a threshold.
- **Magnetism** — blocks drift slightly toward the ball *(combos with Magnet/Homing)*.

Conditional scalers tie to **biome block types** (stone, ghost, holy) so the biome you're in changes which relics shine.

### 4.3 Ball types & fusion *(M5+)*
Equip 1–2 **ball cores** per run; **fuse** them via dungeon picks for combined effects: Heavy, Split, Ghost, Ember (trails fire), Echo (extra rebound). E.g. **Heavy + Ember = molten wrecking ball**; **Ghost + Split = phasing swarm**. (Ball x Pit's fusion idea, adapted.)

### 4.4 Paddle / character mods *(M6+)*
Modify the control surface: width ±, **multi-segment** angled wings, **sticky** catch-and-aim launch, **side-cannons** (auto-fire), **curve-control** (hold to bias bounce angle), trampoline zones. Changes the core *feel* per build.

---

## 5. Economy & Resources

- **Mana** *(in-run, casting)* — generous; regenerates + gains on kills + bonus on perfect deflects. Some spells free/cheap. (Anti-Wizorb-stinginess.) Necromancer swaps regen for a **souls** model (mana per kill).
- **Gold / Treasure** *(in-run)* — drops from blocks/chests; spent at **shops** (campaign rest-nodes and dungeon shop floors) on spells, relics, heals.
- **Shards** *(meta-currency)* — **drip even on death** (so failed dungeons still progress you); spent on permanent unlocks: characters, starting relics, ascension access.
- **Crystals** — *repurposed* from the old dangling currency into **dungeon-only** scrip for in-run shop floors.

---

## 6. Progression — Two Modes

### 6.1 Campaign *(persistent, no permadeath)*
A **branching node map per character** (ported `HeroRoad` concept): levels gated by completion, with **forks** offering choices. Losing a mission just retries it. Between missions you spend **points** on spell levels and **gold** at shops. This is where you *learn the character* and grow permanently. Biomes sequence hell → caverns → witchland → heaven, each introducing its signature block mechanics (§8).

### 6.2 Dungeons *(opt-in roguelike runs, permadeath)*
A **"You've uncovered a dungeon — descend?"** banner appears periodically (after milestones / found in levels). A dungeon is:
- **2–5 levels**, escalating, with **minibosses** mid and a **boss** at the end.
- **Permadeath within the run** — death ends the dungeon (you keep nothing but the shard drip).
- **Pick 1 of 3** after each cleared level. Per the Hades lesson (*choices need variance or they go flat*), the pool deliberately mixes: a spell, a relic, a ball-core/fusion, a heal, a **shop floor**, and occasional **tradeoff/curse-with-upside** picks. Run-buffs stack for *that run only*.
- **Payoff:** clearing grants a **permanent unlock** (a new relic/spell enters the global pool, a character, or shards). Run buffs reset; the unlock persists.

This gives the StS/Hades blend you chose: high-variance tense runs, with lasting reasons to attempt them.

---

## 7. Reward & Choice Design (variance + tradeoffs)

Every offered choice should pass: *"could a smart player reasonably pick a different option for a different build?"* If the answer is "no, just take the stat," it's a bad reward. Tools:
- Mix **categories** (spell / relic / ball / paddle / heal / shop / curse-upside) in the same offer.
- Include **build-enablers** (Pyroclasm, Conductor) that are weak in a vacuum but transformative in the right build.
- Include **tradeoffs** (Glass Cannon) and **conditional** relics (biome-keyed).
- Show **synergy hints** in the UI (highlight when an offered relic combos with what you own).

---

## 8. Biomes, Enemies & Bosses (reuse the soul)

| Biome | Signature block mechanic (kept) | Notes |
|---|---|---|
| **Hell** | Teleporters (color-paired portals warp the ball); **lava blocks** *(finally implemented)* | Starter biome / MVP |
| **Caverns** | Union-of-sticks (connected bridge blocks) | |
| **Witchland** | Ghost portals (toggle ball to a ghost layer to phase through blocks); **necromant enemy revives dead blocks** | Pairs with Ghost imbue/ball |
| **Heaven** | Statue enemies you can turn **ally** or **level up** via altars | Most developed legacy biome |

**Enemies are an active HP threat, not just blocks.** Bats dive at the paddle, beholders/turrets fire projectiles, stalactites fall, boss fists slam — all chip the **HP bar** (§2), demanding dodging and defensive play alongside aiming. This is the second threat axis that makes biomes escalate in danger, not just durability.

**Bosses:** Goblin and Witch (good legacy references) ported; **Demon redesigned properly** (old one was a no-op). Dungeon minibosses drawn from biome enemies. Boss design favors **patterns the ball/paddle must read** (Shatter-style readability) over HP sponges, and **telegraphed attacks you dodge** to protect HP.

---

## 9. MVP Scope → Milestones (ties to doc 03)

- **M1** — Fire Mage; deterministic physics; HP blocks; lives/balls; one Hell grid level; **Ignite (imbue)** + one **active** spell; generous mana. *Proves feel.*
- **M2** — Full Fire Mage kit + **relic/synergy system** + spell leveling + upgrade UI. *Proves build variety.*
- **M3** — All four biomes' blocks + signature mechanics on the grid.
- **M4** — Campaign node map + persistence + rewards.
- **M5** — **Dungeon mode** (permadeath, pick-1-of-3, permanent-unlock payoff) + **ball types/fusion**.
- **M6** — Paladin, Engineer, **Necromancer** (from art) + **paddle/character mods** + bosses.
- **M7** — In-browser grid level editor, **standardized difficulty-curve balance pass**, polish/juice.

The character system's plumbing (signature slot + passive + shared pools) lands in **M1–M2**; its *payoff* (rerun variety) appears once the second character ships in **M6**.

---

## 10. Open Questions (non-blocking)

- **Difficulty/ascension tiers** for dungeons — how many, and what they modify (block HP, fewer picks, elite density)? → tune in M5/M7.
- **How many relics/spells** at launch for healthy synergy density? (Peglin/StS ship dozens; target a minimum viable synergy web by M2, expand to M7.)
- **Daily/seeded dungeon** for long-tail engagement — yes/no? → post-M7.
- **Necromancer** finish-from-art vs cut, and **StarWarrior** tech absorption details → confirmed in M6.
- **HP vs spare-balls tuning** — *both stay, kept distinct* (§2: HP = enemy damage, Balls = drains). Open part is only *numbers*: starting values, whether HP regenerates between levels or only via items/heals, and whether running out of spare balls costs HP or ends the level directly. → M1 feel pass.

---

## 11. What This Fixes From the Old Game

| Old problem | New design |
|---|---|
| Items never implemented (3 empty stubs) | Relics/items are a **core build axis**, fully implemented + tested (M2) |
| Spells felt broken (static timers, dead-ball caches, mixed time bases) | Clean deterministic Core, one tick, every spell unit-tested |
| Levels not standardized (per-block float scale) | **Integer grid** + per-biome difficulty curve |
| Stingy magic / dragging levels | **Generous mana**, frequent casting, perfect-deflect mana bonus |
| Fixed kits, low replay | **Unlockable characters** + **four-axis build variety** + dungeon runs |
| Crystal currency with no sink | Repurposed as **dungeon scrip** |
| Necromancer art unused / StarWarrior half-class | Necromancer becomes a **real character**; StarWarrior becomes **shared tech** |
| No stakes (infinite retries everywhere) | Campaign safe + **dungeon permadeath** for tension |

---

## References

- [Wizorb review — A Critical Hit](https://www.acriticalhit.com/wizorb-indie-game/) · [Shatter solved the Breakout problem — Game Developer](https://www.gamedeveloper.com/design/shatter-solved-the-breakout-problem-please-don-t-keep-making-the-same-mistake-)
- [Rogue Patterns — Steam](https://store.steampowered.com/app/2644140/Rogue_Patterns/) · [Ball x Pit — brick-breaker roguelite (YouTube)](https://www.youtube.com/watch?v=FWjNlqUPqDs)
- [Peglin orb/relic synergy guide](https://gameplay.tips/guides/peglin-ultimate-guide-to-all-orbs-with-strategies-and-tier-list.html) · [Best orbs in Peglin — Game Rant](https://gamerant.com/peglin-best-orbs/)
- [Roguelike vs Roguelite (StS vs Hades) — Switchblade](https://www.switchbladegaming.com/strategy-games/roguelike-vs-roguelite-explained/) · [Hades reward-choice feedback — Steam](https://steamcommunity.com/app/1145360/discussions/2/1864993869493927612/)
