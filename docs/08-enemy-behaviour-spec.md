# Enemy & Hazard Behaviour Spec (port from original `Scripts/`)

Implementation spec for **every** enemy/hazard the original game had, reconstructed from
the original Unity controllers in `Scripts/` (referenced per entry). This is the source of
truth for the `docs/07-flaws-review.md` §E implementation.

**Architecture reminder:** all logic is server-side in `Arkanoid.Core` (deterministic,
per-tick, seeded RNG, no Unity). Each enemy becomes either a **block flag/def** (static
blocks with a behaviour) or a **sim entity** (moving things: enemy balls, missiles, bats,
falling stalactites, hazards). The frontend renders from the snapshot only. New tunables go
in `SimConfig` (no magic numbers). Player **HP** (`Lives`) is the damage resource — hostile
projectiles/hazards reaching the paddle cost HP, mirroring the boss-hazard loop.

Legend: **Src** = original script · **Behaviour** = what it does · **Port** = .NET-sim plan.

---

## HELL

### H1. Hell Ball Spawner + Enemy Ball
- **Src:** `Enemy_Ball_MovimientController.cs`, art `HellBallSpawner`, `HellBallLvl1/2/3`, `HellBallMissile`.
- **Behaviour:** a spawner block periodically emits a hostile **enemy ball** that bounces around the field like a second ball. The enemy ball refuses to drain: when it nears the paddle line (`y < barY + MinDistanceToBar`, 4u) it flips `vel.y` positive (bounces back up). It collides with blocks and the paddle; hitting the **paddle costs the player HP**.
- **Port:** new entity `EnemyBall { Pos, Vel, Hp }`. Spawner = block def `hell_ballspawner` (indestructible or high-hp) with a `SpawnEnemyBall` cadence (`SimConfig.HellSpawnerInterval`). Each tick: integrate, bounce off walls/blocks; if `Pos.Y > paddleLine - band` reflect upward; on paddle overlap → `Lives--` + despawn (or reflect). Render as `HellBallLvl1`.

### H2. Color-paired Teleporter
- **Src:** `Hell/Teleporter.cs`, art `SkullRed/Blue/Green(+Active)`.
- **Behaviour:** each teleporter has a **colour**. When the ball collides, it warps to a **random other teleporter of the same colour** that's off cooldown; both endpoints then get a `minDelayToAcept` (0.2s) cooldown so it doesn't ping-pong.
- **Port:** extend the existing single teleporter into **colour groups**. Block def gains `teleportColor` (0/1/2 → red/blue/green). On ball–teleporter overlap, pick a same-colour partner with `cooldown<=0`, set ball pos to it, set both cooldowns = `SimConfig.TeleportCooldown`. (We already have a one-pair teleporter; this generalises it.)

### H3. Lava block / Lava Spawner
- **Src:** `LavaBlock/LavaBlockEater.cs`, `LabaBlockController.cs` (stub), `BlockEffects_OpenLavaSpawner.cs`, art `LavaSpowner(+Active)`, `LavaBegining/MainPart/End`.
- **Behaviour:** lava blocks **merge** — when two touch, one absorbs the other (sums HP) growing a bigger lava mass. A spawner "opens" to release lava. (Even the original calls the full lava mechanic a stub.)
- **Port (simplified):** a `lava_spawner` block that, on a timer, extends a **rising lava hazard** from the bottom edge upward N cells; the lava line damages the **ball** (resets it) or the **paddle** (HP) on contact. Skip the merge logic. **Lower priority** (P2) — implement after the clean enemies.

---

## CAVERNS

