# Social Systems, Economy & Leaderboards — Shipped (2026-06-13)

Implements the A→B→C plan (`2026-06-13-social-economy-implementation-plan.md`), **fully local on SQLite —
no cloud**. Everything runs on `localhost` (backend `:5080`, frontend `:5175`); the only new dependency is
`Microsoft.Data.Sqlite` (a local file DB at `saves/arkanoid.db`).

## What shipped

### Spine (P0)
- **Currencies**: added `CardDust`, `ModuleCores`, `Medals`, `EventTokens`, `SeasonTokens` to the profile;
  a pure `Wallet` (atomic multi-currency spend). Legacy currencies kept (the Gems-merge is deferred — see
  below). `SeasonClock` (server-owned season/week/day buckets) + `GET /meta/clock`.
- **Leaderboard** (`ILeaderboardStore` port): `SqliteLeaderboardStore` (local) + `InMemoryLeaderboardStore`
  (tests). `LeaderboardService` = deterministic **bot cohorts** (so leagues are meaningful in single-player),
  promotion/demotion, placement rewards.
- **Anti-cheat / shadow-ban**: battles are **server-authoritative** (the server runs the sim, so it *computes*
  the score — the client never sends one). The direct `/lb/submit` path is plausibility-checked; an impossible
  score earns strikes and a **silent shadow-ban** (the score is recorded but excluded from every other
  player's board; the cheater still sees their own row, with no signal). Endpoints `/lb/league`,
  `/lb/standing`, `/lb/submit`, `/lb/resolve`.

### Phase A
- **Cards** (`config/cards.json`): equip-limited passive layer; effects applied at run start (reuse the
  item modifier hooks via the shared `RunModifier`); Card-Dust leveling. `/cards*` + Cards screen.
- **Daily missions** (`config/missions.json`): 3/day from a deterministic weekly pool; progress recorded
  **server-authoritatively at battle end**; claim for Gems + Card Dust; 7-day streak chest. `/daily*` + board.
- **Weekly Trial + Leagues**: a server-chosen shared level + weekly seed; the server scores the run and
  submits it (no client score). League ladder UI (Wood→Champion, promo/demo, bot cohort).

### Phase B
- **Prestige campaign loop** (the reframed "endless"): beat heaven-boss → **Ascend** into New Game+ —
  campaign progress wipes, all meta (cards/modules/currencies/level) is kept, tier++. Battles harden
  (reused `ApplyTier`) and get remixed enemies (`PrestigeService.ApplyMutators`); rewards scale +50%/tier;
  a **prestige leaderboard** ranks by tier. Ascend button + prestige badge in the campaign.
- **Modules** (`config/modules.json`): 4 slots (core/paddle/ball/field), rarity Common→Mythic, **rerollable
  substats** (Module Cores), level-up; effects at run start via `RunModifier`. `/modules*` + Modules screen.

### Phase C
- **Season Festival** (`config/seasons.json`, `config/events.json`): rotating themes + a **battle-pass reward
  track** (Season Tokens, earned every battle); a **weekly event** whose modifier is live in all battles
  (e.g. Inferno = +ball damage) with its own tokens + milestone reward; **season + event leaderboards**.
  `/season`, `/season/claim-tier`, `/event/claim` + Season scene.

### Progressive feature unlocks (campaign-gated)
Features reveal as you advance the campaign, so new players aren't dumped into every system at once
(`FeatureGates`, `GET /features`):
- **Daily Missions** — from the start. **Cards** — beat Hell II. **Season** — Hell III. **Modules** —
  Hell boss. **League** — Caverns boss. **Prestige** — Heaven boss (campaign clear).
- **Clear UI**: locked menu buttons are dimmed with a 🔒 badge; tapping one shows a centered toast naming
  the exact level that unlocks it. Clearing a gate level fires a **"🔓 Unlocked: <feature>"** beat in the
  level-clear reward overlay. Core meta (Heroes/Loadout/Items/Skills/Settings) is always available.

## Tests
Backend **452/452** (xUnit): Wallet/Clock 9, Leaderboard 10, Cards 8, Daily 6, Trial 3, Prestige 7,
Modules 7, Season 7 (+ existing 388). E2E (Playwright, against the live SQLite-backed server): `lb`,
`cards`, `dailies`, `league`, `prestige`, `modules`, `season`. Every UI screen was screenshot-reviewed.

## How to run (fully local)
1. Backend: from `backend/`, `dotnet run --project Arkanoid.Server` (creates `saves/arkanoid.db` on first run).
2. Frontend: from `frontend/`, `npm run dev`.
3. New menu dock entries: **Cards · Modules · Daily · League · Season** (plus Heroes/Loadout/Items/…).

## Deferred (logged, not blocking)
- **Gems currency merge** (Crystals + CampaignGold → Gems): kept legacy currencies to avoid a risky live-save
  migration; the new chase currencies are additive. (Already noted in `round-2-recommendations.md`.)
- **Prestige biome-order reorder** (the "potentially randomized *locations*"): the mutator delivers the
  harder + randomized-*enemies/blocks* core; full biome remap of the node graph is a follow-up.
- **Skill-Points → Cards fold** (plan B-CARD-FOLD, optional): Points and Cards both work independently.
- Premium/IAP monetization faucets: the economy is built provider-agnostic; no IAP wired (all earnable).
