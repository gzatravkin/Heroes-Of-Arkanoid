const BASE = "http://localhost:5080";

// ── Editor types ─────────────────────────────────────────────────────────────

export interface BlockTypeDef {
  id: string;
  biome: string;
  sprite: string;
}

export interface LevelData {
  id: string;
  biome: string;
  cols: number;
  rows: number;
  rows_data: string[];
  legend: Record<string, string>;
}

export interface SaveLevelResult {
  ok: boolean;
  id: string;
}

// ── Shared types ─────────────────────────────────────────────────────────────

export interface Profile {
  level: number;
  exp: number;
  // Economy rework: the 3 spend currencies.
  sparks: number;
  souls: number;
  insight: number;
  completedLevels: string[];
  unlockedRelics: string[];
  spellLevels: Record<string, number>;
  achievements: string[];
  tutorialSeen: boolean;
  selectedCharacter?: string;
  unlockedSpellSlots?: number;
  heroPool?: string[];
  // Stat engine (§5): per-hero progression + account masteries.
  heroProgress?: Record<string, { level: number; exp: number; stars: number; ascendPips?: number }>;
  masteries?: Record<string, number>;
  // Legacy currencies — still serialized (= 0 after migration); kept so existing scenes compile.
  points: number;
  crystals: number;
  campaignGold: number;
  heroTokens?: Record<string, number>;
}

// ── Rolls (economy rework §2) ────────────────────────────────────────────────
export type RollKind = "card" | "module" | "spell" | "hero";
export interface RollResult {
  kind: number; id: string; name?: string; wasNew: boolean; level: number; stars: number; wasted: boolean; copies: number;
}
export interface RollResponse {
  ok: boolean; reason?: string; result?: RollResult;
  sparks: number; souls: number; insight: number;
}
interface RollPool { cost: number; coin: "sparks" | "souls"; canRoll: boolean; poolEmpty?: boolean; }
export interface RollState {
  sparks: number; souls: number; insight: number;
  card: RollPool; module: RollPool; spell: RollPool; hero: RollPool;
}
export interface SeasonShopOffer { id: string; label: string; cost: number; kind: string; amount: number; }
export interface SeasonShopData { tokens: number; offers: SeasonShopOffer[]; }

export interface CampaignNode {
  id: string;
  label: string;
  biome: string;
  unlocked: boolean;
  completed: boolean;
  stars: number;
}

export interface CampaignData {
  nodes: CampaignNode[];
}

/** Progressive feature unlocks (campaign-gated). */
export interface FeatureInfo { feature: string; name: string; unlocked: boolean; requiredLevel: string; requiredLabel: string; }
export interface FeaturesResponse { features: FeatureInfo[]; }

export interface RiftOffer {
  opened: boolean;
  dungeonId: string;
  name: string;
  floors: number;
}

export interface CompleteResult {
  reward: {
    expGained: number;
    sparksGained: number;
    soulsGained: number;
    insightGained: number;
    leveledUp: boolean;
    newLevel: number;
    firstClear: boolean;
    levelStars?: number;
    starBonusSouls?: number;
    characterUnlocked?: string | null;
    spellsUnlocked?: string[];
    slotsUnlocked?: number;
    featuresUnlocked?: string[];
  } | null;
  rift: RiftOffer | null;
  /** Hero XP accrued this battle (§5.3): the selected hero's level progress. */
  heroXp?: HeroXpResult;
}

/** Per-battle hero-XP accrual result (§5.3). */
export interface HeroXpResult {
  heroId: string;
  xpGained: number;
  newLevel: number;
  leveledUp: boolean;
  stars: number;
  tokensGranted: number;
}

/** Rift roll mode sent to /complete. "none" = never (default for direct callers). */
export type RiftMode = "roll" | "force" | "none";

export interface UpgradeResult {
  ok: boolean;
  profile: Profile;
}

/** Resolved §5 hero stat block + progression, for the Masteries/Heroes screen. */
export interface HeroStatsResult {
  hero: string;
  level: number;
  exp: number;
  xpToNext: number;
  stars: number;
  tokens: number;
  nextStarCost: number;
  stats: {
    power: number;
    vitality: number;
    critChance: number;
    critDamage: number;
    multiball: number;
    tempo: number;
  };
}

export interface DungeonDef {
  id: string;
  name: string;
  floors: string[];
  rewardRelic: string;
  rewardCrystals: number;
}

export interface DungeonsResult {
  dungeons: DungeonDef[];
}

