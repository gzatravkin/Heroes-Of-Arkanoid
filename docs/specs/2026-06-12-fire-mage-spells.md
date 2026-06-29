# Fire Mage Spells — Behavior Contract

Source of truth: `docs/01-current-game-design.md` §59 and `docs/04-new-game-design.md` §2.
This doc is the **acceptance contract**: every test and review checks against the "Must" rows here, not against "a projectile spawned".

## ignite (passive imbue, 0 mana)
- **Design (§59):** "ball hitting paddle near center gains a fire/explosion effect" + "Damaged blocks keep burning" + "spreads DoT over time, with limits".
- **Must:**
  1. Casting arms ignite; the **next paddle deflect** imbues the ball (`IgniteHitsLeft > 0`).
  2. An ignited ball hitting a block **sets that block on fire** (a burn state with remaining time), not just instant damage.
  3. A burning block takes **damage over time** on a fixed cadence until it dies or the burn expires.
  4. Fire **propagates ring-by-ring to cardinal neighbours over time**, capped at N generations (no full-board instant wipe).
- **Reject:** the old "kill spreads one instant ring of chip damage" model.

## fireball (projectile, 25 mana)
- **Design (§59):** "Ring: spawns a fireball projectile."
- **Must:** a fireball projectile launches upward from the paddle, damages the first block it hits, with a **small AoE splash** (fire identity).

## firewall (placement, 35 mana)
- **Design (§59):** "Wall: a spreading fire wall that burns blocks over time."
- **Must:** a wall rises up the board; blocks in its band take damage over time **and are set on fire** (ties into the burn system) as it passes.

## turret (active, 25 mana)
- **Design (§59):** "Turret: paddle-mounted auto-firing turret." Design-doc identity (user-confirmed): **fires when the ball is caught/deflected by the paddle**, for a duration.
- **Must:**
  1. Casting activates the turret for a duration (HUD `turretActive` true).
  2. A bolt fires **on each paddle deflect** while active — **zero bolts if the ball is never deflected**.
- **Reject:** the old metronome (`tickInterval`) auto-fire unrelated to the ball.

## phoenix (active, 30 mana)
- **Design (§59):** "Phoenix: a damaging phoenix **orbits a random ball**."
- **Must:**
  1. Casting spawns a **Phoenix entity** with its **own position**, bound to a random alive ball, for a duration.
  2. The phoenix **orbits** its target ball (position distinct from the ball each tick).
  3. It damages blocks it passes over (within its own hit radius), on a damage cadence.
  4. It is **visible** — serialized in the snapshot and drawn by a dedicated render layer (`phoenixbirthanimpic` art).
- **Reject:** the old invisible AoE pulse centered on `Balls[0]`.

## Process gates (per CLAUDE.md)
- Tests assert the **Must** rows (trigger + identity), and were written to FAIL against the pre-fix code.
- FM6 playtest: cast each spell in a Playwright scenario, screenshot reviewed against this contract.

## Status — DONE (2026-06-12)
- **ignite** ✅ burn DoT + ring-by-ring over-time spread, capped at `FireConfig.SpreadGenerations` (`BurnSystem`).
- **fireball** ✅ projectile + AoE splash.
- **firewall** ✅ rising band that leaves blocks burning (ties into BurnSystem). Playtested: clear rising flame band.
- **turret** ✅ fires on paddle deflect (`SpellSystem.OnPaddleHit`), not a timer. Playtested.
- **phoenix** ✅ visible `Phoenix` entity orbiting a ball (`PhoenixSystem` + `PhoenixLayer` using the `Phoenics` bird sprite). Playtested: clear orbiting phoenix.
- Tests: `Arkanoid.Tests/FireMageSpellTests.cs` (+ updated character/relic/kit tests) — 311/311 backend green.
- Playtest: `tests/firemage-playtest.spec.ts` — phoenix, firewall, HUD HP all reviewed against this contract.
- Bonus (same bug class): fixed the T9 `lives→hp` / `treasureBonus→crystalBonus` frontend drift; HUD HP bar now reads real HP and swaps sprite by danger level (green/amber/red — the "3-4 bar levels").

Round 2 (Paladin / Engineer / Necromancer kits) deferred — same template + playtest gate applies.

## Owner rework 2026-06-16 (Fire Wall + Conflagration "don't work" report)

Owner reported Fire Wall "doesn't work properly" and Conflagration "does nothing." Findings + owner-approved fixes (all verified live; 648 backend tests green):

- **Fire Wall — render BUG (root cause of "doesn't work"):** `FireWallLayer.update` only set the flame-tile `Y` in the rebuild-on-count-change branch, so once the wall existed the band **froze at its spawn point** while the sim wall rose and burned blocks above it (verified: sim Y 546→319, sprite Y stuck at 574). Fixed: tiles now track the wall's `Y` every frame.
- **Fire Wall — owner redesign:** now **spawns AT the ball's height** (`SpellSystem.Archetypes` firewall case uses the live ball's `Pos.Y`, not `Grid.Height`) and sweeps up from there; **cannot be cast while the ball rests on the paddle** ("on the bar" → `SpellFizzle`, no mana). **Power bumped** (`SpellCatalog` firewall `damage 1→3`, `damageInterval 0.4→0.25`, `bandHalfHeight 22→24`) so it clears tough(3)/elite(5) — now ~47/49 on hell-9 (was 18/49). Positional: cast with the ball low = near board-clear; ball high = top rows only.
- **Conflagration — owner redesign (self-sufficient):** previously fizzled silently with no fire on the board (the "does nothing"). Now `ConflagrationSystem` is **self-sufficient**: with fire it detonates **every burning block board-wide** (the ignite-combo payoff); with NO fire it still bursts the **cluster of blocks around the ball** (fallback: nearest 6) so a bare cast always damages + spends mana. Only a truly empty board fizzles.
- **New `SpellFizzle` event** (+ wire `"spellFizzle"` + frontend `Effects.spawnFizzle` cool-grey "dud" puff) so a blocked cast is never a silent no-op.
- Tests: `FireMageSpellTests` (Conflagration self-sufficient + board-wide; Fire Wall spawns-at-ball + blocked-on-bar + damages) and `SpellTests.FireWall_Cast…` updated. NOTHING committed.

## Critical review pass (2026-06-12, second sweep)
Drove each spell live (`tests/firemage-review.spec.ts`) and inspected frames. Found & fixed two real issues the first pass missed:
1. **Burning blocks were invisible** — only a per-tick FX puff, no persistent "on fire" state. Added `burning` to the snapshot + a looping flame overlay & warm tint in `BlockLayer`. The "fire propagates over time" mechanic is now clearly visible (confirmed: rows of blocks burning as fire spreads).
2. **Fire Wall expired before reaching the blocks** on real levels — riseSpeed 90 × 3.0s ≈ 270px, but hell-1's blocks sit higher; the unit test only passed because its test level is 4 rows tall. Bumped to riseSpeed 150 / lifetime 4.5 / band 22 so it sweeps the full board.
- **Balance note (for the user):** Fire Wall is now strong enough to clear the tutorial level in one cast (sweep + ignite + burn). Reasonable for a 35-mana spell, but may want tuning down for harder boards.
- Reviewed clean: phoenix orbit motion, turret bolt, fireball splash, HUD HP level-swap, main menu, battle HUD — all coherent.
