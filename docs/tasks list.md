# Arkanoid RPG Spell System Review and Redesign Proposal

## Context

The game is an Arkanoid-style RPG with:

* Campaign locations: Hell, Cavern, Witch Village, Heaven
* Enemies that shoot at the player
* Bosses
* Characters with passives
* Active spells
* Modules / items / rifts / daily quests / weekly events
* Potential star-based campaign progression
* Potential Tower-style duplicate progression

Current spell catalog:

* 4 heroes: Fire Mage, Paladin, Engineer, Necromancer
* 6 spells per hero element
* 2 neutral spells
* 26 total spells
* Each hero has:

  * 1 signature spell locked to slot 0
  * 3 starting spells including signature
  * −20% mana cost on own-element spells
* All non-signature spells are globally rollable via Souls

## High-Level Diagnosis

The spell list has strong fantasy ideas, but the current structure has 5 major risks:

1. Too many spells are “different ways to clear blocks”
2. Global spell rolling weakens hero identity
3. Duplicate progression on spells will create too much random grind
4. Some spells overlap with modules/cards/items
5. Some high-cost spells are not interactive enough

The spell system should emphasize what makes this game special:

* Ball control
* Paddle positioning
* Enemy bullet reflection / defense
* Boss interaction
* Elemental world mechanics
* Short tactical decisions inside Arkanoid gameplay

The game should not become a spreadsheet where the player mostly upgrades random passive spell duplicates.

## Core Recommendation

Use this separation:

| System     | Role                     | Duration            | Progression                           |
| ---------- | ------------------------ | ------------------- | ------------------------------------- |
| Character  | Defines playstyle        | Permanent selection | Unlock + level                        |
| Spell      | Active tactical button   | During level/run    | Deterministic upgrade branches        |
| Module     | Permanent build modifier | Loadout             | Random unlock + duplicate progression |
| Rift Card  | Temporary rule change    | Current rift only   | No permanent duplicate leveling       |
| Star Goals | Mastery/replay           | Campaign level      | Unlock rewards/side content           |

Do not make spells, modules, and cards all use the same random duplicate upgrade model.

That would make the systems feel redundant.

## Important System Change: Do Not Use Random Duplicate Progression for Spells

Current idea:

* Buy random spells/items/modules
* Need 3/5/7/11 duplicates to upgrade

Recommendation:

* Use that model only for Modules.
* Do not use it for Spells.
* Spells should be upgraded deterministically.

Reason:

Spells are active gameplay tools. If spell progression is random, the player may feel the game is blocking their preferred playstyle.

Better spell progression:

* Unlock spells from bosses, campaign locations, achievements, or star chests.
* Upgrade spells using elemental essence or spell tomes.
* At upgrade levels, offer branches.

Example:

Fireball / Conflagration upgrade choices:

* Bigger explosion radius
* Lower mana cost
* More damage to burning blocks
* Leaves burning area after detonation

This creates player agency.

Modules can still use duplicates because modules are passive build optimization. Random duplicates are less painful there.

## Global Pool Problem

Current rule:

> Any hero can roll any non-signature spell from the global pool via Souls. Affinity is only a cost discount.

This weakens hero identity too much.

Example problem:

If Paladin can easily use Fire Mage / Engineer / Necromancer spells, then the difference between heroes becomes mostly “20% mana discount + passive.” That is not enough.

Recommendation:

Use weighted access instead of fully equal access.

### Option A: Soft Affinity Bias

Any hero can use any non-signature spell, but rolls are weighted:

* 60% own element
* 25% neutral / general
* 15% off-element

This keeps variety but preserves identity.

### Option B: Campaign Unlock Pool

At first, heroes mostly use own element + neutral spells.

Off-element spells become available after defeating the relevant world boss or reaching campaign rank 2.

Example:

* Defeat Hell boss → fire spells can appear in global pool
* Defeat Cavern boss → tech/cavern modules unlock
* Defeat Witch boss → death/curse spells unlock
* Defeat Heaven boss → holy spells unlock

### Option C: Rifts Allow Wild Cross-Builds

Normal campaign should preserve hero identity.

Rifts can be the place where weird builds happen.

Example:

* Campaign: mostly own-element spell identity
* Rifts: wider spell/card pool, strange combinations, temporary chaos

Recommended approach:

Use Option A + C.

Campaign should be readable. Rifts can be experimental.

## Spell Role Taxonomy

Every spell should have a clear role.

Use these roles:

