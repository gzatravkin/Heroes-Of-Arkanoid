# Current Game Design — Old Unity Arkanoid RPG

> Reverse-engineered from `Arkanoid game/Scripts/` (246 C# files, ~9,000 lines) and `Sprites/` (755 PNG).
> This document describes **what the game is** from a player's perspective. For how it's coded, see `02-current-implementation.md`. For the redesign/migration, see `03-migration-plan.md`.

**One-line summary:** A class-based RPG/roguelite **Arkanoid (breakout)** with per-class spell trees, equippable items, between-run progression, and four biome-flavored worlds (hell → caverns → witchland → heaven). The skeleton is ambitious and largely data-driven, but it is a **vertical-slice prototype**: one class and one boss are stubbed, three items and an entire currency are inert, several spells have latent bugs, and levels are bespoke hand-tuned prefabs rather than a standardized curve.

---

## 1. Core Loop

A single mission is a classic breakout board:

- **Paddle ("Bar")** moved by mouse/touch. Has a size level and a default ball speed (`DefaultBallSpeed = 15`, `DefaultSize = 1`).
- **Ball(s)** deal a fixed `BallDamage = 1` per block contact. Multiple balls can be live at once.
- **Blocks** have `HP` (default 2), optional effects, and a `NeedToKill` flag.
- **Player resources during play:** **Lives** (an HP bar — taking damage reduces it) and **Balls** (a count of spare tries — losing a ball decrements it). When lives hit 0 → mission lost.

**Win conditions** are component-based and composable (`RequiereWinAll / WinOne / WinOneWithTolerance`):
- *Destroy all* blocks flagged `NeedToKill`.
- *Destroy something* specific (e.g. a boss).
- *Timer* — survive N seconds, or finish within a limit.

**Lose:** the scene simply **reloads** — no penalty, no run reset, no permadeath. Missions are infinitely retryable.
**Win:** rewards granted, and the player advances along a branching campaign map.

---

## 2. Hero Classes

Heroes are data assets (`SO_Hero`). Each defines interface sprites, name/description, a campaign map (`HeroRoad`), an array of **invisible Skills** (passives/stat boosts), an array of **visible Spells** (active, castable), a resource controller (`SpellSystem`), a paddle prefab, and starting lives/balls.

| Class | Identity | Resource model | Kit |
|---|---|---|---|
| **Fire Mage** | Burn / area-damage caster | Mana (regenerates over time) | Passive (ignite ball), Phoenix, Ring (fireball), Turret, Wall |
| **Paladin** | Defensive / ball-multiplier | — | Passive shield, Duplication, Last Day (nukes), Penetration, Spear |
| **Engineer** | Gadget / chain-reaction caster | — | Passive (bomblets), Lightning, Magnet, Radiation, Rocket |
| **StarWarrior** | Ball-trajectory manipulator | Souls (per kill) likely | **Only 2 spells: Back, Inverse — no passive. Unfinished.** |
| **Necromancer** | (planned 4th class) | — | **Art-only: 47 sprites + Skeleton prefabs, zero code. Abandoned.** |

**Resource economy is per-hero:**
- *Mana* model — regenerates with time (`MpRegeneration`).
- *Souls* model — gained per block destroyed (`MPPerSoul = 10`), a "kills fuel casting" economy.

Each class is its **own separate campaign**; switching class switches the whole road and skill set. The "no more levels" dialog literally tells the player to "play another class — a new campaign awaits."

---

## 3. Skills & Spells

**Structure:** two subtypes — `SO_InvisibleSkill` (passives/stat) and `SO_VisibleSkill` (active spells with a resource cost). Each skill has up to `MaxLevel` (default 10) and a per-level cost: `startPrice + priceForLevel * level`.

Numeric properties scale with level via `SkillNumberParameter`, which supports **line / curve / grid** interpolation (most are 3-point grids `(lvl0, lvl5, lvl10)` smoothly lerped). Skills self-discover their tunable fields by reflection, and spell descriptions are format strings filled from those parameters.

**Acquisition / leveling:** points come from character level-ups into a `LibrePoints` pool, spent in the upgrade UI. Skills can be **locked** until "opened" (auto, or via in-level scripts). Full respec/refund is supported.

**Per-spell behavior:**

*Fire Mage* — Passive: ball hitting paddle near center gains a fire/explosion effect. Phoenix: a damaging phoenix orbits a random ball. Ring: spawns a fireball projectile. Turret: paddle-mounted auto-firing turret. Wall: a spreading fire wall that burns blocks over time.

*Paladin* — Passive: a reflecting shield above the paddle. Duplication: clones a ball into N smaller balls. Last Day: each top-wall hit drops a "nuke" line. Penetration: piercing ball (clears up to `maxBlocks`). Spear: a piercing spear projectile.

*Engineer* — Passive: chance per kill to drop a particle; collecting enough triggers a nuke. Lightning: chains across random blocks. Magnet: pulls nearby blocks toward a ball for a duration. Radiation: AoE damage zone. Rocket: homing/accelerating rocket.

*StarWarrior* — Back: auto-recalls a ball toward the paddle for a time. Inverse: flips a ball's vertical velocity upward (anti-drain save).

**Half-finished / suspect spell code (the "spells feel broken" symptom):**
- `StarWarrior_Back` caches one ball forever — if it dies, the spell silently no-ops.
- `Engineer_Lighting.LastTime` is `static` (shared across instances → timing stalls).
- `Paladin_LastDay` mixes `RealTime` (gate) with `battleTime` (write) — inconsistent throttling.
- `Fire_Passive` damage/size/hits/velocity are `[HideInInspector]` — frozen to code defaults, untunable (an abandoned balance pass).
- Mixed time bases across spells (`battleTime` / `RealTime` / `unscaledDeltaTime`).

---

## 4. Items

> **The owner is correct — items were never fully implemented.**

Items are `SO_AbstractItem` (max level 3), active only when slotted into one of **3 equip slots**. On battle start, slotted items run their initialization and apply leveled coefficients via event subscriptions.

**Implemented items (~17):** Drill (+treasure), Tom (+exp), Phoenix (+max HP %), Flask (HP regen per kill), Helm (chance to negate HP loss), Ring (chance to refund mana), Balance (HP↔MP cross-heal), Gem (mana regen ×), Staff (+max resource), Hammer/Torch (paddle bonuses), BarSpeed, Clock (slows time when >15 blocks remain), FourLeaf (chance to refund a lost ball), JadeBall (chance for extra block hit), BallModificator (ball size + visual).

**Empty stubs (no effect at all):** `SO_ItemCrown`, `SO_ItemMark`, `SO_ItemSun` — class bodies are empty.

**Acquisition is thin:** items only drop by accumulating Treasure Points to 100, which upgrades a random non-maxed item. There is **no shop** that spends currency on items.

---

## 5. Upgrades & Progression (Meta)

Persistent, per-class:
- **Per character:** class index, Level, Exp, `LibrePoints` (spendable) and `TotalPoints` (lifetime, for refunds), missions won, per-skill levels + opened flags.
- **XP curve:** `round(100 × 1.1^(level-1))` — gentle exponential. Level-ups grant points from a tunable grid.
- Points buy skill levels in the upgrade panel; full respec available.
- **Items persist globally**, independent of class.

---

## 6. Biomes / World Progression

**Four biomes** (folder/in-code names differ from the owner's terms):

| Biome (owner) | In-code name | Signature mechanics |
|---|---|---|
| Hell | `Location_1_Hell` | Teleporters (color-paired portals warp the ball); lava blocks **(stub — not implemented)** |
| Caverns | `Location_2_Dungeion` | `UnionOfSticks` (connected bridge blocks) |
| Witchland | `Location_3_Village` | Ghost portals (toggle ball to a "ghost" layer to pass through blocks); bats, beholders, pots; **Necromant enemy that revives destroyed blocks** |
| Heaven | `Location_4_Heavens` | Statue enemies (Melee/Shield/WindMaster) that can be turned **ally** or **leveled up** by hitting an Altar/Vase. Most developed biome. |

**Level sequencing is a branching node graph, not a linear list.** Each class's `HeroRoad` holds `RoadElement` nodes with prerequisite edges. A level opens once its prerequisites are won; forks present a choice ("There's a fork!"). Biome is partly inferred from which biome's blocks dominate a level.

**Special layout controllers:** `StrechedLevel` (blocks slowly descend toward the paddle — Space-Invaders pressure) and `MultiplyFloorLevelController` (clear one "floor" to reveal the next).

---

## 7. Bosses

Four boss folders, **highly uneven completeness**:
- **Goblin** — most complete. Animator state machine: hops between 3 positions, queues random attack stacks, drops stalactites.
- **Witch** — animator-driven; can grab a ball and make it "fly" with custom velocity.
- **Statue** — minimal: plays a random action animation.
- **Demon** — **essentially a stub** (empty collision handlers); only `DemonFist` does anything.

Bosses are beaten as "destroy something" targets. Much of their behavior lives in Unity Animator assets, not scripts.

---

## 8. Economy & Resources

**Two parallel economies:**

*In-run (casting):* mana or souls per class — spent per spell, optional free casts, mana regenerates / souls accrue per kill.

*Meta currencies:*
- **Skill/Upgrade Points** — from level-ups, spent on skills.
- **Treasure Points** — accrue to 100 → random item upgrade.
- **Crystals** — four colors (Blue/Green/Red/Yellow) with full buy/affordability logic, handed out as level rewards — **but there is no spend sink**; no shop consumes them, and Yellow is never even granted. A designed-but-dangling currency.

Per-level rewards (`Premy`): exp (~200), three crystal colors, treasure points, and a random-items count.

---

## 9. Roguelike Elements

This is a **light roguelite, not a true roguelike:**
- **Runs = the persistent branching campaign per class.** Progress and unlocks persist; **no permadeath** — a loss just reloads the scene.
- **Randomization is mostly *within* a mission** (random spell targeting, lightning chains, boss attack stacks, stalactite scatter, occasional random skill grants). **No procedural board generation** — levels are hand-authored prefabs.
- **Choices** = fork nodes in the road graph + the 3-slot item loadout + skill build.

---

## 10. Known Design Problems (with evidence)

1. **Items never fully implemented** — `SO_ItemCrown/Mark/Sun` are empty; only one trickle acquisition source; no item shop.
2. **StarWarrior class unfinished** — 2 spells, no passive (every other class has a passive + full kit), and no art.
3. **Necromancer is art-only** — 47 sprites + Skeleton prefabs, zero code; its skill list is a Paladin clone. Decide: finish it or drop it.
4. **Boss content uneven** — Demon is a no-op; Statue is one line; only Goblin/Witch are real.
5. **Lava block stub** — Hell's signature lava mechanic is an empty class.
6. **Levels not standardized — blocks scaled individually.** Block HP is per-prefab; each level is a hand-baked prefab of individually-placed, individually-**scaled** blocks; each level can even define its own field dimensions. No global difficulty curve or normalized block table. **(Root cause detailed in `02-current-implementation.md` §4.)**
7. **Spell timing bugs / locked params** — see §3 (static timers, cached dead balls, mixed time bases, hidden untunable fields). This is the "spells feel broken" symptom.
8. **Crystal currency has no sink** — fully implemented, handed out, never spent.
9. **Loss handling is crude** — scene reload, no summary, no stakes, undercutting the roguelike framing.
10. **Unfinished UI plumbing** — some win-condition description generation is commented out with `//TODO`.

**Net:** an impressively complete data-driven skeleton wrapped around a prototype's worth of finished content. The redesign keeps the *identity* (class-based RPG breakout, biome progression, spell trees, items) and the *art*, and rebuilds the *mechanics* clean.
