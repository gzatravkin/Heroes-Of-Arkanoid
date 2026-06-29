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
  HellStandart2:         "hell/StandartHell2",   // cobblestone CORNER (sloped) — used as a corner cap
  HellDemon:             "hell/Standart2Hell",   // demon-rune BODY (the "tough" block)
  HellDemon2:            "hell/Standart2Hell2",  // demon-rune CORNER (sloped)
  HellInvulnerable:      "hell/HellInvulnerable",
  SkullRed:              "hell/SkullRed",
  SkullBlue:             "hell/SkullBlue",
  SkullGreen:            "hell/SkullGreen",
  LavaMainPart:          "hell/LavaMainPart",
  HeavenAltarV2:         "heaven/HeavenAltarV2",
  HeavenVaza:            "heaven/HeavenVaza",

  // ── Dungeon biome blocks ─────────────────────────────────────────────────
  DungeonStandart:       "dungeon/DungeonStandart",
  DungeonStandart2:      "dungeon/DungeonStandart2",
  DungeonCorner:         "dungeon/Dungeon2Standart2", // sloped rocky CORNER cap
  DungeonInvulnerable:   "dungeon/DungeonInvulnerable",
  Stalactite:            "dungeon/Stalactite",
  DungeonCart:           "dungeon/DungeonCart",

  // ── Village biome blocks ─────────────────────────────────────────────────
  VillageStandart:       "village/blocks/VillageStandart",
  VillageStandart2:      "village/blocks/VillageStandart2",
  VillageStandart3:      "village/blocks/VillageStandart3",  // alt body variant
  VillageCorner:         "village/blocks/Village2Standart",  // sloped CORNER cap
  VillageCorner2:        "village/blocks/Village2Standart2", // sloped CORNER cap (tough style)
  VillageStandart2Ghost: "village/blocks/VillageStandart2Ghost",
  BatSleeping:           "village/enemies/BatSleeping",
  GrateBomb:             "dungeon/GrateBomb",
  Kotelok1:              "village/blocks/Kotelok1",
  Kotelok2:              "village/blocks/Kotelok2",
  Kotelok3:              "village/blocks/Kotelok3",
  LavaSpowner:           "hell/LavaSpowner",

  // ── Heaven biome blocks ──────────────────────────────────────────────────
  StandartHaven:         "heaven/StandartHaven",
  Standart2Haven:        "heaven/Standart2Haven",
  HavenCorner:           "heaven/StandartHaven2",   // sloped rock CORNER cap
  HavenBrickCorner:      "heaven/Standart2Haven2",  // sloped brick CORNER cap (tough style)
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
  VillageDeathGhost:     "village/enemies/VillageDeathGhost", // ghost necromant (raises ghost corpses)
  Portal:                "village/blocks/VillagePortal",
  HeavenMeleeStatue:     "heaven/HeavenMeleeStatue",
  WindMaster2:           "heaven/WindMaster2",
  HeavenDefender:        "heaven/HeavenDefender",
  HeavenBoss:            "heaven/HeavenBoss",
  ColumnTop:             "heaven/ColumnTop",
  Column:                "heaven/Column",
  ColumnBottom:          "heaven/ColumnBottom",

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