1. Damage
2. Control
3. Defense
4. Ball manipulation
5. Bullet interaction
6. Summon/minion
7. Combo/payoff
8. Boss utility
9. Economy/mana utility

Each hero should have a balanced kit, but not everything.

Suggested identity:

| Hero        | Identity                                          |
| ----------- | ------------------------------------------------- |
| Fire Mage   | Burn, chain explosions, aggressive board clearing |
| Paladin     | Shield, reflection, precision, survival           |
| Engineer    | Targeting, gadgets, zones, ball control           |
| Necromancer | Decay, souls, minions, sacrifice/snowball         |

## Biggest Current Overlaps

### Duplicate vs Raise

Duplicate creates extra ball copies. Raise creates skeleton helper-balls.

These are too similar.

Recommendation:

* Duplicate should not create permanent full-power ball clones.
* Raise should be Necromancer’s special summon-ball identity.
* Duplicate should become a temporary Holy echo / guardian ball, or move out of Holy.

### Magnet vs Recall vs Slow Time

All three are ball-control/safety tools.

Recommendation:

* Recall = emergency defensive save
* Slow Time = precision / reaction window
* Magnet = offensive targeting tool

Make sure they do not all solve the same “I am about to lose the ball” problem.

### Turret vs Engineer Theme

Turret is currently Fire, but thematically it feels Tech/Engineer.

Recommendation:

* Move Turret to Engineer, or re-theme it as “Flame Familiar” / “Cinder Imp.”
* Fire should not have a literal paddle turret unless the game’s fantasy supports tech-magic hybrids.

### Last Day / Lightning / Rocket / Fireball

Several spells are “cast and blocks die.”

That is okay, but each needs a distinct interaction pattern:

* Fireball / Conflagration: payoff for burning setup
* Lightning: anti-cluster chain
* Rocket: targeted single priority enemy/armor/boss tool
* Last Day: should not be random smite only; it should interact with bullets, shields, or marked targets

## Recommended Spell Slot Model

Each character has:

* Slot 0: Signature spell
* Slot 1: Player-selected spell
* Slot 2: Player-selected spell

Later upgrades can unlock:

* Extra passive spell modifier
* Alternative signature variant
* Fourth spell slot only in late game or rifts

Do not start with too many active spells. Arkanoid already has ball movement, enemy bullets, paddle control, and spell timing.

Three active spells is enough.

## Mana Economy Recommendation

Mana should come from active play, not just passive waiting.

Sources:

* Breaking blocks
* Reflecting enemy bullets
* Maintaining combos
* Killing enemies
* Boss phase breaks
* Necromancer soul bonus
* Engineer passive regeneration

Avoid infinite mana loops.

Critical rule:

Any spell that increases mana gain must have a cap.

Example:

Drain currently gives +6 bonus mana per kill while active.

This can snowball too hard with minions and multi-ball.

Add:

* Max bonus mana per cast
* Reduced returns from summoned minion kills
* No mana from spell-caused chain kills, or reduced mana from them

Recommended rule:

> Direct ball kills grant full mana. Spell/minion kills grant 50% mana. Chain reaction kills grant 25% mana.

This prevents infinite spell loops.

## Fire Mage Review

Current Fire Mage:

* Passive: Ignited kills spread fire to all 8 neighbors
* Signature: Ignite
* Starting kit: Ignite, Conflagration, Fire Wall
* Other spells: Turret, Phoenix, Ashfall

### Fire Identity

Fire should be:

* Aggressive
* Chain-based
* Setup + payoff
* High damage
* Less defensive

### Keep

* Ignite
* Conflagration
* Phoenix
* Ashfall concept

### Change

#### Ignite

Current:

> Next N paddle deflects make ball set blocks alight.

Good spell. Keep.

Change:

* Make burning visually obvious.
* Burning should tick damage and mark blocks as burnable.
* Fire Mage passive should make burning deaths spread.
* Non-Fire heroes can use Ignite, but Fire Mage should be clearly better at it.

Suggested formula:

* Level 1: next 4 paddle deflects ignite first 2 blocks hit
* Upgrade branch A: more deflects
* Upgrade branch B: stronger burn
* Upgrade branch C: burn spreads farther, but costs more

#### Conflagration

Current:

> Detonates every burning block. If no fire exists, erupts fire burst around the ball.

Good payoff spell.

Change:

* Keep as main Fire combo payoff.
* Reduce frustration when no burning blocks exist.
* Add targeting clarity.

Suggested behavior:

> Detonate all burning blocks for damage. If fewer than 3 burning blocks exist, create a fire burst at the ball first, then detonate it.

