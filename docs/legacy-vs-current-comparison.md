# Legacy vs. Current — Decision-Oriented Comparison

> Inputs: `docs/legacy-mechanics.md` (the old Unity build, read from `Scripts/`) and `docs/current-mechanics.md` (the C#/.NET + web rewrite, read from `backend/Arkanoid.Core` + `config/`). Art inventoried from the git-tracked `Sprites/` tree (the working tree shows them deleted, but 1606 files remain in the index).
>
> **Purpose:** decide *what to port forward from the previous game*. This is a reuse map, not a spec.
>
> **Settled divergence (do NOT recommend reverting):** the rewrite's **economy** is deliberately, owner-approvedly different — 3 currencies (Sparks/Souls/Insight) + fixed-price random rolls + a **linear** campaign chain (`docs/2026-06-14-economy-rework-proposal.md`). The legacy point-buy + treasure-roll + per-hero branching roads model is **historical**, not a regression. Everything in §1's *Economy* table and the per-hero-roads note is flagged SETTLED and is out of scope for "reuse."

---

## 0. TL;DR — the reuse verdict

The rewrite faithfully carried forward the **best of the legacy soul**: the trigger-rich, ball-relationship spell design (on-catch Turret, top-wall-bounce Last Day, orbiting Phoenix), the 4 bosses (Demon/Goblin/Witch + a Heaven boss), and most themed Heaven/Village hazard blocks. The economy was intentionally rebuilt.

What got **flattened or dropped** and is worth reviving:
1. The **2-damage-type model** (Magic vs Ball) and its composable **weakness/immunity blocks** — the richest lost content language.
2. The **composable block-effect stack** (contagion, spawn-on-hit-threshold, kill-children) — replaced by a rigid one-behavior-per-block enum.
3. **Engineer Rocket as a mouse-piloted homing missile** (became the no-damage Concussion Charge) — high player agency, art exists.
4. **Engineer Magnet pulling blocks toward the ball** (became a ball-steer) — rare board-manipulation verb, inverted away.
5. **Lava-eater merging blocks**, **path-follower / patrolling enemies**, and the **roaming enemy ball** — distinct hazard verbs with no current analogue.

And the single biggest **free win**: the legacy **Necromancer art set is fully drawn** (animated Skeleton prefab, SkeletalMage, BoneGolem, shield) and the current Necromancer is **fully coded** — the two were never connected. Wiring legacy art → current spells is mostly an asset-import job (see §4, §5).

---

## 1. Side-by-side mechanic maps

Legend: **BOTH** = concept exists in both (see §2 for drift). **LEGACY-ONLY** = lost in rewrite. **CURRENT-ONLY** = new in rewrite.

### 1a. Heroes / classes

| Concept | Legacy | Current | Status | Note |
|---|---|---|---|---|
| Fire Mage | full kit | full kit | **BOTH** | element=fire |
| Paladin | full kit | full kit | **BOTH** | element=holy |
| Engineer | full kit | full kit | **BOTH** | element=tech |
| Necromancer | **art only, no C#** | **fully coded** | **BOTH** | gap *flipped* — see §4 |
| Star Warrior | code only (Back, Inverse), no art | **absent** as a hero | **LEGACY-ONLY** | `recall` ≈ Back survived as a neutral spell |
| Signature locked in slot 0 | no (free point-buy kit) | yes | **CURRENT-ONLY** | acquisition design |
| Global shared spell pool | no (per-class fixed kit) | yes | **CURRENT-ONLY** | acquisition design |
| Per-element mana affinity (×0.8) | no | yes | **CURRENT-ONLY** | |
| Stat profiles (Power/Vit/Crit/Multiball/Tempo) | implicit (skill levels) | explicit stat block + ★ ascension | **CURRENT-ONLY** | |
| Common upgradeable Life/Size/Tries | yes | folded into stats/mastery | drift | legacy ComunSkills → current stats |

### 1b. Spells (mapped by lineage)

| Legacy spell | Current spell | Status | Identity verdict |
|---|---|---|---|
| FM Passive Fireball (centered-catch enchant → next-block explosion) | FM passive "ignited kills spread" + `ignite` | **drift** | precise **centered-catch** trigger LOST; ignite arms on *any* deflect |
| FM Fire Turret (fires on every ball-catch) | `turret` | **BOTH** | on-catch trigger preserved faithfully |
| FM Phoenix (visible entity orbiting a ball) | `phoenix` | **BOTH** | orbiting-entity identity preserved |
| FM Fire Wall (enchant ball → next hit ignites area, fire spreads over time) | `firewall` (placement wall rises upward) | **drift** | delivery changed: ball-armed area-ignite → rising placement wall |
| FM Fire Ring (fireball spawned above paddle) | — | **LEGACY-ONLY** | redundant with turret/fireball today |
| — | `fireball`=**Conflagration** (detonate all burning blocks) | **CURRENT-ONLY** | |
| — | `ashfall` (ignite-kills rain embers) | **CURRENT-ONLY** | |
| — | `ignite` signature creeping DoT imbue | **CURRENT-ONLY** | legacy fire-spread lived only inside Fire Wall |
| Paladin Passive Shield (reflects **enemy bullets** back up as projectiles) | `shield` cast barrier (reflects **downward ball** up = pit save) + passive wall-save | **drift** | bullet-reflect identity LOST; shield is now a pit-save |
| Paladin Spear (piercing damage hitter) | `spear`=**Lance of Dawn** (no-damage banking pillar) | **drift** | piercing projectile → trick-shot prop |
| Paladin Duplication | `duplicate` | **BOTH** | current clones smaller, inherit charges |
| Paladin Penetration (punch through N blocks) | `penetration` | **BOTH** | now "phases on next deflect" |
| Paladin Last Day (free lines; top-wall hit drops a nuke; storm-cloud entity) | `lastday` (ceiling bounce smites the column) | **drift** | top-wall trigger kept; free-lines + visible cloud → timed aura |
| — | `reckoning` (HP-loss vengeance meter) | **CURRENT-ONLY** | |
| Engineer Lightning (chains random blocks) | `lightning` | **BOTH** | legacy chained on a roll; current fixed jumps |
| Engineer Magnet (**pulls blocks** toward the ball) | `magnet` (**steers balls** toward blocks) | **drift** | subject inverted — block-yank LOST |
| Engineer Radiation (DoT field on a ball) | `radiation`=**Containment Field** (emitter-suppression zone) | **drift** | repurposed to counter emitters |
| Engineer Rocket (mouse-piloted homing missile, explodes on blocks) | `rocket`=**Concussion Charge** (no-damage knockback + pickup yank) | **drift** | piloted-missile identity LOST |
| Engineer Passive Nuke-charge (kill→particle→catch→charge→nuke) | passive = ×1.5 mana regen | **drift** | collect-charge-nuke loop LOST |
| — | `overload` signature (plant a bomb-block) | **CURRENT-ONLY** | |
| — | `tesla`=**Tesla Grid** (both side-walls charged → curtain) | **CURRENT-ONLY** | |
| Necromancer (no coded spells) | `raise`/`decay`/`drain`/`skeleton`=**Bonewalker**/`golem`=**Bone Golem**/`mage`=**Lich's Gaze** | **CURRENT-ONLY** (code) | legacy *art* maps — §4 |
| Star Warrior Back (return ball to paddle) | `recall` (neutral) | **BOTH** | survived as a neutral pool spell |
| Star Warrior Inverse (flip ball velocity up) | — | **LEGACY-ONLY** | trivial, low value |
| — | `slowtime` (neutral) | **CURRENT-ONLY** | |
| Resource: Mana regen | Mana controller | **BOTH** | |
| Resource: Souls-on-kill | +1 mana/kill (Necro ×2, Drain) | **BOTH** | merged into the single mana budget |

### 1c. Blocks & block-effects

**Generic composable effects (legacy `AbstractBlockEffect`, stackable) vs current one-behavior-per-block enum:**

| Legacy block effect | Current analogue | Status |
|---|---|---|
| ChangeSpriteByHP (damage states) | HP-tier / damaged sprites | **BOTH** |
| ShowHitBar | HP bars | **BOTH** |
| OpenLavaSpawner (starts spewing when hurt) | `LavaSpawner` behavior (creeps after first hit) | **BOTH** |
| CreateObjectOnDie (spawn on death) | `forcedDropEffect` (death drops a pickup) | **drift** (narrowed to pickups) |
| CorruptBlock (keyed **contagion** chain) | ShieldStatue allied-corrupt; fire/decay spread | **LEGACY-ONLY** as a *composable attachable* effect |
| GetMoreDamage (×3 **weakness** to a damage type) | — | **LEGACY-ONLY** |
| IgnoreTypeOfDamage (**immune** to one damage type, e.g. ball-proof / spell-only) | only full `Indestructible` | **LEGACY-ONLY** (nuanced form) |
| CreateOnHit (spawn / reveal at HP **thresholds**) | — | **LEGACY-ONLY** |
| KillChildrenOnDie (nested structures) | — | **LEGACY-ONLY** |
| **2-damage-type model (Magic vs Ball)** | single dmg + crit/elemental tags | **LEGACY-ONLY** — the enabler for weakness/immunity blocks |

**Themed / special blocks:**

| Concept | Status | Note |
|---|---|---|
| Teleporter (color-pair warp) | **BOTH** | `Teleporter` behavior |
| Necromant revive loop | **BOTH** (drift) | legacy: kill spawns a ghost, **necromant-ball must touch it** to resurrect; current `Reviver`: same-layer block just revives after 4s (counterplay simplified) |
| Ghost Portal (toggle ghost layer) | **BOTH** | `Portal` behavior |
| Wind Master (push the ball) | **BOTH** | `WindMaster` |
| Heaven Shield Statue (shield allies / corrupt when allied) | **BOTH** | `ShieldStatue` |
| Heaven Melee Statue (fires volleys) | **BOTH** | `Emitter` (heavenmissile), pacifiable |
| Heaven Altar (ally-flip statues) | **BOTH** | `Altar` pacify |
| Heaven Vase (level up statues on death) | **BOTH** | `Vase` |
| Heaven Column (parts slide down) | **BOTH** | `heaven_column_*` |
| Union of Sticks (auto-weld cluster) | **BOTH** | `cavern_union` CollapseUnion |
| Stalactite | **BOTH** | |
| Bomb | **BOTH** | `cavern_bomb` |
| Beholder eye (tracks/aims ball) | **BOTH** | `village_beholder` Emitter |
| Bat (grab ball) | **BOTH** (drift) | legacy: holds then **releases for a speed boost**; current: **carries toward drain** as a threat |
| Lava block — **eater (merges/grows)** | **LEGACY-ONLY** | current Lava = danger-zone HP-drain + creeping spawner (different) |
| Sleeping Bat (nested kill-children) | **LEGACY-ONLY** | depends on nesting |
| Pot (cycles visual states) | **LEGACY-ONLY** | cosmetic |
| Marks / path-followers (waypoint-moving enemies/blocks) | **LEGACY-ONLY** | current Cart rolls, but no general patrol path |
| Enemy roaming ball (stays above paddle) | **LEGACY-ONLY** | no current roaming-enemy-ball |
| Cauldron (siphon mana, refund on death) | **CURRENT-ONLY** (code) | legacy **Kotelok** art exists → wire-up candidate |
| Cart (rolling deflector) | **CURRENT-ONLY** (code) | legacy **DungeonCart** art exists → wire-up candidate |
| BossVase (Seraph fuse) | **CURRENT-ONLY** | |
| Lava danger-zone drain / creeping LavaSpawner | **CURRENT-ONLY** behavior | |

### 1d. Bosses

| Concept | Legacy | Current | Status | Drift |
|---|---|---|---|---|
| Demon (Hell) | DemonFist slam | **Demon**, Fist-Slam column smash | **BOTH** | faithful |
| Goblin (Caverns) | hops 3 positions, drops stalactites | **Goblin King**, hops + stalactite rain | **BOTH** | faithful |
| Witch (Village) | flings/seizes the ball | **The Witch**, grab-hand → throw | **BOTH** | faithful |
| 4th boss | **Statue** (random animated actions) | **Seraph** (Heaven; summons adds + BossVase) | **drift** | Heaven boss reimagined; `HeavenBoss` art exists |
| Driver model | animation-stack | HP-fraction 3-phase + shared attack verbs (AimedShot/Rain/Spread/Summon) | **CURRENT-ONLY** structure | telegraphs standardized |

### 1e. Enemies / hazards

| Concept | Status | Note |
|---|---|---|
| Emitter shots | **BOTH** (drift) | legacy could aim/home; current straight-down only |
| Stalactite fall | **BOTH** | |
| Bat carrier | **BOTH** (drift) | reward→threat (see 1c) |
| Witch grab-hand | **BOTH** | |
| Wind push | **BOTH** | |
| Shield-statue pulse | **BOTH** | |
| Reviver | **BOTH** (drift) | |
| Lava drain / lava creep | **CURRENT-ONLY** behavior | |
| Cart deflector | **CURRENT-ONLY** (art legacy) | |
| Cauldron siphon | **CURRENT-ONLY** (art legacy) | |
| Roaming enemy ball | **LEGACY-ONLY** | |
| Path-follower patrols | **LEGACY-ONLY** | |
| Riposte-paddle parry of hazards | **CURRENT-ONLY** | module, not the Paladin |

### 1f. Ball / paddle / player

| Concept | Status | Note |
|---|---|---|
| Deflect angle from hit position | **BOTH** | legacy `normalizedPos×1.5` up; current `t×maxAngle` up |
| Held first ball / serve | **BOTH** | |
| Multiball | **BOTH** | |
| HP bar + ball-tries lives | **BOTH** | LifeManager → HP + StartBalls spares |
| Timed paddle/ball buff pickups | **BOTH** | BonusVelocity/Size → wide_paddle/slow_ball/… |
| Anti-stuck angle clamping | **BOTH** (drift) | legacy XY-ratio clamp; current min-angle guards + CCD |
| Constant ball speed | **drift** | legacy constant velocity; current **time-ramps 360→750 over 60s** |
| Paddle width | **drift** | legacy size-1 default; current shrunk 96→64→**52** |
| Perfect-deflect band (+mana, drives cards) | **CURRENT-ONLY** | |
| Crit engine (chance/damage stat) | **CURRENT-ONLY** | |
| Combo multiplier (resets on paddle touch) | **CURRENT-ONLY** | |
| Ball cores + fusions (ghost/ember/split…) | **CURRENT-ONLY** | |
| i-frames after a hit | **CURRENT-ONLY** | |

### 1g. Economy / progression — **SETTLED DIVERGENCE (do not revert)**

| Concept | Legacy | Current | Status |
|---|---|---|---|
| Currencies | LibrePoints + Crystals(×4) + SpellPoints + TreasurePoints | Sparks / Souls / Insight | **SETTLED** |
| Acquisition | point-buy upgrades + 100-treasure random item roll | fixed-price **random rolls**, dupes level/ascend | **SETTLED** |
| Spell kit source | per-class **fixed kit** | signature + **global pool** rolls | **SETTLED** |
| Equip passives | 18 Items, 3 slots, 3 levels | 17 Relics + 20 Cards + 12 Modules | **CURRENT-ONLY** (vastly expanded) |
| Campaign map | **per-hero branching roads** | **single linear chain** (48 nodes) | **SETTLED** |
| Rifts / dungeons / mastery / ascension | — | generated rifts, mastery tree, ★ ascension | **CURRENT-ONLY** |
| Win conditions | DestroyAll / DestroySomething / Timer | DestroyAll / SurviveTime / TimeLimit (+overrun) | **BOTH** (DestroySomething dropped) |

> The Items table (legacy) maps loosely onto current relics (Gem/Staff→Mana Battery, Flask→heal-on-kill, Helm→Second Wind, FourLeaf→ball refund, Phoenix→+maxHP, Torch/Hammer→ball speed, Clock/Hourglass→bullet-time, Drill→more rolls, Ring→mana refund, Balance→MP↔HP, JadeBall→extra hit, BallModificator→ball core). The *content* survived; only the *acquisition shell* changed (settled).

---

## 2. Behavior / identity drift (same concept, behaves differently)

Concrete, ordered by how much the identity moved:

1. **Engineer Rocket → Concussion Charge.** Legacy: a missile you **steer with the mouse** for 2 s, then it homes and **explodes on blocks for damage**. Current: instant, **deals no damage**, knocks balls upward (a save) and yanks pickups in. The signature "pilot a rocket" fantasy is gone.
2. **Engineer Magnet — subject inverted.** Legacy **pulls the bricks toward the ball** (frees their bodies; they physically move). Current **steers the balls toward the nearest brick**. Opposite object; the board-rearranging feel is lost.
3. **Paladin Passive Shield — target swapped.** Legacy reflects **enemy bullets** back up as your projectiles (an active anti-projectile defense). Current `shield` is a cast **pit-save barrier** that bounces a *falling ball* back up; the passive is just a once-per-level wall save. The "deflect their fire" identity is gone (only the Riposte module approximates it).
4. **Paladin Spear → Lance of Dawn.** Legacy: a **piercing damage** spear. Current: a **no-damage solid pillar** you bank trick shots off. From offense to geometry tool.
5. **Fire Mage passive trigger loosened.** Legacy fired only on a **centered catch** (`|t| < ~0.1`), rewarding precision. Current `ignite` arms on **any** deflect. The skill-expression of the centered catch is gone (though the current Perfect-Deflect band is a *different* precision reward).
6. **Fire Wall delivery.** Legacy: enchant a ball, and on its **next block hit** ignite an area whose size scales with local density, fire **spreading over time**. Current: a **placement wall** that spawns at ball height and **sweeps upward**. Trigger model entirely changed (ball-armed → placement).
7. **Last Day delivery.** Legacy: grants N **free lines**, each consumed on a **top-wall hit** to drop a nuke, with a **visible storm cloud** while lines remain. Current: a **timed aura** where each ceiling bounce smites the ball's column. The top-wall trigger and the cloud entity survive in spirit; the "bank of lines" economy is gone.
8. **Bat — reward vs threat.** Legacy bat **grabs and holds** the ball, then on release **grants a paddle speed boost** and flies off (a risk→reward). Current bat **carries the ball toward the drain** — a pure threat you must pop. Same grab verb, opposite valence.
9. **Necromant revive loop counterplay.** Legacy: a kill spawns a **ghost** at the grave; you must touch it with your **necromant-ball** to revive — i.e. *killing fuels the enemy unless you also clear ghosts*, a real board puzzle. Current `Reviver`: same-layer corpses simply revive after 4 s. The ghost-touch counterplay is simplified away.
10. **Radiation → Containment Field.** Legacy: a multi-hit DoT field riding a ball. Current: a stationary **emitter-suppression** zone. Re-themed from offense to counter-hazard utility.
11. **Bosses standardized.** Legacy bosses were free-form animation stacks; current bosses share **HP-fraction phases + 4 weighted attack verbs + 0.5 s telegraphs**. More legible, less bespoke. The legacy **Statue** boss was replaced by the **Seraph** (adds + fusing vase).

---

## 3. Legacy-only mechanics — REUSE or SKIP

| Lost mechanic | Verdict | One-line rationale |
|---|---|---|
| **2-damage-type model (Magic vs Ball)** | **REUSE** | The enabler for weakness/immunity bricks; turns "break the wall" into a targeting puzzle and gives spells a clear niche vs the ball. |
| **Weakness block (GetMoreDamage ×type)** | **REUSE** | "Weak to spells / weak to ball" bricks reward kit diversity; trivially fits the crit/elemental damage pipeline. |
| **Immunity block (IgnoreTypeOfDamage)** | **REUSE** | A "ball-proof, spell-only" brick forces spell usage and varies pacing — current only has all-or-nothing Indestructible. |
| **Composable block-effect stack** | **REUSE (selective)** | The one-behavior-per-block enum is an expressiveness regression; restoring *attachable* effects (contagion, spawn-on-threshold) multiplies content cheaply. |
| **CorruptBlock keyed contagion** | **REUSE** | Chain-reaction bricks (clear one, the group cascades) are a satisfying, readable puzzle verb absent today. |
| **Engineer Rocket (mouse-piloted homing missile)** | **REUSE** | High player agency, unique "pilot a weapon" moment; art already drawn. Add as a *distinct* spell — don't overwrite Concussion. |
| **Engineer Magnet (pull blocks to ball)** | **REUSE** | Board-rearranging is a rare verb; the current ball-steer magnet doesn't cover it. |
| **Lava-eater (merging/growing lava mass)** | **REUSE** | Escalating self-growing hazard creates time pressure; Hell lava art exists. |
| **Marks / path-follower patrols** | **REUSE (M/L)** | Enables patrolling enemies and moving brick formations — variety the current static field lacks. |
| **Enemy roaming ball** | **REUSE (S)** | Cheap, distinct enemy (a hostile ball that won't drop past you); pairs well with the existing ball physics. |
| **Bat speed-boost-on-release** | **REUSE (S)** | Restores the risk→reward read; small add on top of the existing Bat. |
| **CreateOnHit progressive reveal** | **REUSE (S)** | "Crack it open to reveal X" bricks; cheap surprise/loot beats. |
| **Heaven statue full level-up/ally nuance** | **SKIP (mostly done)** | Current already ports Altar pacify + Vase level-up + Shield-corrupt; remaining nuance is marginal. |
| **Souls-as-separate-resource** | **SKIP** | Already merged into the unified mana budget; a second resource isn't needed. |
| **Fire Ring** | **SKIP** | Redundant with current Turret + Conflagration. |
| **Star Warrior Inverse** | **SKIP** | Trivial (flip ball up); `recall` already covers the useful Star Warrior verb. |
| **Sleeping Bat / KillChildrenOnDie** | **SKIP (until nesting)** | Only valuable if nested block structures return. |
| **Pot state-cycling, Beholder cosmetic gaze** | **SKIP** | Cosmetic; Beholder already exists as an emitter. |
| **Engineer Nuke-charge passive loop** | **SKIP** | Multi-step collect-charge loop is fiddly; current Overload covers "drop a nuke." |
| **DestroySomething win condition** | **SKIP** | Boss-kill objectives cover the use case. |

---

## 4. Art inventory & mapping

Art lives under `Sprites/` (tracked in git; deleted in the working tree). Heroes are foldered `Clase_1..4` = FireMage / Paladin / Engineer / **Necromancer**. There is **no Star Warrior art folder** (confirms legacy "code-only"). Bosses are **not** a separate folder — they live inside the biome `Locationes/Objects/Location_*` sets.

### 4a. Hero / spell art → mechanic → wired?

| Art set (path under `Sprites/Heroes/`) | Mechanic | In current game? | Flag |
|---|---|---|---|
| `Clase_1_FireMage/Skills/Spell_4_Phonex/*` (20-frame birth, 18-frame death, body, glow) | Phoenix | **yes** (`phoenix`) | **WIRE-UP** — rich animation for an existing spell |
| `Clase_1_FireMage/Skills/Spell_1_FireTurret/FireHeroTurret` | Turret | **yes** (`turret`) | **WIRE-UP** |
| `Clase_1_FireMage/Skills/Spell_3_FireWall/Fire*Animation*` | Fire Wall | **yes** (`firewall`, drifted) | WIRE-UP (re-skin to rising wall) |
| `Clase_1_FireMage/Skills/Spell_2_FireRing/*` | Fire Ring | **no** | **LOST-art** (skip unless revived) |
| `Clase_1_FireMage/Ball/*`, `Spell_0_PassiveFireBall/*` | fire ball/passive | partial | re-skin for ignite |
| `Clase_2_Paladin/Skills/Spell_0_PassiveShield/KnightShield` | Shield | **yes** (`shield`) | **WIRE-UP** |
| `Clase_2_Paladin/Skills/Spell_1_Spear/KnightChain*` | Spear/Lance | **yes** (drifted) | WIRE-UP |
| `Clase_2_Paladin/Skills/Spell_4_LastDay/*Clouds*, KnightLightSpell*` | Last Day storm/smite | **yes** (`lastday`) | **WIRE-UP** (cloud + bolt art) |
| `Clase_3_Engineer/Skills/Spell_4_Rocket/Rocket*, RocketFire*` | Rocket | **drifted** (Concussion, no missile) | **LOST-art / REVIVAL** — perfect for a re-added piloted rocket (§3) |
| `Clase_3_Engineer/Skills/Spell_1_Lighting/Lighting1-4, Spark, Area` | Lightning | **yes** (`lightning`,`tesla`) | **WIRE-UP** |
| `Clase_3_Engineer/Skills/Spell_3_Raditation/Radiation*` | Radiation/Containment | **yes** (drifted) | WIRE-UP |
| `Clase_3_Engineer/Skills/Spell_2_Magnet/*` (icons only, no entity art) | Magnet | **yes** | icons only |
| **`Clase_4_Necromancer/Skills/Spell_1_Skeleton/*`** — full animated **Skeleton** prefab (`Skeleton.prefab`, `.controller`, Birth/Attack/Stand/Death `.anim`), **SkeletalMage**, missiles, glow | Skeleton / Skeletal Mage | **yes** — `skeleton`=Bonewalker, `raise`, `mage`=**Lich's Gaze** | **WIRE-UP (high value)** — see §4c |
| **`Clase_4_Necromancer/Skills/Spell_4_LastDay/BoneGolem*`** (birth/death anim) | Bone Golem | **yes** (`golem`) | **WIRE-UP (high value)** |
| `Clase_4_Necromancer/Skills/Spell_0_PassiveShield/KnightShield`, `Ball/KnightHeroBall` | Necro passive/ball | partial | WIRE-UP |
| `ComunSkills/*` (Life/Shield/Size icons) | common skills | folded into stats | UI only |

### 4b. Biome / block / boss / enemy art → mechanic → wired?

| Art (under `Sprites/Locationes/Objects/`) | Mechanic | In current? | Flag |
|---|---|---|---|
| `Location_1_Hell/Demon*` (Body, Face×3, Hand1-3) | Demon boss | **yes** | WIRE-UP |
| `Location_1_Hell/HellBallSpawner*, HellBallMissile, Skull[colors]Active` | emitter + teleporters | **yes** | WIRE-UP |
| `Location_1_Hell/Lava*, LavaSpowner*` | Lava + LavaSpawner | **yes** (drifted) | WIRE-UP; eater variant is **LOST mechanic** |
| `Location_1_Hell/Standart*Hell*, HellInvulnerable*` | basic/tough/indestructible | **yes** | WIRE-UP |
| `Location_2_Dungeion/Goblin*` (Body, Head, Hand/Leg×) | Goblin boss | **yes** | WIRE-UP |
| `Location_2_Dungeion/Stalactite*, Bomb*, GrateBomb*` | stalactite, bomb | **yes** | WIRE-UP |
| `Location_2_Dungeion/DungeonCart*` | Cart | **yes (CURRENT-ONLY code)** | **WIRE-UP** — art predates the code |
| `Location_2_Dungeion/Stone*, Dungeon*Standart*, Invulnerable*` | rock/basic/union | **yes** | WIRE-UP |
| `Location_3_Village/Enemyes/Witch*` (Head, Hand×3, Magic×4, Skirt, Metla) | Witch boss | **yes** | WIRE-UP |
| `Location_3_Village/Enemyes/Bat*` (+ Ghost + Sleeping + Leg) | Bat / Sleeping Bat | Bat **yes**; Sleeping **no** | WIRE-UP bat; Sleeping = LOST |
| `Location_3_Village/Enemyes/Beholder*` (+Ghost, Missile, anims) | Beholder emitter | **yes** | WIRE-UP |
| `Location_3_Village/Enemyes/VillageDeath*, DeathSphere, Shadow*` (+Ghost) | **Necromant revive loop** (death-caster, soul sphere) | **yes** (`Reviver`) | **WIRE-UP** — this is the village necromant art |
| `Location_3_Village/Blocks/Portal*, BallGhost, *Ghost*` | Ghost portal + ghost layer | **yes** | WIRE-UP |
| `Location_3_Village/Blocks/Kotelok1-3(+Death)` | Cauldron | **yes (CURRENT-ONLY code)** | **WIRE-UP** — Kotelok = cauldron |
| `Location_3_Village/Blocks/VillagePotion*, VillageCorrupt, Metla` | potion/corrupt/broom | corrupt partial | mixed |
| `Location_4_Heavens/HeavenBoss, HeavenBossGlobe, StatueWings` | Heaven boss (→Seraph) | **yes** (reimagined) | WIRE-UP |
| `Location_4_Heavens/HeavenMeleeStatue*, HeavenDefender*(Shield)` | Melee + Shield statues | **yes** | WIRE-UP |
| `Location_4_Heavens/WindMaster*` (circle/glow anims) | Wind Master | **yes** | WIRE-UP |
| `Location_4_Heavens/HeavenAltarV2*, HeavenVaza*` | Altar + Vase | **yes** | WIRE-UP |
| `Location_4_Heavens/Column*(Top/Bottom/Damaged/anim)` | sliding column | **yes** | WIRE-UP |
| `Location_4_Heavens/HolyBall, Missile, Shield, Cloud(s)` | holy projectiles/fx | **yes** | WIRE-UP |
| `Locationes/Fons/*`, `Project Images/*` (app icons, cursor, banner) | backgrounds / store assets | n/a | utility |

### 4c. The two gaps the brief flags

- **Necromancer — legacy was ART-ONLY (no C#).** That is now an **opportunity, not a gap**: the rewrite has a **fully coded** Necromancer, and the legacy art *directly fits it*:
  - `Skeleton.prefab` (fully animated: Birth / Stand / Attack / Death) → current **`skeleton` = Bonewalker** (rooftop-walking minion) and **`raise`** helper-balls.
  - `SkeletalMage*` (+ birth/death/missile/glow) → current **`mage` = Lich's Gaze** (the cursing caster).
  - `BoneGolem*` (birth/death anim) → current **`golem` = Bone Golem**.
  - `KnightShield`, `KnightHeroBall`, `Necr1-4` bars, `NecrHeroIco` → passive, ball skin, HUD.
  This is the single highest-ROI art job: the mechanics already work; they just need their drawn assets imported.
- **Star Warrior — legacy was CODE-ONLY (no art).** The hero doesn't exist in the rewrite, and there is **no Star Warrior sprite folder**. Its one useful verb (Back) already survived as the neutral `recall`. Reviving Star Warrior as a hero would require **new art from scratch** plus re-adding `Inverse` — low priority.

---

## 5. Prioritized "what to reuse from the previous game" — top 8

Ordered by ROI. Effort: **S** ≤ a day-ish, **M** a few days, **L** a week+. None touch the (settled) economy.

| # | Reuse | Effort | Payoff | Why now |
|---|---|---|---|---|
| 1 | **Import the Necromancer art set onto the existing coded spells** (Skeleton→Bonewalker/raise, SkeletalMage→Lich's Gaze, BoneGolem→golem, shield/ball/bars/icon) | **S** | **High** | Mechanics already ship; this is mostly asset import + render wiring. Closes the most visible art gap. |
| 2 | **Wire legacy art for already-coded-but-art-less blocks**: Cart (`DungeonCart`), Cauldron (`Kotelok`), village Necromant (`VillageDeath/DeathSphere`) | **S** | **High** | Code exists; drawn art exists; they're currently unconnected. Pure win. |
| 3 | **Re-introduce the 2-damage-type model + weakness/immunity bricks** (Magic vs Ball; GetMoreDamage / IgnoreTypeOfDamage) | **M** | **High** | Biggest *design* loss; turns wall-clearing into a targeting puzzle and gives spells a reason-to-exist vs the ball. Fits crit/elemental pipeline. |
| 4 | **Restore a composable block-effect layer** (start with CorruptBlock contagion + CreateOnHit threshold reveal) | **M/L** | **High** | Reverses the one-behavior-per-block expressiveness regression; multiplies content per unit of code (the *right* way — bespoke behaviors stay bespoke). |
| 5 | **Re-add Engineer Rocket as a piloted homing missile** (distinct spell, not replacing Concussion) | **M** | **Med/High** | Unique high-agency moment; **art already drawn** (`Rocket*`, `RocketFire*`). |
| 6 | **Add a block-pulling Magnet variant** (yanks bricks toward the ball) | **M** | **Med** | Restores a board-rearrange verb the current ball-steer magnet abandoned. |
| 7 | **Lava-eater hazard** (merging/growing lava mass) + **roaming enemy ball** | **M** | **Med** | Two distinct escalation/threat verbs; Hell + ball physics already in place; lava art exists. |
| 8 | **Path-follower patrols + Bat speed-boost-on-release** | **M** (paths) / **S** (bat) | **Med** | Patrols add field variety beyond the static grid; the bat tweak restores its risk→reward read. |

---

## Appendix — counts at a glance

| | Legacy | Current |
|---|---|---|
| Heroes (coded) | 3 + Necro(art) + StarWarrior(2 spells) | 4 |
| Active spells | 15 coded | 26 |
| Block effects / behaviors | 9 composable effects + ~16 themed systems | 18 fixed behaviors |
| Damage types | **2 (Magic, Ball)** | 1 (+crit/elemental tags) |
| Bosses | 4 (Goblin, Witch, Statue, Demon) | 4 (Demon, Goblin, Witch, Seraph) |
| Equip passives | 18 Items | 17 Relics + 20 Cards + 12 Modules |
| Campaign | per-hero branching roads | linear 48-node chain (settled) |
| Currencies | LibrePoints/Crystals/SpellPoints/Treasure | Sparks/Souls/Insight (settled) |
| Sprite files tracked | 1606 (incl. `.meta`) | — |
