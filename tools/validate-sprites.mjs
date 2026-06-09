// validate-sprites.mjs — audits that every sprite key the game can emit or the
// renderer references resolves to a real atlas frame (missing keys silently
// render as Texture.WHITE, i.e. invisible/white squares).
//
// Checks:
//   1. config/blocks.json `sprite` values        (legacy short keys → ALIAS map)
//   2. textures.ts ALIAS targets                 (atlas keys)
//   3. BlockLayer.ts BLOCK_DAMAGED targets       (atlas keys)
//   4. every atlas-key-shaped string literal in frontend/src/render/*.ts
//   5. animations.json sequences                 (frame keys)
//
// Usage: node tools/validate-sprites.mjs   (exit 1 if anything is missing)

import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const atlasDir = path.join(root, "frontend", "public", "atlas");

// ── 1. Load every frame key from the packed atlas ───────────────────────────
const frames = new Set();
for (const sheet of JSON.parse(fs.readFileSync(path.join(atlasDir, "atlas-index.json"), "utf8"))) {
  const data = JSON.parse(fs.readFileSync(path.join(atlasDir, sheet), "utf8"));
  for (const key of Object.keys(data.frames ?? {})) frames.add(key);
}

// animations.json maps animation name → { frames: [...] } or [...]
const animations = JSON.parse(fs.readFileSync(path.join(atlasDir, "animations.json"), "utf8"));

// ── 2. Parse the ALIAS map out of textures.ts ───────────────────────────────
const texturesSrc = fs.readFileSync(path.join(root, "frontend", "src", "render", "textures.ts"), "utf8");
const alias = new Map();
for (const m of texturesSrc.matchAll(/^\s*([A-Za-z0-9_]+):\s*"([^"]+)",?\s*$/gm)) {
  alias.set(m[1], m[2]);
}

const problems = [];
const ok = [];

function checkAtlasKey(key, source) {
  if (frames.has(key)) { ok.push(`${key}  (${source})`); return true; }
  problems.push(`MISSING atlas frame: "${key}"  ← ${source}`);
  return false;
}

function checkLegacyKey(key, source) {
  const target = alias.get(key);
  if (!target) {
    // tex() falls back to direct atlas lookup
    if (frames.has(key)) { ok.push(`${key}  (${source}, direct)`); return true; }
    problems.push(`UNRESOLVED legacy key: "${key}" (no ALIAS entry, no direct frame)  ← ${source}`);
    return false;
  }
  return checkAtlasKey(target, `${source} via ALIAS ${key}`);
}

// ── 3. blocks.json sprites ───────────────────────────────────────────────────
const blocks = JSON.parse(fs.readFileSync(path.join(root, "config", "blocks.json"), "utf8")).types;
for (const b of blocks) checkLegacyKey(b.sprite, `blocks.json ${b.id}`);

// ── 4. ALIAS targets themselves ──────────────────────────────────────────────
for (const [short, target] of alias) checkAtlasKey(target, `textures.ts ALIAS ${short}`);

// ── 5. Atlas-key-shaped literals in render sources ───────────────────────────
const renderDir = path.join(root, "frontend", "src", "render");
const keyShaped = /"((?:[a-z][a-z0-9_]*\/)+[A-Za-z0-9_]+)"/g;
for (const file of fs.readdirSync(renderDir)) {
  if (!file.endsWith(".ts")) continue;
  const src = fs.readFileSync(path.join(renderDir, file), "utf8")
    .replace(/\/\*[\s\S]*?\*\//g, "")   // block comments
    .replace(/^\s*\/\/.*$/gm, "")        // full-line comments
    .replace(/\s\/\/[^"\n]*$/gm, "");    // trailing comments (no quotes)
  for (const m of src.matchAll(keyShaped)) {
    const key = m[1];
    // skip obvious non-atlas paths (urls, art/ direct-png paths)
    if (key.startsWith("art/") || key.startsWith("http")) continue;
    if (!frames.has(key) && !(key in animations)) {
      // animation strips resolve "<key>" via animations.json or "<key>1..N" frames
      const hasStrip = frames.has(`${key}1`) || frames.has(`${key}_1`);
      if (!hasStrip) problems.push(`MISSING atlas frame: "${key}"  ← frontend/src/render/${file}`);
    }
  }
}

// ── 6. animations.json frame lists ───────────────────────────────────────────
for (const [name, def] of Object.entries(animations)) {
  const list = Array.isArray(def) ? def : def.frames ?? [];
  for (const f of list) {
    if (!frames.has(f)) problems.push(`MISSING animation frame: "${f}" in animations.json "${name}"`);
  }
}

console.log(`Atlas frames: ${frames.size}`);
console.log(`Checked OK: ${ok.length}`);
if (problems.length) {
  console.log(`\nPROBLEMS (${problems.length}):`);
  for (const p of [...new Set(problems)]) console.log("  " + p);
  process.exit(1);
} else {
  console.log("All sprite references resolve. ✔");
}
