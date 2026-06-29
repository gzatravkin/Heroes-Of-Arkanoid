// Shared helpers for the collection screens (Items / Modules / Spells): real-icon mapping + the
// manual copy-threshold level cost (mirrors backend Leveling.CopiesForNextLevel: 2L+1 → 3,5,7,9,11,13…).

const ITEM = (name: string) => `/items/${name}.png`;

// Cards (now surfaced as "Items") → real item sprites (1:1, thematic).
const CARD_ICON: Record<string, string> = {
  headhunter: "ItemMark",        underdog: "ItemHelm",          opening_gambit: "ItemHourglass",
  cleanup_crew: "ItemDrill",     bank_shot: "ItemBalance",      executioners_edge: "ItemHummer",
  overkill: "ItemSun",           erosion: "ItemForceRing",      dead_center: "ItemGem",
  metronome: "ItemMotor",        phase_window: "ItemOrb",       avalanche: "ItemJadeBall",
  keystone: "ItemRing",          domino: "ItemTomOfKnowladge",  martyrs_brand: "ItemPhoenix",
  ricochet: "ItemzFourLeafClover", sleight_of_hand: "ItemMagicCrown", hot_hand: "ItemTorch",
  redline: "ItemFlask",          channeling: "ItemStaff",
};

// Modules → real item sprites (slot-thematic; reuse across the separate Modules screen is fine).
const MODULE_ICON: Record<string, string> = {
  tidal_core: "ItemOrb",     twin_soul_core: "ItemJadeBall", fission_core: "ItemSun",
  hollow_ball: "ItemGem",    brittle_glass: "ItemFlask",     spin_loaded: "ItemMotor",
  gyro_paddle: "ItemBalance", drumhead_paddle: "ItemHummer", riposte_paddle: "ItemForceRing",
  gravity_well: "ItemMagicCrown", toll_roads: "ItemMark",    pressure_cooker: "ItemHourglass",
};

export function cardIcon(id: string): string { return ITEM(CARD_ICON[id] ?? "ItemGem"); }
export function moduleIcon(id: string): string { return ITEM(MODULE_ICON[id] ?? "ItemOrb"); }

/** Copies needed to advance FROM `level` to the next (backend Leveling.CopiesForNextLevel). */
export function copiesForNextLevel(level: number): number { return 2 * level + 1; }

/** Equipped-first ordering for every collection grid. `rank` returns a sort key (lower = earlier);
 *  e.g. equip-slot index for equipped entries, a large constant for the rest. Stable copy. */
export function sortEquippedFirst<T>(items: T[], rank: (x: T) => number): T[] {
  return [...items].sort((a, b) => rank(a) - rank(b));
}

/** Rarity → accent colour, shared across collection screens. */
export const RARITY_COLOR: Record<string, string> = {
  common: "#a9b6c8", rare: "#6cc0ff", epic: "#5a9bff", legendary: "#ffce5a", mythic: "#ff9d5a",
};
export function rarityColor(r: string): string { return RARITY_COLOR[r] ?? "#9fb0c8"; }
