---
name: level-balance-bot
description: Run an automated "reasonable player" bot against ANY level (boss fight or regular) to get balance data — win rate, survival time, blocks cleared, a single Level Score (speed + HP/balls lost), damage dealt per spell where measurable, and hazard-dodge difficulty. Use before/after any level, boss, hazard, or spell tuning change, or when a level "feels off."
---

# Level balance bot

`backend/Arkanoid.Tests/LevelBalanceBotTests.cs` is a data-gathering tool, not a pass/fail
regression gate. It plays a full level in-process (pure C# sim, no browser, no WASM, runs in
well under a second per seed) with a **reasonably good but not perfect** player model, and
prints a table of stats you use to decide whether/how to retune. It exists because "does this
feel unfair" is unanswerable by reading config numbers — you have to actually play it out, many
times, and look at where the damage and the deaths actually come from.

**This is general-purpose across the whole campaign — not boss-only.** Point it at any
`config/levels/*.json` file with any character/loadout combination. Boss-specific stats (boss
HP) only appear in the report when the level actually has boss blocks; the level-agnostic
"blocks cleared %" metric always applies and is the right progress signal for regular levels.

Written 2026-07-01 after the hell demon boss was reported "completely off and unpassable."
That investigation is the reference example — see `git log -p` on this file's introduction (it
started life as `BossBotTests.cs` before being generalized) for the full worked example,
including a real bug it found: the level file placing 4 boss-behavior tiles where the per-tile
HP was tuned for 2, silently doubling the boss's effective HP.

## What the bot actually does

- **Ball catching**: always predicts the descending ball's arrival X (folding the trajectory
  off the side walls, since balls bounce and hazards don't) and aims for it.
- **Hazard dodging**: always computes the paddle position that overlaps the fewest incoming
  hazards for the soonest-arriving "wave" (hazards whose arrival time is within 0.08s of each
  other — i.e. genuinely simultaneous threats).
- **Movement is speed-capped, NOT a teleport** (`MaxPaddleSpeed`, 1400px/s, added 2026-07-02).
  Earlier versions set the paddle instantly to the ideal target every tick, which is a real bug
  for balance data even though the *diagnostic* metrics (`InfeasibleDodgeCount`/`MaxRequiredSpeed`,
  still computed from the ideal target) are fine — a teleporting paddle never fails a
  geometrically possible dodge, so HP-lost/balls-dropped flatlined at 0 across 9 of 11 hell
  levels, hiding any real difficulty signal. With a real speed cap the paddle can fall
  genuinely short when the ideal target moves faster than a human could track, and the resulting
  HP-lost/balls-dropped numbers are what should drive level tuning — **not raw hazard-density
  counts** (emitters/elites/tough-block tallies look like a reasonable proxy but don't interact
  additively: one emitter added to an already block-dense level can spike difficulty far more
  than the same emitter on a sparse one, because slow clears mean longer hazard exposure).
- **Catch angle is randomized**, not deliberately steered toward the boss/blocks
  (`MaxRandomAngleFrac` in `LevelBot`). This is the "reasonably good, not robotic" part — a bot
  that always catches dead-center reflects the ball perfectly vertical (see
  `BallPhysics.ResolvePaddle`: angle = offset/half × maxAngle) and can get stuck bouncing
  forever in one empty lane, which overstates how bad a level is. A bot that always steers
  optimally overstates how good real play is. Random angle each catch is the middle ground.
- **Spell casting**: every ~0.3–1.0s, if any loadout slot is affordable, casts a **random**
  affordable one. Verifies whether the cast actually "landed" (spent mana / dealt damage) vs.
  fizzled, per spell.
- Runs **8 different game-RNG seeds** (attack-pattern/rain/spread/enemy rolls all draw from the
  `GameInstance` seed) so one lucky/unlucky pattern draw can't stand in for "is this balanced."

## Running it

```bash
cd backend
dotnet test Arkanoid.Tests --filter "FullyQualifiedName~LevelBalanceBotTests" --logger "console;verbosity=detailed"
```

The detailed logger is required — xUnit only surfaces `ITestOutputHelper` output at that
verbosity. Runtime is under a second for all 8 seeds.

## Reading the output

Two things print: a full blow-by-blow report for seed 1 (every spell cast with mana
before/after, every hit taken with the hazard kind, every "undodgeable" or "infeasible dodge"
moment with a timestamp), then an aggregate table across all 8 seeds:

