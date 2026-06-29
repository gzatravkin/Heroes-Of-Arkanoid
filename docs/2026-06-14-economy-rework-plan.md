# Economy Rework Implementation Plan

> **For agentic workers:** Execute phase-by-phase. Each phase is independently buildable, testable, and committable. Steps use checkbox (`- [ ]`) syntax. Tests assert DESIGN (trigger + identity), not "a number moved" (CLAUDE.md cardinal rule).

**Goal:** Replace the 11-currency, deterministic-upgrade economy with **3 currencies + fixed-price pure-random rolls**, a **global spell pool + affinity**, a **boss-unlocked hero roll pool**, **mastery on C3 / respec on C2**, **no module substats**, a **linear campaign map**, and a **Season Shop** — migrating existing saves.

**Architecture:** Backend `Arkanoid.Core/Meta` holds pure currency/roll/upgrade logic over `Profile`; `Arkanoid.Server/Endpoints` exposes it; `GameInitializer` maps owned/equipped state into a run. Frontend Svelte scenes call `metaApi`. Spec: `docs/2026-06-14-economy-rework-proposal.md`.

**Tech Stack:** C#/.NET 8 + xUnit; Svelte 5 + Vite + PixiJS; Playwright. Commands run from `backend/`, `frontend/`, `tests/`.

---

## File Structure (decomposition)

**New (Core/Meta):**
- `RollService.cs` — pure-random pulls (card/module/spell/hero), dupe→level/★, maxed-pool guard. One responsibility: turn a coin spend into a collection change.
- `SpellAffinity.cs` — spell↔hero element match → cast bonus. Pure lookup.

**Modify (Core/Meta):** `Wallet.cs` (enum), `Profile.cs` (3 coins + migration + module/hero model), `Upgrades.cs` (mastery→Insight, respec, slot-unlock; delete spell-Points path), `CardService.cs` (dupe-levels), `ModuleService.cs` (gut substats/reroll → dupe-levels), `StatResolver.cs` (hero pips helper), `CharacterCatalog.cs` (affinity field), `Rewards.cs` (boss→hero-pool, coin sources).

**Modify (Server):** `Endpoints/RollEndpoints.cs` (new), `CardEndpoints.cs`, `ModuleEndpoints.cs`, `ProfileEndpoints.cs` (mastery/slot/respec/hero), `DungeonEndpoints.cs` (coin sources), `GameInitializer.cs` (module level map + affinity), `Endpoints/SeasonEndpoints.cs` (Season Shop), `Meta/ProfileStore.cs` (migrate on load).

**Modify (config):** `campaign.json` (linear), `characters.json` (affinity), `modules.json` (unchanged ids).

**Modify (frontend):** `net/metaApi.ts`, `scenes/CampaignScene.svelte` (linear map, drop shops + Points panel), a roll UI (retool `CardsScene.svelte` / `LoadoutScene.svelte` + new section), a Season Shop screen.

**Tests:** `RollServiceTests.cs`, `CurrencyMigrationTests.cs`, `SpellAffinityTests.cs`, `SpellSlotTests.cs`, `MasteryEconomyTests.cs`, `HeroPoolTests.cs`; update `CardTests.cs`, `ModuleTests.cs`, `CampaignShopTests.cs`, all-levels passability; Playwright `economy.spec.ts`.

---

## Phase 0 — Linear campaign map

**Files:** Modify `config/campaign.json`, `frontend/src/scenes/CampaignScene.svelte`; update `backend/Arkanoid.Tests/CampaignShopTests.cs`.

