# 10 — Enemy Mechanics & Art Audit (2026-06-09)

> Deep audit of all 12 ported enemies + 4 bosses: sim behaviour vs `docs/08` spec,
> and whether every enemy **renders with the correct original art**. Includes the
> fixes applied during the audit and the verification evidence.
> New tool: `tools/validate-sprites.mjs` — fails if any sprite reference doesn't
> resolve to a real atlas frame (missing keys silently render white).

## Verification evidence

- xUnit: **212/212 green** (3 new: emitter missile-kind tag, hell-boss bolt tag, bat flyaway).
- Playwright `enemies/boss/boss-fight` specs: **14/14 green**, screenshots regenerated
  in `tests/demo-screenshots/` (incl. new `enemy-bat-flyaway.png`).
- Full-suite regression: **125 passed / 2 failed (4.8 m)** — the 2 failures are the
  *pre-existing* issues from before this audit (editor Save button outside the mobile
  viewport — see docs/09 §1.4 — and the hud-live worker-contention flake). No
  regressions from the enemy changes.
- `node tools/validate-sprites.mjs`: all 79 referenced keys resolve against 759 atlas frames.
- New log lines: sim `emitter fired … kind=…`, `bat released ball + flyaway`; client
  `[ark:boss] rig created type=… sprite=…` + `window.__bossRigType` test hook.

## Fixed during this audit (wrong-art bugs)

| Bug | Root cause | Fix |
|---|---|---|
| **Heaven boss rendered as a Demon.** `inferBossType("HeavenBoss")` → Unknown → fallback rig = `hell/DemonBody`, real sprite hidden. | No Heaven rig existed | New 4-part Heaven rig (`HeavenBoss` + additive `HeavenBossGlobe` halo + 2 swaying `HolyBall`s), label **THE SERAPH**. Playwright asserts `__bossRigType === "Heaven"`. |
| **Enemy missiles were red dots.** Emitter/boss hazards had `Kind=""`; `HellBallMissile`/`BeholderMissile`/`heaven/Missile` art never used. | Kind never tagged | `missileKind` per emitter block def (config-driven); boss bolts tagged per biome (hell→hellball, heaven→heavenmissile, caverns→stalactite, village→witchmagic). HazardLayer maps kind→original missile sprite. |
| **In village, ALL generic hazards rendered as spinning bats** — beholder missiles looked like bats; actual bats never produced a flying sprite. | Biome-gated "bat skin" guess in HazardLayer | Removed biome gating. Beholder missiles use `BeholderMissile`. Bat release now spawns a harmless `kind="bat"` flyaway hazard (drifts up, despawns above board, wing-flap flutter instead of rotation). |

## Sim fidelity vs docs/08 (all 12 verified by reading every system)

**Faithful ports:** colour-paired teleporters (cooldown + same-colour cycle), bomb chain
(Chebyshev radius, recursing same-tick), necromant revive queue (delay, cancels when
necromant dies), windmaster (falloff push, speed-preserving — an improvement), stalactite
column-trigger + Goblin boss drop, ghost portal phase toggle, lava ball-drain, altar/vase
statue pacify, cart sweeps.

**Acknowledged simplifications (recorded, not bugs — candidates for a fidelity pass):**

1. **Hell spawner** fires a falling missile; the original spawned a *bouncing enemy ball*
   that refused to drain (spec H1). The `HellBallLvl1-3` art + `hell/hellballlvl`
   animation strip are still unused.
2. **Bat** grabs on contact with no *sleeping→wake* state (original: slept on a host
   block, woke when host hit) and no release speed-bonus.
3. **Melee/Shield statues** act on a timed cadence; originals reacted *to ball hits*,
   and the ally state made them fight FOR you (ally bullets / corrupting nearby blocks)
   rather than just holding fire.
4. **Vase** duplicates the Altar (pacify); original *levelled statues up* (risk/reward).
5. **Beholder** aims at the *first* ball, not nearest; no eye-tracking aim in snapshot.
6. **Bosses** share one pattern system; Goblin doesn't hop between 3 positions, Witch
   doesn't grab the ball, Demon's fists are generic bolts.
7. Fire/decay-spread kills bypass `NecromantSystem.OnBlockDestroyed` + bonus drops
   (inconsistent with ball kills — necromant can't revive spread-killed blocks).

## Unused original art still on the table (look-upgrade backlog)

| Enemy | Available, unused | Payoff |
|---|---|---|
| Beholder | `Beholder1/2/3` HP tiers (+anim strip), Attack/Death animations | Damage states + attack tell |
| Statues | `*Active` variants + 3 glowing-part overlays each | **Pacified statues currently look identical to hostile ones** — readability bug-adjacent |
| Altar | `HeavenAltarV2Active` | Feedback when triggered |
| WindMaster | `WindMasterV2Circle`, `Glow1/2` strip | **Wind radius is invisible** — biggest readability gap |
| Skulls | `Skull{Red,Blue,Green}Active`, `SkullAnimation` | Teleport feedback; ring is always blue regardless of colour |
| Necromant | `VillageDeathCastAnimation`, `DeathSphere` | Revive currently has no visual — sphere should fly to the cell |
| Bat | `BatFlyAnimation3` (2-frame flap), `BatGhost*` | True flap animation |
| Hell spawner | `HellBallSpawnerDeathAnimation`, `HellBallLvl1-3` | Death flourish; bouncing-ball enemy |
| Columns | `ColumnDamaged/Destroyed` family | Columns alpha-fade instead of cracking |
| Cauldrons | `Kotelok1/2/3` + death frames + anim strip | **Witchland signature block — still 0 refs** (flaw A2 never actually closed) |
| Lava | `LavaSpowner(+Active/Damaged/Destroyed)`, `LavaBegining/End` | Spawner mechanic from spec H3 |
| Cart | `DungeonCartWheel` | Rolling wheels |
| Misc | `Stalactite2`, `GrateBomb`, ghost-layer enemy variants | Variety |

**Damage-state coverage:** `BLOCK_DAMAGED` covers only the 8 basic blocks — every
*enemy* block still alpha-fades (the "vanishing" look flaw A3 fixed for basics only).

## Workflow note (explains the mystery screenshot deletions)

`tests/global-setup.ts` **wipes `demo-screenshots/` on every Playwright run**, so a
*filtered* run (e.g. `npx playwright test enemies.spec.ts`) deletes all committed
screenshots and regenerates only its own — that's where the 16 uncommitted deletions
in git status came from. Either run the full suite before committing screenshots, or
change the wipe to only remove shots the current run will re-create.

## Game-design evaluation

This doc covers *fidelity and art correctness*. The companion
**`11-enemy-design-proposals.md`** evaluates whether the mechanics are good *design*
(verdict: solid hazard set, weak enemy roster — no shared design language, danger
doesn't pay, flattened ports removed the decisions) and proposes the redesigns.

## Recommended next slice (in order)

1. **Statue Active variants + WindMaster aura circle** — pure renderer, fixes the two
   readability gaps (pacified-looks-hostile, invisible wind).
2. **Beholder damage tiers** (Beholder1→2→3 by HP bucket) + enemy-block damage states.
3. **Necromant DeathSphere** visual on revive (event already exists: `deathMark`/`revive`).
4. **Cauldron block** (Kotelok art, e.g. on-death potion splash AoE) — closes flaw A2 for real.
5. **Fidelity pass**: bouncing hell enemy-ball, statue ally mode that fights for you,
   vase level-up, bat wake states. (Gate each through feature-critic — some may not be
   worth the complexity.)
