# Social Systems, Economy Remake & Leaderboards — Implementation Plan

**Scope:** deliver Proposals **A (Weekly Trial Ladder)**, **B (Prestige Campaign Loop + Modules)** and
**C (Season Festival)** from `2026-06-13-social-economy-proposals.html`, in that order, on a shared
technical spine. Each phase is independently shippable and player-visible.

> **B reframe (per the latest call):** the "Endless Gauntlet" is **not** a separate wave mode. It is a
> **Prestige Campaign Loop** — beat the campaign (heaven-boss), then **Ascend** into a fresh campaign at
> a higher prestige tier with scaled difficulty and randomized biome order / layouts / enemies / blocks.
> Persistent power (Cards/Modules) carries across loops; prestige depth drives the competitive ladder.
> This reuses the campaign map, the existing `DungeonService.ApplyTier` hardening, and `RiftService`
> randomization rather than building a new mode.

---

## 0. Assumptions & still-open toggles (defaults chosen so the plan isn't blocked)

| Decision | Default taken | How to flip later |
|---|---|---|
| **Monetization** | F2P, **all earnable**; `Shards` is the prestige/premium currency with *earn-only* faucets. | Premium currency + IAP/ads is a **faucet config + store screen** bolt-on; the economy is built provider-agnostic so adding an IAP source touches one service, not the model. |
| **Cards/Modules vs run-draft** | **Layer** — Modules are a *permanent* layer; run-draft relics/cores/mods stay the *per-run* layer. | A future "unify" pass would map run-draft picks onto Module substats; not in this plan. |
| **Leaderboard score source** | **Two boards**: Phase A = weekly **shared-seed Trial** (skill); Phase B adds **Prestige depth** (grind). | Cumulative season score (C) is additive, not a replacement. |
| **Skill Points** | Kept separate in A; **folded into Cards** in B. | If you want them kept, skip task **B-CARD-FOLD**. |

**Standing rules (CLAUDE.md):** content (Cards, Modules, prestige mutators, events) is judged by *play vs
the design docs*, not LOC. Write **design-fidelity tests first** (assert trigger + identity, red→green).
Run `npx vite build` after every `.svelte` change. **Playtest** every reward/feel/visual change before
"done". Reconcile drift before extending. Commit only when the user asks.

**Server/test reminders:** rebuild `Arkanoid.Server` + restart on :5080 with
`ARKANOID_CHEATS=1` / `ASPNETCORE_ENVIRONMENT=Development` to deploy Core changes; meta/shared-profile
specs run `--workers=1`.

---

## PHASE 0 — Shared spine & economy foundation
*Prerequisite for A/B/C. No new player content yet; this is the plumbing + the economy cutover.*

