# Current Game Mechanics — Comprehensive Reference

Generated from the live code/config (backend `Arkanoid.Core` + `config/*.json`) on 2026-06-16. Numbers are read directly from source; a Fire-Mage spell rebalance is in flight, so spell damage/mana values may drift slightly. Each entry lists **name · behavior/identity · key params · source file**.

Conventions: "deflect" = ball touches paddle; `t` = normalized paddle hit offset (−1..1); cell size = 32px; tick = 1/60s. Mana costs below are from `config/characters.json` (the loaded file; `CharacterCatalog.DefaultJson` carries cheaper fallback values).

---

## 1. Heroes / Classes

Source: `config/characters.json`, `backend/Arkanoid.Core/Meta/CharacterCatalog.cs`, base stats in `backend/Arkanoid.Core/Meta/HeroStats.cs` (`StatResolver`).

Each hero has a **signature** spell locked in hotbar slot 0 (never draftable), a default **starting** loadout, and a full themed **spells** list. Non-signature spells form a **global shared pool** any hero can roll/equip (economy rework). Hotbar = signature + up to 3 flex slots (`Loadouts.MaxSlots = 4`).

| Hero | Signature | Starting kit | Passive | Element (affinity) |
|------|-----------|--------------|---------|--------------------|
| **Fire Mage** (`fire_mage`) | `ignite` | ignite, fireball (Conflagration), firewall | Ignited kills spread fire to neighbours | fire |
| **Paladin** (`paladin`) | `shield` | shield, spear (Lance of Dawn), duplicate | Once per level, a lost ball is saved (wall save) | holy |
| **Engineer** (`engineer`) | `overload` | overload, lightning, rocket (Concussion) | Mana regenerates faster (×1.5) | tech |
| **Necromancer** (`necromancer`) | `raise` | raise, decay (Rot & Collapse), drain | Killing blocks grants extra mana/souls (×2) | death |

Full per-hero themed spell lists (all 6 each) are catalogued in §2.

**Base stat profiles** (Lvl 1 / ★0; `Power, Vitality(HP), CritChance, CritDamage, Multiball, Tempo`):
- Fire Mage: 3, 3, 0.12, ×1.7, 0, ×1.1 (crit-leaning glass cannon)
- Paladin: 3, 6, 0.04, ×2.2, 0, ×0.9 (tanky, big rare crits)
- Engineer: 2, 4, 0.06, ×1.5, **1**, ×1.2 (starts with a multiball; fast regen)
- Necromancer: 2, 5, 0.08, ×2.0, 0, ×1.0

Per-hero ★ perks (`StatResolver`): stat-flat perks fold into `Resolve`; behavioral perks (`PerksFor`) gate sim hooks: FM ★3 ignited blocks +15% crit dmg, ★5 crit-kill ignites a neighbour; Paladin ★3 first ball-drain saved, ★5 below-50%-HP +25% crit dmg; Engineer ★1 +tempo, ★3 +1 starting ball (★5 deferred); Necro ★1 heal 1 HP/60 kills, ★3 crits drain +4 mana, ★5 full-combo kill may raise a helper-ball.

---

## 2. Spells

Catalog: `Spells/SpellCatalog.cs` (`DefaultJson` holds behavioral params); dispatch: `Sim/Systems/SpellSystem.Archetypes.cs` (`Cast`). Five archetypes — Projectile, Imbue, TimedAura, Placement, Instant — but **many spells are bespoke** and route to their own system before the archetype switch. Leveling adds `damagePerLevel`/`durationPerLevel`/etc.; cap = level 10 (`SpellService.MaxSpellLevel`). Matching-element hero pays ×0.8 mana (`SpellAffinity`).

### Fire Mage

