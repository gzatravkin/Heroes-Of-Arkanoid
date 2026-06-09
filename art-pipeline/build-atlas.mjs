/**
 * build-atlas.mjs
 * Walks Sprites/, packs all PNGs into PixiJS-v7-compatible spritesheet atlases
 * (≤2048×2048 each), writes atlas-N.png + atlas-N.json to frontend/public/atlas/,
 * and emits animations.json.
 *
 * Frame keys are stable paths derived from the relative Sprites/ path:
 *   Sprites/Locationes/Objects/Location_1_Hell/HellStandart.png
 *   → hell/HellStandart
 *
 * Animation frames group when a folder contains files matching:
 *   <BaseName><Number>.png   (e.g. PhoenixDeathAnimPic1..18)
 *   <BaseName>Animation<N>.png
 *   <BaseName>Anim<N>.png
 *   <BaseName><N>.png  (pure suffix number)
 *
 * Run: node art-pipeline/build-atlas.mjs  (from repo root)
 *   or: npm run build                     (from art-pipeline/)
 */

import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import sharp from "sharp";
import { MaxRectsPacker } from "maxrects-packer";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, "..");
const SPRITES_DIR = path.join(REPO_ROOT, "Sprites");
const OUT_DIR = path.join(REPO_ROOT, "frontend", "public", "atlas");
const MAX_ATLAS = 2048;
const PADDING = 2; // px gap between sprites

// ── 1. Collect all PNGs ────────────────────────────────────────────────────

function walkDir(dir, results = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walkDir(full, results);
    } else if (entry.isFile() && /\.(png|jpg|jpeg)$/i.test(entry.name)) {
      results.push(full);
    }
  }
  return results;
}

// ── 2. Build stable frame key from file path ───────────────────────────────

/**
 * Normalise a Sprites/ relative path into a stable dot-slash key.
 * Spaces replaced with _, uppercase left intact (keys are case-sensitive).
 *
 * Examples:
 *   Locationes/Objects/Location_1_Hell/HellStandart.png  → hell/HellStandart
 *   Heroes/Clase_1_FireMage/Skills/Spell_4_Phonex/PhoenixDeathAnimPic1.png
 *     → firemage/spell_phonex/PhoenixDeathAnimPic1
 */
function makeKey(absPath) {
  const rel = path.relative(SPRITES_DIR, absPath);
  const parts = rel.split(path.sep);
  const filename = parts.pop();
  const base = filename.replace(/\.(png|jpg|jpeg)$/i, "");

  const dirs = parts.map((p) => {
    p = p.replace(/\s+/g, "_");
    // Shorten common directory names into compact namespace segments
    const lower = p.toLowerCase();
    if (lower === "locationes") return null;
    if (lower === "objects") return null;
    if (lower === "location_1_hell") return "hell";
    if (lower === "location_2_dungeion" || lower === "location_2_dungeon") return "dungeon";
    if (lower === "location_3_village") return "village";
    if (lower === "location_4_heavens") return "heaven";
    if (lower === "fons") return "fons";
    if (lower === "heroes") return null;
    if (lower.startsWith("clase_1_")) return "firemage";
    if (lower.startsWith("clase_2_")) return "paladin";
    if (lower.startsWith("clase_3_")) return "engineer";
    if (lower.startsWith("clase_4_")) return "necromancer";
    if (lower === "skills") return null;
    if (lower === "bars") return "bars";
    if (lower === "ball") return "ball";
    if (lower === "interface") return "ui";
    if (lower === "comunskills") return "comunskills";
    if (lower.startsWith("icos_levelskill") || lower.startsWith("icos levelskill")) return "levelskill";
    if (lower === "hintsystem") return "hints";
    if (lower === "items") return "items";
    if (lower === "effects") return "effects";
    if (lower === "project_images" || lower === "project images") return "app";
    if (lower.startsWith("spell_")) {
      // e.g. Spell_4_Phonex → spell_phonex
      const m = lower.match(/spell_\d+_(.*)/);
      return m ? `spell_${m[1]}` : lower;
    }
    if (lower === "blocks") return "blocks";
    if (lower === "enemyes" || lower === "enemies") return "enemies";
    if (lower === "bonus") return "bonus";
    if (lower === "menu_main") return "menu_main";
    if (lower === "menu_skill") return "menu_skill";
    if (lower === "menu_inventory") return "menu_inventory";
    if (lower === "missionselect") return "missionselect";
    if (lower === "rewards") return "rewards";
    if (lower === "battle_inteface" || lower === "battle inteface") return "battle_ui";
    if (lower === "buttons") return "buttons";
    if (lower === "achivements" || lower === "achievements") return "achievements";
    if (lower === "unclassicated" || lower === "unclassified") return "unclassified";
    if (lower === "comun") return "comun";
    if (lower === "skill_size") return "skill_size";
    if (lower === "new_system" || lower === "new system") return "new_system";
    if (lower === "shkatulka") return "shkatulka";
    // Default: clean and lower
    return p.replace(/[^a-zA-Z0-9_]/g, "_").toLowerCase();
  }).filter(Boolean);

  // Deduplicate consecutive identical segments (e.g. ui/ui/ → ui/)
  const deduped = [];
  for (const seg of dirs) {
    if (seg !== deduped[deduped.length - 1]) deduped.push(seg);
  }

  return [...deduped, base].join("/");
}

