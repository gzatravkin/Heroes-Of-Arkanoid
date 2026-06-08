import { Texture } from "pixi.js";

// Maps backend sprite keys -> source PNGs in the old art set.
// During M1 we load a small handful; full atlas packing is a later milestone.
const SRC: Record<string, string> = {
  // Hell biome blocks
  HellStandart:     "/art/HellStandart.png",
  HellStandart2:    "/art/HellStandart2.png",
  HellInvulnerable: "/art/HellInvulnerable.png",
  SkullRed:         "/art/SkullRed.png",
  // Cavern biome blocks
  DungeonStandart:     "/art/DungeonStandart.png",
  DungeonStandart2:    "/art/DungeonStandart2.png",
  DungeonInvulnerable: "/art/DungeonInvulnerable.png",
  // Village biome blocks
  VillageStandart:      "/art/VillageStandart.png",
  VillageStandart2:     "/art/VillageStandart2.png",
  VillageStandart2Ghost:"/art/VillageStandart2Ghost.png",
  // Heaven biome blocks
  StandartHaven:     "/art/StandartHaven.png",
  Standart2Haven:    "/art/Standart2Haven.png",
  InvulnerableHaven: "/art/InvulnerableHaven.png",
  // Game objects
  Ball:      "/art/Ball.png",
  Paddle:    "/art/Paddle.png",
  Explosion: "/art/Explosion.png",
  // Relic icons (referenced by Snapshot.activeRelics[].icon)
  ItemHummer: "/art/ItemHummer.png",
  ItemDrill:  "/art/ItemDrill.png",
  ItemTorch:  "/art/ItemTorch.png",
  ItemGem:    "/art/ItemGem.png",
};

const cache = new Map<string, Texture>();
export function tex(key: string): Texture {
  if (cache.has(key)) return cache.get(key)!;
  const t = SRC[key] ? Texture.from(SRC[key]) : Texture.WHITE;
  cache.set(key, t);
  return t;
}