| Spell (id) | Display | Archetype | Behavior / identity | Key params | Mana | System file |
|------------|---------|-----------|---------------------|-----------|------|-------------|
| `ignite` | Ignite | Imbue | Arms next deflect: ball-hits **light** blocks (no direct dmg) — a slow DoT that **creeps** one block/2.5s. Signature spread mechanic. | hits 4 (+1/lvl); burn 1 dmg/7s; chain = SpreadBlocksBase(2)+lvl; per-block burn cap min(6, 2+lvl) | 25 | `BurnSystem.cs`, `SpellSystem.cs` (`ApplyIgniteOnDeflect`) |
| `fireball` | **Conflagration** | Instant (bespoke) | NOT a projectile: **detonates every burning block at once** + chains fire from kills. Self-sufficient — with no fire it bursts the cluster around the ball (radius 2.5 cells). Empty board fizzles. | damage 6 (+2/lvl); chains via `BurnSystem.LightNeighbours` | 60 | `ConflagrationSystem.cs` |
| `firewall` | Fire Wall | Placement | Wall spawns **at the ball's height** and sweeps **upward**, burning blocks it passes. Fizzles (no mana) if cast while ball rests on paddle. | lifetime 3.6s; rise 150px/s; dmg 2 (+1/lvl) per 0.25s; band ±24px | 80 | `SpellSystem.Archetypes.cs` (`firewall`) + `UpdateFireWalls` |
| `turret` | Turret | TimedAura | Paddle-mounted: fires one upward bolt **each time you catch the ball** (NOT on a timer). | duration 7s (+1/lvl); bolt dmg 2, speed 460, radius ×0.6 | 30 | `SpellSystem.cs` (`FireTurretBolt`, fires in `OnPaddleHit`) |
| `phoenix` | Phoenix | TimedAura (bespoke) | Visible entity that **orbits a ball**, scorching blocks it sweeps. Re-targets if its ball drains. | duration 6s (+1/lvl); orbit radius 56; ang speed 3 rad/s; hit radius 1.3 cells; dmg 2 per 0.4–0.45s | 70 | `PhoenixSystem.cs` |
| `ashfall` | Ashfall | TimedAura (bespoke) | Timed buff: while active, every **ignite-kill** (burning block destroyed) rains a vertical ember down its column. | duration 6s (+1/lvl); ember dmg 2, speed 420, pierces 3 blocks | 40 | `AshfallSystem.cs` (rain hook in `BlockDamage`) |

### Paladin

| Spell (id) | Display | Archetype | Behavior / identity | Key params | Mana | System file |
|------------|---------|-----------|---------------------|-----------|------|-------------|
| `shield` | Shield | Placement | Conjures a barrier above the paddle that **reflects a downward ball back up** (pit save). | lifetime 4s (+0.5/lvl); width ×1.2 paddle | 25 | `SpellSystem.Archetypes.cs` (`barrier`) + `BallSystem.ResolveBarriers` |
| `spear` | **Lance of Dawn** | TimedAura (bespoke) | NOT a piercing projectile: drops a temporary **solid pillar** mid-lane (0.55 height) to bank trick shots off. No damage. Max 3 active. | lifetime 5s (+1/lvl); width 0.8 cell, height 4 cells, at 0.55× board height | 15 | `LanceSystem.cs` |
| `duplicate` | Duplicate | Instant | Splits the lead ball into N **smaller** clones (×0.8 radius), fanned 15°·n. Inherits ignite/decay charges. | copies 1 (+1/lvl) | 40 | `SpellSystem.Archetypes.cs` (`duplicate`) |
| `penetration` | Penetration | Imbue | Arms next deflect: hits **punch straight through** blocks (no bounce) for N phases. | hits 3 (+1/lvl) → `PhasesLeft` | 25 | `SpellSystem.KitSpells.cs` (`ApplyPenetrationOnDeflect`) |
| `lastday` | Last Day | TimedAura (bespoke trigger) | While active, each **ceiling bounce** smites the ball's column (judgment). Inter-bounce cooldown. | duration 8s (+1/lvl); col dmg 2; cooldown 0.5s | 80 | `SpellSystem.KitSpells.cs` (`OnTopWallBounce`) |
| `reckoning` | Reckoning | Instant (bespoke meter) | Arms a vengeance meter: every HP lost charges it; at threshold it auto-smites 5 evenly-spaced columns. | threshold 3 HP (−1/lvl, min 1); smite dmg 3 (+1/lvl); 5 columns | 35 | `ReckoningSystem.cs` (`OnHpLost`) |

### Engineer

