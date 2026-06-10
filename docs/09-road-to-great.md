# 09 — Road to Great: Evaluation & Proposal (2026-06-09)

> Audit of (a) the legacy Unity project (`Scripts/` + `Sprites/`), (b) the current
> rebuilt game, against the bar: **"a great game with great UI — game design, level
> design, interesting enemies, well-thought skills, everything shiny."**
> Companions: `01`–`04` (design/migration), `05` (P-milestones), `07` (flaws punch-list),
> `08` (enemy spec). This doc is the next-stage plan after all of those.

---

## 1. Verification — what exists, confirmed today

### 1.1 Legacy Unity project (frozen reference)
- `Scripts/` — **246 C# files** present, matching the audit in `01`/`02`. Unchanged, reference-only.
- `Sprites/` — **755 PNGs** present; **86.3% now reachable** through the atlas
  (91.4% excluding baked-text/platform junk) per `docs/asset-usage.md`.
- Everything worth lifting has been lifted: the block-effect pipeline, leveled
  parameters, the two-resource survival model, all 12 enemy behaviours, 4 bosses,
  the biome identities, the item roster, the campaign-road concept.
- **Verdict: the legacy project is fully mined.** Nothing blocking remains in it
  except art still unused (mostly inactive-icon variants and set dressing).

