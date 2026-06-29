# Spell Reference

All 26 spells. Costs and formulas are from `SpellCatalog.cs` + `config/characters.json`.

**Affinity:** each hero gets −20% mana cost on spells of their element (×0.8 mana).  
**Signature:** locked to that hero; always in slot 0.  
**Starting kit:** the 3 spells a hero begins a run with (includes their signature).  
**Global pool:** all non-signature spells can be rolled by any hero via Souls.

---

## Fire Mage — element: **Fire**

*Passive:* Ignited kills spread fire to all 8 neighbors.  
*Signature:* Ignite | *Starting kit:* Ignite, Conflagration, Fire Wall

| ID | Name | Cost | Archetype | Trigger | Effect | Formula |
|----|------|------|-----------|---------|--------|---------|
| `ignite` | **Ignite** ⭐ | 25 (20 w/ affinity) | Imbue | Next N paddle deflects | Ball sets hit blocks alight; fire spreads to neighbors (passive helps). | hits = 4 + 1/lvl |
| `fireball` | **Conflagration** | 60 (48) | Instant | Cast | Detonates every burning block. If no fire exists, erupts a fire burst around the ball. | dmg per detonation = 6 + 2/lvl |
| `firewall` | **Fire Wall** | 80 (64) | Imbue → Placement | Next block hit | Ball's next block hit spawns a 14-block wall of fire spreading outward (r=96). | count=14, radius=96 |
| `turret` | **Turret** | 30 (24) | TimedAura | Fires on every paddle catch | Paddle-mounted turret fires a bolt on each ball catch. | dmg=2, speed=460, dur=7+1/lvl s |
| `phoenix` | **Phoenix** | 70 (56) | TimedAura | Cast | Orbiting entity bound to a ball; scorches blocks it sweeps (tick every 0.45 s, r=56). | dmg/tick=2, dur=6+1/lvl s |
| `ashfall` | **Ashfall** | 40 (32) | TimedAura | Cast | While active, each ignite-kill rains an ember down that column (pierces ~3 blocks, 2 dmg). | dur=6+1/lvl s |

---

## Paladin — element: **Holy**

*Passive:* Once per level, a lost ball is saved automatically.  
*Signature:* Shield | *Starting kit:* Shield, Spear, Duplicate

| ID | Name | Cost | Archetype | Trigger | Effect | Formula |
|----|------|------|-----------|---------|--------|---------|
| `shield` | **Shield** ⭐ | 25 (20) | Placement | Cast | Barrier above paddle; reflects enemy bullets back up as player bolts. | lifetime=4+0.5/lvl s, width×1.2 |
| `spear` | **Spear** | 15 (12) | Projectile | Cast | Piercing spear flies straight up through a column. | dmg=1+1/lvl, pierce=8 blocks, speed=620 |
| `duplicate` | **Duplicate** | 40 (32) | Instant | Cast | Splits active ball into extra copies; clones carry active imbues. | copies=1+1/lvl |
| `penetration` | **Penetration** | 25 (20) | Imbue | Next N paddle deflects | Ball punches through blocks without bouncing. | hits=3+1/lvl |
| `lastday` | **Last Day** | 80 (64) | TimedAura | Cast | Repeatedly smites random blocks across the board. | dmg=2, cooldown=0.5 s, dur=8+1/lvl s |
| `reckoning` | **Reckoning** | 35 (28) | Instant (meter) | Auto-fires when meter fills | Arm once per level; enemy damage charges the meter; at threshold fires judgment pillars on 5 columns. | threshold=3−1/lvl hits, smite dmg=3+1/lvl |

---

## Engineer — element: **Tech**

*Passive:* Mana regenerates faster.  
*Signature:* Overload | *Starting kit:* Overload, Lightning, Rocket

