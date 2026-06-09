/**
 * assets.ts — Atlas loader for the Arkanoid art pipeline.
 *
 * Usage:
 *   await loadAtlas();          // call once at startup, before first render
 *   tex("hell/StandartHell")   // returns Texture for a single frame
 *   anim("firemage/spell_phonex/phoenixdeathanimpic") // returns Texture[] — animation keys are all-lowercase
 *   bg("hell")                  // returns background Texture for biome
 *
 * Frame keys match the stable paths produced by build-atlas.mjs.
 */

import { Assets, Texture, BaseTexture, Spritesheet } from "pixi.js";

// ── State ───────────────────────────────────────────────────────────────────
let loaded = false;
const frameMap = new Map<string, Texture>();
const animMap  = new Map<string, Texture[]>();

// Atlas files are numbered atlas-0.json … atlas-N.json
// Discover them from the generated index written at build time.
const ATLAS_BASE = "/atlas";

// ── Animation manifest ──────────────────────────────────────────────────────
interface AnimDef { frames: string[]; fps: number }
let animManifest: Record<string, AnimDef> = {};

// ── Public API ───────────────────────────────────────────────────────────────

/**
 * Load all spritesheet atlases.  Safe to call multiple times — resolves
 * immediately on subsequent calls.
 */
export async function loadAtlas(): Promise<void> {
  if (loaded) return;

  // Load animation manifest first
  const animResp = await fetch(`${ATLAS_BASE}/animations.json`);
  animManifest = await animResp.json();

  // Discover atlas count via index
  const indexResp = await fetch(`${ATLAS_BASE}/atlas-index.json`);
  const index: string[] = await indexResp.json();

  // Load each atlas
  for (const filename of index) {
    const jsonUrl  = `${ATLAS_BASE}/${filename}`;
    const imageUrl = `${ATLAS_BASE}/${filename.replace(".json", ".png")}`;

    // In Pixi v7, Spritesheet constructor expects a BaseTexture.
    // Assets.load returns a Texture; we extract its baseTexture.
    const texture = await Assets.load<Texture>(imageUrl);
    const base: BaseTexture = texture instanceof Texture
      ? texture.baseTexture
      : (texture as unknown as BaseTexture);
    const data = await fetch(jsonUrl).then((r) => r.json());

    const sheet = new Spritesheet(base, data);
    await sheet.parse();

    // Register all frames
    for (const [key, tex] of Object.entries(sheet.textures)) {
      frameMap.set(key, tex as Texture);
    }
  }

  // Build animation texture arrays from the manifest
  for (const [animKey, def] of Object.entries(animManifest)) {
    const textures: Texture[] = [];
    for (const frameKey of def.frames) {
      const t = frameMap.get(frameKey);
      if (t) textures.push(t);
    }
    if (textures.length > 0) {
      animMap.set(animKey, textures);
    }
  }

  loaded = true;
}

/**
 * Return the Texture for a given frame key.
 * Returns Texture.WHITE if the key is not found (never throws).
 */
export function tex(key: string): Texture {
  return frameMap.get(key) ?? Texture.WHITE;
}

/**
 * Return the ordered Texture[] for an animation key.
 * Returns [] if the animation is not found.
 */
export function anim(key: string): Texture[] {
  return animMap.get(key) ?? [];
}

/**
 * Return the background Texture for a biome name.
 * Level biome strings: "hell" | "caverns" | "cavern" | "village" | "heaven"
 * (caverns/cavern both map to the dungeon/2Dungeon background)
 */
export function bg(biome: string): Texture {
  const bgMap: Record<string, string> = {
    hell:    "fons/1Hell",
    dungeon: "fons/2Dungeon",
    caverns: "fons/2Dungeon",
    cavern:  "fons/2Dungeon",
    village: "fons/3Village",
    heaven:  "fons/5Heaven",
  };
  const key = bgMap[biome];
  if (key) {
    const t = frameMap.get(key);
    if (t) return t;
  }
  return Texture.WHITE;
}

/**
 * Return the parallax hell background textures (HellFon1/2/3), sorted.
 * Returns [] when not loaded (non-hell biomes or before loadAtlas).
 */
export function hellParallaxFrames(): Texture[] {
  const keys = ["fons/HellFon1", "fons/HellFon2", "fons/HellFon3"];
  return keys.map(k => frameMap.get(k)).filter((t): t is Texture => !!t);
}

/**
 * Expose frame and animation maps for debugging.
 * window.__atlas.frames() lists all loaded frame keys.
 * window.__atlas.ready is true only after loadAtlas() fully completes.
 */
if (typeof window !== "undefined") {
  (window as unknown as Record<string, unknown>).__atlas = {
    get ready() { return loaded; },
    frames: () => [...frameMap.keys()],
    anims:  () => [...animMap.keys()],
    tex,
    anim,
  };
}