export interface DungeonRunState {
  dungeonId?: string;
  floors?: string[];
  floorIndex?: number;
  relics?: string[];
  ballCores?: string[];
  paddleMods?: string[];
  draftedSpells?: string[];
  pendingChoices?: string[];
  active?: boolean;
  cleared?: boolean;
  rewardCrystals?: number;
  hp?: number;
  gold?: number;
}

/** One item for sale on a dungeon shop floor (docs/04 §6.2). */
export interface ShopItem {
  id: string;
  kind: "relic" | "core" | "paddleMod" | "spell" | "heal" | "spell_level" | "points";
  price: number;
}

export interface ShopItemsResult {
  items: ShopItem[];
  gold: number;
}

export interface ShopBuyResult {
  ok: boolean;
  gold: number;
  run?: DungeonRunState;
  error?: string;
}

/** A Card definition (the persistent passive layer, plan §A.1). */
export interface CardDef {
  id: string;
  name: string;
  rarity: string;
  icon: string;
  effect: string;
  magnitude: number;
  effectValue?: string;
  description: string;
}

export interface CardsResponse {
  cards: CardDef[];
  owned: Record<string, { level: number; copies: number }>;
  equipped: string[];
  slots: number;
  maxLevel: number;
}

/** Daily missions (plan §A.2). */
export interface DailyMission {
  id: string; name: string; metric: string; target: number; progress: number;
  claimed: boolean; complete: boolean; rewardGems: number; rewardCardDust: number;
}
export interface DailyResponse {
  missions: DailyMission[]; streak: number; streakTarget: number; dayEndsAt: string;
  gems: number; cardDust: number;
}
export interface DailyClaimResponse {
  ok: boolean; gems: number; cardDust: number; streakBonus: boolean; streak: number;
}

/** Prestige campaign loop (plan §B.1). */
export interface PrestigeResponse { tier: number; canAscend: boolean; score: number; rank: number; }

/** Season Festival (plan §C). */
export interface SeasonTierView { tier: number; tokens: number; rewardGems: number; rewardCardDust: number; rewardModuleCores: number; claimed: boolean; claimable: boolean; }
export interface SeasonEventView { id: string; name: string; effect: string; magnitude: number; milestoneTokens: number; rewardModuleCores: number; rewardGems: number; tokens: number; claimed: boolean; claimable: boolean; }
export interface SeasonResponse {
  seasonId: number; theme: string; tokens: number; seasonEndsAt: string; weekEndsAt: string;
  track: SeasonTierView[]; seasonRank: number; ev: SeasonEventView | null;
}

/** Modules (plan §B.2). */
export interface ModuleDef { id: string; name: string; slot: string; rarity: string; effect: string; magnitude?: number; effectValue?: string; description?: string; }
export interface ModulesResponse {
  modules: ModuleDef[]; owned: Record<string, number>; copies: Record<string, number>;
  equipped: Record<string, string>; maxLevel: number;
}

/** A league cohort row + the league view (plan §A.3). */
export interface CohortEntry { rank: number; playerId: string; displayName: string; score: number; isMe: boolean; isBot: boolean; }
export interface LeagueResponse {
  board: string; weekId: number; weekEndsAt: string;
  tier: number; tierName: string;
  myRank: number; myScore: number; cohortSize: number;
  promoteTop: number; demoteBottom: number;
  entries: CohortEntry[];
}

export interface FloorClearedResult {
  isLastFloor: boolean;
  run?: DungeonRunState & { pendingChoices?: string[] };
  profile?: Profile;
}

export interface RiftFinishResult {
  won: boolean;
  depth: number;
  totalFloors: number;
  soulsGained: number;
  heroXp?: HeroXpResult;
  profile?: Profile;
}

export interface SpellDef {
  id: string;
  name: string;
  icon: string;
  manaCost: number;
  desc?: string;
}

export interface RelicDef {
  id: string;
  name: string;
  description: string;
  icon: string;
}

export interface RelicsResponse {
  relics: RelicDef[];
}

export interface CharacterDef {
  id: string;
  name: string;
  passive: string;
  icon: string;
  spells: SpellDef[];
}

/** Per-hero ★ progress for the Heroes screen (manual ascension). */
export interface HeroProgressView {
  stars: number; pips: number; maxStars: number; ascendCost: number; canAscend: boolean;
}
export interface CharactersResponse {
  characters: CharacterDef[];
  progress?: Record<string, HeroProgressView>;
  neutralSpells?: SpellDef[];
  selected: string;
  unlocked: string[];
  shards?: number;
  unlockCost?: number;
}