| Spell (id) | Display | Archetype | Behavior / identity | Key params | Mana | System file |
|------------|---------|-----------|---------------------|-----------|------|-------------|
| `overload` | Overload | Placement | Plants a **bomb-block** at the paddle's column that chain-detonates neighbours. Denied if cell occupied. | aoe radius 1 cell (+1/lvl); placement row = Rows−3; bomb dmg from `Enemies.BombDamage` (3) | 35 | `SpellSystem.Archetypes.cs` (`bomb`) + `BlockDamage.Explode` |
| `lightning` | Lightning | Instant | Strikes a random block; chains to nearby blocks. | dmg 2 (+1/lvl); 6 jumps (+1 with Conductor relic); chain radius 110 | 25 | `SpellSystem.Archetypes.cs` (`lightning`) |
| `rocket` | **Concussion Charge** | Instant (bespoke) | Deals **NO damage**. Detonates at paddle: knocks balls **away from blast** (always upward — a save) and **yanks falling pickups** toward you. | knockback radius 180 (+20/lvl); yank radius ×1.6; yank speed 240 | 30 | `ConcussionSystem.cs` |
| `radiation` | **Containment Field** | Placement (zone) | Auto-deploys onto nearest **emitter** (else above paddle); **suppresses** emitters caught inside and melts blocks over time. | lifetime 4s; radius 140; dmg 1 (+1/lvl) per 0.5s; suppresses emitters | 45 | `SpellSystem.ClassSpells.cs` (`UpdateZones`); suppression in `EmitterSystem.IsContained` |
| `magnet` | Magnet | TimedAura | Steers balls toward nearest block. | duration 4s (+1/lvl); steer 120°/s | 20 | `EffectSystem.cs` (`UpdateMagnet`) |
| `tesla` | **Tesla Grid** | Projectile-tag (bespoke) | Armed for the level: each **side-wall bounce** charges that wall; **both charged → horizontal lightning curtain** fries the frontmost band, then resets. | dmg 3 (+1/lvl); band = front row +2 above; curtain cooldown 0.5s | 80 | `TeslaGridSystem.cs` (`OnWallBounce`) |

### Necromancer

| Spell (id) | Display | Archetype | Behavior / identity | Key params | Mana | System file |
|------------|---------|-----------|---------------------|-----------|------|-------------|
| `raise` | Raise | Instant | Summons friendly **skeleton helper-balls** served from the paddle. | copies 1 (+1/lvl); radius ×0.85 | 35 | `SpellSystem.Archetypes.cs` (`raise`) |
| `decay` | **Rot & Collapse** | Imbue | Arms next deflect: hits **ROT** blocks (permanently −2 max HP) and a rotted kill **collapses the column** above into the gap. Also spreads chip damage. | hits 4 (+1/lvl); RotMaxHpLoss 2; spread range 2 / chip 2 | 15 | `BlockDamage.cs` (`SpreadDecay`), `GravitySystem.CollapseColumn`, `SpellSystem.cs` (`ApplyDecayOnDeflect`) |
| `drain` | Drain | TimedAura | While active, each kill drains extra mana (souls) to you. | duration 6s (+1/lvl); +6 mana/kill | 20 | `SpellSystem.ClassSpells.cs` (`DrainBonusMana`) |
| `skeleton` | **Bonewalker** | TimedAura (bespoke) | Minion that **walks the block rooftops**, meleeing the block underfoot as it strides. | duration 5s (+1/lvl); walk 60px/s; melee 3 dmg per 0.5s | 40 | `BonewalkerSystem.cs` |
| `golem` | **Bone Golem** | TimedAura (bespoke) | Bodyguard that rises from paddle, **climbs a column bulldozing blocks** and **soaks enemy hazards** until its HP runs out. | HP 8 (+2/lvl); climb 70px/s; bulldoze 4 dmg per 0.15s | 70 | `BoneGolemSystem.cs` |
| `mage` | **Lich's Gaze** | TimedAura (bespoke) | Slow **lighthouse beam** at the paddle that sweeps an arc, **cursing** blocks it crosses; cursed blocks take bonus ball damage. | duration 4s (+1/lvl); curse bonus 2 (+1/lvl); arc ±~75° | 45 | `LichGazeSystem.cs`; curse bonus in `Modifiers.BallDamage` |

### Neutral pool (any hero)

| Spell (id) | Display | Archetype | Behavior | Key params | Mana | System |
|------------|---------|-----------|----------|-----------|------|--------|
| `recall` | Recall | TimedAura | Steers balls back toward the paddle (anti-drain save). | duration 2.5s (+0.5/lvl); steer 240°/s | 15 | `EffectSystem.cs` (`UpdateRecall`) |
| `slowtime` | Slow Time | TimedAura | Slows every ball (×0.5 of ramped speed). | duration 4s (+0.5/lvl) | 20 | `EffectSystem.cs` (`UpdateSlowTime`) |

**Reworked spells (old → new):** Fireball→**Conflagration** (board detonation, not a projectile), Rocket→**Concussion Charge** (no-damage save/yank), Spear→**Lance of Dawn** (banking pillar), Skeletal Mage→**Lich's Gaze** (cursing beam), Skeleton→**Bonewalker** (rooftop minion), Bone Golem (climbing bodyguard, not a projectile), Radiation→**Containment Field** (emitter suppression), Lightning gained **Tesla Grid** as the signature ultimate, Decay→**Rot & Collapse** (gravity), plus new **Ashfall** / **Reckoning** / **Tesla Grid**.

---

## 3. Blocks & Behaviors