// ── 3. Detect animation sequences ─────────────────────────────────────────

/**
 * Group frames in the same directory that share a base name and have
 * sequential numeric suffixes. Returns Map<animKey, {frames: string[], fps: number}>
 *
 * Patterns detected:
 *   BaseName + Number (e.g. Lighting1, Lighting2, Lighting3)
 *   BaseName + AnimPic + Number
 *   BaseName + Animation (single-word files that are sprite sheet rows - treated
 *     as static unless there are multiple numbered variants)
 */
function detectAnimations(framesByDir) {
  const animations = new Map(); // animKey → { frames: [frameKey...], fps }

  for (const [, frames] of framesByDir) {
    if (frames.length < 2) continue;

    // Group by base (strip trailing digits from filename part of key)
    const groups = new Map(); // baseKey → [{key, n}]
    for (const { key } of frames) {
      const lastSlash = key.lastIndexOf("/");
      const name = lastSlash >= 0 ? key.slice(lastSlash + 1) : key;
      const prefix = lastSlash >= 0 ? key.slice(0, lastSlash + 1) : "";

      // Match: BaseName + optional(Anim|Animation|AnimPic|DeathAnim|BirthAnim) + Number
      const m = name.match(/^(.*?)(\d+)$/);
      if (!m) continue;
      // Use lowercase baseKey so case-variant sequences (Shkatulka vs shkatulka)
      // merge into a single animation group.
      const baseName = m[1].toLowerCase();
      const n = parseInt(m[2], 10);
      const baseKey = prefix.toLowerCase() + baseName;

      if (!groups.has(baseKey)) groups.set(baseKey, []);
      groups.get(baseKey).push({ key, n });
    }

    for (const [baseKey, entries] of groups) {
      if (entries.length < 2) continue;
      entries.sort((a, b) => a.n - b.n);

      // Check they form a reasonably contiguous sequence (gaps ≤ 2 allowed)
      const nums = entries.map((e) => e.n);
      const min = nums[0], max = nums[nums.length - 1];
      const range = max - min + 1;
      if (range > entries.length * 2) continue; // too sparse — not an animation

      const fps = guessAnimFps(baseKey);
      animations.set(baseKey.replace(/\/$/, ""), {
        frames: entries.map((e) => e.key),
        fps,
      });
    }
  }
  return animations;
}

function guessAnimFps(baseKey) {
  const k = baseKey.toLowerCase();
  if (k.includes("death")) return 12;
  if (k.includes("birth")) return 12;
  if (k.includes("fly")) return 10;
  if (k.includes("stand")) return 8;
  if (k.includes("cast")) return 10;
  if (k.includes("attack")) return 12;
  if (k.includes("lighting") || k.includes("lighting")) return 15;
  if (k.includes("phonex") || k.includes("phoenix")) return 12;
  if (k.includes("skeleton")) return 10;
  if (k.includes("shkatulka")) return 8;
  return 10;
}

// ── 4. Load image dimensions ───────────────────────────────────────────────

async function loadImageInfo(absPath) {
  const meta = await sharp(absPath).metadata();
  return { w: meta.width, h: meta.height };
}

// ── 5. Pack sprites into atlases ──────────────────────────────────────────