/** One spell in the loadout pool for the selected character (docs/04 §3). */
export interface SpellPoolEntry {
  id: string;
  name: string;
  icon: string;
  manaCost: number;
  desc: string;
  level: number;
  copies: number;
  signature: boolean;
  owned: boolean;
  equipped: boolean;
}

export interface SpellsResponse {
  character: string;
  signature: string;
  unlockedSlots: number;
  loadout: string[];
  spells: SpellPoolEntry[];
}

export interface SpellEquipResult {
  ok: boolean;
  loadout: string[];
}

// ── Client ───────────────────────────────────────────────────────────────────

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`);
  return res.json() as Promise<T>;
}

async function post<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`, { method: "POST" });
  return res.json() as Promise<T>;
}

async function postJson<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  return res.json() as Promise<T>;
}

export const metaApi = {
  getProfile: ()                 => get<Profile>("/profile"),
  getCampaign: ()                => get<CampaignData>("/campaign"),
  getFeatures: ()                => get<FeaturesResponse>("/features"),
  complete: (level: string, treasureBonus = 0, riftMode: RiftMode = "none", blocks = 0) =>
    post<CompleteResult>(`/complete?level=${encodeURIComponent(level)}&treasureBonus=${treasureBonus}&rift=${riftMode}&blocks=${blocks}`),
  // Stat engine (§5.6/§5.4): spend a Skill Point on a Mastery node; spend Hero Tokens to ascend a hero's ★.
  mastery: (node: string)        => post<UpgradeResult>(`/mastery?node=${encodeURIComponent(node)}`),
  // Manual hero ascension (2026-06-15): spend banked duplicate pips to raise a hero's ★.
  ascendHero: (hero: string)     => post<UpgradeResult>(`/hero/ascend?hero=${encodeURIComponent(hero)}`),
  getHeroStats: (hero: string)   => get<HeroStatsResult>(`/hero/stats?hero=${encodeURIComponent(hero)}`),
  reset: ()                      => post<unknown>("/reset"),
  getDungeons: ()                => get<DungeonsResult>("/dungeons"),
  startDungeon: (id: string)     => post<unknown>(`/dungeon/start?id=${encodeURIComponent(id)}`),
  getDungeonState: ()            => get<DungeonRunState>("/dungeon/state"),
  // hp/gold = the cleared floor's remaining HP and accumulated Gold, carried into the next floor
  // (docs/04 §6.2 permadeath, §5 Gold).
  floorCleared: (hp = 0, gold = 0, blocks = 0) => {
    const q: string[] = [];
    if (hp > 0) q.push(`hp=${hp}`);
    if (gold > 0) q.push(`gold=${gold}`);
    if (blocks > 0) q.push(`blocks=${blocks}`);
    return post<FloorClearedResult>("/dungeon/floor-cleared" + (q.length ? "?" + q.join("&") : ""));
  },
  // Hero XP (§5.3) for battles that don't go through /complete (a loss).
  heroXp: (blocks = 0, won = false) =>
    post<{ heroXp: HeroXpResult }>(`/hero/xp?blocks=${blocks}&won=${won}`),
  pick: (choice: string)         => post<unknown>(`/dungeon/pick?choice=${encodeURIComponent(choice)}`),
  fail: ()                       => post<unknown>("/dungeon/fail"),
  // Continuous Rift (2026-06-16): one end-of-rift grant for all floors cleared (the rift is a single battle).
  riftFinish: (depth: number, won: boolean, blocks = 0) =>
    post<RiftFinishResult>(`/dungeon/rift-finish?depth=${depth}&won=${won ? 1 : 0}&blocks=${blocks}`),
  // Dungeon shop floor (docs/04 §6.2): browse + buy with in-run Gold.
  getShopItems: ()               => get<ShopItemsResult>("/dungeon/shop/items"),
  buyShopItem: (id: string)      => post<ShopBuyResult>(`/dungeon/shop/buy?item=${encodeURIComponent(id)}`),
  // Cards (plan §A.1): the persistent passive layer.
  getCards: ()                   => get<CardsResponse>("/cards"),
  equipCard: (id: string)        => post<{ ok: boolean; equipped: string[] }>(`/cards/equip?id=${encodeURIComponent(id)}`),
  unequipCard: (id: string)      => post<{ ok: boolean; equipped: string[] }>(`/cards/unequip?id=${encodeURIComponent(id)}`),
  cardLevelUp: (id: string)      => post<{ ok: boolean }>(`/cards/levelup?id=${encodeURIComponent(id)}`),
  grantCard: (id: string)        => post<{ ok: boolean }>(`/cards/grant?id=${encodeURIComponent(id)}`),
  // Leaderboard / league (plan §A.3) — SQLite-backed, provider-abstracted.
  getLeague: (board = "trial")   => get<LeagueResponse>(`/lb/league?board=${board}`),
  submitScore: (board: string, score: number) => post<{ ok: boolean; accepted: boolean }>(`/lb/submit?board=${board}&score=${score}`),
  // Daily missions (plan §A.2).
  getDaily: ()                   => get<DailyResponse>("/daily"),
  claimDaily: (id: string)       => post<DailyClaimResponse>(`/daily/claim?id=${encodeURIComponent(id)}`),
  // Prestige campaign loop (plan §B.1).
  getPrestige: ()                => get<PrestigeResponse>("/campaign/prestige"),
  ascend: ()                     => post<{ ok: boolean; tier: number }>("/campaign/ascend"),
  // Season Festival (plan §C).
  getSeason: ()                  => get<SeasonResponse>("/season"),
  claimSeasonTier: (tier: number)=> post<{ ok: boolean }>(`/season/claim-tier?tier=${tier}`),
  claimEvent: ()                 => post<{ ok: boolean }>("/event/claim"),
  // Modules (plan §B.2).
  getModules: ()                 => get<ModulesResponse>("/modules"),
  equipModule: (id: string)      => post<{ ok: boolean }>(`/modules/equip?id=${encodeURIComponent(id)}`),
  unequipModule: (slot: string)  => post<{ ok: boolean }>(`/modules/unequip?slot=${encodeURIComponent(slot)}`),
  moduleLevelUp: (id: string)    => post<{ ok: boolean }>(`/modules/levelup?id=${encodeURIComponent(id)}`),
  // Rolls (economy rework §2): fixed-price random pulls + the pool state for the UI.
  roll: (kind: RollKind)         => post<RollResponse>(`/roll/${kind}`),
  rollState: ()                  => get<RollState>("/roll/state"),
  devCoins: (sparks: number, souls: number, insight: number) =>
    post<{ ok: boolean }>(`/dev/coins?sparks=${sparks}&souls=${souls}&insight=${insight}`),
  // Season Shop (economy rework §7): exchange Season Tokens for coins / bonus rolls.
  getSeasonShop: ()              => get<SeasonShopData>("/season/shop"),
  buySeasonOffer: (offer: string)=> post<RollResponse & { tokens: number; roll?: RollResult }>(`/season/shop/buy?offer=${encodeURIComponent(offer)}`),
  // Mastery respec + spell-slot unlock (economy rework §3/§6).
  resetMasteries: ()             => post<{ ok: boolean; profile: Profile }>("/mastery/reset"),
  unlockSpellSlot: ()            => post<{ ok: boolean; profile: Profile }>("/spells/unlock-slot"),
  getCharacters: ()              => get<CharactersResponse>("/characters"),
  getRelics: ()                  => get<RelicsResponse>("/relics"),
  selectCharacter: (id: string)  => post<unknown>(`/character/select?id=${encodeURIComponent(id)}`),
  // Spell loadout (signature + drafted pool)
  getSpells: ()                  => get<SpellsResponse>("/spells"),
  equipSpell: (id: string)       => post<SpellEquipResult>(`/spell/equip?id=${encodeURIComponent(id)}`),
  unequipSpell: (id: string)     => post<SpellEquipResult>(`/spell/unequip?id=${encodeURIComponent(id)}`),
  spellLevelUp: (id: string)     => post<{ ok: boolean }>(`/spell/levelup?id=${encodeURIComponent(id)}`),
  // Editor
  getBlockTypes: ()                   => get<BlockTypeDef[]>("/editor/blocktypes"),
  loadLevel: (id: string)             => get<LevelData>(`/editor/load?id=${encodeURIComponent(id)}`),
  saveLevel: (body: LevelData)        => postJson<SaveLevelResult>("/editor/save", body),
  // Achievements & tutorial
  unlockAchievement: (id: string)     => post<{ ok: boolean; achievements: string[] }>(`/achievement/unlock?id=${encodeURIComponent(id)}`),
  markTutorialSeen: ()                => post<{ ok: boolean }>("/tutorial/seen"),
};