Config: `config/blocks.json` (73 type entries). Entity: `Entities/Block.cs`; behavior enum: `BlockBehavior` (18 values). A block has exactly **one** behavior; structural flags (`Indestructible`, `BallPhases`, `NeedToKill`) and parametric fields are orthogonal. Damage routed through `BlockDamage.DamageBlock`.

### Behaviors (`BlockBehavior`)

| Behavior | Identity | Key params / source |
|----------|----------|---------------------|
| `None` | Plain block (basic/tough/elite/corner/column). 1/3/5 HP tiers; elite renders cold-tinted. | `blocks.json` |
| `Boss` | Multi-pattern phased fight; fires hazards. | `BossSystem.cs` (§4) |
| `Teleporter` | Ball warps to next same-colour partner (color 0/1/2). Cooldown 18 ticks. | `BallSystem.cs` |
| `Emitter` | Periodically fires a hazard **straight down its column** (homing removed; EmitAim retained as data). | `EmitterSystem.cs` |
| `Bomb` | On death, damages all blocks within `ExplodeRadius` (Chebyshev), chaining other bombs. Bomb dmg 3. | `BlockDamage.Explode` |
| `Stalactite` | Detaches into a falling hazard when a ball passes beneath; 0.35s shake telegraph first; pierces blocks while falling. | `StalactiteSystem.cs` |
| `Reviver` | Necromant: while alive, killed **same-layer** blocks revive after 4s. Regular↔regular, ghost↔ghost only. | `ReviverSystem.cs` |
| `WindMaster` | Continuously pushes balls away (force 900, radius 110, linear falloff; speed-preserving deflection). | `WindSystem.cs` |
| `ShieldStatue` | Pulses every 3.5s, granting nearby blocks (radius 2) 2.5s damage immunity. Allied (via Altar) version **corrupts** instead. | `ShieldSystem.cs` |
| `Portal` | Toggles the ball's ghost phase; ball passes through. Cooldown 18 ticks. | `BallSystem.cs` |
| `Bat` | Snatches the ball and carries it toward the drain; pop with a 2nd ball / projectile / paddle touch to free it. | `BallSystem.cs`, `CombatSystem.UpdateBatCarrier` |
| `Lava` | Indestructible; **drains 1 HP per 3s** while in the bottom danger zone. Ball **destroys** the lava cell it flies over (counterplay). | `LavaSystem.cs`, `BallSystem.cs` |
| `Altar` | Ball-hit **pacifies (allies)** Heaven statues for 8s; bounces as solid. | `BallSystem.PacifyStatues` |
| `Vase` | On death, **levels up** all statues (faster fire, bigger kill reward); drops mana-surge pickup. | `BallSystem.LevelUpStatues` |
| `Cart` | Periodically rolls a horizontal **ball-deflecting** obstacle above the paddle (0.8s edge telegraph; no paddle damage). | `EmitterSystem.LaunchCart`, `CombatSystem.DeflectBallsOffCart` |
| `Cauldron` | Siphons player mana while alive (6/s); **refunds all stored mana on death**. | `CauldronSystem.cs` |
| `LavaSpawner` | After first hit, creeps lava into adjacent cells (1 per 6s, max 6); killing it **retracts** all its crept lava. | `LavaSystem.cs` |
| `BossVase` | Seraph-summoned; self-shatters after an 8s fuse and levels his adds; killing it defuses. | `BossSystem.UpdateVaseFuses` |

### Notable block types (`blocks.json`)

| Type id | Biome | HP | Behavior / flags | Notes |
|---------|-------|----|------------------|-------|
| `*_basic` | all | 1 | None | standard fill |
| `*_tough` | all | 3 | None; some `forcedDropEffect` | drops wide/shield/etc. on death |
| `*_elite` | all | 5 | None, `elite` | hardened tier |
| `*_corner_*` | all | =basic/tough | None | cosmetic chamfer caps (flipX/flipY) |
| `hell_obsidian`, `cavern_rock`, `heaven_statue` | — | 1 | Indestructible | walls |
| `hell_teleporter` / `_blue` / `_green` | hell | 1 | Teleporter (color 0/1/2), indestructible | |
| `hell_ballspawner` | hell | 6 | Emitter (1.8s, "hellball") | |
| `cavern_bomb` | caverns | 1 | Bomb (radius 1); drops multiball | |
| `cavern_stalactite` | caverns | 1 | Stalactite, indestructible | |
| `cavern_union` | caverns | 1 | None, `union` | bridge — collapses together (`CollapseUnion`) |
| `cavern_cart` | caverns | 1 | Cart, indestructible | |
| `village_beholder` | village | 4 | Emitter (1.6s, "beholdermissile") | aims "ball" (data only) |
| `village_necromant` / `_ghost` | village | 6 | Reviver (ghost = `ballPhases`) | symmetric necromancy |
| `village_ghost` | village | 1 | None, `ballPhases` | only a phased ball hits it |
| `village_portal` | village | 1 | Portal | |
| `village_bat` | village | 1 | Bat | |
| `village_cauldron` | village | 3 | Cauldron; drops fireshot | |
| `heaven_melee_statue` | heaven | 5 | Emitter (2.0s, "heavenmissile"), aim paddle → statue | pacifiable |
| `heaven_shield_statue` | heaven | 5 | ShieldStatue | |
| `heaven_windmaster` | heaven | 4 | WindMaster | |
| `heaven_altar` | heaven | 1 | Altar, indestructible | |
| `heaven_vase` | heaven | 2 | Vase; drops mana-surge | |
| `heaven_column_top/mid/bottom` | heaven | 4 | None | columns (+1 dmg w/ Pillar Doctrine) |
| `hell_lava` | hell | 1 | Lava, indestructible | |
| `hell_lava_spawner` | hell | 2 | LavaSpawner | |
| `*_boss` (4) | each | 24–26 | Boss | Demon/Goblin/Witch/Seraph |