This makes the spell never feel wasted.

#### Fire Wall

Current:

> Next block hit spawns a 14-block wall of fire spreading outward.

Risk:

* Could feel random because the player may not control exactly where next block hit happens.
* High mana cost needs high clarity.

Change:

> Next ball hit creates a horizontal or vertical fire line through the hit cell. The direction depends on paddle hit zone.

Example:

* Left paddle hit → vertical fire wall
* Center hit → cross-shaped fire wall
* Right paddle hit → horizontal fire wall

This makes it more skill-based.

#### Turret

Current:

> Paddle-mounted turret fires a bolt on each ball catch.

Problem:

* Mechanically good.
* Thematically Tech/Engineer, not Fire.
* Also may overlap with enemies/bullets too much.

Recommendation:

Either move to Engineer or re-theme.

Fire version:

> Flame Familiar: a small fire spirit follows the paddle and shoots burning sparks at burning or nearby blocks.

Engineer version:

> Auto-Turret: deploys a paddle turret that fires at nearest enemy/projectile/block on catch.

Recommended: move Turret to Engineer and replace Fire slot with Flame Nova or Cinder Imp.

#### Phoenix

Current:

> Orbiting entity bound to a ball; scorches blocks it sweeps.

Good. Very fantasy-rich.

Change:

* Make Phoenix the “premium fire aura.”
* It should orbit the strongest/main ball.
* It should not become too invisible in multiball chaos.

Suggested improvement:

> Phoenix orbits the main ball and leaves a short burning trail. When the attached ball is lost, Phoenix dives upward once before disappearing.

This creates emotional value and saves it from feeling like just another DoT.

#### Ashfall

Current:

> While active, each ignite-kill rains ember down that column.

Good synergy, but too narrow.

Change:

> While active, any burning block death drops an ember down that column. Fire Mage ignite kills drop stronger embers.

This lets it work with other fire spells, not only Ignite.

## Fire Mage Final Recommended Kit

Signature:

* Ignite

Starting spells:

* Ignite
* Conflagration
* Phoenix

Unlock later:

* Fire Wall
* Ashfall
* Flame Familiar / Flame Nova

Move Turret to Engineer unless re-themed.

## Paladin Review

Current Paladin:

* Passive: once per level, a lost ball is saved automatically
* Signature: Shield
* Starting kit: Shield, Spear, Duplicate
* Other spells: Penetration, Last Day, Reckoning

### Paladin Identity

Paladin should be:

* Defensive
* Precise
* Reflective
* Anti-bullet
* Good at survival
* Good at boss mechanics

### Keep

* Shield
* Spear
* Reckoning concept

### Change heavily

* Duplicate
* Penetration
* Last Day

#### Shield

Current:

> Barrier above paddle; reflects enemy bullets back up as player bolts.

Excellent signature.

Change:

* Make Shield central to Paladin identity.
* Allow skill expression.

Suggested behavior:

> Creates a holy barrier above paddle. Blocks enemy bullets. Bullets blocked near the center are reflected as stronger holy bolts.

This connects to the character passive idea of “perfect center hit reflects bullets.”

Upgrade branches:

* Wider shield
* Longer duration
* Stronger reflected bolts
* Center-perfect reflection bonus

#### Spear

Current:

> Piercing spear flies straight up through a column.

Good simple low-cost precision spell.

Change:

* Make it more Paladin-specific.

Suggested behavior:

> Fires a holy spear upward. Pierces blocks and enemies. Deals bonus damage to shielded enemies, cursed enemies, or boss armor.

This makes it useful in boss fights.

#### Duplicate

Current:

> Splits active ball into extra copies; clones carry active imbues.

Problem:

This is probably too strong and too generic.

Also, if clones carry active imbues, it can explode balance with Ignite, Penetration, Phoenix, etc.

Recommendation:

Do not keep this as a normal permanent-upgraded Paladin spell in current form.

Options:

1. Move Duplicate to Neutral / Arcane.
2. Rework it into Holy Echo.
3. Make it a Rift card instead of a permanent spell.

Best option:

> Rework Duplicate into Holy Echo: creates 1 temporary echo ball for 8 seconds. Echo ball deals reduced damage and does not carry imbues unless upgraded.

This prevents it from becoming the obvious best spell in the game.

Suggested rule:

* Echo ball damage: 50%
* Duration: 8 seconds
* Does not generate full mana
* Does not duplicate further
* Upgrade can allow it to carry 1 selected property