async function packAtlases(allFiles) {
  console.log(`Loading metadata for ${allFiles.length} images…`);
  const infos = await Promise.all(
    allFiles.map(async (f) => {
      try {
        const { w, h } = await loadImageInfo(f);
        return { absPath: f, key: makeKey(f), w, h };
      } catch (e) {
        console.warn(`  SKIP (load error): ${path.basename(f)} — ${e.message}`);
        return null;
      }
    })
  );
  const valid = infos.filter(Boolean);
  console.log(`  Loaded: ${valid.length}  Skipped: ${infos.length - valid.length}`);

  // Sort largest-first for better packing
  valid.sort((a, b) => b.w * b.h - a.w * a.h);

  const packer = new MaxRectsPacker(MAX_ATLAS, MAX_ATLAS, PADDING, {
    smart: true,
    pot: true,   // power-of-two textures (better GPU perf)
    square: false,
    allowRotation: false,
    tag: false,
  });

  for (const info of valid) {
    packer.add(info.w, info.h, info);
  }

  console.log(`  Bins produced by packer: ${packer.bins.length}`);
  return { bins: packer.bins, valid };
}

// ── 6. Render each bin to PNG + JSON ──────────────────────────────────────

async function renderBin(bin, binIndex) {
  const atlasW = bin.width;
  const atlasH = bin.height;
  const outPng = path.join(OUT_DIR, `atlas-${binIndex}.png`);
  const outJson = path.join(OUT_DIR, `atlas-${binIndex}.json`);

  console.log(`  atlas-${binIndex}: ${atlasW}×${atlasH}  sprites=${bin.rects.length}`);

  // Composite all sprites onto a single canvas using sharp
  const compositeInputs = [];
  const frames = {};

  for (const rect of bin.rects) {
    const info = rect.data;
    compositeInputs.push({
      input: info.absPath,
      left: rect.x,
      top: rect.y,
    });

    frames[info.key] = {
      frame: { x: rect.x, y: rect.y, w: rect.width, h: rect.height },
      rotated: false,
      trimmed: false,
      spriteSourceSize: { x: 0, y: 0, w: rect.width, h: rect.height },
      sourceSize: { w: rect.width, h: rect.height },
    };
  }

  // Create transparent canvas and composite
  await sharp({
    create: {
      width: atlasW,
      height: atlasH,
      channels: 4,
      background: { r: 0, g: 0, b: 0, alpha: 0 },
    },
  })
    .composite(compositeInputs)
    .png({ compressionLevel: 9 })
    .toFile(outPng);

  // PixiJS v7 spritesheet JSON format
  const atlasJson = {
    meta: {
      app: "arkanoid-art-pipeline",
      version: "1.0",
      image: `atlas-${binIndex}.png`,
      format: "RGBA8888",
      size: { w: atlasW, h: atlasH },
      scale: "1",
    },
    frames,
    animations: {}, // populated in post-process step
  };

  fs.writeFileSync(outJson, JSON.stringify(atlasJson, null, 2));
  return { frames: Object.keys(frames), pngPath: outPng };
}

// ── 7. Post-process: add animation sequences to JSONs ─────────────────────

function injectAnimations(animations, binCount) {
  // Map frame key → atlas index
  const frameToAtlas = new Map();
  for (let i = 0; i < binCount; i++) {
    const json = JSON.parse(
      fs.readFileSync(path.join(OUT_DIR, `atlas-${i}.json`), "utf8")
    );
    for (const key of Object.keys(json.frames)) {
      frameToAtlas.set(key, i);
    }
  }

  // Load all atlas JSONs
  const atlasJsons = [];
  for (let i = 0; i < binCount; i++) {
    atlasJsons.push(
      JSON.parse(fs.readFileSync(path.join(OUT_DIR, `atlas-${i}.json`), "utf8"))
    );
  }

  // Inject into each atlas JSON
  for (const [animKey, { frames, fps }] of animations) {
    // Find which atlas the first frame belongs to (frames may split across bins
    // but in practice animations are small enough to fit in one)
    const firstAtlas = frameToAtlas.get(frames[0]);
    if (firstAtlas === undefined) continue;
    atlasJsons[firstAtlas].animations[animKey] = frames;
    // Store fps in meta.fps map
    if (!atlasJsons[firstAtlas].meta.fps) atlasJsons[firstAtlas].meta.fps = {};
    atlasJsons[firstAtlas].meta.fps[animKey] = fps;
  }

  for (let i = 0; i < binCount; i++) {
    fs.writeFileSync(
      path.join(OUT_DIR, `atlas-${i}.json`),
      JSON.stringify(atlasJsons[i], null, 2)
    );
  }
}

