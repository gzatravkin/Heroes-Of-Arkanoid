import { metaBridge } from "../wasm/bridge";

/**
 * WasmApi — drop-in replacement for metaApi backed by synchronous WASM calls
 * wrapped in Promise.resolve() so callers see the same async interface.
 *
 * Import alias so scene files can swap with one line:
 *   import { wasmApi as metaApi } from "../net/WasmApi";
 */

function pid(): string {
  return (typeof localStorage !== "undefined" && localStorage.getItem("ark_pid")) || "default";
}

function call<T>(method: string, ...args: (string | number | boolean)[]): T {
  const json: string = metaBridge()[method](...args);
  return JSON.parse(json) as T;
}

export const wasmApi = {
  getProfile:       ()                                                             => Promise.resolve(call<any>("GetProfile", pid())),
  getCampaign:      ()                                                             => Promise.resolve(call<any>("GetCampaign", pid())),
  getFeatures:      ()                                                             => Promise.resolve(call<any>("GetFeatures", pid())),
  complete:         (level: string, treasureBonus = 0, riftMode = "none", blocks = 0) =>
                      Promise.resolve(call<any>("Complete", pid(), level, treasureBonus, riftMode, blocks)),
  mastery:          (node: string)                                                 => Promise.resolve(call<any>("Mastery", pid(), node)),
  ascendHero:       (hero: string)                                                 => Promise.resolve(call<any>("AscendHero", pid(), hero)),
  getHeroStats:     (hero: string)                                                 => Promise.resolve(call<any>("GetHeroStats", pid(), hero)),
  reset:            ()                                                             => Promise.resolve(call<any>("ResetProfile", pid())),
  getDungeons:      ()                                                             => Promise.resolve(call<any>("GetDungeons")),
  startDungeon:     (id: string)                                                   => Promise.resolve(call<any>("StartDungeon", pid(), id)),
  getDungeonState:  ()                                                             => Promise.resolve(call<any>("GetDungeonState", pid())),
  floorCleared:     (hp = 0, gold = 0, blocks = 0)                                => Promise.resolve(call<any>("FloorCleared", pid(), hp, gold, blocks)),
  heroXp:           (blocks = 0, won = false)                                      => Promise.resolve(call<any>("HeroXp", pid(), blocks, won)),
  pick:             (choice: string)                                               => Promise.resolve(call<any>("DungeonPick", pid(), choice)),
  fail:             ()                                                             => Promise.resolve(call<any>("DungeonFail", pid())),
  riftFinish:       (depth: number, won: boolean, blocks = 0)                      => Promise.resolve(call<any>("RiftFinish", pid(), depth, won, blocks)),
  getShopItems:     ()                                                             => Promise.resolve(call<any>("GetShopItems", pid())),
  buyShopItem:      (id: string)                                                   => Promise.resolve(call<any>("BuyShopItem", pid(), id)),
  getCards:         ()                                                             => Promise.resolve(call<any>("GetCards", pid())),
  equipCard:        (id: string)                                                   => Promise.resolve(call<any>("EquipCard", pid(), id)),
  unequipCard:      (id: string)                                                   => Promise.resolve(call<any>("UnequipCard", pid(), id)),
  cardLevelUp:      (id: string)                                                   => Promise.resolve(call<any>("CardLevelUp", pid(), id)),
  grantCard:        (id: string)                                                   => Promise.resolve(call<any>("GrantCard", pid(), id)),
  getLeague:        (_board = "trial")                                             => Promise.resolve(call<any>("GetLeague")),
  submitScore:      (board: string, score: number)                                 => Promise.resolve(call<any>("SubmitScore", board, score)),
  getDaily:         ()                                                             => Promise.resolve(call<any>("GetDaily", pid())),
  claimDaily:       (id: string)                                                   => Promise.resolve(call<any>("ClaimDaily", pid(), id)),
  getPrestige:      ()                                                             => Promise.resolve(call<any>("GetPrestige", pid())),
  ascend:           ()                                                             => Promise.resolve(call<any>("Ascend", pid())),
  getSeason:        ()                                                             => Promise.resolve(call<any>("GetSeason", pid())),
  claimSeasonTier:  (tier: number)                                                 => Promise.resolve(call<any>("ClaimSeasonTier", pid(), tier)),
  claimEvent:       ()                                                             => Promise.resolve(call<any>("ClaimEvent", pid())),
  getModules:       ()                                                             => Promise.resolve(call<any>("GetModules", pid())),
  equipModule:      (id: string)                                                   => Promise.resolve(call<any>("EquipModule", pid(), id)),
  unequipModule:    (slot: string)                                                 => Promise.resolve(call<any>("UnequipModule", pid(), slot)),
  moduleLevelUp:    (id: string)                                                   => Promise.resolve(call<any>("ModuleLevelUp", pid(), id)),
  roll:             (kind: string)                                                 => Promise.resolve(call<any>("Roll", pid(), kind)),
  rollState:        ()                                                             => Promise.resolve(call<any>("RollState", pid())),
  devCoins:         (sparks: number, souls: number, insight: number)               => Promise.resolve(call<any>("DevCoins", pid(), sparks, souls, insight)),
  // Season shop — stub (no server equivalent in WASM)
  getSeasonShop:    ()                                                             => Promise.resolve({ tokens: 0, offers: [] } as any),
  buySeasonOffer:   (_offer: string)                                               => Promise.resolve({ ok: false } as any),
  resetMasteries:   ()                                                             => Promise.resolve(call<any>("ResetMasteries", pid())),
  unlockSpellSlot:  ()                                                             => Promise.resolve(call<any>("UnlockSpellSlot", pid())),
  getCharacters:    ()                                                             => Promise.resolve(call<any>("GetCharacters", pid())),
  getRelics:        ()                                                             => Promise.resolve(call<any>("GetRelics")),
  selectCharacter:  (id: string)                                                   => Promise.resolve(call<any>("SelectCharacter", pid(), id)),
  getSpells:        ()                                                             => Promise.resolve(call<any>("GetSpells", pid())),
  equipSpell:       (id: string)                                                   => Promise.resolve(call<any>("EquipSpell", pid(), id)),
  unequipSpell:     (id: string)                                                   => Promise.resolve(call<any>("UnequipSpell", pid(), id)),
  spellLevelUp:     (id: string)                                                   => Promise.resolve(call<any>("SpellLevelUp", pid(), id)),
  // Editor — not supported offline
  getBlockTypes:    ()                                                             => Promise.resolve([] as any),
  loadLevel:        (_id: string)                                                  => Promise.resolve({} as any),
  saveLevel:        (_body: unknown)                                               => Promise.resolve({ ok: false, id: "" } as any),
  unlockAchievement:(id: string)                                                   => Promise.resolve(call<any>("UnlockAchievement", pid(), id)),
  markTutorialSeen: ()                                                             => Promise.resolve(call<any>("MarkTutorialSeen", pid())),
};