#### Penetration

Current:

> Ball punches through blocks without bouncing.

Problem:

Great Arkanoid mechanic, but not clearly Holy. Also overlaps with modules.

Recommendation:

Move Penetration to Ball Module or Neutral spell.

If kept in Holy, re-theme:

> Consecrated Strike: next N hits pierce blocks and destroy enemy bullets on contact.

This makes it Paladin-like.

Better module version:

> Piercing Core Module: first block hit after paddle deflect is pierced.

Recommendation:

Do not keep Penetration as a Paladin spell unless re-themed.

#### Last Day

Current:

> Repeatedly smites random blocks across the board.

Problem:

High-cost random auto-damage is not very interactive.

Change:

> Judgment Day: for several seconds, reflected bullets and Spear hits call down smites on nearby blocks/enemies.

This makes it interact with Paladin gameplay.

Alternative:

> Smite marked targets. Shield-reflected bullets mark enemies/blocks. Last Day detonates all marks.

This is much better than random block smiting.

#### Reckoning

Current:

> Arm once per level; enemy damage charges meter; at threshold fires judgment pillars on 5 columns.

Problem:

* “Instant meter auto-fires” is confusing.
* Threshold formula looks dangerous.
* If threshold becomes too low, it may auto-trigger constantly.
* If it requires taking enemy damage, it rewards playing badly unless carefully designed.

Change:

> Reckoning: for 8 seconds, blocked/reflected enemy bullets charge a judgment meter. When full, fires holy pillars in 5 columns.

This rewards defense skill, not taking damage.

Suggested trigger:

* Shield blocks: +1 charge
* Perfect paddle bullet reflect: +2 charge
* Taking damage: +1 emergency charge
* At 5 charges: fire judgment pillars

## Paladin Final Recommended Kit

Signature:

* Shield

Starting spells:

* Shield
* Spear
* Holy Echo

Unlock later:

* Consecrated Strike
* Judgment Day
* Reckoning

Move original Penetration to module or neutral.

Rework Duplicate into Holy Echo.

## Engineer Review

Current Engineer:

* Passive: mana regenerates faster
* Signature: Overload
* Starting kit: Overload, Lightning, Rocket
* Other spells: Containment Field, Magnet, Tesla Grid

### Engineer Identity

Engineer should be:

* Gadgets
* Targeting
* Zones
* Ball control
* Enemy emitter suppression
* Technical combo setups

### Keep

* Lightning
* Rocket
* Containment Field
* Magnet
* Tesla Grid concept

### Change

* Overload needs stronger skill-based placement.
* Starting kit should include Magnet or Containment Field, not only direct damage.

#### Overload

Current:

> Plants a bomb-block at row 3 below paddle; chain-detonates neighbors.

Problem:

A bomb near row 3 below paddle may feel disconnected from block field action.

Change:

> Next ball hit plants an Overload Charge into the hit block. After short delay, it explodes and chains to adjacent damaged or electric blocks.

This makes the signature depend on ball control.

Suggested behavior:

* Cast Overload
* Next ball-block hit plants charge
* Charge explodes after 0.5s
* Explosion chains to nearby cracked/electrified blocks

Upgrade branches:

* Larger chain radius
* Faster detonation
* Bonus vs armored blocks
* Electrifies blocks for Tesla synergy

#### Lightning

Current:

> Strikes random block and arcs to nearby blocks.

Good, but random targeting can feel low-skill.

Change:

> Strikes the block nearest to the ball or nearest to the paddle aim line, then chains.

This gives the player some control.

#### Rocket

Current:

> Homing missile locks nearest block and explodes.

Problem:

Overlaps with Lightning as “direct cast damage.”

Change:

Make Rocket the anti-priority spell.

Suggested behavior:

> Fires a homing rocket at the nearest enemy, boss weak point, armored block, or highest-HP block.

Priority order:

1. Boss weak point
2. Active enemy emitter
3. Armored block
4. Highest HP block
5. Nearest block

This makes Rocket distinct.

#### Containment Field

Current:

> Deploys zone; melts blocks inside and suppresses enemy emitters.

Excellent spell.

This is one of the best current spells because it interacts with enemies, not only blocks.

Change:

* Add clear visual circle.
* Suppressed enemy emitters should show disabled state.
* Good against bullet-heavy levels.

Suggested starting kit candidate.

#### Magnet

Current:

> Steers ball toward blocks.

Good. Very Arkanoid-specific.

Problem:

If too strong, it plays the game for the player.

Change:

> Magnet gently curves the ball toward valid block targets, but only after paddle deflect or while above mid-screen.

This keeps player agency.

Recommended:

Engineer starting kit should include Magnet because it reinforces Engineer as the control/gadget hero.

#### Tesla Grid

Current:

> Electrify walls; once both side walls are hit, a full-width horizontal lightning curtain fires.

Great idea, but must be readable.

Change:

> For 10 seconds, side walls become charged. Hitting left and right wall arms a Tesla Curtain. When both are charged, a horizontal lightning beam fires through the ball’s current height.

This rewards intentional angle play.

UI requirement:

* Left wall charge icon
* Right wall charge icon
* Beam preview when armed

## Engineer Final Recommended Kit

Signature:

* Overload

Starting spells:

* Overload
* Magnet
* Containment Field

Unlock later:

* Lightning
* Rocket
* Tesla Grid
* Auto-Turret if Turret is moved from Fire

## Necromancer Review

Current Necromancer:

* Passive: each block kill grants extra mana/souls
* Signature: Raise
* Starting kit: Raise, Rot & Collapse, Drain
* Other spells: Bonewalker, Bone Golem, Lich’s Gaze

### Necromancer Identity

Necromancer should be:

* Decay
* Souls
* Minions
* Sacrifice
* Snowballing
* Risk/reward

### Current Problem

Necromancer has too much mana snowball potential:

* Passive gives mana on block kill
* Drain gives bonus mana on block kill
* Raise creates extra helper balls
* Bonewalker kills blocks
* Bone Golem destroys blocks
* Lich’s Gaze makes blocks take more damage

This can easily become infinite mana / auto-play.

### Keep

* Raise
* Rot & Collapse
* Bone Golem
* Lich’s Gaze concept

### Change

* Drain needs a cap
* Bonewalker and Bone Golem need clearer separation
* Minion kills should not generate full mana

#### Raise

Current:

> Spawns friendly skeleton helper-ball; smaller, bounces and breaks blocks.

Good signature.

Change:

Raise should be Necromancer’s special version of multiball. Therefore Duplicate/Holy Echo must be weaker or different.

Suggested rules:

* Skeleton ball lasts 10 seconds or until 12 hits
* Skeleton ball deals reduced damage
* Skeleton ball generates reduced mana
* Skeleton ball prioritizes cursed/rotted blocks if possible

Upgrade branches:

* More skeleton balls
* Longer duration
* Skeletons apply rot
* Skeletons collect souls but at reduced rate

#### Rot & Collapse

Current:

> Ball permanently lowers target block max HP; rotted block death drops blocks above into gap.

Very good. This is unique and should stay.

Risk:

Dropping blocks can create physics/layout bugs.

Implementation rule:

* Collapse should be grid-based and deterministic.
* Do not physically simulate falling blocks unless necessary.
* Preview or animate collapse clearly.

Suggested improvement:

> Rotted blocks become brittle. When destroyed, blocks above shift down one cell and take minor rot damage.

#### Drain

Current:

> Each block kill yields +6 bonus mana while active.

Problem:

Too much snowball potential.

Change:

> Soul Harvest: mark nearby blocks/enemies. Killing marked targets grants bonus mana up to a cap.

Suggested:

* Duration: 6 seconds
* Bonus mana per marked kill: +4
* Max bonus per cast: 40
* Minion/spell kills grant 50% bonus
* Chain kills grant 25% bonus

This keeps it useful but not infinite.

#### Bonewalker

Current:

> Skeleton walks rooftops of block field and melees each block it stands on.

Problem:

May overlap with Raise and Bone Golem.

Recommendation:

Either cut from initial version or make it very distinct.

Better version:

> Bonewalker crawls along the top surface of the brick formation, applying rot but dealing low damage.

This makes it a rot applier, not another generic minion DPS.

#### Bone Golem

Current:

> Bodyguard climbs a column from paddle, bulldozes 3-wide, tanks enemy fire until HP depleted.

Good. Very distinct if it blocks bullets.

Change:

Make Bone Golem the Necromancer defensive/offensive hybrid.

Suggested behavior:

* Summons golem at paddle position
* Golem climbs upward
* Blocks enemy bullets
* Crushes blocks in a narrow path
* Dies when HP is depleted

This is strong and readable.

#### Lich’s Gaze

Current:

> Slow lighthouse sweep at paddle curses blocks it crosses; cursed blocks take bonus damage from ball.

Good, but needs clarity.

Change:

