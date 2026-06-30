# Firebase Social Integration — Design Spec
_Owner-approved 2026-06-30_

## Scope

Two phases of social infrastructure on top of the existing WASM-offline game:

- **Phase 1** — Cloud identity + global leaderboards. Fully on Firebase Spark (free tier, no credit card).
- **Phase 2** — Community events + weekly competition season. Requires Firebase Blaze plan (credit card on file; free-tier quotas cover a small game at ~$0/month, but the card is required for Cloud Functions).

The game stays 100% playable offline; Firebase is additive. Every Firebase call is fire-and-forget or best-effort — a network failure never blocks gameplay.

---

## Phase 1 — Cloud Identity + Global Leaderboards

### 1.1 Identity model

On first app load, Firebase signs the player in **anonymously**. The resulting `uid` is the player's permanent key. A nickname is derived once and stored in Firestore:

```
nickname = "{HeroClass}#{uid.slice(0,4).toUpperCase()}"
// e.g. "FireMage#A3F2"  (hero class from profile.selectedCharacter)
```

Nickname is set once at first sign-in and never auto-changed. The player may rename later (Phase 2 stretch).

A **"Link with Google"** button calls `linkWithPopup(GoogleAuthProvider)`. The `uid` is preserved — all Firestore docs keyed by `uid` remain intact. This enables cross-device sync.

Identity state is stored in Firebase Auth (persisted in IndexedDB by the SDK). `ark_pid` localStorage key continues to work as the WASM profile namespace; `uid` is the Firestore key. They are kept in sync by `FirebaseAuth.ts`.

### 1.2 Profile backup / restore

On every `complete()` call (level win), after the WASM write, `FirestoreSync.syncProfile()` mirrors the serialized profile JSON to `users/{uid}/profileSnapshot`. Cost: 1 Firestore write per win.

On Google sign-in on a new device: if `users/{uid}/profileUpdatedAt` is newer than the local WASM save, offer a modal: **"Cloud save found (Jun 30) — Restore?"** Accepting overwrites the local WASM profile.

### 1.3 Leaderboard A — Souls earned (time-windowed)

Three parallel collections, one per time window:

```
lb/souls_day/scores/{uid}
lb/souls_week/scores/{uid}
lb/souls_month/scores/{uid}

Each doc:
  nickname:     string
  souls:        number   ← earned since periodStart
  periodStart:  timestamp  ← midnight UTC of current day/Monday/1st-of-month
  updatedAt:    timestamp
```

**No Cloud Functions needed.** Period resets are client-driven:

On `submitSouls(amount, uid)`:
1. Read the user's doc for each period.
2. Compute `currentPeriodStart` (deterministic, UTC-aligned).
3. If `doc.periodStart == currentPeriodStart` → `increment(amount)`.
4. If `doc.periodStart < currentPeriodStart` → set `souls = amount, periodStart = currentPeriodStart` (natural reset).
5. All three writes run in parallel (not a single transaction — occasional off-by-one on network retry is acceptable for a leaderboard).

`submitSouls` is called from `campaignFlow.ts` after a win, passing `reward.soulsGained + reward.starBonusSouls`.

**Leaderboard query:**
```typescript
collection(db, `lb/souls_${window}/scores`)
  .where("periodStart", "==", currentPeriodStart(window))
  .orderBy("souls", "desc")
  .limit(100)
```
Player's own rank: find index in result set; if not in top 100, read own doc separately and binary-search rank via a count query.

### 1.4 Leaderboard B — Progression (all-time)

```
lb/progression/scores/{uid}
  nickname:       string
  maxLevelIndex:  number   ← ordinal in the linear campaign chain (0-based)
  maxLevelId:     string   ← human-readable (e.g. "heaven-boss")
  maxSpellLevel:  number   ← max(spells[].level) across all owned spells
  maxHeroStars:   number   ← max(heroes[].stars) across all owned heroes
  updatedAt:      timestamp
```

Updated by `FirestoreSync.syncProgression(profile, campaignCatalog)` on every `complete()`. The level index is computed by `campaignCatalog.Nodes.findIndex(n => n.id === levelId)`.

**Leaderboard query:**
```typescript
collection(db, "lb/progression/scores")
  .orderBy("maxLevelIndex", "desc")
  .orderBy("maxSpellLevel", "desc")
  .orderBy("maxHeroStars", "desc")
  .limit(100)
// Requires a composite index: maxLevelIndex DESC, maxSpellLevel DESC, maxHeroStars DESC
```

### 1.5 Firestore security rules

```
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /users/{uid} {
      allow read: if true;
      allow write: if request.auth != null && request.auth.uid == uid;
    }
    match /lb/{board}/scores/{uid} {
      allow read: if true;
      allow write: if request.auth != null && request.auth.uid == uid;
    }
    match /events/{eventId} {
      allow read: if true;
      allow write: if false;  // admin SDK / Cloud Functions only
    }
    match /events/{eventId}/participants/{uid} {
      allow read: if request.auth != null && request.auth.uid == uid;
      allow write: if request.auth != null && request.auth.uid == uid;
    }
  }
}
```

### 1.6 Frontend files