### C1. Stalactite (falling spike)
- **Src:** `Cave/UnionOfSticks.cs`, dropped by `Bosses/Goblin/GoblinController.cs::StalaktitCreation`, art `Stalactite`, `Stalactite2`.
- **Behaviour:** stalactites hang from the ceiling (and unite into clusters via `UnionOfSticks`). The **Goblin boss drops 5 at random scatter** within a radius; they **fall** and are a hazard. A falling stalactite that reaches the paddle costs HP; hitting the ball deflects/breaks.
- **Port:** entity `Stalactite { Pos, Vel, Hp, falling }`. As blocks: `cavern_stalactite` hangs (static) until triggered (ball hits its column, or boss drop) → becomes a falling entity (`Vel.y = +SimConfig.StalactiteFallSpeed`). Reaching paddle → `Lives--`; off-screen → despawn. Boss path: `BossSystem` Goblin pattern spawns `StalactiteDropCount` (5) at scattered X.

### C2. Bomb block
- **Src:** art `Bomb`, `GrateBomb`, `*Stand/Vertical` (controller via generic block-effect).
- **Behaviour:** on destruction, **explodes**, dealing damage to all blocks within a radius (chain-detonating other bombs).
- **Port:** block def `cavern_bomb` with `onDeath: explode`. In `BlockDamage`, when a bomb block dies, damage every block within `SimConfig.BombRadius` cells (recursing into other bombs same tick). Emit an `explosion` event for VFX.

### C3. Mine cart *(optional, P3)*
- **Src:** art `DungeonCart`, `DungeonCartWheel`.
- **Behaviour:** a cart that rolls horizontally along a rail; a rolling hazard / moving block.
- **Port:** entity that translates along a row, reflecting at walls; low priority.

---

## WITCHLAND

### W1. Beholder (tracking eye that shoots)
- **Src:** `Village/BeholderController/LookAtBall.cs`, art `Beholder1/2/3(+Ghost)`, `BeholderAttackAnimation`, `BeholderMissile`.
- **Behaviour:** a block whose **eye tracks the nearest ball** (eye sprite leans toward the ball). On a cadence it **fires a missile** toward the ball/paddle; missile reaching the paddle costs HP. Ghost variant lives on the ghost layer.
- **Port:** block def `village_beholder` (destructible, the eye-track is purely visual via snapshot `aimX/aimY`). Sim: every `SimConfig.BeholderFireInterval`, spawn a `Missile { Pos, Vel }` aimed at the nearest ball's position. Reuse a generic `Missile` entity (also used by H1/Heaven). Snapshot exposes beholder aim direction for the eye render.

### W2. Bat (ball-grabber)
- **Src:** `Village/Bat/BatController.cs`, `SleepingBatController.cs`, art `BatFlyAnimation*`, `BatSleeping`.
- **Behaviour:** on ball contact the bat **grabs the ball** (holds it stuck at the bat for `TimeToKeep` ≈ 3s), then **releases it with a speed bonus** and **flies away** (floats up, becomes intangible). **Sleeping bats** sit on a host block and wake (become active) when the host is hit/destroyed.
- **Port:** entity `Bat { Pos, state(sleeping/active/grabbing/flyaway), grabTimer, hostBlock }`. On overlap with an alive ball → `grabbing`: pin the ball to the bat, freeze it, count down `SimConfig.BatHoldTime`; on expiry release (apply temp ball-speed bonus), switch to `flyaway` (drift up, no collision), despawn off-screen. Sleeping → active when host block damaged.