> A rotating beam from the paddle curses blocks/enemies. Ball hits against cursed targets deal bonus damage and generate souls.

This creates positioning gameplay.

## Necromancer Final Recommended Kit

Signature:

* Raise

Starting spells:

* Raise
* Rot & Collapse
* Bone Golem

Unlock later:

* Soul Harvest
* Lich’s Gaze
* Bonewalker

Drain should become Soul Harvest with a cap.

## Neutral Spell Review

Current Neutral:

* Recall
* Slow Time

### Recall

Current:

> Steers all balls back toward the paddle.

Good emergency spell.

Change:

* Make it a defensive utility.
* It should not also be an offensive targeting spell.

Suggested:

> Pulls all balls toward safe return arcs above the paddle for a short duration.

### Slow Time

Current:

> Slows every ball to ×0.5 speed.

Good skill-assist spell.

Risk:

Can make difficult levels trivial if spammed.

Change:

* Also slow enemy bullets, or choose clearly:

  * Slow balls only = aiming aid
  * Slow enemies/bullets too = defensive spell

Recommended:

> Slow Time slows balls and enemy bullets, but reduces ball damage during the effect.

This prevents it from becoming pure upside.

## New Spell List Proposal

### Fire

| Spell                       | Role                        | Status                 |
| --------------------------- | --------------------------- | ---------------------- |
| Ignite                      | Signature, burn setup       | Keep                   |
| Conflagration               | Burn payoff explosion       | Keep, improve fallback |
| Phoenix                     | Fire aura / ball companion  | Keep                   |
| Fire Wall                   | Skill-based fire line       | Rework targeting       |
| Ashfall                     | Burning death column payoff | Keep, broaden trigger  |
| Flame Familiar / Flame Nova | Replace or re-theme Turret  | Add/rework             |

### Holy

| Spell              | Role                            | Status |
| ------------------ | ------------------------------- | ------ |
| Shield             | Signature, defense/reflection   | Keep   |
| Spear              | Precision projectile            | Keep   |
| Holy Echo          | Reworked Duplicate              | Rework |
| Consecrated Strike | Reworked Penetration            | Rework |
| Judgment Day       | Reworked Last Day               | Rework |
| Reckoning          | Reflection/bullet charge payoff | Rework |

### Tech

| Spell             | Role                           | Status                    |
| ----------------- | ------------------------------ | ------------------------- |
| Overload          | Signature, planted charge      | Rework trigger            |
| Magnet            | Ball control                   | Keep                      |
| Containment Field | Zone + suppress emitters       | Keep                      |
| Lightning         | Chain damage                   | Keep, less random         |
| Rocket            | Priority target / boss utility | Rework targeting          |
| Tesla Grid        | Wall-angle skill combo         | Keep, improve readability |
| Auto-Turret       | Optional, moved from Fire      | Optional                  |

### Death

| Spell          | Role                            | Status                  |
| -------------- | ------------------------------- | ----------------------- |
| Raise          | Signature, skeleton helper ball | Keep, cap duration/mana |
| Rot & Collapse | Decay + board manipulation      | Keep                    |
| Bone Golem     | Defensive column summon         | Keep                    |
| Soul Harvest   | Reworked Drain                  | Rework with cap         |
| Lich’s Gaze    | Curse beam                      | Keep                    |
| Bonewalker     | Rot-applier minion              | Delay or rework         |

### Neutral

| Spell     | Role                  | Status             |
| --------- | --------------------- | ------------------ |
| Recall    | Emergency ball save   | Keep               |
| Slow Time | Precision/defense aid | Keep with tradeoff |

## Recommended Starting Kits

### Fire Mage

* Ignite
* Conflagration
* Phoenix

Reason:

This gives setup, payoff, and fantasy immediately.

### Paladin

* Shield
* Spear
* Holy Echo

Reason:

This gives defense, precision, and safe multiball-style power without making Paladin too generic.

### Engineer

* Overload
* Magnet
* Containment Field

Reason:

This makes Engineer feel like the control/gadget hero immediately.

### Necromancer

* Raise
* Rot & Collapse
* Bone Golem

Reason:

This gives summon identity, decay identity, and defensive/offensive minion identity.

## Spells to Delay or Cut from Initial Release

Delay:

* Bonewalker
* Tesla Grid
* Ashfall
* Judgment Day / Last Day
* Reckoning
* Flame Familiar / Turret

Reason:

These are more complex or require strong visual clarity.

Initial release should prioritize spells that directly prove the core gameplay:

