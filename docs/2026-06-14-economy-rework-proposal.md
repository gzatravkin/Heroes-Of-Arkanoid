# Economy Rework Proposal — 3 Currencies + Random-Unlock + Linear Map

**Date:** 2026-06-14
**Status:** PROPOSAL — awaiting review. No code written. Nothing committed.
**Supersedes/amends:** the 2026-06-13 social/economy plan's currency sprawl, `docs/04` §5 (economy) and §6.1 (branching map), and `docs/2026-06-13-content-and-stats-design.md` §5.6/§5.10 (Points-funded mastery + per-hero drafted kits).

---

## 0. Why

Two problems:

1. **Currency sprawl.** The profile carries ~11 currencies (`Exp`→`Points`, `Crystals`, `Shards`, `CampaignGold`, `CardDust`, `ModuleCores`, `Medals`, `EventTokens`, `SeasonTokens`, per-hero `HeroTokens`), accreted across three separate plans. The player can't reason about what to spend where.
2. **Deterministic upgrades have no chase.** Every upgrade today is "pick exactly the thing you want and pay for it" (spells & masteries from `Points`; cards from `CardDust`; modules from `ModuleCores`; ★ from `HeroTokens`). Flat and gridy.

**Goal:** collapse to **3 currencies**, flip all progression to **fixed-price random rolls from pools** (duplicates are never wasted — they level the item or ascend the hero), and **delete the campaign branching** (it does no gameplay work — see `CampaignScene.svelte` / `config/campaign.json`: Hell is a linear snake drawn as a fork; the only real forks are dead-end shop detours + one forced Heaven reconvergence).

---

## 1. The 3 currencies

Names are **placeholders** — rename freely.

| Coin | Spent on (sinks) | Earned from (sources) | Rarity |
|---|---|---|---|
| **Sparks** (C1 — *gear*) | random **Card** roll · random **Module** roll (dupes level the item) | level clears, daily missions, first-clears | common |
| **Souls** (C2 — *loadout & roster*) | random **Spell** roll · random **Hero** roll (dupes → spell level / hero ★) · **+spell-slot** unlocks · **mastery respec** | biome **boss** clears, **rift** depth, milestones | rare |
| **Insight** (C3 — *grind*) | **level Mastery** nodes | **every** battle won (each campaign level / rift floor) | abundant |

Design intent of the split:
- **C3 is the bottomless sink** (mastery) fed by the most abundant source, so there's *always* something to spend on without it starving unlocks.
- **C1 is the steady build layer** (the gear you equip: cards + modules).
- **C2 is the scarce identity layer** (which spells/heroes you have, how many you can equip, and the gated respec). Putting slot-unlocks + respec here makes them real choices against rolling.

`Points` is **deleted**.

---

## 2. The random-roll system (applies to Cards, Modules, Spells, Heroes)

- **One roll = one fixed-price pull** from that pool. **Pure random** (no pity, no dupe protection — confirmed).
- **Duplicates are never dead:**
  - Card / Module / Spell duplicate → **+1 level** of that item (capped).
  - Hero duplicate → **+1 ★** for that hero (replaces `HeroTokens`).
- **Maxed-pool terminal rule:** when *nothing* in a pool can still improve (all owned + all maxed), the roll button **disables** — it never eats currency for nothing.
- **Pools:**
  - **C1 pool:** all cards + all modules (mixed).
  - **C2 spell pool:** the **global** spell list (large).
  - **C2 hero pool:** the **4–5 heroes** that have been *unlocked into the pool* (small — see §4).

> Open default (flag if wrong): **separate roll buttons per category** within a coin — "Roll Card", "Roll Module", "Roll Spell", "Roll Hero" — rather than one mixed button. Clearer intent, lets the player chase the layer they want. All priced in the coin from §1.

---

## 3. Spells

- **Loadout = signature + up to 3 global.**
  - **Signature spell:** locked to its hero, *cannot* be placed on another hero, **always equipped** (slot 0). Carries class identity.
  - **+3 flex slots:** filled from the **global** spell pool — **any spell on any hero**.
