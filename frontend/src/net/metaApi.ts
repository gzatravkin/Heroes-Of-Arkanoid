const BASE = "http://localhost:5080";

// ── Shared types ─────────────────────────────────────────────────────────────

export interface Profile {
  level: number;
  exp: number;
  points: number;
  crystals: number;
  completedLevels: string[];
  unlockedRelics: string[];
  spellLevels: Record<string, number>;
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

export interface CompleteResult {
  reward: {
    expGained: number;
    pointsGained: number;
    crystalsGained: number;
    leveledUp: boolean;
    newLevel: number;
    firstClear: boolean;
  } | null;
}

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

export interface CharacterDef {
  id: string;
  name: string;
  passive: string;
  icon: string;
}

export interface CharactersResponse {
  characters: CharacterDef[];
  selected: string;
  unlocked: string[];
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

export const metaApi = {
  getProfile: ()                 => get<Profile>("/profile"),
  getCampaign: ()                => get<CampaignData>("/campaign"),
  complete: (level: string)      => post<CompleteResult>(`/complete?level=${encodeURIComponent(level)}`),
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
};
