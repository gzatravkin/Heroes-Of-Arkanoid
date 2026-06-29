# Overnight session — 2026-06-13

Autonomous session. **Nothing committed** — all changes are in the working tree for review.

## 1. Pickable spells system (the roadmap — `2026-06-13-pickable-spells-roadmap.md`)

Replaced the per-class **fixed 5-spell kit** with the design's **signature + drafted-pool** model
(docs/04 §3/§4.1/§5). Signature locked in hotbar slot 0; the rest are owned/equipped from a shared
pool and drafted in-run. **Phases 1–5 complete** (only S14 "Raise as a true summon" deferred — a
design call). See the roadmap doc for the full breakdown. Highlights:

- **Backend:** `characters.json` signature/starting/pool; `Profile.EquippedSpells` +
  `UnlockedSpellSlots`; `Loadouts` resolver; `SpellEndpoints` (/spells, equip, unequip);
  `GameInstance.SetLoadout`/`DraftSpell` + `CastSlot` reads the loadout; snapshot exposes it.
- **Equip UI:** `LoadoutScene` (signature locked, variable slots, Slots-Full cap, character name in
  header), HUD hotbar reads the snapshot loadout, menu + character-screen entry points.
- **In-run picks:** dungeon floor-clears can draft any pool spell (`spell:` choices); campaign
  biome-boss clears permanently unlock pool spells + grow the hotbar (cap 5).
- **Spell descriptions** (S15): behavior-accurate one-liners for all 20 spells on Skills, Loadout,
  and the pick overlay.
- **Reward overlay** now announces "+1 Spell Slot!" and "New spells: …" on a boss clear.

## 2. Block-break animation (B1) — the "really weird" one

`Effects.spawnBlockDestroy` was stacking **three** simultaneous bursts (oversized crack-strip +
additive fireball + a per-biome "secondary" that wrongly played the *vaza/hell-ball death* on every
brick). Replaced with a coherent **biome shatter (normal blend, snappy) + one small biome-tinted
spark**. Removed the dead `getBiomeSecondaryStrip`.

## 3. Content fidelity

- **Paladin Duplicate → smaller balls** (docs/01 §61: "clones a ball into N *smaller* balls").
  Clones now spawn at 0.8× radius. Fidelity test added. **Made it visible too:** the snapshot `BallDto`
  had no radius, so the renderer drew every ball at a fixed size — the smaller clone was an *invisible*
  hitbox shrink (a stealth nerf). Added `radiusScale` to the snapshot and per-ball sprite/halo/aura
  scaling in `BallLayer`, so smaller balls now actually look smaller and the hitbox matches the visual.
  Verified on screen (two balls, the clone clearly ~0.8×).

## 4. Spell leveling actually does something now (real bug)

The Skills screen lets you spend points to level **any** spell, but only the 4 Fire-Mage starters had
non-zero `*PerLevel` scaling — leveling the other 16 was a **silent no-op** (points wasted), despite
docs/04 §4.1 "spells level up." Fixed for **13** spells, at the same rate as the working 4 (consistency, not new balance):
- **Projectile damage/level** — Spear, Rocket, Golem, Skeletal Mage.
- **TimedAura duration/level** — Skeleton, Drain, Magnet, Last Day, Phoenix.
- **Imbue hits/level** — Penetration, Decay (mirrored Ignite's scaling; the deflect-apply code read
  `.Hits` flat — now level-scaled).
- **Damage/level (small code change)** — Lightning (instant), Radiation (zone).
- Tests added (Spear, Decay, Lightning, Radiation, Phoenix). **Still dead-leveling (balance-sensitive
  target must be chosen):** Shield (barrier lifetime/width), Duplicate (copies), Overload (bomb) —
  logged in the gap doc.

## 5. Dungeon UX / balance (found via regression)

- **Dungeon pick mix** (docs/04 §5): the Phase-3 draft initially dumped all ~16 pool spells into the
  choice pool (~40% spells, crowding out the dungeon-EXCLUSIVE relics/cores). `GenerateChoices` now
  reserves exactly **one** spell slot + fills the rest from relics/cores/mods (shuffled) — a reliable
  mix, never an all-spells pick.
- **Dungeon run summary completeness:** the "Collected Buffs" row showed only relics + ball-cores —
  **paddle mods AND drafted spells were silently omitted**. Now all four acquisition types appear
  (drafted spells via the `spell:` tag + a spell glyph). This surfaced as a real `dungeon.spec`
  regression (picking a spell left the buffs row empty) and is now fixed + guarded.
- **Dungeon-clear crystal label** (round-2-recs §5): the clear overlay showed `profile.crystals`
  (total balance) under "Permanent Reward" — read as if the run granted your whole balance. Now shows
  **"+{run.rewardCrystals} Crystals"** (the actual run grant) with a quiet "Total: N" sub-line.

## Verification

- Backend **337/337** (xUnit). Full Playwright **203/203** (workers=1) green through every change;
  a closing full E2E run validated the final cumulative diff. New specs: `spell-loadout` (8),
  `spell-picks` (3, incl. reward-overlay unlock display), plus added loadout/leveling unit tests.
- Screens reviewed via screenshots: loadout, pick overlay (spell cards), Skills descriptions, menu
  dock, boss-clear reward overlay. All on-theme and readable.
- Verified (not assumed): Fire Wall **does** reach/clear the top row and softens tough blocks — the
  earlier "doesn't reach" suspicion was a mid-rise capture artifact, not a bug.

## Deferred / needs design input

- **S14 — Necromancer "Raise" as a true summoned helper-ball/entity** vs the current timer-turret
  (docs/04 §3; round-2-recommendations §1). A design call (helper-ball vs entity, and how it
  interacts with the existing Skeleton spell).
- Balance items needing difficulty intent: combo ceiling (4), Fire Wall sweep strength.