Runtime-spawned types: `overload_bomb` (Overload spell), `boss_vase` / `seraph_add` (Seraph), crept `hell_lava` (spawner).

---

## 4. Bosses

Source: `Sim/Systems/BossSystem.cs`; tunables in `SimConfig.BossConfig`. One boss system, 4 biome variants (`BossKind`). HP fraction drives 3 phases (P2 ≤0.60, P3/enrage ≤0.30); attack interval 1.6 / 1.2 / 0.75s; every attack has a 0.5s telegraph.

**Shared attack verbs** (`BossPattern`): randomly chosen, weighted by phase:
- **AimedShot** — a hazard drops from the boss toward the paddle column (no live homing; `HazardAimStrength = 0` → falls straight). Dmg 1, speed 240.
- **Rain** — 3 hazards at random X columns.
- **Spread** — 4-shot fan, ±35°.
- **Summon** — single fast aimed shot (×1.6 speed) — or the biome-special below.

**Biome specials:**

| Boss | Kind | Signature behavior |
|------|------|--------------------|
| **Demon** (Hell) | Hell | AimedShot = **Fist Slam**: telegraphs the paddle's column, then smashes it — damages every block in the column (1) and the player (1) if under it. |
| **Goblin King** (Caverns) | Caverns | **Hops** between 3 anchors each cycle (`GoblinHopOffset = 2`); Rain = scattered **stalactites** (`StalactiteSystem.BossDrop`). |
| **The Witch** (Village) | Village | AimedShot = **Witch Grab**: a homing grab-hand seizes a ball, carries it to the boss, holds 1.2s, then hurls it at the paddle (×1.4 speed) — catch it. Poppable. |
| **Seraph** (Heaven) | Heaven | Summon alternates **adds** (melee statues, max 2, 3 HP) and a **BossVase** (8s fuse → levels his adds; kill to defuse). |

---

## 5. Ball / Paddle / Player / Combat

Sources: `Physics/BallPhysics.cs`, `Sim/Systems/BallSystem.cs`, `CombatSystem.cs`, `SpellSystem.cs`, `SimConfig.cs`.

### Ball & paddle physics
- **Ball speed**: starts 360, **accelerates over 60s up to 750** (time-based ramp `RampedBallSpeed`, replaces the old per-brick ramp); New Game+ adds per-tier speed.
- **Ball radius** 5; **damage** base 1 (overridden by hero Power). Start with `StartBalls = 2` spares.
- **Paddle**: width **52** (~20% of board, shrunk from 96→64→52), height 16, max deflect angle 60° (`+DeflectAngleBonusDeg`). Deflect angle = `t × maxAngle`, always upward.
- **CCD**: balls moving >½ cell/tick sub-step to prevent tunneling.
- **Min-angle guards**: 20° from horizontal after wall/block bounces (`EnforceMinAngle`); 0.30 min vertical ratio off the paddle (`ClampVertical`).
- **Block reflection**: dominant-penetration-axis flip; one block per tick (deterministic), 3×3 neighbourhood scan.

### Deflect & mana
- **Perfect deflect**: `|t| < 0.18` (`PerfectDeflectBand`) → +8 mana (`ManaPerfectDeflectBonus`), `PerfectDeflect` event, drives crit cards/modules. Overcharge relic adds bonus.
- **Mana**: max 100; **regen 4/s** (full ≈25s; Engineer ×1.5, Tempo stat, mana_battery, Channeling card modulate); **+1 per kill** (Necro ×2; +item bonuses; +Drain). Mana is now a real budget (regen 14→4, kill 4→1 in the difficulty rework).
- **Combo**: every kill `Count++`, multiplier = min(4, 1 + Count/3); **resets on any paddle contact**. Gates many cards.