### W3. Necromant (block reviver)
- **Src:** `Necromant/NecromantController.cs`, `RecuperableObject.cs`, `NecromantSphere.cs`, `DeathMark.cs`, `RevivibleByNecromant.cs`, art `VillageDeath(+Ghost)`, `DeathSphere`.
- **Behaviour:** subscribes to block-death. When a **revivible** block dies (and isn't already death-marked), the Necromant marks it and spawns a **death sphere/ghost** that, after travelling/delay, **revives the block** at its original cell. You must kill the Necromant or destroy blocks faster than it revives.
- **Port:** block def `village_necromant` (the caster, must be killed). On any normal block death, if a Necromant is alive and the cell isn't pending, enqueue a `Revive { cell, blockId, timer }` (`SimConfig.NecromantReviveDelay`); on timer, re-add the block (unless its Necromant is dead). Spawn a `death_sphere` VFX entity travelling to the cell.

### W4. Ghost Portal (phase toggle)
- **Src:** `Village/GhostPortal/GhostPortalController.cs`, `GhostBallEffect.cs`, art `Portal`, `BallGhost`.
- **Behaviour:** a portal trigger **toggles the ball between Normal and Ghost phase**. A ghost-phase ball **passes through ghost-layer blocks** (and vice-versa). This is the "real" village mechanic (richer than today's always-phasing `village_ghost` block).
- **Port:** add `Ghost` bool to `Ball`. Block def `village_portal` toggles `ball.Ghost` on overlap (cooldown). Collision step skips ghost-flagged blocks when `ball.Ghost`, and skips solid blocks when not — i.e. ghost ball only interacts with ghost blocks. Visual: tint the ball.

### W5. Pots *(cosmetic, P3)* — `PotController.cs` cycles sprite states on a timer. Set-dressing only.

---

## HEAVEN (turn-ally mechanic — the original's deepest biome)

Statues are **hostile by default**; the player can flip the whole set to **ally** (helpful)
by hitting an **Altar**, or **level them up** (stronger) by destroying a **Vase**.

### He1. Melee Statue (projectile turret)
- **Src:** `Heavens/MeleeStatue.cs` : `AbstractStatue` (`AbstractBallCollied`), art `HeavenMeleeStatue(+Active/glowing)`.
- **Behaviour:** when the **ball hits it**, it **fires a projectile** (level-scaled). Hostile bullet targets the player; if **ally**, fires an ally bullet that damages enemy blocks instead.
- **Port:** block def `heaven_melee_statue` with `Level` + `allyTimer`. On ball collision → spawn `Missile` (down at paddle if hostile / up at blocks if ally), scaled by Level. Shares the `Missile` entity.

### He2. Shield Statue (block reinforcer)
- **Src:** `Heavens/ShieldStatue.cs`, art `HeavenDefender(+Active)`, `Shield`.
- **Behaviour:** when the **ball hits it**, it **shields all blocks in a radius** (adds protective HP/`Shield` so they're harder to clear) — unless **ally**, in which case it **corrupts** (damages) those blocks for you.
- **Port:** block def `heaven_shield_statue`. On ball collision → for blocks within `SimConfig.ShieldStatueRadius`: if hostile, grant a one-hit `Shield` flag; if ally, deal damage. Snapshot exposes a `shielded` flag for the block overlay.

### He3. WindMaster (ball pusher)
- **Src:** `Heavens/WindMasterScript.cs`, art `WindMaster2`, `WindMasterV2Circle/Glow`.
- **Behaviour:** continuously **pushes the ball away** from itself with a force that **falls off with distance** (within `DistanceToZeroForce` ≈ 7u). Deflects your aim — control hazard.
- **Port:** block/entity `heaven_windmaster`. Each tick, for each ball within radius, add an outward impulse `force * (1 - dist/maxDist)` to `ball.Vel` (clamped). `SimConfig.WindMasterForce/Radius`.

### He4. Column (swaying stacked pillar)
- **Src:** `Heavens/ColumnPart.cs`, art `Column`, `ColumnTop/Bottom(+Damaged/Destroyed)`.
- **Behaviour:** a **vertical stack** of parts (bot→middle→top) where each part **follows the one below** (a wobbling tower). Destroying a part **shortens** the column and the new top shows a **damaged** sprite.
- **Port:** author as a column of linked `heaven_column` blocks (bot/mid/top sprites by row). Simplest faithful port: static stacked blocks with correct top/bottom/middle sprites + damage-state swap (ties into §A3). Skip the physical sway (visual-only) initially.

### He5. Altar — `HeavensAltarScript.cs`: **ball hits altar → all statues become ally for 15s** (`AddAllyTimeToAll`).
### He6. Vase — `HeavensVaseScript.cs`: **destroy vase → all statues level up for 15s** (`LevelUpToAll`).
- **Port:** `heaven_altar` (ball-collision → set `allyTimer` on all statue blocks), `heaven_vase` (on death → increment statue `Level`, timed). Statues read these flags.

---

## Bosses (already partially present)

| Boss | Src | Behaviour | Status |
|------|-----|-----------|--------|
| Demon (Hell) | `Bosses/Demon/Demon.cs`, `DemonFist.cs` | multi-pattern, fists/hazards | **implemented** (BossSystem) |
| Goblin (Caverns) | `Bosses/Goblin/GoblinController.cs` | hops 3 positions, random 1–5 attack stacks, **drops 5 stalactites** in a radius | hops/HP done; **add stalactite drop** (C1) |
| Witch (Witchland) | `Bosses/Witch/WitchController.cs` | magic attacks (`WitchMagic1-4`) | present as block; wire magic projectiles |
| Statue/Heaven boss | `Bosses/Statue/StatueController.cs`, art `HeavenBoss`, `HeavenBossGlobe` | finale | **add `heaven-boss` level + pattern** (§A7) |

---

## Supporting systems these need (build first)

1. **`Missile` entity** (shared by H1 enemy-ball-missile, W1 beholder, He1 melee statue) — `{ Pos, Vel, hostile }`; hostile reaching paddle → `Lives--`; ally reaching a block → damage. One system, many spawners.
2. **Block damage-state sprites** (§A3) — renderer swaps `*Damaged`/`*Destroyed` by `hp/maxHp` bucket instead of alpha-fade. Needed by Column + every block.
3. **Mirrored/oriented block variants** (§F) — `flipX/flipY` per cell so asymmetric/corner art (skulls, columns, channels) orient correctly.
4. **Ball `Ghost` flag** (W4) and **Block `Shield` flag** (He2).
5. **`BossSystem` Goblin stalactite pattern** (C1).

## Build order (within §E) — progress

- [x] **Wave 1** (`7f177b1`): shared emitter system → **H1** Hell spawner, **W1** Beholder, **He1** Melee statue (data-driven), **C2** bombs, + per-cell mirror flags (§F). 4 xUnit + 4 Playwright.
- [x] **Wave 2** (`56eaa2d`): **H2** colour-paired teleporters. +1 xUnit.
- [x] **Wave 3** (`587ff63`): Caverns **C1** stalactites (column-trigger fall + Goblin BossDrop path + dropStalactites cheat).
- [x] **Wave 4a** (`f24d8f4`): Witchland **W3** necromant (block reviver + revive queue).
- [x] **Wave 4b** (`0f99764`): Witchland **W4** ghost-portal (ball phase toggle).
- [x] **Wave 5** (`8d10c9f`): Heaven **He3** windmaster (ball deflector).
- [x] **Wave 5b** (`8d9a076`): Heaven **He2** shield statue.
- [x] **Refactor + Bat** (`3941143`): behaviour-bool soup → one `BlockBehavior` enum; Witchland **W2** bat (ball-grabber).
- [x] **Boss wiring** (`1f772fe`): Goblin rains stalactites; **heaven-boss** finale level + campaign node.
- [x] **§07 polish so far**: A3 damage-state sprites (`e519fc4`); Fire-Mage hotbar I2 + inventory padlocks I1 + watermark D3 (`7bacc00`); balls-bar B1 + rift emoji G (`f5e4222`).

**ALL 12 ENEMY TYPES DONE.** Remaining refinements/polish: **He5/He6** altar/vase ally-toggle, **He4** columns, Hell **H3** lava, Caverns **C3** cart, Witch boss magic, Russian badges (D2, needs art), HUD spell-emoji fallbacks, stretched-art→9-slice (H).

Each wave: SimConfig tunables, snapshot fields, frontend render, xUnit + Playwright proof + screenshot.