| File | Role |
|---|---|
| `frontend/src/net/firebase.ts` | Init Firebase app, export `auth`, `db` |
| `frontend/src/net/FirebaseAuth.ts` | `initAuth()`, `linkGoogle()`, `onNicknameReady(cb)` |
| `frontend/src/net/FirestoreSync.ts` | `syncProfile()`, `submitSouls(amount)`, `syncProgression()` |
| `frontend/src/net/FirestoreLeaderboard.ts` | `getBoard(type, window)` → `{rows, playerRank}` |
| `frontend/src/scenes/LeaderboardScene.svelte` | New scene: tabs Active (Day/Week/Month) + Progression |
| `frontend/src/ui/AuthBadge.svelte` | Corner chip: nickname + "Link with Google" |
| `frontend/src/scenes/CampaignScene.svelte` | Add leaderboard button + `<AuthBadge>` |

`firebase` npm package added to `frontend/package.json`. Firebase config (apiKey, projectId, etc.) stored in `frontend/.env` as `VITE_FIREBASE_*` vars; `.env` is gitignored. A `frontend/.env.example` is committed.

`initAuth()` is called once in `frontend/src/main.ts` before scene routing.

### 1.7 Integration points with existing code

- `campaignFlow.ts` line ~59: after `metaApi.complete()` returns, call `FirestoreSync.submitSouls(reward.soulsGained + (reward.starBonusSouls ?? 0))` and `FirestoreSync.syncProgression(profile, catalog)`. Both are fire-and-forget (`catch(() => {})` — never block the reward overlay).
- `WasmApi.ts` / `metaApi.ts`: no changes. Firestore is a parallel layer, not a replacement.
- Campaign map navigation button added to open `LeaderboardScene`.

---

## Phase 2 — Events + Weekly Competition Season

_Requires Firebase Blaze plan (credit card; actual charges at game scale ≈ $0)._

### 2.1 Community events

**Firestore shape:**
```
events/active
  id:           string
  name:         string   ← "Summer Blitz"
  description:  string
  targetCount:  number
  currentCount: number   ← incremented per level-clear
  rewardSouls:  number
  startsAt:     timestamp
  endsAt:       timestamp
  status:       "active" | "complete"

events/{id}/participants/{uid}
  clearedCount: number   ← levels cleared by this player during the event
  rewarded:     bool
```

**On level-clear (Phase 2 addition to `campaignFlow.ts`):**
```typescript
if (activeEvent?.status === "active") {
  await runTransaction(db, async tx => {
    const ev = await tx.get(activeEventRef);
    const next = (ev.data().currentCount ?? 0) + 1;
    tx.update(activeEventRef, { currentCount: next,
      status: next >= ev.data().targetCount ? "complete" : "active" });
    tx.set(participantRef, { clearedCount: increment(1) }, { merge: true });
  });
}
```

**Reward claiming:** On app load and after each level-clear, check `events/active.status`. If `"complete"` and `participants/{uid}.rewarded != true`: show a claim banner → call `claimEventReward()` which adds Souls via `wasmApi.devCoins(0, rewardSouls, 0)` then marks `rewarded: true` in Firestore.

**Event creation:** Admin script / manual Firestore console write. No in-game admin UI in scope.

**Progress bar:** Shown on `CampaignScene` when `events/active` exists. Real-time listener via `onSnapshot`.

### 2.2 Weekly competition season

The `lb/progression/scores` collection is already all-time. A season layer adds weekly snapshots:

```
lb/seasons/{isoWeek}/scores/{uid}
  ← same fields as lb/progression/scores, written at week-close by Cloud Function
```

**Cloud Function: `resolveWeeklySeason`** — scheduled every Monday 00:01 UTC:
1. Read top 1000 from `lb/progression/scores` (snapshot of the just-ended week).
2. Compute tier per player: Diamond (top 1%), Gold (top 10%), Silver (top 30%), Bronze (rest).
3. Write to `lb/seasons/{lastWeek}/scores/{uid}`.
4. Write `users/{uid}/lastSeasonResult: {rank, tier, week}`.

**In-game season results modal:** On first visit of a new ISO week, if `users/{uid}/lastSeasonResult.week == lastWeek` and not yet shown: display "Season results — Gold #42 ▲ promoted" modal before campaign screen. Dismiss → mark shown in localStorage.

**Season rank chip:** Small tier badge (Bronze/Silver/Gold/Diamond) next to the leaderboard button on the campaign screen, read from `users/{uid}/lastSeasonResult`.

### 2.3 Phase 2 frontend files

| File | Role |
|---|---|
| `frontend/src/net/FirestoreEvents.ts` | `subscribeEvent(cb)`, `claimEventReward()`, `recordLevelClear()` |
| `frontend/src/ui/EventBanner.svelte` | Campaign-screen progress bar + claim button |
| `frontend/src/ui/SeasonResultModal.svelte` | End-of-week tier reveal modal |
| `functions/resolveWeeklySeason.ts` | Cloud Function (scheduled, Node 20) |
| `functions/package.json` + `functions/tsconfig.json` | Functions sub-project |

---

## What is NOT in scope

- In-app purchases / payments
- Google Play Store / TWA packaging
- Friend lists, direct messaging, guilds
- Chat or replays
- Admin dashboard UI (events created via Firestore console)

---

## Open questions / deferred decisions

| # | Question | Deferred to |
|---|---|---|
| 1 | Player nickname rename (paid or free?) | Phase 2 or later |
| 2 | Anti-cheat for Souls leaderboard (client-reported, unvalidated) | Post-launch |
| 3 | Firebase Blaze upgrade timing | Before Phase 2 implementation |
| 4 | Event cadence (weekly? bi-weekly?) | Content/ops decision at launch |