- **Acquisition:** spells are collected like cards — **C2 spell roll**; first copy unlocks, **duplicates raise spell level**.
- **Spell affinity (soft identity):** each spell has an affinity (**fire / holy / tech / death** — matching fire_mage / paladin / engineer / necromancer). A spell cast by a **matching-affinity hero** gets a **small bonus** (proposed: +10–15% magnitude *or* −1 mana; pick one in impl). This keeps "Fire Mage runs fire best" without locking the pool.
- **Slot growth:** flex slots start at **1** and grow **1 → 2 → 3** by **spending Souls (C2)**, escalating cost (proposed 1st unlock < 2nd). (My pick — override if you'd rather it be C3.)

⚠️ **Deliberate design override:** this replaces the doc's *"signature + drafted-in-run-from-a-shared-pool"* (CLAUDE.md cardinal rule / `docs/04` §3, §4.1) with **meta random-unlock from a global pool + affinity**. On approval we **amend CLAUDE.md and `docs/04`** to record this as the intended model (not silent drift). Identity now rests on **signature spell + affinity + the hero's stat profile + ★ perks** — we must keep those distinct enough to carry it.

---

## 4. Heroes

- There are **4–5 heroes** total → the hero pool is **small**, so pure-random unlock is not painful.
- **Boss clear adds a hero to the C2 hero pool.** The hero is **locked until you roll their first card** (C2 hero roll). This gates *which* heroes can even appear behind campaign progress.
- **Duplicate hero rolls → +1 ★** (ascension), using the existing ★ ceiling/perk system (`StatResolver.MaxStars`). **`HeroTokens` retired.**

---

## 5. Cards & Modules

- **Cards:** C1 roll; first copy unlocks at L1, **duplicates → +1 level** (keeps `CardService` equip/slot logic; `LevelUp` now driven by duplicates, not a dust spend). `CardSlots` (start 3, grows) unchanged.
- **Modules:** C1 roll; duplicates level them. **Delete the substat/reroll system** (`ModuleService.RollSubstats` / `Reroll` / `SubstatCount` / `RarityMult` and `ModuleInstance.Substats`) — it already contradicts the §2 "no sub-stats, one slot-bound passive" runtime I built. This **shrinks code + removes a currency need** (`ModuleCores` reroll). One equipped module per slot (core/paddle/ball/field) unchanged.

---

## 6. Mastery

- **Level a mastery node:** spend **Insight (C3)** (replaces the `Points` spend in `Upgrades.TryUpgradeMastery`). Per-node max levels unchanged (`StatResolver.MasteryMaxLevels`).
- **Respec:** a **full reset** refunds all Insight you've spent (re-allocatable) and costs a flat **Souls (C2)** fee — C2 gates respec-spam. Structure stays the current account-wide `Profile.Masteries` map.

---

## 7. Season Shop (the only live-ops piece in scope)

Per your direction — **no skin system yet.**

- A **Season Shop** lets you exchange the **Season Tokens** you earn from play during a season for:
  - **bundles of C1 / C2 / C3**,
  - **bonus roll tokens** ("rerolls" = a free pull in a chosen pool — my interpretation; flag if you meant something else),
  - **TBD "something else"** (candidates, all non-cosmetic: a **respec voucher**, a **targeted hero pull** = pick which unlocked hero to ascend, or **exclusive cards/modules** seeded into the C1 pool).
- Season Tokens stay a **track meter**, not a 4th wallet you budget — the shop is just the faucet that converts them into the 3 real coins.
- ⚠️ **Balance flag:** a token→currency faucet with no cosmetic sink means the season can trivialize the grind. Cap weekly conversion / price it against expected season earn.

**Leagues (`Medals`) and Events (`EventTokens`) stay PARKED** (their own plan, untouched). When built, they follow the same pattern: their track *dispenses the 3 coins*, the League Shop *prices items in C2* — they do **not** become new spend-wallets. (Confirm you're OK leaving these parked.)

---

## 8. Campaign map

- **Delete all branching.** Flatten `config/campaign.json` to a single linear chain (one `requires` parent each, single lane). Remove the transpose/fork/reconvergence drawing in `CampaignScene.svelte` (`connectors` becomes a simple spine).
- **Shops + `CampaignGold` removed.** Shop nodes (`type:"shop"`) and the `openCampaignShop` flow go away; any owned `CampaignGold` converts to **Sparks (C1)** on migration. (The Season Shop is now the only shop.)
- Update campaign-passability tests (`CampaignShopTests.cs` and the all-levels suite) to the linear graph.

---

## 9. Migration (old 11 → new 3)

| Old | New | Note |
|---|---|---|
| `Points` | **delete** | mastery now C3; spells now rolled |
| `CardDust` + `ModuleCores` | → **Sparks (C1)** | gear layer; conversion rate TBD |
| `Crystals` + `Shards` + `HeroTokens` | → **Souls (C2)** | chase/meta → spells/heroes |
| `CampaignGold` | → **Sparks (C1)** | shops removed |
| `SeasonTokens` | **keep** | feeds Season Shop |
| `Medals`, `EventTokens` | **keep, parked** | leagues/events plan |

Conversion is a **one-time migration** on profile load (sum old → new at a chosen rate), then the old fields are dropped from `Profile`.

---

## 10. Proposed starting numbers (all tunable)

- Sources per battle: **C3** ~steady (every win); **C1** on level/daily clears; **C2** only on boss/rift/milestone.
- Roll prices: C1 roll cheap; C2 spell/hero roll moderate; mastery node = `base × nodeLevel` in C3.
- Slot unlocks (C2): escalating (e.g. slot 2 = X, slot 3 = 2X).
- Respec: flat C2.
- (Real values set during impl + a balance pass.)

---

## 11. Required doc/code changes (high level — full impl plan comes after approval)

**Docs:** amend **CLAUDE.md** (global-spell-pool + affinity is now intended, not drift) and **`docs/04` §3/§4.1/§5/§6.1**; mark `docs/2026-06-13-content-and-stats-design.md` §5.6/§5.10 superseded.

**Backend:** `Profile.cs` (new coins, drop old, migration), new `Wallet`/roll service for pure-random pulls + maxed-pool guard, `Upgrades.cs` (mastery→C3, respec→C2, delete `TryUpgradeSpell`/spell-Points path), `CardService`/`ModuleService` (dupe-levels; delete module substats/reroll), spell-affinity bonus in the spell cast/stat path, hero pool + boss-unlock hookup, `config/campaign.json` linear, Season Shop endpoint.

**Frontend:** roll UI (the 4 pools), drop the campaign fork drawing + shop nodes, drop the `Points`/Upgrades spell-leveling panel in `CampaignScene.svelte`, Season Shop screen.

**Tests:** roll determinism + dupe-levels + maxed-pool guard; affinity bonus; boss→hero-pool→unlock; migration; linear-map passability.

---

## 12. Open items for your review

1. **Slot-growth currency = C2** (my pick) — or C3?
2. **Separate roll buttons per category** (my default) — or one mixed roll per coin?
3. **Season "rerolls" = bonus roll tokens** (my read) — or did you mean something else?
4. **Season "something else"** — which of {respec voucher / targeted hero pull / exclusive pool items}, or none yet?
5. **Leagues + Events stay parked** — confirm.
6. **Currency names** — Sparks / Souls / Insight are placeholders.