| column | meaning |
|---|---|
| `outcome` / `time` | Won/Lost and sim-seconds survived |
| `cleared` | **level-agnostic progress**: % of `NeedToKill` blocks destroyed. Use this for regular levels; use `boss hp` (only shown when the level has boss blocks) for boss fights. |
| `boss hp` | start→end boss HP, when applicable |
| `hp lost` | player HP lost (out of `StartHp`) |
| `dmg:<spellId>` (one column per spell actually cast, generated dynamically — the table adapts to whatever loadout you pass in) | **damage attributed to each spell** — the balance signal. Instant-effect spells (Conflagration, Lightning, Reckoning, anything that deals damage synchronously at cast time) are measured *exactly* via before/after board HP around every cast. Burn-DoT spells that share one mechanism (fire-mage's Ignite/Fire Wall) are split by a burst-size heuristic (Fire Wall's own design spec requires ≥3 blocks lit per proc; Ignite lights exactly one) — this specific split is fire-mage-specific; other classes' instant spells are captured generically with no special-casing needed. |
| `dmg:ball` | plain ball-collision damage (not a spell) — context for how much of the clear is "ball hits blocks" vs. "spells did work" |
| `max speed` / `infeasible` | the fastest **instantaneous** paddle reposition the bot needed that tick, and how many ticks needed >2000px/s (a generous ceiling for a real flick/drag). The sim lets the paddle teleport, so this number reveals dodges that are *geometrically* possible but not *humanly* possible. |
| `undodge` | count of waves where literally **no** paddle position anywhere on the board avoided every simultaneous hazard — a hard bug if ever nonzero, not a difficulty tuning question |

Aggregate footer adds win rate, avg survival time, avg blocks cleared, avg boss HP remaining
(when applicable), summed per-spell damage, and total spell-cast landed/fizzled counts.

## Pointing it at a different level/character/loadout

Everything you'd want to vary is a parameter to `RunScenario(...)`. Copy the example test
method (`Bot_PlaysHellBoss_ReportsFullStatistics`) and change the call:

```csharp
RunScenario(title, configRoot, blockCat, relicCat, bonusCat, charCat,
    levelId,        // any file in config/levels/, e.g. "cavern-9", "village-boss", "heaven-3"
    characterId,    // e.g. "fire_mage", "paladin", "engineer", "necromancer"
    loadout,        // string[] of spell ids matching CastSlot's index order for that character
    cfg,            // a SimConfig — pass SimConfig.Default or a `new SimConfig { Boss = new() {...} }`
    halveBossHp)    // quick lever for testing "what if boss tiles had half HP" (no-op if no boss blocks)
```

To A/B test a tuning change, call `RunScenario` twice with different `cfg` values and print
both aggregate tables — see git history on this file for a worked 4-scenario comparison
(baseline vs. boss-HP fix vs. two cadence hypotheses vs. combined). Delete extra scenarios once
you've settled on values; don't leave a permanent multi-scenario sweep committed unless it's
actively being iterated on.

`Seeds` (currently `{1..8}`) and `maxSimSeconds` (300) are shared across all scenarios in the
file — bump seed count for more statistical confidence, at roughly linear runtime cost (still
sub-second per seed).

## Whole-biome sweeps + Continuous Rift + HTML reports

`Bot_PlaysWholeInfernoAndRift_GeneratesHtmlReport` (2026-07-01) is the pattern for a broader
"how's this biome doing" pass instead of one level/one class: loop `RunScenario` over every
level id in a biome × every class's `CharacterDef.Starting` kit (read from the catalog, not
hardcoded, so it can't drift from the real starting kits), collect results into a list of
`(Level, Class, List<LevelBot>)` tuples, then render an HTML table (color-graded by win rate)
via `File.WriteAllText` — 12 levels × 4 classes × 3 seeds runs in ~10s wall-clock. Copy this
method and swap the biome prefix / level count for other biomes (caverns/village/heaven).

