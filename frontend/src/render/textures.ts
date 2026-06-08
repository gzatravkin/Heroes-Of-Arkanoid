import { Texture } from "pixi.js";

// Maps backend sprite keys -> source PNGs in the old art set.
// During M1 we load a small handful; full atlas packing is a later milestone.
const SRC: Record<string, string> = {
  HellStandart: "/art/HellStandart.png",
  HellStandart2: "/art/HellStandart2.png",
  Ball: "/art/Ball.png",
  Paddle: "/art/Paddle.png",
};

const cache = new Map<string, Texture>();
export function tex(key: string): Texture {
  if (cache.has(key)) return cache.get(key)!;
  const t = SRC[key] ? Texture.from(SRC[key]) : Texture.WHITE;
  cache.set(key, t);
  return t;
}
