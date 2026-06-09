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
  points: number;
  crystals: number;
  completedLevels: string[];
  unlockedRelics: string[];
  spellLevels: Record<string, number>;
  achievements: string[];
  tutorialSeen: boolean;
}

export interface CampaignNode {
  id: string;
  label: string;
  biome: string;
  x: number;
  y: number;
  unlocked: boolean;
  completed: boolean;
}

export interface CampaignData {
  nodes: CampaignNode[];
}

export interface RiftOffer {
  opened: boolean;
  dungeonId: string;
  name: string;
  floors: number;
}

export interface CompleteResult {
  reward: {
    expGained: number;
    pointsGained: number;
    crystalsGained: number;
    leveledUp: boolean;
    newLevel: number;
    firstClear: boolean;
  } | null;
  rift: RiftOffer | null;
}

/** Rift roll mode sent to /complete. "none" = never (default for direct callers). */
export type RiftMode = "roll" | "force" | "none";

export interface UpgradeResult {
  ok: boolean;
  profile: Profile;
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
  pendingChoices?: string[];
  active?: boolean;
  cleared?: boolean;
}

export interface FloorClearedResult {
  isLastFloor: boolean;
  run?: DungeonRunState & { pendingChoices?: string[] };
  profile?: Profile;
}

export interface SpellDef {
  id: string;
  name: string;
  icon: string;
}

export interface CharacterDef {
  id: string;
  name: string;
  passive: string;
  icon: string;
  spells: SpellDef[];
}

export interface CharactersResponse {
  characters: CharacterDef[];
  selected: string;
  unlocked: string[];
}

// ── Items ────────────────────────────────────────────────────────────────────

export interface ItemDef {
  id: string;
  name: string;
  icon: string;
  maxTier: number;
  cost: number[];
  effect: string;
  description: string;
  ownedTier: number;
  equipped: boolean;
}

export interface ItemsResponse {
  items: ItemDef[];
  crystals: number;
  equipped: string[];
}

export interface ItemBuyResult {
  ok: boolean;
  crystals: number;
  ownedTier: number;
}

export interface ItemEquipResult {
  ok: boolean;
  equipped: string[];
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
  complete: (level: string, treasureBonus = 0, riftMode: RiftMode = "none") =>
    post<CompleteResult>(`/complete?level=${encodeURIComponent(level)}&treasureBonus=${treasureBonus}&rift=${riftMode}`),
  upgrade: (spell: string)       => post<UpgradeResult>(`/upgrade?spell=${encodeURIComponent(spell)}`),
  reset: ()                      => post<unknown>("/reset"),
  getDungeons: ()                => get<DungeonsResult>("/dungeons"),
  startDungeon: (id: string)     => post<unknown>(`/dungeon/start?id=${encodeURIComponent(id)}`),
  getDungeonState: ()            => get<DungeonRunState>("/dungeon/state"),
  floorCleared: ()               => post<FloorClearedResult>("/dungeon/floor-cleared"),
  pick: (choice: string)         => post<unknown>(`/dungeon/pick?choice=${encodeURIComponent(choice)}`),
  fail: ()                       => post<unknown>("/dungeon/fail"),
  getCharacters: ()              => get<CharactersResponse>("/characters"),
  selectCharacter: (id: string)  => post<unknown>(`/character/select?id=${encodeURIComponent(id)}`),
  // Items
  getItems: ()                   => get<ItemsResponse>("/items"),
  buyItem: (id: string)          => post<ItemBuyResult>(`/item/buy?id=${encodeURIComponent(id)}`),
  equipItem: (id: string)        => post<ItemEquipResult>(`/item/equip?id=${encodeURIComponent(id)}`),
  unequipItem: (id: string)      => post<ItemEquipResult>(`/item/unequip?id=${encodeURIComponent(id)}`),
  // Editor
  getBlockTypes: ()                   => get<BlockTypeDef[]>("/editor/blocktypes"),
  loadLevel: (id: string)             => get<LevelData>(`/editor/load?id=${encodeURIComponent(id)}`),
  saveLevel: (body: LevelData)        => postJson<SaveLevelResult>("/editor/save", body),
  // Achievements & tutorial
  unlockAchievement: (id: string)     => post<{ ok: boolean; achievements: string[] }>(`/achievement/unlock?id=${encodeURIComponent(id)}`),
  markTutorialSeen: ()                => post<{ ok: boolean }>("/tutorial/seen"),
};