### Crit (stat engine §5.7)
- On a killable, non-indestructible block: `Rng < CritChance` → `dmg × CritDamage` (rounded), raises `Crit` event, sets `LastHitWasCrit`. Applies to bosses too. Caps: crit chance ≤0.75, crit dmg ×1.0–4.0.

### Damage to player & i-frames
- `DamagePlayer`: blocked while `_damageImmunity > 0`; on hit sets **1.5s i-frames** (`DamageImmunity`), raises `PlayerHit`, charges Reckoning + Martyr's Brand. HP 0 → Lost.
- **Saves on full drain** (in order, `WinLoseSystem`): Shield power-up auto-save → Paladin passive wall-save (1/level) → Paladin ★3 drain-save → consume a spare ball → else Lost.
- **Second Wind** relic negates the first HP loss each level.

### Multiball / serve
- `StatMultiball` (0–2) extra balls served on top of the main ball; Engineer starts with 1, Juggler mastery adds more.
- Ball-cores on serve (`ApplyBallCoresOnServe`): ghost (phase charges), ember (ignite 2), split (extra ball); fusions stack (echo/frost = Stasis, ghost/split, heavy/ember = Molten).

### Win/lose objectives (`WinLoseSystem`)
- Default: all `needToKill` blocks dead → **Won**. Multi-floor (Caverns) slides next floor in; Rift mode pauses for a 1-of-3 modifier draft.
- `SurviveTime` (Heaven Judgement) → win on timer. `TimeLimit` (Caverns Demolition) → lose if it expires.
- **Overrun loss**: Hell descend pacing or Pressure Cooker module pushing a needToKill block onto the bottom row.

---

## 6. Enemies / Hazards

Sources as noted; tunables in `SimConfig.EnemiesConfig`. Hazards live in `g.Hazards` (`Projectile` with `HazardBehavior`). Default hazard speed 210, dmg 1, radius 8.

| Hazard / enemy | Behavior | Key params | Source |
|----------------|----------|-----------|--------|
| **Emitter shot** | Fires straight down the emitter's column on cadence; 0.5s telegraph window. Suppressed inside a Containment Field; allied (Altar) statues fire **ally bolts** at blocks instead. | default interval 2.5s; vase-level haste | `EmitterSystem.cs` |
| **Stalactite** | Falls when ball passes beneath (0.35s shake); pierces blocks one-per-tick on the way down. | fall 260px/s; arm 0.35s; block dmg 1 | `StalactiteSystem.cs` |
| **Bat carrier** | Carries grabbed ball toward drain; rescue = 2nd ball/projectile/paddle. | carry 70, flyaway 140px/s | `BallSystem`, `CombatSystem` |
| **Witch grab-hand** | Homes → grabs → carries to boss → holds 1.2s → throws at paddle ×1.4. Poppable while carrying. | grab speed 160 | `BossSystem`, `CombatSystem` |
| **Cart** | Rolls left→right above paddle, **deflects** balls (no paddle damage). | speed 150, interval 4s, telegraph 0.8s, radius ×1.6 | `EmitterSystem`, `CombatSystem` |
| **Lava drain** | 1 HP per 3s while lava sits in the bottom danger zone. | interval 3s | `LavaSystem` |
| **Lava creep** | Spawner creeps 1 lava cell per 6s (max 6) after first hit; retracts on death. | | `LavaSystem` |
| **WindMaster push** | Bends ball heading away (speed preserved). | force 900, radius 110 | `WindSystem` |
| **Shield statue pulse** | Grants neighbours 2.5s immunity every 3.5s (radius 2). | | `ShieldSystem` |
| **Cauldron siphon** | Drains 6 mana/s; refunds on death. | | `CauldronSystem` |
| **Reviver (necromant)** | Revives same-layer corpses after 4s. | | `ReviverSystem` |
| **Beholder / hell spawner / melee statue** | Emitter variants (see §3). | | `EmitterSystem` |

Hazards that reach the paddle deal `DamagePlayer`; a moving **Riposte Paddle** module parries them back up instead.

---

## 7. Relics / Cards / Modules

### Relics (17) — `config/relics.json`, `Relics/RelicCatalog.cs`, effects in `Sim/Modifiers.cs`

