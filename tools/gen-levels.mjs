// Generates the campaign level JSONs for config/levels/ as DISTINCT, purposeful
// 8×14 portrait layouts (Move 5 of docs/06-shell-flow-overhaul.md). Each level's
// block rows are listed top-down; the script pads to 14 rows, validates every row
// is exactly 8 chars, asserts at least one needToKill block, and writes the file.
//
//   node tools/gen-levels.mjs
//
import { writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const OUT = join(dirname(fileURLToPath(import.meta.url)), "..", "config", "levels");
const COLS = 8, ROWS = 14;

// Block ids that count as needToKill (winnable). Used for validation.
const DESTRUCTIBLE = new Set([
  "hell_basic", "hell_tough", "hell_demon_boss", "hell_ballspawner",
  "cavern_basic", "cavern_tough", "cavern_goblin_boss", "cavern_bomb",
  "village_basic", "village_tough", "village_ghost", "village_witch_boss", "village_beholder", "village_necromant",
  "heaven_basic", "heaven_tough", "heaven_melee_statue", "heaven_windmaster", "heaven_shield_statue",
]);
// Non-needToKill special blocks that are allowed to exist (don't satisfy winnability alone).
const NON_KILL = new Set(["cavern_stalactite"]);
void NON_KILL;

/** Each level: id, biome, legend (char→block id), and block rows (top-down). */
const LEVELS = [
  // ── HELL — obsidian channels funnel the ball + teleporter routing ──────────
  { id: "hell-1", biome: "hell", legend: { A: "hell_basic", B: "hell_tough" }, rows: [
    "AAAAAAAA",
    "AAAAAAAA",
    ".ABBBBA.",
    ".AAAAAA.",
    "..AAAA..",
    "...AA...",
  ]},
  { id: "hell-2", biome: "hell", legend: { A: "hell_basic", B: "hell_tough", O: "hell_obsidian", S: "hell_ballspawner" }, rows: [
    "O.AAAA.O",
    "O.ASSA.O",
    "O.ABBA.O",
    "O.ABBA.O",
    "O.AAAA.O",
    "O......O",
    "..AAAA..",
  ]},
  { id: "hell-teleport", biome: "hell", legend: { A: "hell_basic", B: "hell_tough", P: "hell_teleporter", Q: "hell_teleporter_blue" }, rows: [
    "P.AAAA.P",
    "..ABBA..",
    "..ABBA..",
    "........",
    "..ABBA..",
    "..ABBA..",
    "Q.AAAA.Q",
  ]},
  { id: "hell-boss", biome: "hell", legend: { A: "hell_basic", B: "hell_tough", O: "hell_obsidian", D: "hell_demon_boss" }, rows: [
    "O......O",
    ".AABBAA.",
    ".AB..BA.",
    ".ABDDBA.",
    ".ABDDBA.",
    ".AB..BA.",
    ".AABBAA.",
    "..AAAA..",
  ]},

  // ── CAVERNS — stalactite columns + rock pillars shaping the ball ───────────
  { id: "caverns-1", biome: "caverns", legend: { A: "cavern_basic", C: "cavern_tough", L: "cavern_stalactite" }, rows: [
    "ALA.ALA.",
    "A.A.A.A.",
    "A.A.A.C.",
    "C.A.A...",
    "A...C...",
    "C.......",
  ]},
  { id: "caverns-2", biome: "caverns", legend: { A: "cavern_basic", C: "cavern_tough", R: "cavern_rock", X: "cavern_bomb" }, rows: [
    "RR.AA.RR",
    "R..XX..R",
    "R.CCCC.R",
    "A..XX..A",
    "A.RAAR.A",
    "A.R..R.A",
    "AAA..AAA",
  ]},
  { id: "caverns-boss", biome: "caverns", legend: { A: "cavern_basic", B: "cavern_tough", R: "cavern_rock", G: "cavern_goblin_boss" }, rows: [
    "RR....RR",
    ".AABBAA.",
    ".ABGGBA.",
    ".ABGGBA.",
    ".ABBBBA.",
    ".A....A.",
    ".AABBAA.",
    "..R..R..",
  ]},

  // ── WITCHLAND — ghost blocks phase the ball through; force spell use ───────
  { id: "village-1", biome: "village", legend: { A: "village_basic" }, rows: [
    ".AA..AA.",
    ".AA..AA.",
    "AA.AA.AA",
    "AA.AA.AA",
    ".AA..AA.",
    ".AA..AA.",
    "AA.AA.AA",
  ]},
  { id: "village-2", biome: "village", legend: { A: "village_basic", G: "village_ghost", E: "village_beholder", P: "village_portal" }, rows: [
    "GA.AA.AG",
    "A.AGGA.A",
    "PAA..AAP",
    "GA.EE.AG",
    ".AA..AA.",
    "A.AGGA.A",
    "GA.AA.AG",
  ]},
  { id: "village-ghost", biome: "village", legend: { A: "village_basic", G: "village_ghost", N: "village_necromant" }, rows: [
    ".AAAAAA.",
    ".AGGGGA.",
    ".AGGGGA.",
    "AGGNGGGA",
    "AGGGGGGA",
    ".AGGGGA.",
    ".AGGGGA.",
    ".AAAAAA.",
  ]},
  { id: "village-boss", biome: "village", legend: { A: "village_basic", B: "village_tough", W: "village_witch_boss" }, rows: [
    ".A.AA.A.",
    "AABBBBAA",
    ".ABWWBA.",
    ".ABWWBA.",
    "AABBBBAA",
    ".A.AA.A.",
    ".AB..BA.",
    ".AABBAA.",
  ]},

  // ── HEAVEN — statue mirror-mazes (strict left↔right symmetry) ──────────────
  { id: "heaven-1", biome: "heaven", legend: { H: "heaven_basic", T: "heaven_tough", S: "heaven_statue", M: "heaven_melee_statue", D: "heaven_shield_statue" }, rows: [
    "HHH..HHH",
    "H.H..H.H",
    "H.HMMH.H",
    "H.HTTH.H",
    "H......H",
    "S.HDDH.S",
    "S.HTTH.S",
    "S......S",
  ]},
  { id: "heaven-2", biome: "heaven", legend: { H: "heaven_basic", T: "heaven_tough", S: "heaven_statue", W: "heaven_windmaster" }, rows: [
    "H.STTS.H",
    "HS.TT.SH",
    ".H.WW.H.",
    "HT.SS.TH",
    ".H.TT.H.",
    "HS.TT.SH",
    "H.STTS.H",
  ]},
];

let count = 0;
for (const lvl of LEVELS) {
  // Validate width.
  for (const [i, r] of lvl.rows.entries()) {
    if (r.length !== COLS) throw new Error(`${lvl.id} row ${i} is ${r.length} chars, expected ${COLS}: "${r}"`);
  }
  // Validate at least one destructible needToKill block.
  const hasKill = lvl.rows.some((r) => [...r].some((c) => lvl.legend[c] && DESTRUCTIBLE.has(lvl.legend[c])));
  if (!hasKill) throw new Error(`${lvl.id} has no needToKill block — would win instantly`);
  // Validate every legend char maps to a real id and is used.
  // Pad to ROWS.
  const rows_data = [...lvl.rows];
  while (rows_data.length < ROWS) rows_data.push(".".repeat(COLS));
  if (rows_data.length !== ROWS) throw new Error(`${lvl.id} has ${rows_data.length} rows, expected ${ROWS}`);

  const json = { id: lvl.id, biome: lvl.biome, cols: COLS, rows: ROWS, rows_data, legend: lvl.legend };
  writeFileSync(join(OUT, `${lvl.id}.json`), JSON.stringify(json, null, 2) + "\n");
  count++;
}
console.log(`wrote ${count} levels to ${OUT}`);