- [ ] **0.1 Write the failing test** — `backend/Arkanoid.Tests/CampaignTopologyTests.cs`:
```csharp
using System.Linq;
using System.Text.Json;
using Xunit;
public class CampaignTopologyTests
{
    [Fact]
    public void Campaign_IsLinear_NoForks_NoShops()
    {
        var json = System.IO.File.ReadAllText(TestPaths.Config("campaign.json"));
        using var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        // No shop nodes survive the rework.
        Assert.DoesNotContain(nodes, n => n.TryGetProperty("type", out var t) && t.GetString() == "shop");
        // Linear: every node requires at most one parent, and no two nodes share a parent (no forks).
        var parents = new List<string>();
        foreach (var n in nodes)
            if (n.TryGetProperty("requires", out var r) && r.GetArrayLength() > 0)
            {
                Assert.Equal(1, r.GetArrayLength());
                parents.Add(r[0].GetString()!);
            }
        Assert.Equal(parents.Count, parents.Distinct().Count()); // each node is required by ≤1 child
    }
}
```
(If `TestPaths.Config` doesn't exist, inline the repo-root path the other tests use — grep an existing test that reads `config/`.)

- [ ] **0.2 Run it — expect FAIL** (current map has forks + shop nodes).
Run: `dotnet test --filter CampaignTopologyTests` from `backend/`.

- [ ] **0.3 Rewrite `config/campaign.json` linear.** Keep all level nodes + bosses in biome order (hell→caverns→village→heaven); **drop every `type:"shop"` node**; set each node `x = index, y = 0`, `requires = [previous id]`. Boss nodes stay inline before the next biome's first node.

- [ ] **0.4 Run 0.1 — expect PASS.** Also run the all-levels passability suite to confirm no dropped level breaks the chain: `dotnet test --filter Passab` (adjust filter to the suite name).

- [ ] **0.5 Simplify `CampaignScene.svelte`.** Replace the transposed fork/reconvergence drawing: `connectors` becomes a single spine (each node → its one parent, a straight segment). Remove `isShop`/`shopNodeSrc`/`openCampaignShop`/`buildCampaignShopOverlay` usage and the shop branch in the node `onclick`. Remove the `camp-profile-gold` display block. Keep node art/glow.

- [ ] **0.6 Delete/retool `CampaignShopTests.cs`** (campaign shops no longer exist) — remove the file or convert its assertions to "no shop endpoint".

- [ ] **0.7 Verify frontend compiles.** Run: `npx vite build` from `frontend/`. Expected: success (no Svelte template error).

- [ ] **0.8 Commit.**
```
git add config/campaign.json frontend/src/scenes/CampaignScene.svelte backend/Arkanoid.Tests/CampaignTopologyTests.cs backend/Arkanoid.Tests/CampaignShopTests.cs
git commit -m "feat(campaign): flatten map to linear chain, remove fork/shop nodes"
```

---

## Phase 1 — 3-currency foundation + migration

**Files:** Modify `Wallet.cs`, `Profile.cs`, `Meta/ProfileStore.cs`; new `backend/Arkanoid.Tests/CurrencyMigrationTests.cs`.

**Locked enum** (`Wallet.cs`):
```csharp
public enum Currency { Sparks, Souls, Insight, Medals, EventTokens, SeasonTokens }
```
Rewrite `Get`/`Add` switch to: `Sparks→p.Sparks`, `Souls→p.Souls`, `Insight→p.Insight`, plus the unchanged Medals/EventTokens/SeasonTokens. `CanAfford`/`TrySpend` unchanged.

**Profile.cs:** add `Sparks`/`Souls`/`Insight` int props. **Keep** `Medals`/`EventTokens`/`SeasonTokens`. Demote the retired fields to **deserialize-only legacy** so migration can read them, then zero them:
```csharp
// New coins
[JsonPropertyName("sparks")]  public int Sparks  { get; set; }
[JsonPropertyName("souls")]   public int Souls   { get; set; }
[JsonPropertyName("insight")] public int Insight { get; set; }
[JsonPropertyName("currencyMigrated")] public bool CurrencyMigrated { get; set; }
// Legacy (read for one-time migration; no longer spent)
[JsonPropertyName("crystals")] public int Crystals { get; set; }
[JsonPropertyName("shards")] public int Shards { get; set; }
[JsonPropertyName("points")] public int Points { get; set; }
[JsonPropertyName("campaignGold")] public int CampaignGold { get; set; }
[JsonPropertyName("cardDust")] public int CardDust { get; set; }
[JsonPropertyName("moduleCores")] public int ModuleCores { get; set; }
// HeroTokens stays as a field for migration only (no longer earned/spent).
```
Add the migration method:
```csharp
/// <summary>One-time fold of the legacy 11-currency soup into the 3 coins (proposal §9). Idempotent.</summary>
public void MigrateCurrencies()
{
    if (CurrencyMigrated) return;
    Sparks  += CardDust + ModuleCores + CampaignGold;
    Souls   += Crystals + Shards + HeroTokens.Values.Sum();
    Insight += Points;
    CardDust = ModuleCores = CampaignGold = Crystals = Shards = Points = 0;
    HeroTokens.Clear();
    CurrencyMigrated = true;
}
```

- [ ] **1.1 Write `CurrencyMigrationTests.cs`:**
```csharp
[Fact]
public void Migrate_FoldsLegacyIntoThreeCoins_AndIsIdempotent()
{
    var p = new Profile { CardDust = 5, ModuleCores = 3, CampaignGold = 2,  // → Sparks 10
                          Crystals = 7, Shards = 4,                          // → Souls 11 (+tokens)
                          Points = 9 };                                      // → Insight 9
    p.HeroTokens["fire_mage"] = 6;                                           // → Souls +6 = 17
    p.MigrateCurrencies();
    Assert.Equal(10, p.Sparks); Assert.Equal(17, p.Souls); Assert.Equal(9, p.Insight);
    Assert.Equal(0, p.Points); Assert.Empty(p.HeroTokens); Assert.True(p.CurrencyMigrated);
    p.Sparks = 99; p.MigrateCurrencies(); // idempotent: no re-fold
    Assert.Equal(99, p.Sparks);
}
```
- [ ] **1.2 Run — expect FAIL** (`Sparks`/`Souls`/`Insight`/`MigrateCurrencies` don't exist).
- [ ] **1.3 Implement** the enum, Profile fields, `MigrateCurrencies`, and Wallet switch above. Call `p.MigrateCurrencies()` at the end of `ProfileStore.Load` (and save back if it mutated).
- [ ] **1.4 Fix the compile fan-out.** Every `Currency.Crystals/Shards/Points/CampaignGold/CardDust/ModuleCores` reference now won't compile. Re-point: reward/grant sites for gear → `Currency.Sparks`; rift/boss/meta → `Currency.Souls`; mastery → `Currency.Insight`. (grep `Currency\.` and `\.Crystals`/`\.Points` etc.) The detailed source wiring is Phase 6 — here just make it build by mapping each site to the right new coin.
- [ ] **1.5 Run full backend** `dotnet build && dotnet test` from `backend/`. Expected: green (some currency-specific assertions in old tests may need the coin rename — fix them).
- [ ] **1.6 Commit** `feat(economy): collapse 11 currencies to Sparks/Souls/Insight + save migration`.

---

## Phase 2 — Roll service (pure random, dupe→level/★, maxed guard)

**Files:** new `RollService.cs`, `RollServiceTests.cs`; modify `Profile.cs` (module/hero model), `CardService.cs`, `ModuleService.cs`, `StatResolver.cs`.

**Model changes (Profile.cs):**
- Modules become collected-like-cards: replace `List<ModuleInstance> OwnedModules` → `Dictionary<string,int> OwnedModules` (defId→level) and `Dictionary<string,string> EquippedModules` stays but now maps slot→**defId**.
- Hero ascension by dupes: add `int AscendPips` to `HeroProgress`; add `[JsonPropertyName("heroPool")] public List<string> HeroPool` (heroes that boss-clears have made rollable).

**RollService.cs (locked surface):**
```csharp
public enum RollKind { Card, Module, Spell, Hero }
public readonly record struct RollResult(RollKind Kind, string Id, bool WasNew, int Level, int Stars, bool Wasted);

public static class RollService
{
    // Pure random over the FULL pool (proposal §2: no dupe protection). Caller has already spent the coin.
    public static RollResult RollCard(Profile p, CardCatalog cat, Rng rng) { /* pick id; new→Grant L1; owned&<max→+lvl; else Wasted */ }
    public static RollResult RollModule(Profile p, ModuleCatalog cat, Rng rng) { /* same, OwnedModules dict */ }
    public static RollResult RollSpell(Profile p, CharacterCatalog cat, Rng rng) { /* pool = cat.Pool() (no signatures); SpellLevels */ }
    public static RollResult RollHero(Profile p, Rng rng) { /* pool = p.HeroPool; new→UnlockedCharacters+HeroProgress; owned→AscendPips++ then consume per StarTokenCost */ }

    // Maxed-pool terminal rule (proposal §2): false ⇒ disable the roll button (nothing can improve).
    public static bool CanRollCard(Profile p, CardCatalog cat);
    public static bool CanRollModule(Profile p, ModuleCatalog cat);
    public static bool CanRollSpell(Profile p, CharacterCatalog cat);
    public static bool CanRollHero(Profile p);
}
```
Hero dupe→★ consume loop: `while (hp.Stars < StatResolver.MaxStars && hp.AscendPips >= StatResolver.StarTokenCost(hp.Stars+1)) { hp.AscendPips -= StatResolver.StarTokenCost(hp.Stars+1); hp.Stars++; }`

- [ ] **2.1 Write `RollServiceTests.cs`** asserting DESIGN:
```csharp
// determinism: same seed ⇒ same id
[Fact] public void RollCard_IsDeterministicForSeed() { /* two RollCard with Rng(7) on fresh profile ⇒ equal Id */ }
// dupe levels, never wasted while improvable
[Fact] public void RollSpell_OwnedNotMaxed_RaisesLevel() { /* pre-own a spell at L1; force-seed to it; expect WasNew=false, Level=2 */ }
// pure-random can hit a maxed item ⇒ Wasted (proposal §2: no protection mid-pool)
[Fact] public void RollCard_Maxed_ReturnsWasted() { /* own one card at MaxCardLevel in a 1-card catalog; roll ⇒ Wasted=true, no change */ }
// signatures excluded from spell pool
[Fact] public void SpellPool_ExcludesSignatures() { Assert.DoesNotContain("ignite", CharacterCatalog.Default.Pool()); }
// hero: boss-pool dupe ascends via pips, not free ★ per roll
[Fact] public void RollHero_DuplicateAscends_ByPipThreshold() { /* HeroPool=[fire_mage], already owned ★0; roll until pips reach StarTokenCost(1)=10 ⇒ Stars becomes 1 */ }
// maxed-pool guard
[Fact] public void CanRollHero_FalseWhenAllOwnedAndMaxed() { /* HeroPool=[fire_mage] at ★6 ⇒ CanRollHero=false */ }
```
- [ ] **2.2 Run — expect FAIL.**
- [ ] **2.3 Implement** the Profile model changes, `HeroProgress.AscendPips`, `RollService`. Update `CardService.Grant` to no longer mint CardDust on dupe (dupes now level via RollService); keep `Equip`/`Unequip`/`LevelUp` (LevelUp may stay for the roll path or be inlined). 
- [ ] **2.4 Gut `ModuleService.cs`:** delete `Craft`, `Reroll`, `RollSubstats`, `RollMagnitude`, `SubstatCount`, `RarityMult`, and `ModuleInstance.Substats`. Keep `Equip`/`Unequip` rewritten against the `Dictionary<string,int> OwnedModules` + slot→defId model. Delete `ModuleInstance` if now unused (or shrink to nothing — check `ModuleEffects`/`GameInitializer` first).
- [ ] **2.5 Run — expect PASS.** `dotnet test --filter RollService` from `backend/`.
- [ ] **2.6 Commit** `feat(economy): pure-random roll service + dupe-levels; drop module substats`.

---

## Phase 3 — Spells: signature + flex slots + affinity

**Files:** modify `CharacterCatalog.cs` (affinity), new `SpellAffinity.cs`, modify `Upgrades.cs` (slot unlock), `GameInitializer.cs` (apply affinity); new `SpellAffinityTests.cs`, `SpellSlotTests.cs`.

**Loadout rule:** `Profile.UnlockedSpellSlots` = total hotbar = **1 signature + flex**; default **2**, max **4** (flex 1→3). Signature auto-owned with the hero, never in `Pool()`, always slot 0.

**Affinity:** add `[JsonPropertyName("affinity")] public string Affinity` to `SpellSlotDef` (values `fire|holy|tech|death|neutral`). Hero affinity map in `SpellAffinity.cs`:
```csharp
public static class SpellAffinity
{
    static readonly Dictionary<string,string> Hero = new()
        { ["fire_mage"]="fire", ["paladin"]="holy", ["engineer"]="tech", ["necromancer"]="death" };
    public const double MatchManaMult = 0.8; // matching affinity ⇒ −20% mana (min 0)
    public static bool Matches(string spellId, string heroId, CharacterCatalog cat)
    {
        var a = cat.DisplayOf(spellId)?.Affinity ?? "neutral";
        return a != "neutral" && Hero.TryGetValue(heroId, out var h) && h == a;
    }
}
```
Apply at run start in `GameInitializer`: for each equipped spell, if `SpellAffinity.Matches`, reduce its effective mana cost by `MatchManaMult` (set per-spell mana on the run's loadout — find where the loadout's mana costs are read into the sim and apply there).

**Slot unlock (`Upgrades.cs`):**
```csharp
public const int MaxSpellSlots = 4;
public static int SlotUnlockCost(int currentSlots) => currentSlots * 40; // Souls; escalates
public static bool TryUnlockSpellSlot(Profile p)
{
    if (p.UnlockedSpellSlots >= MaxSpellSlots) return false;
    if (!Wallet.TrySpend(p, Currency.Souls, SlotUnlockCost(p.UnlockedSpellSlots))) return false;
    p.UnlockedSpellSlots++; return true;
}
```
Delete `Upgrades.TryUpgradeSpell` (spells level via rolls now).

- [ ] **3.1 Write `SpellAffinityTests.cs`:** matching hero (`Matches("ignite","fire_mage")==true`), non-matching (`Matches("ignite","paladin")==false`), neutral spell never matches.
- [ ] **3.2 Write `SpellSlotTests.cs`:** `TryUnlockSpellSlot` spends Souls + raises slots; fails at `MaxSpellSlots`; fails when short on Souls. A loadout-validation test: a hero's signature is always slot 0 and the global flex slots accept any non-signature owned spell up to `UnlockedSpellSlots-1`.
- [ ] **3.3 Run — expect FAIL. 3.4 Implement.** Add `affinity` values to every spell in `config/characters.json` (fire_mage spells=fire, paladin=holy, engineer=tech, necromancer=death; recall/slowtime=neutral). 3.5 Apply affinity in `GameInitializer`. 3.6 Run — expect PASS. `npx vite build` after any svelte loadout change.
- [ ] **3.7 Commit** `feat(spells): global pool + signature lock + flex slots (Souls) + affinity bonus`.

---

## Phase 4 — Heroes: boss-unlock → roll pool

**Files:** modify `Rewards.cs` (`CharacterUnlocks` → add to `HeroPool` instead of `UnlockedCharacters`), `HeroPoolTests.cs`.

- [ ] **4.1 Write `HeroPoolTests.cs`:** clearing a biome boss **adds the next hero to `HeroPool`** but **does NOT** add to `UnlockedCharacters` (locked until rolled); a subsequent `RollService.RollHero` with that pool unlocks it.
- [ ] **4.2 Run — FAIL. 4.3 Implement:** in `Rewards` (the boss-clear path), `if (!p.HeroPool.Contains(hero) && !p.UnlockedCharacters.Contains(hero)) p.HeroPool.Add(hero);`. Remove the direct `UnlockedCharacters.Add` for boss rewards.
- [ ] **4.4 Run — PASS. 4.5 Commit** `feat(heroes): boss clears seed the hero roll pool (unlock via roll)`.

---

## Phase 5 — Mastery: level on Insight, respec on Souls

**Files:** modify `Upgrades.cs`; `MasteryEconomyTests.cs`.

```csharp
public static bool TryUpgradeMastery(Profile p, string node) {
    if (!StatResolver.MasteryMaxLevels.TryGetValue(node, out var max)) return false;
    int cur = p.Masteries.TryGetValue(node, out var l) ? l : 0;
    if (cur >= max || !Wallet.TrySpend(p, Currency.Insight, MasteryCost(cur))) return false;
    p.Masteries[node] = cur + 1; return true;
}
public static int MasteryCost(int curLevel) => 25 * (curLevel + 1); // Insight, scales
public const int RespecCost = 60; // Souls
public static bool ResetMasteries(Profile p) {
    int refund = p.Masteries.Sum(kv => Enumerable.Range(0, kv.Value).Sum(i => MasteryCost(i)));
    if (!Wallet.TrySpend(p, Currency.Souls, RespecCost)) return false;
    p.Masteries.Clear(); Wallet.Add(p, Currency.Insight, refund); return true;
}
```
- [ ] **5.1 Tests:** level spends Insight (fails when short); `ResetMasteries` costs Souls, clears nodes, refunds the exact Insight spent; reset fails (no-op) when Souls short. **5.2 FAIL → 5.3 implement → 5.4 PASS. 5.5 Commit** `feat(mastery): Insight to level, Souls to respec (with Insight refund)`.

---

## Phase 6 — Endpoints + coin sources + Season Shop

**Files:** new `RollEndpoints.cs`, `SeasonEndpoints.cs`; modify `CardEndpoints.cs`, `ModuleEndpoints.cs`, `ProfileEndpoints.cs`, `DungeonEndpoints.cs`, `Program.cs` (register), `GameInitializer.cs`.

- [ ] **6.1 `RollEndpoints.cs`** — `POST /roll/{card|module|spell|hero}`: check `RollService.CanRoll*`; spend the coin (`Sparks` for card/module, `Souls` for spell/hero) via `Wallet.TrySpend`; call the matching `RollService.Roll*`; save; return `{ ok, result, balance }`. `GET /roll/state` returns each pool's `canRoll` + prices for the UI.
- [ ] **6.2 Update `CardEndpoints`/`ModuleEndpoints`:** drop `/levelup`,`/reroll`,`/craft`,`grant`-dust; `/modules` no longer returns substats; keep `/equip`,`/unequip`,`GET`. Cheat grant becomes "add coins".
- [ ] **6.3 `ProfileEndpoints`:** mastery upgrade → `TryUpgradeMastery` (Insight); add `/mastery/reset` (`ResetMasteries`); add `/spells/unlock-slot` (`TryUnlockSpellSlot`); remove the spell-Points upgrade route. Hero ascension route removed (dupes auto-ascend in `RollHero`).
- [ ] **6.4 Coin sources** (`DungeonEndpoints` + campaign complete path): every floor/level win → **Insight** (steady) + **Sparks** (clear bonus); biome **boss** clear → **Souls** + seed `HeroPool`; rift depth jackpot → **Souls** (replace the old Crystals/tokens payout in `RiftModifierService.Depth*` callers).
- [ ] **6.5 `SeasonEndpoints.cs`** — `GET /season/shop` lists offers; `POST /season/buy?offer=` spends `SeasonTokens` for one of: `{sparks_bundle, souls_bundle, insight_bundle, bonus_roll}` (a `bonus_roll` grants a free `RollService` pull in a chosen pool). No skins. Weekly cap field on `SeasonState`.
- [ ] **6.6 Integration tests** (`backend/Arkanoid.Tests/EconomyEndpointTests.cs` or Playwright): a `/roll/card` with funds mutates `OwnedCards` + debits Sparks; with all-maxed pool returns `ok:false`; `/season/buy` debits SeasonTokens.
- [ ] **6.7 Build server** (`dotnet build`), run suite. **6.8 Commit** `feat(economy): roll/season endpoints + coin sources; drop dust/cores/reroll routes`.

---

## Phase 7 — Frontend roll UI + map cleanup + Season Shop

**Files:** `net/metaApi.ts`, `scenes/CampaignScene.svelte`, `scenes/CardsScene.svelte` / `scenes/LoadoutScene.svelte`, new Season Shop screen.

- [ ] **7.1 `metaApi.ts`:** add `roll(kind)`, `rollState()`, `unlockSpellSlot()`, `resetMasteries()`, `seasonShop()`, `seasonBuy(offer)`; drop `levelUpCard`/`reroll`/`craft`/spell-upgrade calls; replace `crystals/points/...` profile fields with `sparks/souls/insight`.
- [ ] **7.2 CampaignScene:** remove the Points-driven "Spell Upgrades" panel (`upgrade()`/`SPELLS` block); show the 3 coins in the profile bar; keep the linear map from Phase 0.
- [ ] **7.3 Roll UI:** a screen with four "Roll" buttons (Card/Module = Sparks, Spell/Hero = Souls), each showing price + disabled when `rollState().canRoll==false`, and a result reveal (new vs +level vs ★ vs wasted). Reuse `CardsScene`/`LoadoutScene` layout.
- [ ] **7.4 Season Shop screen:** list offers, buy with SeasonTokens.
- [ ] **7.5 `npx vite build`** — must succeed (Svelte template check). **7.6 Playwright** `tests/economy.spec.ts`: drive a roll, screenshot the reveal + the linear map + the 3-coin bar; assert no console errors.
- [ ] **7.7 Critically review screenshots** against the proposal (visual-quality-bar memory) before calling done. **7.8 Commit** `feat(economy/ui): roll screen, 3-coin bar, season shop; drop upgrade panel`.

---

## Phase 8 — Docs reconcile

- [ ] **8.1** Amend **CLAUDE.md**: record that spells now use a **global pool + affinity + meta random-unlock** (intended, supersedes the signature+drafted-in-run model) so it isn't later flagged as drift.
- [ ] **8.2** Update `docs/04` §3/§4.1/§5/§6.1 and add a "SUPERSEDED by 2026-06-14 economy rework" banner to `docs/2026-06-13-content-and-stats-design.md` §5.6/§5.10.
- [ ] **8.3** Move resolved items out of `docs/round-2-recommendations.md` (module substats, HeroTokens). **8.4 Commit** `docs: reconcile design docs with economy rework`.

---

## Verification gate (whole feature)
- `dotnet test` from `backend/` — all green.
- `npx vite build` from `frontend/` — success.
- `npx playwright test economy` from `tests/` — roll reveal + linear map + season shop screenshots reviewed against the proposal.
- A migrated legacy profile loads with correct Sparks/Souls/Insight and zeroed legacy fields.

## Open defaults baked in (from proposal §12; change here if needed)
Slot unlock = **Souls**; **separate** roll buttons per pool; "rerolls" = **bonus roll tokens**; season "something else" = deferred; leagues/events parked; coin names **Sparks/Souls/Insight** (placeholders).