| Relic | Effect |
|-------|--------|
| Glass Cannon | +1 ball damage, −1 life |
| Flint Core | +1 ball dmg vs blocks ≥3 HP |
| Pyroclasm | Fire spread harder (+1 burn) & farther (diagonals) |
| Mana Battery | +50 max mana, ×1.6 regen |
| Conductor | Lightning chains +1 block |
| Overcharge | Perfect deflect → +8 bonus mana |
| Split Shot | Every 6th block killed splits an extra ball |
| Souljar | Every 5th block killed pays a crystal |
| Lodestone | Falling bonuses drift to the paddle (speed 60) |
| Ember Heart | Ignite lasts +2 hits |
| Second Wind | First HP loss each level negated |
| Midas Touch | Caught bonuses pay +2 crystals |
| Lead Paddle | Paddle +25% wide, regen −25% |
| Sapper's Charge | Bomb blasts reach +1 cell |
| Hellwalker | Once per serve, lava rebounds the ball |
| Ghost Lens | Ghost-phased ball +1 damage |
| Pillar Doctrine | Statues & columns take +1 ball damage |

### Cards (20) — `config/cards.json`, `CardSystem.cs` (rarity: 7 common-ish… actual: common/rare/epic/mythic). All are bespoke passive triggers; per-level scaling via `CardLevel` (cap 10).

| Card | Rarity | Trigger / effect |
|------|--------|------------------|
| Headhunter | common | +1/lvl dmg to TOP-row blocks |
| Underdog | common | +1/lvl dmg to bottom-two-row blocks |
| Opening Gambit | common | First kill/level detonates a small AoE (2 +1/lvl) |
| Dead Center | common | Perfect deflect arms a +3/lvl burst on next block hit |
| Cleanup Crew | rare | ≤6 blocks left → +2/lvl ball dmg |
| Bank Shot | rare | Each wall bounce banks +1 dmg (cap 3+lvl) onto next hit |
| Executioner's Edge | rare | Crit on low-HP block (≤20+5·lvl %) executes for double |
| Avalanche | rare | Combo ≥8 kill may crush the block below (3+lvl) |
| Keystone | rare | Load-bearing kill may collapse the column above |
| Martyr's Brand | rare | After losing HP, +2/lvl ball dmg for 3+lvl s |
| Ricochet | rare | Side-wall bounce may fire a row-crossing bolt |
| Sleight of Hand | rare | Centre-catch a pickup → it duplicates |
| Channeling | rare | Regen pauses while ball aloft, doubles while cradled low |
| Overkill | epic | >2× overkill spills the excess to the block behind |
| Metronome | epic | Consecutive perfect deflects stack +1 dmg (reset on miss) |
| Phase Window | epic | Combo ≥15(−2/lvl) → ball phases through blocks |
| Domino | epic | 3+ kills in 1s → next kill chain-explodes |
| Hot Hand | epic | Ball grows each 5-kill combo milestone |
| Redline | epic | Longer aloft → faster (+40% cap) & +dmg; reset on catch |
| Erosion | mythic | Ball cracks indestructible walls (~16 hits, fewer/lvl) |

### Modules (12) — `config/modules.json`, `ModuleSystem.cs`. Slot-bound passives (slots: core/ball/paddle/field); per-level via `ModuleLevel` (cap 5).

| Module | Slot | Rarity | Effect |
|--------|------|--------|--------|
| Tidal Core | core | epic | Alternates HEAVY (+dmg) / SWIFT (+speed) each deflect |
| Twin Soul Core | core | legendary | Two tethered twins (softer); tether slices blocks; lose one → tether dies |
| Fission Core | core | legendary | Ball splits on kills, re-fuses bigger on catch |
| Hollow Ball | ball | rare | Big light ball: wide coverage, −1 dmg, jittery |
| Brittle Glass Ball | ball | epic | Huge dmg (+3+2/lvl) but shatters on indestructibles |
| Spin-Loaded | ball | rare | Off-centre catch imparts curving spin (decays) |
| Gyro Paddle | paddle | epic | Deflect angle driven by paddle movement speed |
| Drumhead Paddle | paddle | rare | Perfect deflect → column shockwave (2+lvl) |
| Riposte Paddle | paddle | epic | Moving paddle parries hazards into a bolt (2+lvl) |
| Gravity Well | field | epic | Ball pulled toward the block-field centroid |
| Toll Roads | field | rare | Only crit / post-perfect kills pay gold — and pay double |
| Pressure Cooker | field | epic | Field creeps down (overrun = loss); kills shove it back up |

---

## 8. Economy / Progression

Sources: `Meta/Wallet.cs`, `RollService.cs`, `Upgrades.cs`, `HeroStats.cs`, `RiftService.cs`, `Loadouts.cs`, `config/campaign.json`, `config/dungeons.json`. Authoritative model: `docs/2026-06-14-economy-rework-proposal.md`.