For **Continuous Rift** (a single GameInstance stacking multiple floors, docs/04 §7), use
`RunRiftScenario`: it builds the floor list the same way `RiftService.GenerateRift` does
(seeded shuffle of the biome's levels, cycling to reach `floorCount`, ending at `{biome}-boss`),
loads it via `LevelLoader.FromRiftFloorFiles`, and calls `g.SetRiftMode(true)`. Mid-rift the sim
freezes for a 1-of-3 §8 modifier draft (`GameInstance.AwaitingRiftDraft` / `RiftDraftChoices` /
`PickRiftModifier`) — `LevelBot.Run` auto-picks one at random each time so the run doesn't stall;
if you extend the bot further, this is the place to add smarter picks. `LevelBot.FloorsCleared`
(= `GameInstance.FloorIndex`) and `FloorsTotal` (set by the caller after construction) are the
right progress metric for a rift — `BlocksStart/BlocksEnd/BossStartHp` only reflect whichever
floor was active when the run ended, not the whole run. Rifts take much longer than a single
level (11 floors of survival) — give `Run()` a generous `maxSimSeconds` (1800 worked for a
10-floor + boss hell rift).

## Level Score — single-number outcome metric (2026-07-03)

Per-spell **damage** is only exact for instant-effect spells (Conflagration, Lightning, the
fire-mage burn split) — every delayed/utility/summon spell shows a meaningless "—" for damage,
which makes a pure damage table useless for judging most of the roster. `LevelBot.LevelScore` is
a single scalar that sidesteps this: `SimSeconds + (HpLost + BallsDropped) * SecondsPerLostHpOrBall`
(15s per HP/ball lost) for a won run, or a flat `FailScore` (1000) for a loss/timeout — **lower is
always better**, and a run that failed gets zero credit for how far it got. Use it to compare
runs/loadouts/tuning changes with one number instead of eyeballing four.

`Bot_TestsEverySpell_AcrossFiveRandomLevels` uses Level Score to gauge **spell suitability**
without damage attribution: for each spell, it splits a class's runs into "landed this spell at
least once" vs. "never landed it" and compares the average Level Score of each group. A spell
whose "cast" group scores much better than its "not cast" group is a positive signal; reversed is
worth a look. **This is a correlation over the sweep's random casting, not a controlled A/B** — a
reactive/defensive spell (Shield, Recall) gets cast disproportionately *because* a run is already
going badly, which can make it look artificially worse than it is, and small-n splits (anything
under ~10 runs per group) are noise-prone. Treat a reversed signal as "worth investigating with a
deliberate ablation test (same loadout minus that one spell)," not as a verdict on its own.

## Priority-aim: teaching the bot to notice obvious threats/targets (2026-07-03)

A whole-campaign sweep (all 4 biomes × 11 levels + boss × 4 classes) found several spots where the
bot's pure-random catch angle was *understating* real difficulty, not overstating it — a "reasonably
good" human notices some things the random model doesn't. `LevelBot.NextCatchAngle()` biases the
post-deflect catch angle SIGN (not magnitude — still imprecise/random-ish, not pixel-aim) toward a
priority column, checked in this order:

1. **`FindLavaPriorityColX`** — a Hell lava spawner that's already been hit once (`0 < Hp < MaxHp`)
   is actively creeping (`LavaSystem`) and will drain 1 HP every 3s once lava reaches the danger row
   — a **direct `CombatSystem.DamagePlayer` call with no `Hazards` entry at all**, so it's both
   invisible to the dodge model AND not dodgeable by paddle positioning once it starts. Only
   counterplay is finishing the spawner. Same blind-spot shape as Fist Slam (see below) — tag it in
   `Hits` via the `LavaDrain` event, don't let it show as a misleading empty `[]`.
2. **`FindReviverPriorityColX`** — Village necromant blocks (`ReviverSystem`) resurrect every
   same-layer block destroyed while they live, so clearing progress doesn't stick until the matching
   necromant dies. Without this, a level reads as "stuck at ~5% cleared" even though the ball is
   landing plenty of hits — it's just undoing itself. Only the REGULAR-layer reviver is targetable
   this way; a ghost-layer reviver needs the ball phased first (see the portal limitation below).
3. **`FindBossPriorityColX`** — during any boss fight, just aim at the boss. Obvious for a human,
   not automatic for a random-angle ball. Engineer's Rocket already has boss-priority hardcoded into
   the spell itself (`PickRocketTarget`); this gives every other kit the same "obviously focus the
   boss" instinct instead of penalizing whichever class's kit doesn't have it built in — without it,
   Fire Mage/Paladin were shown clearing 40-80% of a level's blocks while the boss sat near full HP.
4. **`FindEmitterPriorityColX`** — a `paddle`/`ball`-aimed emitter (Hell ball-spawners, Heaven Seraph
   adds) fires every `EmitInterval` for as long as it's alive; finishing an already-damaged one is
   pure upside (stops the barrage, and it's `needToKill` anyway).
5. **`FindPortalPriorityColX`** — Village's ghost-phase levels gate roughly half their blocks behind
   a phase only reachable by bouncing through a `village_portal` block. Fires probabilistically
   (40% chance per catch, NOT max-angle hard commit — see the reverted-experiment note below) whenever
   the ball's current phase can't reach some remaining `NeedToKill` block.

Also added: **reactive defensive casting** — `MaybeCastSpell` bypasses its normal random-timer
cooldown to immediately cast a `Placement` spell with `PlacementKind` `"barrier"`/`"firewall"`
(Shield/Fire Wall) the moment a hazard is inbound and no barrier is currently up. Random-timer
casting was wasting Shield's ~4s uptime on idle moments half the time, then had zero uptime exactly
when a barrage rolled through — this is what surfaced Paladin (whose only anti-projectile tool IS
Shield) losing far more than its kit should to Hell's ballspawner emitters.

**These are all bot fixes, not balance changes** — they make a "reasonably good" bot behave like a
reasonably good human would (notice the obvious threat/target), rather than a purely random one that
understates real play. Verify any HP/hazard-count tuning AFTER these are in, not before — several
levels that looked broken at 0-33% win turned out fine once the bot stopped being blind to lava
drain, boss focus, or reviver blocks.

**Soft (randomized-magnitude) bias beats hard (max-angle) commitment for priority-aim, even when the
target is a narrow specific column.** Tried max-angle commitment for both the portal and boss-focus
priorities: portal committed hard was net-negative (crowded out ball-catching/dodging badly enough to
drop Paladin/Engineer to literal 0% wins at `village-10`/`village-11` in an A/B, worse than the
20-40%-ish it was meant to fix), and boss-focus committed hard dropped Paladin from 75%→38% at
`caverns-boss` the same way. A soft, probabilistic nudge (same magnitude range as the random angle
elsewhere) gets most of the benefit without breaking survival elsewhere — don't reach for "commit
harder" as the first fix when a priority-aim heuristic underperforms; check whether it's crowding out
normal play first.

## Known limitations (don't over-read the numbers)

- **Village's ghost-phase/portal puzzle (`village-7`, `village-10`, `village-11`) reads as "level is
  harder than most" but is very likely partly a BOT limitation, not a pure game one.** Roughly half
  these levels' blocks are ghost-layer (`village_ghost`/`BallPhases`) and only hittable when the
  ball's phase is toggled by bouncing through a `village_portal` block — a deliberate routing puzzle
  a real player aims for on purpose (`FindPortalPriorityColX` above narrows but doesn't close this
  gap — even after it, these three levels sit at 17-33% win rather than the 67-100% typical
  elsewhere). This content was previously human-verified via MCP screenshots (see
  `village-ghost-phase-rework` memory) — don't treat these numbers as a verdict that the levels are
  broken; they're the softest spots in the whole campaign but not hard walls (no class is at literal
  0%). A smarter bot that deliberately banks shots toward a known portal column would close the
  remaining gap, but that's a materially bigger project (multi-step trajectory planning) than the
  priority-aim nudges above — get a human playtest before tuning content further here.
- **A kit whose burst has no way to guarantee it lands ON the boss specifically underperforms at
  boss fights, no matter how much damage-per-cast it deals — confirmed and then actually fixed
  content-side, not just bot-side (2026-07-03).** Fire Mage's Ignite (imbue — lights whatever the
  ball last touched) + Conflagration (detonates whatever's currently burning) had no way to
  GUARANTEE its burst hit boss tiles, unlike Engineer's Rocket (hardcoded boss-priority homing via
  `PickRocketTarget`). Confirmed via `bot.SpellDamage` totals massively exceeding actual boss-HP
  reduction (20 total Conflagration damage dealt in one heaven-boss run, boss HP only dropped by 3)
  — most of the burst was landing on nearby fodder, not the boss. Bumping Conflagration's flat
  damage (6→9) barely moved the win rate (~12%) because the damage still wasn't landing on the right
  target — the fix that actually worked was giving Conflagration a **guaranteed boss splash**
  (`ConflagrationSystem.Cast`): if the boss isn't itself among the currently-burning blocks, it still
  takes half-damage splash every detonation, on top of full damage to whatever IS burning. Paired
  with an analogous BOT-side fix for Paladin's Spear (a straight-shot, no-homing projectile that only
  hits the boss if the paddle happens to be lined up at cast time) — `MaybeCastSpell` now casts it
  reactively the moment the paddle IS lined up with the boss column, instead of on a random timer
  that usually fires down an unrelated lane. Net result: Fire Mage went from ~17-33% to 75-100% at
  caverns-boss/heaven-boss; Paladin's gain was smaller (its bottleneck was more about Shield's uptime
  than Spear's aim) but nothing regressed. **When a class structurally can't land its burst on the
  intended target, look for a targeted content fix analogous to what the class's kit is already
  missing** (Rocket already homes; Conflagration didn't; giving it guaranteed partial splash instead
  of full homing preserves "Ignite-first, non-guaranteed" identity while removing the near-total
  whiff) — don't assume every gap needs either "buff the number" or "accept it as class identity."