* Ignite
* Conflagration
* Phoenix
* Shield
* Spear
* Holy Echo
* Overload
* Magnet
* Containment Field
* Raise
* Rot & Collapse
* Bone Golem
* Recall
* Slow Time

That is 14 spells, enough for first playable balance.

Then add the remaining spells once the core loop is validated.

## Spell Upgrade System

Use deterministic spell upgrade branches.

Example structure:

Each spell has levels 1–5.

* Level 1: base unlock
* Level 2: numeric improvement
* Level 3: branch choice
* Level 4: numeric improvement
* Level 5: major upgrade / evolved form

Example: Shield

* Level 1: creates barrier
* Level 2: +duration
* Level 3 branch:

  * wider shield
  * stronger reflected bullets
  * shorter cooldown/mana cost
* Level 4: improves chosen branch
* Level 5: perfect-center blocks fire a holy beam

Example: Magnet

* Level 1: mild ball steering
* Level 2: longer duration
* Level 3 branch:

  * stronger steering
  * affects multiple balls
  * also pulls mana shards/powerups
* Level 4: improves branch
* Level 5: first target hit during Magnet is overloaded/electrified

Do not require duplicates for these upgrades.

Use:

* Fire Essence
* Holy Essence
* Tech Essence
* Death Essence
* Neutral Essence
* Spell Tomes

Sources:

* Bosses
* Star chests
* Rifts
* Daily/weekly rewards
* Campaign rank clears

## Module Progression

Modules can use Tower-style duplicate progression.

Example:

* Unlock random module
* Duplicates needed: 3 / 5 / 7 / 11
* Modules are passive loadout modifiers
* Module slots:

  * Ball module
  * Paddle module
  * Spell module

This is enough random progression.

Do not also make spells random duplicate progression.

## Rift Spell/Card Interaction

Rifts should use temporary cards to create crazy builds.

Example Rift Cards:

| Card               | Effect                                               |
| ------------------ | ---------------------------------------------------- |
| Glass Cannon       | Ball damage +80%, max HP -1                          |
| Bullet Feast       | Reflected bullets grant mana                         |
| Infernal Multiball | +2 balls, but ball speed +30%                        |
| Cursed Paddle      | Paddle size -30%, rewards +30%                       |
| Holy Shield        | Every 20 seconds block one hit                       |
| Spellstorm         | Spell cooldown/mana cost reduced, enemies shoot more |
| Bone Army          | Minion duration +50%, minion mana gain -50%          |
| Tesla Chamber      | Wall bounces charge lightning                        |

Rift cards should be temporary.

Do not put these into permanent inventory at first.

## Star System Recommendation

Use stars for mastery and controlled gating.

Each campaign level has 3 stars:

1. Clear the level
2. Clear with HP/balls remaining
3. Complete special challenge

Special challenge examples:

* Reflect 5 bullets
* Kill all enemies
* Clear under 90 seconds
* Do not lose HP
* Break 30 burning blocks
* Use only 1 spell
* Kill boss phase with Spear
* Keep combo above X

Do not hard-gate main campaign too aggressively.

Recommended gating:

* Next location requires approximately 60–70% of available stars
* 100% stars unlock bonus chests, cosmetics, hard mode, or side bosses
* Campaign rank progression may require more stars, but not perfect stars

Avoid:

> Player cannot continue because they missed one annoying challenge star.

## Campaign Rank Recommendation

Campaign ranks should not only increase HP/damage.

Each rank should add world mutations.

Examples:

### Hell

* Rank 1: normal fire bricks
* Rank 2: burning bricks spread if ignored
* Rank 3: enemy bullets leave fire trails
* Rank 4: boss fire phases become faster

### Cavern

* Rank 1: armored rocks
* Rank 2: falling stalactites
* Rank 3: ricochet crystals alter ball angles
* Rank 4: armored enemies shield nearby blocks

### Witch Village

* Rank 1: cursed blocks
* Rank 2: curses shrink paddle temporarily
* Rank 3: enemies transform blocks
* Rank 4: boss curses one spell slot temporarily

### Heaven

* Rank 1: shielded blocks
* Rank 2: healing enemies
* Rank 3: light beams block angles
* Rank 4: boss has rotating shield phases

This makes replaying campaign feel mechanically different.

## Boss Recommendations

Each location should end with a boss that tests that world’s mechanic.

### Hell Boss

* Fire projectiles
* Burning brick spread
* Weak points exposed after extinguishing fire clusters

### Cavern Boss

* Armored shell
* Requires angle control
* Stalactite hazards
* Weak to Overload / Spear / Rot