### Currencies (`Currency`)
- **Sparks** — cards + modules. **Souls** — spells, heroes, spell-slot unlocks, mastery respec. **Insight** — mastery levels.
- Live-ops: Medals, EventTokens, SeasonTokens. Legacy (migration-only, no sources): Crystals, Shards, Points, CampaignGold, CardDust, ModuleCores. In-run **Gold** lives on the GameInstance, not the wallet.

### Rolls (`RollService`) — fixed-price pure-random over the full pool; duplicates **level** the item / **ascend** the hero; a roll onto a maxed entry is **Wasted** (no dupe protection).
| Roll | Cost | Currency | Pool / dupe rule |
|------|------|----------|------------------|
| Card | 30 | Sparks | all cards; dupe banks a copy (cap lvl 10) |
| Module | 40 | Sparks | all modules; dupe banks a copy (cap lvl 5) |
| Spell | 50 | Souls | **global non-signature pool** (`cat.Pool()`); cap lvl 10 |
| Hero | 80 | Souls | `HeroPool` (boss-unlocked); dupe banks an **ascend pip** |

Leveling is **manual** (spend banked copies/pips): `SpellService`/`CardService`/`ModuleService.TryLevelUp`, `Upgrades.TryAscendHero`.

### Loadout & affinity
- Hotbar = signature (slot 0, locked) + up to 3 flex slots. Slot unlock: `Upgrades.TryUnlockSpellSlot`, cost = `currentSlots × 40` Souls (cap 4 slots). FIFO when full.
- **Spell affinity**: a spell on its matching-element hero pays ×0.8 mana (`SpellAffinity.MatchManaMult`).

### Mastery (account-wide, `StatResolver` + `Upgrades`)
| Node | Max | Per-level |
|------|-----|-----------|
| Sharpshooter | 5 | +0.01 crit chance |
| Brutality | 5 | +0.05 crit damage |
| Conditioning | 3 | +1 vitality (HP) |
| Juggler | 2 | +1 multiball |
| Momentum | 5 | +0.02 tempo |
- Cost: `25 × (curLevel+1)` Insight. Respec: 60 Souls (refunds spent Insight).

### Hero leveling / ascension
- XP curve: `80 × 1.12^(lvl-1)`, levels 1–30. ★ multiplier = `1.08^stars` (★0–★6, ★6 ≈ ×1.59). Star token costs (banked pips): 10/20/40/70/110/160.

### Campaign & rifts
- **Campaign** (`campaign.json`): linear chain, 4 biomes × 11 levels + boss = 48 nodes (Hell, Caverns, Witchland/village, Heaven).
- **Dungeons** (`dungeons.json`): 2 fixed (Ember Depths, Ghost Spire) + **generated rifts** (`RiftService`): seeded biome gauntlet up to ~10 escalating floors ending at the biome boss; depth-scaled reward; ascension tiers 0–5.
- **Rift §8 modifier draft** (1-of-3 between floors, `GameInstance.ApplyRiftModifierLive`): field_medic, berserker, ironclad, keen_edge, cruelty, twin_serve, prospector, cursed_bounty, wide_gait, snowball.

### Pickups / bonuses (`config/bonuses.json`, `BonusSystem.cs`, `SimConfig.PickupsConfig`)
Drop chance 12%; fall 130px/s. Effects: extra_ball, mana_surge (+30, or full), wide_paddle (+24, 15s), slow_ball, heal, coins (10 crystals/5 gold), fireshot (breaks plain walls 10s), shield (one auto-save). Some block deaths force a specific drop (`forcedDropEffect`).

---

## Inventory Summary

| Category | Count |
|----------|-------|
| Heroes / classes | 4 |
| Spells (class 24 + neutral 2) | **26** |
| — reworked-into-bespoke spells | 9 (Conflagration, Concussion, Lance of Dawn, Lich's Gaze, Bonewalker, Bone Golem, Containment Field, Tesla Grid, Rot&Collapse) + new Ashfall/Reckoning |
| Block type entries (`blocks.json`) | 73 (+4 runtime) |
| Block behaviors (`BlockBehavior`) | 18 |
| Bosses | 4 (Demon, Goblin King, Witch, Seraph) + 4 shared attack verbs |
| Hazard / enemy behaviors | ~12 |
| Relics | 17 |
| Cards | 20 |
| Modules | 12 |
| Pickups / bonuses | 11 |
| Mastery nodes | 5 |
| Spend currencies | 3 (Sparks, Souls, Insight) |
| Rift §8 modifiers | 10 |
| Campaign nodes | 48 (4 biomes) |