- `HumanSpeedThreshold` (2000px/s) is a judgment call, not measured from real input hardware —
  treat "infeasible dodges" as a strong signal, not a certified human-reachability oracle.
- No cards/modules/masteries are applied — this is "fresh player, base stats," which is the
  most conservative and generally most relevant read for "is this level fair," but won't
  reflect a geared/mastery'd character's experience. For content gated behind progression (rifts
  open only after a campaign clear), also test with `StatResolver.Resolve`/`Apply` at a
  representative level/★ — see `RunRiftScenario`'s `progressedLevel`/`progressedStars` params.
  A 2026-07-01 rift sweep found 3 of 4 classes went from 0-33% win at base stats to 100% at
  hero level 15/2★ — the base-stats number alone would have wrongly flagged the mode as broken.
- **`CharacterDef.Starting` (config/characters.json's fixed 3-spell kit) is itself a KNOWN-DRIFTED
  field** — CLAUDE.md documents that the real acquisition model is signature-locked + spells
  drafted from a global pool, not fixed per-class kits. Don't assume a class's `Starting` array
  is a fair test of its damage potential: Engineer's is magnet/radiation/overload (zero burst
  damage — 0% win vs. the hell boss) purely because its real burst tools (Lightning, Rocket)
  aren't in that fixed array, even though they're in its spell pool and the class's design intent
  explicitly leans on drafted/relic power, not the starting 3. Before concluding a class is
  underpowered, try its `Spells` pool's stronger picks in the loadout, not just `Starting` —
  swapping Engineer to `["overload","lightning","rocket"]` took hell-boss from 0%→100% with zero
  numeric changes.