### 0.1 Economy consolidation
- [ ] **P0-ECON-1** — Introduce **Gems** as the unified meta-soft currency. Add `Profile.Gems`; migrate
  reads of `Crystals` + `CampaignGold` to `Gems` (keep `Crystals` field as a deprecated alias that
  back-fills `Gems` on load so existing saves don't lose balance). *Files:* `Profile.cs`, `Rewards.cs`,
  `CampaignShopService.cs`, items meta-shop, `ProfileEndpoints`, `metaApi.ts`, all HUD/scene currency
  chips. *Test:* a profile with legacy `crystals`+`campaignGold` loads with `Gems == sum`; level-clear &
  shop spend operate on `Gems`. *Note:* finishes the Crystals→Gems rename already logged in
  `round-2-recommendations.md`.
- [ ] **P0-ECON-2** — Define the currency catalog as data: `CurrencyId { Gold, Gems, Shards, CardDust,
  ModuleCore, Medal, EventToken }` with display metadata (icon, name, scope). One source of truth for
  HUD/shop/reward rendering. *Test:* every currency referenced in code resolves to a catalog entry.
- [ ] **P0-ECON-3** — `WalletService` (Core, pure): `CanAfford(profile, cost)`, `Spend`, `Grant`,
  multi-currency costs. Replaces ad-hoc `profile.X -= n`. *Test:* spend fails atomically when any
  component is short.

### 0.2 Season clock & reset infra
- [ ] **P0-TIME-1** — `SeasonClock` (server-owned): `Now()`, `CurrentSeason()`, `CurrentWeek()`,
  `DayKey()`; never trust device time. Config: season length, week boundary, daily reset hour (UTC).
  *Test:* week/day rollover math at boundaries (DST-agnostic, UTC).
- [ ] **P0-TIME-2** — `GET /meta/clock` endpoint → `{ now, seasonId, seasonEndsAt, weekId, dayKey }`;
  frontend countdown helper reads it (no `Date.now()` for gating). *Test:* E2E reads clock, renders a
  countdown.

### 0.3 Leaderboard abstraction + anti-cheat (the spine's hard part — do early to de-risk)
- [ ] **P0-LB-1** — Define the port `ILeaderboardProvider` (Core/contracts): `SubmitScore`,
  `GetLeague(season,league,page)`, `GetMyStanding`, `GetSeason`, `SubscribeLeague?`. DTOs:
  `ScoreSubmission { playerId, boardId, seed, inputLog, clientScore, buildHash, version, nonce, sig }`,
  `SubmitResult { accepted, rank, shadowed }`. *Test:* contract compiles; no provider import leaks into
  game code.
- [ ] **P0-LB-2** — `InMemoryLeaderboard` adapter (tests + offline dev). *Test:* submit/read/rank round-trip.
- [ ] **P0-LB-3** — **Replay verifier** (the secret weapon): `ScoreVerifier.Verify(submission)` runs
  `Arkanoid.Core` **headless** with `(seed, inputLog)` → canonical score; accept iff
  `|canonical − clientScore| ≤ ε`. *Reuses the deterministic sim.* *Test (design-fidelity):* a hand-built
  valid input log verifies; a tampered `clientScore` is rejected; a tampered `inputLog` that doesn't
  reach the score is rejected.
- [ ] **P0-LB-4** — Input-log capture in the client: record the deterministic input stream
  (paddle/serve/cast events + tick) during a ranked run; submit it, not just the number. *Files:* battle
  loop / Connection. *Test:* captured log re-simulates to the same score locally (parity check).
- [ ] **P0-LB-5** — Plausibility guard: APM ceiling, min duration, score/tick max, inputs-in-bounds.
  *Test:* impossible APM / sub-threshold duration → flag.
- [ ] **P0-LB-6** — **Shadow-ban**: `Profile.ShadowFlag` (+ strike counter); flagged accounts' scores
  write only to a private shadow bucket; public reads exclude shadowed. **No client feedback.** Admin/
  appeal path = a manual flag reset. *Test:* shadowed submit returns `accepted:true` (looks normal) but
  `GetLeague` for others never includes it; the shadowed player still sees their own entry.
- [ ] **P0-LB-7** — `FirebaseLeaderboard` adapter (Firestore storage) **+ verification on trusted code**:
  decide host = reuse `Arkanoid.Server` as verifier with Firestore as storage (recommended — we already
  have the sim) **vs** Cloud Function w/ sim-as-WASM. *Deliver:* the chosen adapter behind the same port.
  *Test:* integration test against the Firebase emulator; same contract tests as InMemory pass.
- [ ] **P0-LB-8** — Server endpoints: `POST /lb/submit`, `GET /lb/league`, `GET /lb/standing` delegating
  to the configured provider; auth via signed session token + nonce (anti-replay). *Test:* submit→read flow.

**Phase 0 exit:** Gems migration green on legacy saves; a ranked dummy score round-trips through the
provider with replay verification + shadow-ban proven by tests; clock endpoint live.

---

## PHASE A — Weekly Trial Ladder + Dailies + Cards
*Smallest cohesive system exercising all three pillars (rewards / economy / leaderboard).*

### A.1 Cards (the meta-power layer)
- [ ] **A-CARD-1** — `Card` model + `CardCatalog` (data-driven, `config/cards.json`): id, name, icon,
  rarity, effect spec, per-level scaling. Seed ~12 cards reusing our vocabulary (Molten Core +ball dmg,
  Tithe +gold, Overflow +start mana, Second Wind survive-first-death, Draftsman +1 pick option, …).
- [ ] **A-CARD-2** — `Profile`: `OwnedCards { id→{level, copies} }`, `EquippedCards[]`, `CardSlots`
  (starts 3, grows via Shards/account level). *Test:* equip capped at `CardSlots`; can't equip unowned.
- [ ] **A-CARD-3** — **Card effects applied at run start** in `GameInitializer` (and dungeon floors):
  each equipped card's effect mutates the `GameInstance` (dmg mult, start mana, etc.). *Design-fidelity
  test per card:* e.g. "Tithe makes a coins pickup grant +X% Gold"; "Second Wind survives exactly one
  lethal hit per level, then not." **Bespoke behavior, no generic stat bag.**
- [ ] **A-CARD-4** — Card leveling: duplicates → `CardDust`; `CardService.LevelUp(profile, cardId)`
  spends Dust, raises level/scaling, capped. *Test:* level scaling matches catalog; cap respected.
- [ ] **A-CARD-5** — Endpoints: `GET /cards` (catalog + owned + equipped + slots), `POST /cards/equip`,
  `POST /cards/levelup`. `metaApi` methods. *Test:* equip/levelup persist.
- [ ] **A-CARD-6** — **Cards screen** (Svelte): grid of owned cards (rarity frames), equip/unequip into
  slots, level-up button (Dust cost), locked-slot hints. `vite build` + Playwright. *Playtest:* screenshot-
  review the screen against the visual bar; confirm effects feel real in a battle.

### A.2 Daily missions (weekly-resetting pool)
- [ ] **A-DAILY-1** — `MissionDef` catalog (`config/missions.json`): id, description, metric
  (blocks_destroyed, floors_cleared, perfect_deflects, spells_cast, …), target, reward (Gems + CardDust).
- [ ] **A-DAILY-2** — Daily assignment: `DailyService.RollDaily(profile, dayKey, seed)` picks 3 distinct
  missions from a **weekly pool** that reshuffles on week rollover; deterministic per `dayKey`. *Test:*
  same dayKey → same 3; week rollover changes the pool; 3 distinct.
- [ ] **A-DAILY-3** — Progress tracking: sim/run emits metric deltas → `DailyService.Record(profile,
  metric, amount)`; mission completes at target; claim grants reward (once). *Test:* progress accrues,
  completes at target, claim is idempotent, resets at `dayKey` change.
- [ ] **A-DAILY-4** — 7-day streak chest (bonus on consecutive claim days). *Test:* streak increments,
  breaks on a missed day.
- [ ] **A-DAILY-5** — Endpoints `GET /daily`, `POST /daily/claim`; `metaApi`. *Test:* claim flow.
- [ ] **A-DAILY-6** — **Daily board UI** (entry from menu): 3 mission cards w/ progress bars, claim
  buttons, streak meter, countdown to reset (from clock). `vite build` + Playwright + screenshot review.

### A.3 Weekly Trial + Leagues
- [ ] **A-TRIAL-1** — Weekly Trial definition: one hand-tuned gauntlet level + a **weekly shared seed**
  (from `SeasonClock.weekId`). Score = blocks + depth + time bonus, computed in Core (deterministic).
  *Test:* score formula deterministic for seed.
- [ ] **A-TRIAL-2** — Trial run = ranked: enters via a "Weekly Trial" menu entry; on finish, captures the
  input log (P0-LB-4) and submits (P0-LB-8). One scored attempt/week (or best-of, decide in tuning).
  *Test (E2E):* play trial → submit → standing reflects score.
- [ ] **A-LEAGUE-1** — League model: cohorts of ~30, tiers Wood→Champion; weekly **promote top 7 /
  demote bottom 7**; `LeagueService` assigns/seeds cohorts. *Test:* promo/demo math on a mock cohort.
- [ ] **A-LEAGUE-2** — Placement rewards (Gems + CardDust + rare Card by tier/rank), granted at week
  rollover. *Test:* rewards scale by placement; granted once per week.
- [ ] **A-LEAGUE-3** — Minimal **league shop** (spend `Medal` from placement on a rotating Card). *Test:*
  buy deducts Medals, grants card.
- [ ] **A-LEAGUE-4** — **Ladder UI**: your cohort list (rank, name, score), your standing, promo/demo
  zone highlight, tier badge, countdown. Reads via provider (no Firebase import in UI). Playwright +
  screenshot review.

**Phase A exit:** dailies mint CardDust → Cards strengthen Trial runs → Trial placement mints Medals/
Gems → Gems buy run-shop edge. Full loop playable; leaderboard replay-verified; shadow-ban live. Full
backend suite + E2E green; map/screens screenshot-reviewed.

---

## PHASE B — Prestige Campaign Loop + Modules
*Grow A into the Tower-style power chase, with the prestige loop as the second (grind) leaderboard.*

### B.1 Prestige Campaign Loop (the reframed "endless")
- [ ] **B-PRES-1** — `Profile.PrestigeTier` (0 = base). On final heaven-boss clear, offer **Ascend**:
  increments `PrestigeTier`, clears campaign `CompletedLevels` (campaign progress only), **persists**
  Cards/Modules/Gems/Shards/account level. *Test:* ascend resets map progress, keeps meta; tier++.
- [ ] **B-PRES-2** — **Difficulty scaling per tier** for campaign battles: reuse the `ApplyTier`
  pattern in `GameInitializer` when `PrestigeTier>0` (block HP/MaxHp +tier; optionally enemy emit-rate
  scale). *Design-fidelity test:* tier-N campaign level has every destructible block hardened by N.
- [ ] **B-PRES-3** — **Randomized biome order**: `PrestigeService.BiomeOrder(tier, profileSeed)` shuffles
  hell/caverns/witchland/heaven deterministically per loop; the campaign map is regenerated in that order
  (keep the graph/forks shape, swap which biome fills each segment). *Test:* deterministic per (tier,seed);
  differs across tiers; all 4 biomes present, boss-gated structure intact.
- [ ] **B-PRES-4** — **Layout/enemy/block mutators**: `PrestigeMutator.Apply(level, tier, seed)` (Core,
  pure) transforms a loaded level — biome-appropriate swaps (standard→armored, +emitters, +lava/ghost
  hazards), density up with tier. Reuses existing block behaviors (no new block types required for v1).
  *Design-fidelity test:* a mutated level keeps it winnable (passability) AND differs from the template
  (e.g., block-type histogram changed, ≥1 added hazard at tier≥1).
- [ ] **B-PRES-5** — **Scaling rewards**: Gems/CardDust/ModuleCore payouts scale with `PrestigeTier`.
  *Test:* tier-N clear pays > tier-(N-1).
- [ ] **B-PRES-6** — **Prestige leaderboard** board: standing = `PrestigeTier` then within-tier progress
  (nodes cleared this loop / prestige score). Submitted ranked (replay-verified where a single scored run
  applies; for cumulative progress, server-authoritative state, not client claim). *Test (E2E):* ascending
  raises prestige standing.
- [ ] **B-PRES-7** — UI: Ascend prompt on campaign completion (rewards preview, "Prestige N→N+1"),
  prestige badge on the campaign header + profile, prestige ladder screen. Playwright + screenshot review.
  *Playtest:* run a tier-1 loop end-to-enough to confirm it *feels* like a harder, freshly-shuffled campaign.

### B.2 Modules (4 slots, rarity, substats, reroll)
- [ ] **B-MOD-1** — `Module` model + `ModuleCatalog` (`config/modules.json`): slot
  (Core/Paddle/Ball/Field), rarity (Common→Mythic), main effect, substat pool; Epic+ unique named effect.
- [ ] **B-MOD-2** — `Profile.OwnedModules[]` (instances w/ rolled substats + level), `EquippedModules`
  (one per slot). *Test:* one module per slot; equip validates slot.
- [ ] **B-MOD-3** — **Module effects at run start** (`GameInitializer`): slot maps onto our axes — Core→
  ball-core, Paddle→paddle-mod, Ball→dmg/speed/crit substats, Field→economy/mana/drop substats.
  *Design-fidelity test per slot:* e.g. an equipped Core module applies its ball-core behavior at serve;
  a Field module's +gold substat increases coins payout. **Reuse existing core/mod behaviors.**
- [ ] **B-MOD-4** — Crafting/leveling/reroll with `ModuleCore`: `ModuleService.Level`, `Reroll(substats)`,
  `Craft(rarity)`. *Test:* reroll changes substats deterministically per RNG; level caps; cost deducts.
- [ ] **B-MOD-5** — Endpoints + `metaApi` (`/modules`, equip/level/reroll). *Test:* persistence.
- [ ] **B-MOD-6** — **Modules screen**: 4 slot frames, inventory by rarity, substat display, reroll/level
  buttons (ModuleCore cost). `vite build` + Playwright + screenshot review.
- [ ] **B-LEAGUE-MOD** — League/prestige shops sell Modules + ModuleCore. *Test:* buy flow.

### B.3 Skill-Points fold (optional, default on)
- [ ] **B-CARD-FOLD** — Convert spell upgrades from `Points` to per-spell **Cards** (or a Card-Dust cost);
  migrate existing `Points`/`SpellLevels` into the card layer; retire the campaign upgrade panel or
  repoint it at Cards. *Test:* a migrated profile keeps equivalent spell power; no orphaned Points sink.
  *(Skip this task to keep Skill Points separate.)*

**Phase B exit:** prestige loop replayable with scaled+shuffled+mutated campaigns; Modules fully in
(rarity+reroll) and feeding power; two leaderboards (Trial skill + Prestige grind); economy chase
currencies (CardDust/ModuleCore/Medal) all have sources+sinks. Suites green; screens + a prestige loop
playtested.

---

## PHASE C — Season Festival
*Wrap A+B in recurring Seasons + rotating events; live-ops as the engine, little new core code.*

### C.1 Seasons & reward track
- [ ] **C-SEASON-1** — `Season` model (`config/seasons.json` or generated): id, theme, start/end (from
  clock), modifiers. `SeasonService.Current()`. *Test:* current season resolves from clock.
- [ ] **C-SEASON-2** — **Reward track**: tiers with free lane (+ optional premium lane behind Shards/IAP
  toggle); `SeasonTrack` progress via `SeasonToken`. Rewards = Cards/Modules/Gems/cosmetics.
  *Test:* token accrual advances tiers; claim idempotent; premium lane gated.
- [ ] **C-SEASON-3** — Token faucets: dailies, trial, prestige, event play all grant `SeasonToken`.
  *Test:* each faucet credits the track.
- [ ] **C-SEASON-4** — **Battle-pass UI**: track with claimable nodes (free/premium lanes), season
  countdown, theme banner. Playwright + screenshot review.

### C.2 Rotating events (We-Are-Warriors style)
- [ ] **C-EVENT-1** — `EventDef` catalog: themed limited-time modifier (e.g., "Inferno Rising": +lava,
  fire enemies), `EventToken` currency, an event reward track + event leaderboard board id.
- [ ] **C-EVENT-2** — Event play surface: a modified mode (campaign/dungeon/trial with the event
  modifier) that mints `EventToken`. *Design-fidelity test:* the event modifier actually changes the
  board (e.g., lava present) vs base.
- [ ] **C-EVENT-3** — Event leaderboard (reuses the provider/league spine with an `eventId` board) +
  event reward track. *Test:* submit/read on the event board; rewards by placement.
- [ ] **C-EVENT-4** — **Event UI**: event banner on menu, event track, event ladder. Screenshot review.

### C.3 Season leaderboard
- [ ] **C-LB-1** — **Season Score** board: cumulative points across campaign/dungeon/trial/prestige/event
  play, snapshotted into the weekly league standings. Server-authoritative accumulation (not client
  claim). *Test:* score accrues from multiple sources; weekly snapshot freezes standings.

**Phase C exit:** a full season runs end-to-end (theme → track → event → ladder → season payout) with no
new core systems — only content/config + the track/event UI. Suites green; a season dry-run playtested.

---

## Cross-cutting

### Testing strategy (per CLAUDE.md)
- **Backend (xUnit):** every Card/Module/mutator/mission gets a design-fidelity test on its **trigger +
  identity** (written first, red→green). Systems get a structural test (e.g., "a card only takes effect
  when equipped"; "prestige resets campaign progress but not Modules"; "a shadowed score never reaches
  another player's league read").
- **E2E (Playwright):** one happy-path per pillar per phase (claim a daily, submit a trial, equip a card,
  ascend, buy a module, advance the season track). Anti-cheat: a spec that submits a tampered score and
  asserts it's silently shadowed (visible to self, absent for others).
- **Verifier parity:** the client-side input log must re-simulate to the same score the server computes
  (a Core test + a runtime parity assert behind a dev flag).
- **Build gate:** `npx vite build` after every `.svelte` change; meta specs `--workers=1`.

### Playtest checkpoints (don't skip — "it compiled" ≠ done)
1. Cards screen + a battle showing a card's real effect.
2. Daily board + claim feel.
3. Ladder screen + a verified Trial submission.
4. A **prestige tier-1 loop**: confirm it reads as a harder, reshuffled, mutated campaign (screenshot the
   regenerated map + a mutated level).
5. Modules screen + reroll feel; a module's effect in a run.
6. A season track + event modifier visibly changing the board.

### Sequencing & dependencies
```
P0 (spine+economy) ─┬─▶ A.1 Cards ─┐
                    ├─▶ A.2 Dailies ┼─▶ A.3 Trial+Leagues ─▶ (Phase A ships)
                    └─▶ (LB spine) ─┘
A ─▶ B.1 Prestige ──┬─▶ B.2 Modules ─▶ B.3 Fold(optional) ─▶ (Phase B ships)
                    └─(reuses LB spine for prestige board)
B ─▶ C.1 Season ─▶ C.2 Events ─▶ C.3 Season LB ─▶ (Phase C ships)
```
- **P0 gates everything.** Within A, Cards/Dailies are parallel; Trial+Leagues need the LB spine.
- B needs A's Cards (Modules sit beside them) + the LB spine (prestige board).
- C needs B's Cards/Modules as track rewards + the LB spine (season/event boards).

### Rough effort (engineering weeks, solo-ish)
| Phase | Effort | Risk |
|---|---|---|
| P0 spine + economy cutover | ~2 wks | Med (replay verifier + Gems migration on live saves) |
| A — Trial + Dailies + Cards | ~2–3 wks | Low–Med |
| B — Prestige + Modules | ~3–4 wks | Med (prestige mutators winnability + economy breadth) |
| C — Season + Events | ~2–3 wks | Low code / High ongoing content-ops |

### First concrete steps (when you say go)
1. `P0-ECON-1` Gems migration (unblocks every currency-facing change) + its legacy-save test.
2. `P0-LB-1..3` the port + InMemory adapter + **replay verifier** (prove the anti-cheat thesis early).
3. `A-CARD-1..3` Cards catalog + equip + first card effect with a design-fidelity test.

Then I'll convert the chosen slice into a tracked task list and start red→green.
