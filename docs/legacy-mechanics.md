# Legacy Unity Codebase — Gameplay Mechanics Reference

> Source: the previous (Unity / C#) version of the Arkanoid RPG, in `Scripts/` (246 `.cs` files).
> This catalogs **mechanics actually coded** (read from source, not inferred from filenames). It is a durable reference for the rewrite — use it to decide which legacy spells/blocks/bosses to revive.
> Numbers shown as `a/b/c` are the value at **skill level 0 / 5 / 10** (see *Skill parameter system* below). All paths are under `Scripts/`.

---

## 0. Core architecture (how content is wired)

- **Heroes** are `SO_Hero` ScriptableObjects (`Heroes/Abstract/SO_Hero.cs`). Each holds:
  `Skills[]` (passive, `SO_InvisibleSkill`), `Spells[]` (active hotbar, `SO_VisibleSkill`), a `SpellSystem` (`SO_SpellController`), a `PlayerObj` (the paddle prefab), `StartLifes`, `StartBalls`.
- A hero's full kit = `Skills` then `Spells` concatenated (`GetAllSkills()`); each entry has an independent **level** (0–MaxLevel, default max 10) bought with `LibrePoints` (character XP points).
- **Skill parameter system** (`GameClases/SkillNumberParameter.cs`): every tunable number is a `SkillNumberParameter` that scales with skill level via a *line*, an `AnimationCurve`, or a 3-point **grid** (levels 0/5/10). Spells expose these via reflection (`SO_AbstractSkill.RefreshPropetrys`) so the upgrade UI auto-lists them.
- **Active spells** (`Skills/Abstract/SO_VisibleSkill.cs`) carry `Activate_Recourse` = the **mana/soul cost**; casting fires `BattleEventsManager.Events.SpellCasted`.
- **Damage model** (`GameClases/DamageType.cs`): only two types — `Magic` and `Ball`. The plain ball deals `DamageType.Ball`, 1 dmg (`BallController.BallDamage`); spells deal `Magic`.
- **Event bus** (`Events/BattleEvents.cs`): the spine that all content listens to — `Ball_BarCollision(ball, normalizedPos)`, `BallBlockCollision`, `BlockDestroyed`, `BallAdded`, `Ball_Top/Left/RightWallCollision`, `HpLoosed`, `MpLoosed`, `TryLoosed`, `SpellCasted`, `BattleStarted/Winned/Loosed`, `UpdateEvent`.
- **Timing** (`GameClases/TimeType.cs`): `RealTime`, `ScaledTime` (obeys `Time.timeScale`), `BattleTime` (pauses when not in battle). Spells choose which clock they age on.

### Hero roster

| # | Class | Code status | Signature / passive | Active spells (coded) |
|---|-------|-------------|---------------------|------------------------|
| 1 | **Fire Mage** | full | Passive fireball on centered catch | Fire Turret, Fire Ring, Fire Wall, Phoenix |
| 2 | **Paladin** | full | Reflecting shield | Spear, Duplication, Penetration, Last Day |
| 3 | **Engineer** | full | Nuke-charge particle drops | Lightning, Magnet, Radiation, Rocket |
| 4 | **Necromancer** | **assets only** | (sprite folders exist: PassiveShield, Skeleton, Duplication, Penteration, LastDay) | **no dedicated C# — never coded; reuses Paladin-style scripts** |
| — | **Star Warrior** | **partial / cut** | none | Back, Inverse only — no passive, no sprite assets |

> Note the engine names classes "Clase_1..4" in this order: **FireMage, Paladin, Engineer, Necromancer**. Necromancer is staged in art but its spells were never implemented in code (`Skills/` has no Necromancer folder). Star Warrior is the opposite: two coded spells, no art.

---

## 1. Heroes / classes (kits in detail)

### Common (shared, upgradeable) passives — `Skills/ComunSkills/`
| Skill | Effect | Source |
|-------|--------|--------|
| **Life** | `+AddicionalLife` to max HP at run start | `SO_Skill_Life.cs` |
| **Size** | Sets paddle size to the skill's current level | `SO_Skill_Size.cs` |
| **Tries** | `+AddicionalBalls` (extra ball lives) | `SO_Skill_Tries.cs` |

### Fire Mage — `Skills/FireMage/`
| Item | Identity / trigger | Key params (0/5/10) | Source |
|------|--------------------|----------------------|--------|
| **Passive Fireball** | On ball→paddle deflect **within the paddle centre** (`|normalizedPos| < maxNormalizedDistance`), the ball is enchanted: gains a speed bonus, and on its **next block hit** spawns an explosion, then expends itself. | maxNormalizedDistance `0.1/0.13/0.15`; ExplosionSize `0.1/0.13/0.15`; maxHits `1/4/6`; damage 1; bonus velocity `0.5/1.7/5` | `SO_Fire_Passive.cs`, `FirePassiveStuff/Ball_FirePassiveEffect.cs` |
| **Fire Turret** | Attaches a turret to the paddle; it **fires a bolt on every ball-catch** (`Ball_BarCollision`), min 0.1 s between shots. Dies after `MaxTime` or after `MaxBullets` shots. | MaxTime, MaxBullets, BulletDamage, MaxHits (per bolt); min gap 0.1 s | `SO_Fire_TurretSpell.cs`, `FireTurretStuff/BattleFireTurret.cs` |
| **Fire Ring** | Spawns a fireball just above the paddle (a `BlockHitter`). | MaxHits, size scale | `SO_Fire_RingSpell.cs` |
| **Fire Wall** | Enchants a random ball; on its **next block hit**, ignites up to `MaxBlocks` blocks in `Area` (count scaled by local block density vs `BlocksInAreaToMaxDestroy`); fire **spreads over time** and damages each ignited block after a delay. | MaxBlocks, TimeToBurn, TimeToExtend, Damage, Area, BlocksInAreaToMaxDestroy | `SO_Fire_WallSpell.cs`, `FireWallStuff/FireWallController.cs` |
| **Phoenix** | Spawns a **visible Phoenix entity parented to a random ball** that hits blocks (`BlockHitter`) and self-destructs after `LifeTime`. Distinct on-screen body. | LifeTime, Size, Damage, MaxHits | `SO_Fire_PhonexSpell.cs` |

### Paladin — `Skills/Paladin/`
| Item | Identity / trigger | Key params (0/5/10) | Source |
|------|--------------------|----------------------|--------|
| **Passive Shield** | A persistent shield parented to the paddle. On contact with an **enemy bullet** it **reflects the bullet back upward** as a player projectile (`BlockHitter`). | size `0.7/0.9/1.2`; reflected MaxHits `1/1/2`; reflectSpeed `3/4/5` | `SO_Paladin_Passive.cs`, `PaladinPssiveStuff/PaladinShild.cs` |
| **Spear** | Spawns a spear above the paddle (`BlockHitter`). | maxHits `4/6/9`; size `0.8/1/1.2` | `SO_Paladin_SpearSpell.cs` |
| **Duplication** | Spawns `ballsNumber` extra balls at reduced scale near a random ball. | ballsNumber `1/3/4`; addedBallsSize `0.2/0.5/0.7` | `SO_Paladin_DuplicationSpell.cs` |
| **Penetration** | Attaches a penetrating hitter to a random ball that can punch through `maxBlocks` blocks. | maxBlocks | `SO_Paladin_PenterationSpell.cs` |
| **Last Day** | Grants `MaxLines` "free lines". Each time a ball **hits the top wall** (min 0.5 s apart) it consumes one line and drops a nuke at that point; a storm cloud is visible while lines remain. | MaxHitsForLine `3/4/5`; Size `0.7/0.9/1.2`; MaxLines `2/3/4`; min delay 0.5 s | `SO_Paladin_LastDaySpell.cs` |

### Engineer — `Skills/Engineer/`
| Item | Identity / trigger | Key params (0/5/10) | Source |
|------|--------------------|----------------------|--------|
| **Passive (Nuke charge)** | On **block destroyed**, `chance` to spawn a falling particle; when a particle **lands on the paddle** it adds a charge. After `cantidadToBoom` charges, a Nuke drops above the paddle. | chance `0.1/0.12/0.15`; charges to boom `4/3/2`; nuke maxHits `4/3/2`; nukeSize `4/3/2` | `SO_Engineer_Passive.cs`, `EngineerPasiveStuff/Engineer_PassiveParticle.cs` |
| **Lightning** | On cast, picks a random block, then **chains** to more random blocks while a roll < `repeatChance` succeeds; zaps the queued blocks one at a time with a min delay. | repeatChance `0.25/0.55/0.75` | `SO_Engineer_LightingSpell.cs` |
| **Magnet** | For `Time` seconds, **pulls every block in `Area` toward a random ball** at `Velocity` (frees their rigidbodies; skips blocks tagged `Inmagnitable`). Stacks duration on recast. | Area `4/6/8`; Velocity `1/3/4`; Time `5/7/10` | `SO_Engineer_MagnetSpell.cs`, `Engineer_MagneteStuff/Inmagnitable.cs` |
| **Radiation** | Spawns a radiation field on a random ball (`BlockHitter`, multi-hit). | maxHits `5/7/9`; size `0.8/1/1.2` | `SO_Engineer_RadiationSpell.cs` |
| **Rocket** | Launches a rocket from the paddle; after a 2 s arming delay it is **steered by the mouse cursor**, accelerates to max speed, and explodes on the nearest block. | maxHits `8/10/12`; size `0.9/1.3/1.6`; acceleration `5/8/12` | `SO_Engineer_RocketSpell.cs`, `Engineer_RocketStuff/Engineer_RocketController.cs` |

### Star Warrior — `Skills/StarWarrior/` (prototype; 2 spells, no passive/art)
| Item | Identity / trigger | Key params | Source |
|------|--------------------|-----------|--------|
| **Back** | For `time` s after cast, continuously redirects a ball back toward the paddle (a soft "return to me"). | time `0/0.5/1` | `SO_StarWarrior_Back.cs` |
| **Inverse** | Flips a random ball's vertical velocity to point upward (`y = |y|`). Instant. | — | `SO_StarWarrior_Inverse.cs` |

### Spell resource systems — `Spell Controller/`
| Controller | How the resource fills | Source |
|-----------|------------------------|--------|
| **Base** | Holds `Recource`/`MaxRecource` (default 100), spends `Activate_Recourse` per cast; supports `FreeCasts` (granted by tutorial/level scripts). | `Abstract/SO_SpellController.cs` |
| **Mana** | Regenerates `MpRegeneration`/sec on `BattleTime`, capped at max. | `SO_SpellController_Mana.cs` |
| **Souls** | Gains `MPPerSoul` (default **10**) per **block destroyed**. | `SO_SpellController_Soulce.cs` |

---

## 2. Spells / skills — mechanics summary

All active spells are `SO_VisibleSkill` with a mana/soul cost (`Activate_Recourse`) spent via the hero's `SpellController`. Common spawn helpers: most spells `Instantiate` a prefab carrying a **`BlockHitter`** (the universal "damages N blocks then dies" component — see §3) or attach a per-ball/per-paddle behaviour that listens to the event bus. Key recurring **triggers** (the part that gives each spell identity):

- **On ball-catch** (`Ball_BarCollision`): Fire Turret (fires), Fire Mage passive (centre-only enchant), Item Hammer.
- **On next block hit of an enchanted ball** (`BallController.BlockHitted`): Fire Mage passive explosion, Fire Wall ignition.
- **On block destroyed** (`BlockDestroyed`): Engineer passive particle, Souls resource, several items.
- **On top-wall hit** (`Ball_TopWallCollision`): Paladin Last Day.
- **Continuous (`UpdateEvent`)**: Engineer Magnet, Lightning queue, Star Warrior Back.
- **Visible independent entity**: Phoenix (orbits a ball), Engineer Rocket (mouse-piloted), Last Day cloud, Fire Turret (mounted on paddle).

`FreeCasts` are granted by `LocationLogic/OpenSkillScript.cs` (tutorial-style: force-opens a spell at a level + N free casts) and `OpenRandomSkillScript.cs` (levels a random skill mid-run).

---

## 3. Blocks & block effects

### Block core — `GameField/Blocks/`
- **`BlockScript.cs`** — every destructible has `HP` (default 2). `GetHit(damage, damageType)` runs all attached `AbstractBlockEffect.Hitting(ref damage,...)` (modifiers), subtracts HP, runs `Hitted(...)`, then `Die()` at HP ≤ 0. Fires `OnHitted`/`OnDestoyed` and the global `BlockDestroyed`. `NeedToKill` flags whether the block counts toward "destroy-all" win.
- **`BlockHitter.cs`** — the universal **player damage source** (balls' spell prefabs, spears, rockets, bolts). On trigger/collision with a `BlockScript`, deals `Damage`, decrements `MaxHits`, then runs an `OnFinEffect` (destroy object/script/disable). `onStayHit` allows continuous damage.
- **`BulletScript.cs`** — the **enemy damage source**: on hitting the paddle (`AbstractPlayerController`) deals `Damage` to the player, decrements `maxHits`.
- **`FabricOfObjects.cs`** — a **spawner** component: every `TimeToCreate` (on a chosen clock) instantiates `sample` up to `MaxObjects` live, with random start offset, optional child-collision ignore, and a `Prepared` event fired shortly before spawning (used for telegraphs). Drives lava, bats, projectile emitters.
- **`BlockLoader.cs`** — editor/runtime placeholder that resolves to a real block prefab from the configured `BlockCollectors` (palette of blocks per location).
- **`DieWhenItInvisible.cs`** — self-destructs after N frames off-screen (cleanup for reflected/launched objects).
- **`DieWithChilds.cs`** — destroys itself when it has no children left.
- **`PartOfBlock.cs`** — debris/particle cap: tracks a global `CurrentBlocksNumber`, destroys itself past `MaxBlocks` (50) to bound debris.

### Block effects (attachable behaviours) — `Effects/BlockEffects/` (base `AbstractBlockEffect`)
| Effect | Trigger | Behaviour | Source |
|--------|---------|-----------|--------|
| **ChangeSpriteByHP** | on Hitted | Swaps the block sprite to match the new HP value (optional cross-fade). Visual damage states. | `BlockEffect_ChangeSpriteByHP.cs` |
| **CorruptBlock** | on Die | Spreads: damages all nearby `CorruptBlock` blocks sharing the same `key` within `Radius` after `Delay` — a **chain-reaction / contagion**. | `BlockEffect_CorruptBlock.cs` |
| **CreateObjectOnDie** | on Die | Spawns a `DieAnimation` prefab (gibs/loot/successor). | `BlockEffect_CreateObjectOnDie.cs` |
| **CreateOnHit** | on Hitted | At specific HP thresholds (or any), spawns prefab(s) and can swap the sprite; optionally parented. Used for progressive reveals. | `BlockEffect_CreateOnHit.cs` |
| **GetMoreDamage** | on Hitting | Multiplies incoming damage of a chosen `DamageType` by `Coef` (default ×3) — a **weakness** (e.g. weak to Ball or to Magic). | `BlockEffect_GetMoreDamage.cs` |
| **IgnoreTypeOfDamage** | on Hitting | Zeroes incoming damage of a chosen `DamageType` — **immunity** (e.g. ignores Ball, only killable by spells). | `BlockEffect_IgnoreTypeOfDamage.cs` |
| **KillChildrenOnDie** | on Die | Kills all child `BlockScript`s (used for grouped/nested structures and sleeping bats). | `BlockEffect_KillChildrenOnDie.cs` |
| **ShowHitBar** | init / Hitted | Drives a `CustomHitBar` HP bar above the block. | `BlockEffect_ShowHitBar.cs` |
| **OpenLavaSpawner** | on Hitted at HP threshold | Enables a `FabricOfObjects` lava emitter once the block is worn to `HpToOpen` — a block that **starts spewing hazards when damaged**. | `BlockEffects_OpenLavaSpawner.cs` |

### Special / themed blocks — `GameField/Blocks/SpecialBlocks/`
| Block / system | Identity & trigger | Source |
|----------------|--------------------|--------|
| **Necromancer revive loop** | When a `RevivibleByNecromant` block on the necromant's layer dies (and isn't already `DeathMark`ed), a **ghost** (`RecuperableObject`) spawns at its grave holding a "reborn" prefab. If the player's **necromant ball (`NecromantSphere`) touches the ghost**, the block is **resurrected**. So killing fuels the enemy unless you also clear ghosts. | `Necromant/NecromantController.cs`, `NecromantSphere.cs`, `RecuperableObject.cs`, `RevivibleByNecromant.cs`, `DeathMark.cs` |
| **Lava block (eater)** | `LavaBlockEater` blocks **merge on contact** — the survivor absorbs the other's HP and parents it, growing into a bigger lava mass. | `LavaBlock/LavaBlockEater.cs` (+ empty `LabaBlockController.cs`) |
| **Teleporter (Hell)** | Color-coded pairs. A non-kinematic ball entering one teleports to a same-color partner, with a `minDelayToAcept` cooldown to avoid ping-pong. | `Hell/Teleporter.cs` |
| **Union of Sticks (Cave)** | Adjacent stick blocks **auto-weld** at start into one rigid body (recursive overlap test), so a cluster behaves/falls as a single physics object. | `Cave/UnionOfSticks.cs` |
| **Heaven statues** (`AbstractStatue`) | Statues with a **Level** and an **Ally** timer. Altar makes all statues allied for a time; Vase (on death) levels all statues up. While enemy they harm the player; while allied they help. | `Heavens/AbstractStatue.cs`, `HeavensAltarScript.cs`, `HeavensVaseScript.cs` |
| → **Melee Statue** | On ball-collision `Hit()`, fires level-scaled bullets (different volley when allied). | `Heavens/MeleeStatue.cs` |
| → **Shield Statue** | On hit: if enemy, **shields** all blocks in radius (protects them); if allied, **corrupts** them (adds `CorruptBlock`). | `Heavens/ShieldStatue.cs` |
| → **Wind Master** | Applies a falloff **wind force** to any rigidbody (ball) in range — pushes the ball around. | `Heavens/WindMasterScript.cs` |
| → **Column** | Stacked column parts: when a middle part dies, the parts above **slide down** to fill the gap; top shows a damaged sprite. | `Heavens/ColumnPart.cs` |
| **Bat (Village)** | On ball contact, **grabs and holds the ball** for `TimeToKeep`; on release grants a paddle speed bonus, then the bat flies away (gravity flips). | `Village/Bat/BatController.cs` |
| **Sleeping Bat** | On overlapping a block, parents to it and makes that block **kill its children on death** (so destroying the host wakes/kills the bat). | `Village/SleepingBatController.cs` |
| **Beholder eye** | An eye that **tracks a random ball** (cosmetic targeting). | `Village/BeholderController/LookAtBall.cs` |
| **Pot** | Cycles through visual `states` on a timer; dies when a state object is gone. | `Village/PotController.cs` |
| **Ghost Portal** | A ball passing through **toggles between Normal and Ghost layers** (adds/removes a ghost effect) — lets the ball pass through / interact with a different block layer. | `Village/GhostPortal/GhostPortalController.cs`, `GhostBallEffect.cs` |
| **Marks / path followers** | `MarkToBlocks` define waypoints (key-grouped); `MarksFollower` moves an enemy/block along the matching path (with sprite flipping and forward rotation). Can fall back to free-floating "enemy ball" movement. | `MarkToBlocks.cs`, `MarksFollower.cs` |
| **Enemy Ball movement** | A roaming enemy ball that keeps its vertical velocity pointing **up** while below `MinDistanceToBar` (so it doesn't drop past the paddle). | `Enemy_Ball_MovimientController.cs` |
| **AbstractBallCollied** | Base "do X when a ball hits me" (used by statues, altars, `CreateOnBallCollision`). | `AbstractBallCollied.cs` |

---

## 4. Bosses — `GameField/Bosses/`

Bosses are animation-driven (`Effects/GraficEffects/AnimatorController.cs`): they own an `AnimationStack`, play `Begining`, react to a target `BlockScript` being hit (insert `Hitted`; on its death play `Die`), and on each idle pick the next action. Damage to the boss = hitting its linked block.

| Boss | Attack pattern / verbs | Source |
|------|------------------------|--------|
| **Goblin** | Hops between 3 positions (left/centre/right) via jump-animation stacks; on each idle runs a random burst of **1–`AttacksMax` attacks** drawn from an `Attacks` list (interleaved with stand). `StalaktitCreation()` spawns `StalaktitCount` stalactites scattered in a radius. | `Goblin/GoblinController.cs` |
| **Witch** | Animator-driven action list; animation events **fling the target ball** (`StarFly`: makes it kinematic-off and launches it; `EndFly`: re-locks it) — the witch periodically seizes and hurls the ball. | `Witch/WitchController.cs` |
| **Statue** | Picks a random action from `Actions` each idle (animation-driven). | `Statue/StatueController.cs` |
| **Demon** | `Demon.cs` is a stub (empty collision handlers). **`DemonFist`** is the live part: a slam that, while `CanHit`, deals `Damage` to the paddle on contact and spawns a hit effect (one-shot per slam). | `Demon/Demon.cs`, `Demon/DemonFist.cs` |

---

## 5. Player / paddle / ball mechanics — `GameField/Player/`

| System | Behaviour | Source |
|--------|-----------|--------|
| **Paddle controller** | Holds bonuses; `DefaultBallSpeed` 15, `DefaultSize` 1, `MinBallSpeed` floor. Recomputes ball speed & size from active bonuses each frame. Exposes `GetRandomBall()` (target picker most spells use). | `Player_Bar/Player_BarController.cs`, `AbstractPlayerController.cs` |
| **Paddle movement** | Moves toward a destination X (mouse/touch) at `MovimientSpeed` (default 5), clamped to the field walls. | `Player_Bar/BarMove.cs`, `MouseBarInput.cs`, `TouchBarInput.cs`, `AbstractBarInput.cs` |
| **Deflect / catch** | On ball collision, sets the new velocity X from **where on the paddle it hit** (`normalizedPos`×1.5) and forces it upward; fires `Ball_BarCollision(ball, normalizedPos)` — the hook the Fire Mage passive & Fire Turret use. | `Player_Bar/PlayerBar_CollisionController.cs` |
| **Ball physics** | `BallBehavior` keeps the ball at constant `Velocity`, clamps the X/Y ratio (`MaxCoefXYVel` 5) and adds tiny randomness so it never gets stuck horizontal/vertical. | `Player_Bar/BallBehavior.cs` |
| **Ball hit logic** | On collision with a block: fires `BallBlockCollision`, deals 1 `Ball` damage, fires `BlockHitted`. Detects which wall (top/left/right) was hit. | `Player_Bar/BallController.cs` |
| **Ball speed sync** | Ball's `BallBehavior.Velocity` is continuously set to the paddle's `CurrentBallSpeed` (so speed bonuses apply live). | `Player_Bar/BallVelocityController.cs` |
| **Ball serving / lives** | First ball is **held on the paddle** until launched. Lost ball (below `MinPosOfBall`) consumes a ball-life; out of balls = lose. Supports multi-ball (`AddBall`). | `Player_Bar/BarLogic.cs` |
| **Lives & HP** | `LifeManager` tracks float **Lifes** (HP bar) and int **Balls** (tries). HP 0 → lose mission; out of balls → lose. | `LifeManager.cs`, `BallShower.cs` |
| **Bonuses** (timed buffs) | `BonusVelocity` (ball speed), `BonusVelocityOfBar` (paddle speed), `BonusSize` (paddle size). Auto-expire on `BattleTime`; `time = -1` = permanent. | `PlayerBonuses/*.cs` |

---

## 6. Effects — `Effects/`

Mostly cosmetic but several are gameplay-bearing:

| Effect | Role | Source |
|--------|------|--------|
| **PhysicExplotion** | Knockback: pushes nearby rigidbodies away with falloff to `MaxDistance` — physical shove (lava/explosion feel, can scatter blocks). | `PhysicEffects/PhysicExplotion.cs` |
| **HitBlockBySpeed** | A flung object that **damages blocks it rams** if moving faster than `minSpeedToHit` (and optionally damages itself), losing speed each hit. | `PhysicEffects/HitBlockBySpeed.cs` |
| **CreateOnBallCollision** | Spawns a prefab when a ball hits this object. | `PhysicEffects/CreateOnBallCollision.cs` |
| **ConstantRigiBodySpeed / ForceOnStart / MyRigidBody2D** | Give projectiles/debris an initial velocity & spin; `MyRigidBody2D` is a lightweight custom gravity/drag body for gibs. | `PhysicEffects/ConstantRigiBodySpeed.cs`, `GameField/ForceOnStart.cs`, `PhysicEffects/MyRigidBody2D.cs` |
| **Effects_DestroyInTime / SmoothDestroyInTimeWithParticles** | Lifetime cleanup; the smooth one detaches particle systems so they finish before destruction (also used by spells like Phoenix to set `TimeToDestroy`). | `PhysicEffects/Effects_DestroyInTime.cs`, `SmoothDestroyInTimeWithParticles.cs` |
| **Effects_ForwardRotation / SaveChildOnDestroy / RemplaceObjectOfAnimation** | Aim sprites along velocity; reparent children on destroy; swap a prefab for another via animation event. | `PhysicEffects/*.cs` |
| **Grafic effects** | `Effect_Extend` (scale tween), `Effects_ColorSwapper` (gradient flash), `Effects_FadeAndDie`, `Effects_SpriteAnimation`, `Effect_Rotator`, `StaticEffects_SwapOfSprite`, `AnimatorController`, `AutoCenter`. Base `GraficEffect`. | `Effects/GraficEffects/*` |
| **DieAnimation** (data) | Reusable "spawn this prefab when I die" payload (keep parent / inherit sprite / auto-clean). Used by `CreateObjectOnDie` and timed destroyers. | `GameClases/DieAnimation.cs` |
| **OnFinEffect** (data) | "When finished, destroy object / destroy script / disable" — shared end-of-life policy for hitters/bullets. | `GameClases/OnFinEffect.cs` |

---

## 7. Progression / items / upgrades / economy

> **Caveat:** this is the *legacy* economy and does **not** match the current rewrite's owner-approved 3-currency model (`docs/2026-06-14-economy-rework-proposal.md`). Treat it as historical.

- **Currencies (legacy):** XP → character **Level** → **LibrePoints** (skill points) used to buy/upgrade spells; **Crystals** in 4 colors (Blue/Green/Red/Yellow) + **SpellPoints** (`ResourceData`/`CrystalResource`) as a secondary buy currency; **TreasurePoints** which, at 100, roll a **random item upgrade** (`PlayerData.AddTreasurePoints` → `ItemsManager.GetRandomItem`). Mission reward = a `Premy` (exp + crystals + treasure points + random items).
- **Skill upgrades** (`Interface/Upgrades/UpgradePanel.cs`): per-skill levels 0→MaxLevel; price = `startPrice + priceForLevel*level` (`Skill_UpgradeInfo`, defaults max 10 / start 5 / +2). Full respec via `ReturnUpgardes()`. Some skills are locked until "opened" (`AutoOpen=false`, opened by level scripts).
- **XP curve:** `100 × 1.1^(level-1)` to level up (`CharactaerData.GetExpToLevel`).
- **Items** (`GameField/Player/Items/`, `SO_AbstractItem`): equip up to **3** at once (`ItemsData.MaxItemsToChoise`), each levels 0–3, effects fire on the event bus at run start. The coded items (relic-like passives):

| Item | Effect (per level array) | Source |
|------|---------------------------|--------|
| **Balance** | Convert lost MP→HP and lost HP→MP by `Coef` (0/.05/.1/.15) | `SO_ItemBalance.cs` |
| **BallModificator** | Scales every spawned ball and attaches a ball effect | `SO_ItemBallModificator.cs` |
| **BarSpeed (Engine)** | Permanent paddle-speed bonus | `SO_ItemBarSpeed.cs` |
| **Clock** | When >15 block-parts on screen, slow `Time.timeScale` toward 0.3 (bullet-time on clutter) | `SO_ItemClock.cs` |
| **Drill** | +`Coef` to TreasurePoints reward (more item rolls) | `SO_ItemDrill.cs` |
| **Flask** | On block destroyed, heal `HpReg`×maxHP | `SO_ItemFlask.cs` |
| **FourLeaf** | On losing a try, `Chance` to refund a ball | `SO_ItemFourLeaf.cs` |
| **Gem** | Multiply mana regen by `Coef` (1.1/1.2/1.3) | `SO_ItemGem.cs` |
| **Hammer** | On ball-catch, temporary ball-speed burst | `SO_ItemHammer.cs` |
| **Helm** | On HP loss, `Chance` to negate (refund) the damage | `SO_ItemHelm.cs` |
| **JudeBall** | On ball→block, `Chance` to deal an extra hit | `SO_ItemJudeBall.cs` |
| **Phoenix (item)** | +`Coef`×maxHP to max HP | `SO_ItemPhoenix.cs` |
| **Ring** | On MP spent, `Chance` to refund the mana | `SO_ItemRing.cs` |
| **Staff** | +`Coef`×default to max mana pool | `SO_ItemStaff.cs` |
| **Tom** | +`Coef` to XP reward | `SO_ItemTom.cs` |
| **Torch** | Permanent ball-speed bonus | `SO_ItemTorch.cs` |
| **Crown / Sun / Mark** | declared, **no effect coded** (stubs) | `SO_ItemCrown/Sun/Mark.cs` |

- **Acquisition flow:** heroes are chosen from owned `Characters` (`ClassPanel.cs`); items equipped in 3 slots (`ItemPanel.cs`); spells learned/upgraded with LibrePoints (`UpgradePanel.cs`). Random item roll on TreasurePoints threshold. (No fixed-price random-roll gacha like the rewrite — this is point-buy + treasure-roll.)
- **Save system** (`SaveSystem/`): `PlayerData` (heroes, treasure, items, resources), `CharactaerData` (level/exp/points/spell levels/missions won), `GameStatisticData` (blocks destroyed, spells cast, battles, time, hp/mp lost), `AutoSaver`, `SaveWriter`.

---

## 8. Missions / win conditions / level & map structure

- **Win controller** (`LocationLogic/MissionWinController/MissionWinController.cs`): a level has one or more `AbstractMissionWinCondicion`s combined by `RequiereWinAll` / `RequiereWinOne` / `RequiereWinOneWithToleranceToLoose`. On win → grant `Premy` + `BattleWinned`; on loss → reload scene.
- **Win conditions coded:**
  - **DestroyAll** — all `NeedToKill` blocks gone (`DestroyAllWinCondicion.cs`).
  - **DestroySomething** — a specific list of objects destroyed (`DestroySomethingWinController.cs`).
  - **Timer** — survive / beat a time limit; `StateAfterTimer` = Win or Lose (`TimerWinCondicion.cs`).
- **Loss:** HP reaches 0, or run out of ball-lives.
- **Campaign map** (`HeroRoad` / `RoadElement`): a **per-hero node graph** of levels with prerequisite edges (soft/`Rigid`); supports branching forks (`LevelLoader.ChoiseDestination` when a node has >1 open successor). Each hero has its own road (so each class is a separate campaign).
- **Level composition helpers:** `Level.cs` (auto-detects its Location from block palette, holds the `Premy`), `MultiplyFloorLevelController` (multi-floor levels: clear the current floor to drop to the next), `StrechedLevel`, `BallSpeedFitter`, `LevelFitter`, plus a full in-editor **map/level creator** toolset (`Administrator/CreatorTools/`).
- **Config root** (`Configuraciones/SO_Configuraciones.cs`): the global registry — `Heroes[]`, `Items[]`, `Locationes[]`, `BlockCollectors[]` (per-location block palettes), teleporter colors, points-per-level grid, UI prefabs. Loaded from `Resources/SO_Configuraciones`.

---

## 9. Inventory summary (counts)

| Category | Count | Notes |
|----------|-------|-------|
| Hero classes | **4** declared (FireMage, Paladin, Engineer, Necromancer) + 1 cut (Star Warrior) | Necromancer = art only; Star Warrior = code only |
| Coded signature passives | **3** (Fire, Paladin shield, Engineer nuke-charge) | |
| Coded active spells | **15** | Fire ×4, Paladin ×4, Engineer ×4, Star Warrior ×2, (+ Necromancer 0 coded) |
| Common upgradeable skills | **3** (Life, Size, Tries) | |
| Spell resource systems | **2** (Mana regen, Souls-on-kill) | |
| Generic block effects | **9** | corrupt, change-sprite-by-HP, create-on-die, create-on-hit, more-damage, ignore-damage, kill-children, show-hitbar, open-lava-spawner |
| Special/themed block systems | **~16** | Necromant revive, lava-eater, teleporter, union-of-sticks, 4 Heaven statues + altar/vase, bats (×2), beholder, pot, ghost portal, mark paths, enemy-ball |
| Bosses | **4** (Goblin, Witch, Statue, Demon) | animation-stack driven |
| Items / relics | **18** (15 functional + 3 stubs) | 3 equip slots, 3 levels each |
| Win-condition types | **3** (DestroyAll, DestroySomething, Timer) | combinable AND/OR |
| Damage types | **2** (Magic, Ball) | enables weakness/immunity blocks |

### Most interesting / revival-worthy mechanics for a designer

- **Trigger-rich spells, not fire-and-forget** — the identity is in *when* they fire: Fire Turret shoots **on every ball-catch**, the Fire Mage passive only procs on a **centered catch**, Fire Wall/Phoenix arm a ball and pop on its **next block hit**, Last Day fires off **top-wall bounces**. These ball-relationship triggers are the soul of the legacy kit and match the rewrite's "trigger+identity" fidelity bar.
- **Phoenix and Rocket as visible independent entities** — Phoenix orbits a ball; Rocket is **mouse-piloted** for 2 s then homes. Strong, readable on-screen actors.
- **Engineer Magnet** — actively yanks blocks toward the ball; **Necromancer revive loop** — kills feed enemy resurrection unless you also smash the ghosts; **Heaven statues** that flip ally/enemy via altar/vase — all create board-state puzzles beyond "break the bricks."
- **Block-effect toolkit is a reusable content language**: weakness (`GetMoreDamage`), immunity (`IgnoreTypeOfDamage`), contagion (`CorruptBlock` keyed chains), damage-states (`ChangeSpriteByHP`), spawn-on-hit/die, and "starts spewing lava when hurt." This composable model is worth reviving wholesale.
- **Two-resource casting** (regenerating Mana vs. Souls-on-kill) gives classes different cast rhythms cheaply.
- **Themed hazard blocks**: ball-grabbing bats (then speed-boost on release), color teleporters, ghost-portal layer swap, wind-master push, sliding columns — a rich bestiary of non-standard brick behaviours.
- **Per-hero branching campaign roads** — each class its own map graph with forks; different from a single shared linear chain.