- The Ignite/Fire Wall burst-size damage split is an approximation specific to that one class's
  kit shape (two ignite sources sharing a burn mechanism) — good enough to see which spell is
  doing the work, not a precise trace. Every other spell type's damage is exact.
- **The dodge model only sees `GameInstance.Hazards` (projectile-style threats) by default.**
  Some boss attacks deal direct player HP damage through a completely different mechanism with no
  hazard-list entry at all — e.g. the Hell demon's Fist Slam (`BossSystem.FistSlam`) locks onto
  whatever column the paddle occupies at telegraph time and hits it 0.5–0.8s later if the paddle
  is still there, tracked via the `FistTelegraph`/`FistSlam` events, not a `Projectile`. This one
  is already handled (`_fistDangerColX` in `LevelBot.ComputeTarget`), but a bot pointed at a
  *different* boss with an unaddressed direct-damage mechanic will silently under-report survival
  difficulty (it'll look like an unavoidable "hazard" the report can't explain — check
  `Hits`/`nearby hazard kind(s)` for empty-string entries, that's the tell). Found this exact bug
  2026-07-01: it alone made hell-boss look *entirely* unwinnable (0% win rate) until fixed.
- **Boundary math must match the sim's exactly, or the optimizer will pick the edge.** The dodge
  optimizer initially used `<` where the game's actual hit tests use `<=` (`CombatSystem`'s
  `Aabb.IntersectsCircle`, `FistSlam`'s column check) — a position-search that scores by "fewest
  overlaps" will happily land EXACTLY on that boundary, which the game then counts as a hit. Fixed
  via `SafetyMarginPx` (a few pixels of cushion) in `FindBestPosition`. If you add a new kind of
  "avoid this zone" blocker, give it the same treatment, not a bare `<`.

## Related

- CLAUDE.md → "Balance testing: the level bot" section references this skill.
- `backend/Arkanoid.Tests/AllLevelsWinnableTests.cs` is a sibling tool: verifies every campaign
  level is *reachable* (all blocks destroyable) with `setLives 9999` / `setBalls 99` — i.e. it
  tests level design, not hazard-dodge survivability. This bot is the hazard-survivability
  complement to that reachability check.