### Witch Boss

* Curses paddle/ball/spell cooldown
* Summons cursed bricks
* Requires cleansing or precise target damage

### Heaven Boss

* Shield phases
* Bullet reflection opportunities
* Requires timing and precision

Bosses should unlock major things:

* New character
* New spell
* New module family
* Next location
* Rift card pool

## Daily Quest Recommendation

Daily quests should push fun behaviors, not generic chores.

Bad examples:

* Play 3 levels
* Upgrade once
* Open shop

Better examples:

* Reflect 10 bullets
* Break 300 blocks
* Clear 1 level without losing HP
* Complete 1 rift floor
* Earn 5 stars
* Kill 20 enemies with ball hits
* Trigger Conflagration on 8+ burning blocks
* Block 5 bullets with Shield
* Collapse 10 blocks with Rot
* Hit both walls before Tesla fires

Use:

* 5 daily quests offered
* Player needs 3 for the main daily chest
* Extra completion gives bonus but is not mandatory

This reduces chore feeling.

## Weekly Event Recommendation

Do not start with full special event locations.

Start with Weekly Rift Mutation.

Examples:

| Weekly Mutation | Effect                                          |
| --------------- | ----------------------------------------------- |
| Hell Week       | Fire bricks appear in all rifts                 |
| Bullet Hell     | Enemies shoot more, reflected bullets deal more |
| Tiny Paddle     | Paddle smaller, rewards higher                  |
| Spellstorm      | Spell costs lower, enemies stronger             |
| Necro Week      | Rotted blocks spawn more often                  |
| Tesla Week      | Wall bounces generate charge                    |
| Boss Rush       | Every 3rd rift floor is a miniboss              |

This gives LiveOps with lower content cost.

Later, after the game proves retention, add full event locations.

## MVP Spell Set

For first balanced version, use only these 14 spells:

### Fire

* Ignite
* Conflagration
* Phoenix

### Holy

* Shield
* Spear
* Holy Echo

### Tech

* Overload
* Magnet
* Containment Field

### Death

* Raise
* Rot & Collapse
* Bone Golem

### Neutral

* Recall
* Slow Time

Why only 14?

Because each spell needs:

* VFX
* SFX
* UI icon
* tooltip
* upgrade logic
* balance tuning
* boss interaction testing
* rift interaction testing
* mana economy testing
* mobile performance testing

26 spells at launch is likely too much to balance.

Better:

* Launch with fewer strong spells
* Make each feel excellent
* Add the rest through campaign ranks/events

## Concrete Change List for Agents

### Must Change

1. Remove duplicate-based progression from spells.
2. Keep duplicate-based progression only for modules.
3. Change global spell rolling to weighted affinity rolling.
4. Rework Duplicate into Holy Echo.
5. Move or re-theme Turret.
6. Add caps to Drain/Soul Harvest and Necromancer mana generation.
7. Rework Last Day into an interactive Judgment spell.
8. Rework Reckoning to charge from blocked/reflected bullets, not enemy damage taken.
9. Make Overload trigger from next ball hit instead of planting near paddle.
10. Make Lightning/Rocket target selection more player-readable.
11. Make Tesla Grid show left/right wall charge UI.
12. Make Fire Wall placement more skill-based.
13. Reduce launch spell count from 26 to around 14.

### Should Change

1. Engineer starting kit should include Magnet or Containment Field.
2. Necromancer starting kit should include Bone Golem instead of Drain.
3. Fire Mage starting kit should include Phoenix instead of Fire Wall if Fire Wall remains complex.
4. Paladin should not start with full-power Duplicate.
5. Neutral spells should remain utility, not best-in-slot.
6. Spell/minion/chain kills should generate reduced mana.
7. High-cost spells should involve skill or setup, not random board clearing.

### Can Delay

1. Bonewalker
2. Ashfall
3. Tesla Grid
4. Judgment Day
5. Reckoning
6. Flame Familiar / Turret
7. Full weekly event locations

## Final Recommended Design Direction

The game should not be:

> Arkanoid with The Tower’s full economy copied on top.

The game should be:

> Arkanoid roguelite RPG with campaign mastery, bosses, spells, characters, modules, and rifts.

Spells should make the player feel clever and powerful during the level.

Modules should provide long-term build optimization.

Rift cards should create temporary crazy rule changes.

Stars should reward mastery.

Bosses should validate builds and skill.

Campaign ranks should remix locations with new mechanics.

This separation will make the game deeper without making it messy.