| ID | Name | Cost | Archetype | Trigger | Effect | Formula |
|----|------|------|-----------|---------|--------|---------|
| `lightning` | **Lightning** | 25 (20) | Instant (chain) | Cast | Strikes a random block; arcs to nearby blocks. | dmg=2+1/lvl, chainJumps=6, chainRadius=110 |
| `rocket` | **Rocket** | 30 (24) | Projectile | Cast | Homing missile locks on nearest block and explodes. | dmg=2+1/lvl, aoe=2 flat, aoeRadius=72, speed=280 |
| `radiation` | **Containment Field** | 45 (36) | Placement (zone) | Cast | Deploys a zone; melts blocks inside and suppresses enemy emitters in range. | tick dmg=1+1/lvl per 0.5 s, radius=140, lifetime=4 s |
| `magnet` | **Magnet** | 20 (16) | TimedAura | Cast | Steers the ball toward blocks. | steer=120°/s, dur=4+1/lvl s |
| `overload` | **Overload** ⭐ | 35 (28) | Placement (bomb) | Cast | Plants a bomb-block at row 3 (below paddle); chain-detonates neighbors. | aoeRadius=1+1/lvl cells |
| `tesla` | **Tesla Grid** | 80 (64) | Projectile (triggered) | Both side walls bounced | Electrify the walls; once BOTH walls are hit, a full-width horizontal lightning curtain fires. | dmg=3+1/lvl per curtain |

---

## Necromancer — element: **Death**

*Passive:* Each block kill grants extra mana (souls).  
*Signature:* Raise | *Starting kit:* Raise, Rot & Collapse, Drain

| ID | Name | Cost | Archetype | Trigger | Effect | Formula |
|----|------|------|-----------|---------|--------|---------|
| `decay` | **Rot & Collapse** | 15 (12) | Imbue | Next N paddle deflects | Ball permanently lowers target block's max HP (rots it); rotted-block death drops blocks above into the gap. | hits=4+1/lvl |
| `skeleton` | **Bonewalker** | 40 (32) | TimedAura (minion) | Cast | Skeleton walks rooftops of the block field, meleeing each block it stands on. | dur=5+1/lvl s, melee cadence baked in |
| `drain` | **Drain** | 20 (16) | TimedAura | Cast | Each block kill yields +6 bonus mana while active. | bonusMana=6/kill, dur=6+1/lvl s |
| `golem` | **Bone Golem** | 70 (56) | TimedAura (minion) | Cast | Bodyguard climbs a column from the paddle, bulldozes 3-wide, and tanks enemy fire until its HP is depleted. | HP-based (no duration); lvl scales HP |
| `mage` | **Lich's Gaze** | 45 (36) | TimedAura (beam) | Cast | Slow lighthouse sweep at the paddle curses every block it crosses; cursed blocks take +bonus dmg from ball. | curse bonus=2+1/lvl, dur=4+1/lvl s |
| `raise` | **Raise** ⭐ | 35 (28) | Instant | Cast | Spawns a friendly skeleton helper-ball (smaller, r×0.85) that bounces and breaks blocks alongside yours. | copies=1+1/lvl |

---

## Neutral — no affinity, available to all heroes

| ID | Name | Cost | Archetype | Trigger | Effect | Formula |
|----|------|------|-----------|---------|--------|---------|
| `recall` | **Recall** | 15 | TimedAura | Cast | Steers all balls back toward the paddle — emergency save. | steer=240°/s, dur=2.5+0.5/lvl s |
| `slowtime` | **Slow Time** | 20 | TimedAura | Cast | Slows every ball to ×0.5 speed — aiming and reaction aid. | dur=4+0.5/lvl s |

---

## Affinity cheat-sheet

| Hero | Element | Gets −20% mana on |
|------|---------|-------------------|
| Fire Mage | fire | Ignite, Conflagration, Fire Wall, Turret, Phoenix, Ashfall |
| Paladin | holy | Shield, Spear, Duplicate, Penetration, Last Day, Reckoning |
| Engineer | tech | Lightning, Rocket, Containment Field, Magnet, Overload, Tesla Grid |
| Necromancer | death | Rot & Collapse, Bonewalker, Drain, Bone Golem, Lich's Gaze, Raise |
| — | neutral | Recall, Slow Time (no discount, no owner) |

Any hero can roll any non-signature spell from the global pool via Souls; affinity is a cost discount only, not a lock.