### 1.2 Current build (verified by running it)
- **Backend:** `Arkanoid.Core` (pure C# sim, deterministic, per-instance state) +
  `Arkanoid.Server` (WS host) — **209/209 xUnit tests pass** (run 2026-06-09, 103 ms).
- **Frontend:** PixiJS mobile-first renderer; 12 scenes (menu, campaign, battle,
  character, skills, inventory, dungeon, achievements, settings, editor, tutorial).
- **Playwright:** ~35 spec files at 390×844 mobile viewport with rendering ON
  (result of today's full run recorded in §1.4).
- **Milestones M0–M7 and P0–P7 complete**, plus the shell/flow overhaul (`06-…-report.md`)
  and the full enemy port (`08`: **all 12 enemy types**, all 4 bosses incl. the Heaven finale).
- Nearly the whole `07-flaws-review.md` punch-list is closed (damage-state sprites,
  bombs/cauldrons, 9-slice UI, emoji removal, English badges, biome node art, lava,
  altar/vase, columns, mine-cart, witch magic…).

### 1.3 Content inventory (the numbers that matter)

| Axis | Have now | Notes |
|---|---|---|
| Levels | **14** campaign (4 biomes, 4 bosses) + editor | 8×14 grid, blocks fill top ~half |
| Block types | 36 | incl. all signature mechanics per biome |
| Enemies/hazards | 12 types | spawners, beholders, bats, necromant, statues, windmaster, stalactites, bombs, lava, teleporters, ghost portals, carts |
| Bosses | 4 | Demon, Goblin (stalactite rain), Witch (magic bolts), Heaven finale |
| Characters | 4, **all unlocked from the start** | FM 4 spells; Paladin/Engineer/Necromancer 3 each + passives |
| Spells | 13 total, with per-spell leveling | |
| Relics | **4** | glass_cannon, flint_core, pyroclasm, mana_battery |
| Ball cores | **3** (heavy, ember, split) | no fusion combos |
| Items | 15 × 3 tiers, shop | the old game's never-implemented system — now real |
| Bonuses | 6 falling pickups | |
| Dungeons/Rifts | **2** fixed 3-floor runs | pick-1-of-3, permadeath, relic payoff |
| Audio | **0** — no system, no assets | the only fully-absent subsystem |

### 1.4 Full-suite verification result (today)
- xUnit: **209/209 green** (103 ms).
- Playwright (mobile 390×844, render on, 4 workers): **124 passed, 2 failed (4.8 m)**.
  - `hud-live.spec` "mana fill / fireball affordability" — **flaky under 4-worker
    contention only**; passes serially. Tolerable, but worth a stabilization look.
  - `editor.spec` "palette → paint → save round-trip" — ~~real defect~~ **FIXED
    2026-06-09**: the scene host clips overflow, so the too-tall editor could never
    scroll its Save button into a phone viewport. The editor root now owns its
    scrolling (`height:100vh; overflow-y:auto`) and the palette/actions wrap on
    narrow screens. `editor.spec` green across repeats.
- Housekeeping: 16 `tests/demo-screenshots/*.png` deletions are sitting uncommitted
  in the working tree — regenerate or commit the deletion before the next feature.

---

## 2. Honest scorecard against "great"

| Axis | Grade | Why |
|---|---|---|
| Architecture / tests | **A** | Deterministic core, 209 unit + ~100 visual tests, per-session sim, config-driven. This is genuinely strong — better than most shipped mobile games. |
| Enemies | **A− (was B+)** | **2026-06-09 update:** the full `docs/11` program shipped — danger-pays rule, telegraphs, visible wind aura, statue Active states, beholder tiers, death-sphere markers, Altar/Vase convert system, bat carry with counterplay, stalactite-as-weapon, cauldron (Economy axis), lava creep/retract, and one signature verb per boss (fist columns / hops / ball-grab / summons+fused vase). Remaining to A: encounter tuning across more levels, attack/death animation strips. |
| Game design (core loop) | **A− (was B)** | **2026-06-10:** all four build axes are real — 20 spells across 5-spell kits, a 17-relic web, 6 ball cores with 3 named fusions, and 3 paddle mods (Wide Frame / Grip Tape / Side Cannons) as their own rift pick category. Synergy hints live in the pick UI. Remaining: spell-loadout choice UI (equip 4 of 5). |
| UI | **B−** | Real art everywhere, 9-slice, biome node art, English badges. Still: home screen is sparse, dungeon-run screen is a void, same dark-gradient treatment on every screen, weak spell VFX. Functional, not yet *delightful*. |
| Skills | **B+ (was C+)** | **2026-06-10:** all four classes have 5-spell kits — Phoenix, Penetration, Last Day, Magnet, Overload, Bone Golem and Skeletal Mage ported/designed with original icons, all tested. Remaining to A: loadout choice (equip 4 of 5) and synergy hints. |
| Level design | **A− (was C)** | **2026-06-10:** 30 matrix-conforming levels (7-8 per biome arc incl. composites), exclusive pacing modes + objective flavors, atmosphere kits, 5 machine-enforced lint rules, and procedurally generated rifts (curated-shuffle inheriting the matrix, boss finale, biome-keyed rewards). Remaining to A: fork nodes in the campaign graph. |
| Meta / retention | **B+ (was C)** | **2026-06-10:** 30-level campaign with forks, seeded generated rifts (3-5 floors, boss finale, biome-keyed rewards, synergy-hinted picks), and **ascension tiers 1-5** (clears raise the next rift's tier: +HP hardening, scaled crystals, "+N" banner). Remaining: character unlocking + mastery (needs your gating decision), daily seeded rift (conflicts with rifts-only entry — needs a design call). |
| "Shiny" (juice + audio) | **B (was D)** | **2026-06-10:** the game is no longer silent — a procedural Web Audio SFX engine synthesizes ~35 event cues (impacts, spells, bosses, jingles, timers) with zero assets, honoring the Settings toggle. Trails, shake, vignette, column flashes, atmosphere kits all live. Remaining to A: real sampled SFX + per-biome music (needs sourced/approved assets — the recipes are drop-in replaceable). |

**One-line verdict:** the *engineering* is great; the *game* is a complete, polished
**skeleton of a great game** — every system exists in thin slice, and what's missing
is depth, volume, and sound, not architecture.

---

## 3. The gaps, ranked by impact-per-effort

1. **G1 — No audio.** Nothing else moves the "feels like a real game" needle as much.
2. **G2 — Build depth is decorative.** 4 relics / 3 cores / no fusion / fixed kits means
   no theorycraft, no "broken combo" highs — the design doc's stated replay engine (§4 of `04`).
3. **G3 — Level design has no language.** One win condition, one layout idiom,
   no pacing modes, enemies used as garnish instead of encounter design.
4. **G4 — Meta has no pull.** Everything unlocked day one; rifts are 2 fixed menus;
   nothing to chase after the ~14-level campaign (~1–2 hours).
5. **G5 — Presentation polish residue.** Spell VFX punch, home/dungeon screen dressing,
   per-biome screen identity, hit-stop/combo feedback, onboarding quality.

---

## 4. Proposal — four G-milestones to "great"

Process per milestone stays the house style: feature-critic → demo scene → user approval →
xUnit + Playwright proof → screenshots reviewed against the strict DoD of `05`.

### G1 — Sound & Feel *(do first — biggest single jump)*
**Goal:** the game stops being silent and every impact lands.
- **Audio system:** Web Audio (or `@pixi/sound`) driven by the snapshot **event cues we
  already emit** (block destroyed, spell cast, boss telegraph, bonus catch, win/lose…)
  — the architecture was built for this; zero sim changes needed.
- **Assets:** no audio exists in the legacy folders. Source CC0/royalty-free
  (e.g. Kenney impact/UI packs, freesound CC0 loops) — **needs your approval of the
  pack list before integration** (per the `05` deferral note).
- Per-biome music loop ×4 + menu theme; ~25 SFX (ball/paddle/block-tier hits, breaks,
  explosions, each spell, boss roar/telegraph, UI clicks, win/lose stingers).
- **Feel pass:** hit-stop on heavy kills (already stubbed in Renderer), block-break
  particle bursts per biome, combo counter + popup on kill streaks, paddle squash on hit,
  ball-speed-scaled trail, spell VFX punch-up (flagged I6 — fireball/turret are weak).
- Settings: music/SFX volume sliders (SettingsScene already has the empty space).
- **DoD:** full level start→boss-kill plays with sound on a phone; every event cue has a
  sound; mute toggles persist; Playwright asserts the cue→sound wiring (mock audio node).

### G2 — Build Depth (the replay engine)
**Goal:** two runs of the same character feel different; theorycraft becomes possible.
- **Relics 4 → 16+**, implementing the designed-but-unbuilt set from `04` §4.2
  (Conductor, Overcharge, Split Shot, Souljar, Magnetism…) plus biome-conditional ones.
  The `Modifiers.cs` single-source pattern makes each relic a small, testable change.
- **Ball cores 3 → 6 + fusion:** add Ghost, Echo, Frost cores; fusion = pick 2 cores in a
  rift → combined effect with its own name/VFX (Heavy+Ember = Molten, Ghost+Split = Phantom Swarm…).
- **Complete the class kits to 5 spells each**, porting the missing originals:
  Phoenix (FM), Penetration + Last Day (Paladin), Magnet (Engineer), a 4th+5th for
  Necromancer from its art. Then add **loadout choice**: equip 4 of your 5+ spells —
  the first real build decision in the campaign.
- **Synergy hints in UI** (design `04` §7): highlight offered picks that combo with owned ones.
- **DoD:** ≥16 relics ×, ≥6 cores, ≥3 fusions, 5-spell kits ×4, loadout UI — each with a
  unit test proving the effect and a rift pick-screen screenshot showing hints.

### G3 — World Depth (levels + encounters + rifts)
**Goal:** 30+ levels that read as designed places, not stamps; rifts worth grinding.
- **Level count 14 → 30+** (target ~7 per biome + boss): author with the existing editor
  + `tools/gen-levels.mjs` validation. Introduce a **layout language**: funnels, chambers,
  shielded cores, enemy nests, teleporter mazes — denser and taller (flaw C2).
- **Pacing modes** ported from the original: **descending blocks** (StrechedLevel
  pressure) and **multi-floor** levels (clear a floor → next slides in). Both are
  sim-side systems with existing reference behaviour in `Scripts/`.
- **Win-condition variety:** survive-N-seconds, kill-target-under-timer, escort/protect
  (keep the altar alive). The component-based win system in the original (`01` §1) is the model.
- **Encounter design pass:** each biome's back half combines 2–3 enemy types deliberately
  (e.g. necromant + cauldrons + bats as a Witchland finale gauntlet).
- **Rift generator:** replace the 2 fixed rifts with seeded generation — pick biome
  template + floor count (2–5) + miniboss + curated pick-pool (the `RiftService` seed
  plumbing already exists). Add **elite floors** with curse-with-upside modifiers.
- **DoD:** 30+ levels all pass `all-levels.spec` (load + winnable); each biome has ≥1
  descending and ≥1 multi-floor level; generated rifts deterministic by seed; balance
  table in `docs/balance.md` extended across the full curve.

### G4 — Meta, Retention & Final Shine
**Goal:** reasons to return; every screen delightful.
- **Character unlocking + mastery:** start with Fire Mage; unlock Paladin/Engineer/
  Necromancer via campaign/rift milestones (the design's "unlockable identity lens").
  Per-character mastery track: wins grant mastery levels → cosmetic ball/paddle skins
  (art exists: size tiers, glow variants) + new starting-loadout options.
- **Ascension tiers** for rifts (block HP +, fewer picks, elite density) — the `04` §10
  open question, answered with 5 tiers.
- **Daily seeded rift** (same seed for the day, one attempt, leaderboard-ready locally).
- **Screen identity pass:** per-biome accent/texture on campaign map sections, home
  screen dressing (hero idle + parallax background from `Fons/`), dungeon-run screen
  redesign (flaw I3), inert-arrow cleanup (I5).
- **Onboarding:** convert the static TutorialOverlay into a scripted first level
  (serve → deflect → imbue → cast → win) using the HintSystem art.
- **Release checklist:** PWA install flow verified on a real phone, save migration
  safety, performance budget re-run, full-playthrough recording.
- **DoD:** new-player flow from install → Fire Mage campaign → first rift → character
  unlock, recorded on a phone; all screens pass the "outside eye calls it a game" bar.

---

## 5. Sequencing & effort

| Milestone | Effort (relative) | Why this order |
|---|---|---|
| G1 Sound & Feel | S–M | Cheapest transformation; makes every later demo feel 2× better |
| G2 Build Depth | M | Pure Core+config work on proven patterns; unblocks G3's pick-pools |
| G3 World Depth | L | The volume work; needs G2's content to fill rift picks |
| G4 Meta & Shine | M | Needs G2/G3 content to gate unlocks against |

**Decisions needed from you before starting:**
1. **G1 audio sourcing** — approve using CC0/royalty-free packs (I'll shortlist), or supply originals.
2. **G4 character gating** — OK to lock Paladin/Engineer/Necromancer behind progression in an already-played save? (Proposal: existing saves keep what they have; fresh saves earn them.)
3. **Scope check** — 30+ levels is the "real game" bar from `05` P6; confirm or adjust.
