/**
 * textures.ts — Legacy key resolver.
 *
 * The old renderer used short keys like "HellStandart" pointing at /art/*.png.
 * This module now resolves those keys from the packed atlas via assets.ts,
 * keeping backward compatibility so existing renderer/HUD code keeps working.
 *
 * For new code, use assets.ts tex()/anim()/bg() directly with atlas keys like
 *   tex("hell/StandartHell")
 *   anim("firemage/spell_phonex/PhoenixDeathAnimPic")
 */

import { Texture } from "pixi.js";
import { tex as atlasTex } from "./assets";

// Maps legacy short keys → stable atlas frame keys produced by build-atlas.mjs.
// Keep this list in sync with any new keys the game backend emits.
const ALIAS: Record<string, string> = {
  // ── Hell biome blocks ────────────────────────────────────────────────────
  HellStandart:          "hell/StandartHell",
  HellStandart2:         "hell/StandartHell2",
  HellInvulnerable:      "hell/HellInvulnerable",
  SkullRed:              "hell/SkullRed",
  SkullBlue:             "hell/SkullBlue",
  SkullGreen:            "hell/SkullGreen",

  // ── Dungeon biome blocks ─────────────────────────────────────────────────
  DungeonStandart:       "dungeon/DungeonStandart",
  DungeonStandart2:      "dungeon/DungeonStandart2",
  DungeonInvulnerable:   "dungeon/DungeonInvulnerable",
  Stalactite:            "dungeon/Stalactite",

  // ── Village biome blocks ─────────────────────────────────────────────────
  VillageStandart:       "village/blocks/VillageStandart",
  VillageStandart2:      "village/blocks/VillageStandart2",
  VillageStandart2Ghost: "village/blocks/VillageStandart2Ghost",

  // ── Heaven biome blocks ──────────────────────────────────────────────────
  StandartHaven:         "heaven/StandartHaven",
  Standart2Haven:        "heaven/Standart2Haven",
  InvulnerableHaven:     "heaven/InvulnerableHaven",

  // ── Boss blocks ──────────────────────────────────────────────────────────
  DemonBody:             "hell/DemonBody",
  GoblinBody:            "dungeon/GoblinBody",
  WitchChest:            "village/enemies/WitchChest",

  // ── Enemy blocks (ported originals) ──────────────────────────────────────
  HellBallSpawner:       "hell/HellBallSpawner",
  Bomb:                  "dungeon/Bomb",
  Beholder1:             "village/enemies/Beholder1",
  VillageDeath:          "village/enemies/VillageDeath",
  HeavenMeleeStatue:     "heaven/HeavenMeleeStatue",
  WindMaster2:           "heaven/WindMaster2",

  // ── Game objects ─────────────────────────────────────────────────────────
  // "Ball" and "Paddle" were placeholder art not present in Sprites/ — the game
  // should be updated to use class-specific keys: firemage/ball/FireHeroBall,
  // paladin/ball/KnightHeroBall, firemage/bars/v2FireHero1, etc.
  Ball:    "firemage/ball/FireHeroBall",
  Paddle:  "firemage/bars/v2FireHero1",

  Explosion: "effects/Explosion",

  // ── Relic / item icons ───────────────────────────────────────────────────
  ItemHummer: "items/ItemHummer",
  ItemDrill:  "items/ItemDrill",
  ItemTorch:  "items/ItemTorch",
  ItemGem:    "items/ItemGem",
};

const cache = new Map<string, Texture>();

/**
 * Resolve a texture by key.
 * Accepts both legacy short keys (HellStandart) and full atlas keys
 * (hell/StandartHell).  Falls back to Texture.WHITE if not found.
 */
export function tex(key: string): Texture {
  if (cache.has(key)) return cache.get(key)!;

  // Try alias map first, then direct atlas lookup
  const atlasKey = ALIAS[key] ?? key;
  const t = atlasTex(atlasKey);
  cache.set(key, t);
  return t;
}