// ── 8. Emit animations.json ───────────────────────────────────────────────

function writeAnimationsJson(animations) {
  const out = {};
  for (const [key, { frames, fps }] of animations) {
    out[key] = { frames, fps };
  }
  fs.writeFileSync(
    path.join(OUT_DIR, "animations.json"),
    JSON.stringify(out, null, 2)
  );
  return out;
}

// ── 9. Main ────────────────────────────────────────────────────────────────

async function main() {
  console.log("=== Arkanoid atlas builder ===");
  console.log(`Source: ${SPRITES_DIR}`);
  console.log(`Output: ${OUT_DIR}`);

  // Clean previous output
  if (fs.existsSync(OUT_DIR)) {
    for (const f of fs.readdirSync(OUT_DIR)) {
      if (/^atlas-\d+\.(png|json)$/.test(f) || f === "animations.json") {
        fs.unlinkSync(path.join(OUT_DIR, f));
      }
    }
  } else {
    fs.mkdirSync(OUT_DIR, { recursive: true });
  }

  // Collect files
  const allFiles = walkDir(SPRITES_DIR);
  console.log(`\nFound ${allFiles.length} images`);

  // Build frame key list per directory (for animation detection)
  const framesByDir = new Map();
  for (const f of allFiles) {
    const dir = path.dirname(f);
    if (!framesByDir.has(dir)) framesByDir.set(dir, []);
    framesByDir.get(dir).push({ absPath: f, key: makeKey(f) });
  }

  // Detect animations before packing
  const animations = detectAnimations(framesByDir);
  console.log(`\nDetected ${animations.size} animation sequences`);
  for (const [key, { frames, fps }] of animations) {
    console.log(`  [anim] ${key}  frames=${frames.length}  fps=${fps}`);
  }

  // Pack
  console.log("\nPacking sprites…");
  const { bins } = await packAtlases(allFiles);

  // Render bins
  console.log(`\nRendering ${bins.length} atlas sheet(s)…`);
  const binResults = [];
  for (let i = 0; i < bins.length; i++) {
    binResults.push(await renderBin(bins[i], i));
  }

  // Inject animations
  injectAnimations(animations, bins.length);

  // Write animations.json
  const animOut = writeAnimationsJson(animations);

  // Write atlas-index.json (list of atlas JSON filenames in order)
  const indexEntries = Array.from({ length: bins.length }, (_, i) => `atlas-${i}.json`);
  fs.writeFileSync(
    path.join(OUT_DIR, "atlas-index.json"),
    JSON.stringify(indexEntries, null, 2)
  );

  // Report
  const totalPacked = binResults.reduce((s, b) => s + b.frames.length, 0);
  const pctPacked = ((totalPacked / allFiles.length) * 100).toFixed(1);

  console.log("\n=== Coverage Report ===");
  console.log(`Total source images : ${allFiles.length}`);
  console.log(`Packed into atlases : ${totalPacked}  (${pctPacked}%)`);
  console.log(`Atlas sheets        : ${bins.length}`);
  console.log(`Animations detected : ${animations.size}`);

  // List atlas file sizes
  for (let i = 0; i < bins.length; i++) {
    const pngPath = path.join(OUT_DIR, `atlas-${i}.png`);
    const size = fs.statSync(pngPath).size;
    const kb = (size / 1024).toFixed(1);
    console.log(`  atlas-${i}.png  ${bins[i].width}×${bins[i].height}  ${kb} KB`);
  }

  // Report key animations (all keys are lowercased after detection)
  const keyAnims = [
    "firemage/spell_phonex/phoenixdeathanimpic",
    "firemage/spell_phonex/phoenixbirthanimpic",
    "necromancer/spell_skeleton",
  ];
  console.log("\nKey animation checks:");
  for (const k of keyAnims) {
    const found = [...animations.entries()].find(([key]) => key.startsWith(k));
    if (found) {
      console.log(`  OK  ${found[0]}  (${found[1].frames.length} frames)`);
    } else {
      console.log(`  --  ${k}  (not found)`);
    }
  }

  console.log("\nDone.");
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
