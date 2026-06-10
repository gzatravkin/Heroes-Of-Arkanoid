// Generates the campaign level JSONs for config/levels/ against the BIOME IDENTITY
// MATRIX (docs/12): each biome owns two layout idioms, one pacing mode and one
// objective flavor, and every level is linted against the five consistency rules:
//   1. Marker      — each non-boss level shows ≥1 of its biome's two idioms.
//   2. Exclusivity — pacing/objective fields never cross biomes.
//   3. Block set   — a level only uses its biome's block ids.
//   4. Depth       — ≥6 occupied rows within rows 0-9; rows 12-13 stay clear.
//   5. Crescendo   — each non-boss level adds ≥1 element absent from the previous.
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
  "village_basic", "village_tough", "village_ghost", "village_witch_boss",
  "village_beholder", "village_necromant", "village_cauldron",
  "heaven_basic", "heaven_tough", "heaven_melee_statue", "heaven_windmaster",
  "heaven_shield_statue", "heaven_angel_boss",
  "heaven_column_top", "heaven_column_mid", "heaven_column_bottom", "heaven_vase",
]);

// Rule 3: biome → required block-id prefix.
const BIOME_PREFIX = { hell: "hell_", caverns: "cavern_", village: "village_", heaven: "heaven_" };
// Rule 2: which extra fields each biome may use (docs/12 exclusivity).
const BIOME_FIELDS = {
  hell:    ["descendInterval"],
  caverns: ["timeLimit", "floors"],
  village: [],
  heaven:  ["surviveTime", "escalateInterval"],
};

/**
 * Levels, ordered per biome (crescendo applies in this order).
 * rows are listed top-down; floors are extra row-sets (caverns collapse).
 */
const LEVELS = [
  // ════ HELL — The Furnace (ROUTE): channels + teleporter circuits; descend ════
  { id: "hell-1", biome: "hell", legend: { A: "hell_basic", B: "hell_tough", O: "hell_obsidian", L: "hell_lava" }, rows: [
    "AAAAAAAA",
    "AAAAAAAA",
    "O.BBBB.O",   // first funnel — the walls teach routing
    "O.AAAA.O",
    "..AAAA..",
    "...AA...",
    "........",
    "........",
    "L......L",   // lava corners: route around, don't graze
  ]},
  { id: "hell-2", biome: "hell", legend: { A: "hell_basic", B: "hell_tough", O: "hell_obsidian", S: "hell_ballspawner" }, rows: [
    "O.AAAA.O",
    "O.ASSA.O",   // spawners guarded inside the channel
    "O.ABBA.O",
    "O.ABBA.O",
    "O.AAAA.O",
    "O......O",
    "..AAAA..",
  ]},
  { id: "hell-3", biome: "hell", legend: { A: "hell_basic", G: "hell_teleporter_green" }, rows: [
    "..AAAA..",
    "..AAAA..",
    "G.AAAA.G",   // green teleporter pair: ball enters one side, exits the other
    "..AAAA..",
    "...AA...",
    "...AA...",
  ]},
  { id: "hell-teleport", biome: "hell", legend: { A: "hell_basic", B: "hell_tough", P: "hell_teleporter", Q: "hell_teleporter_blue" }, rows: [
    "P.AAAA.P",   // red circuit up top…
    "..ABBA..",
    "..ABBA..",
    "........",
    "..ABBA..",
    "..ABBA..",
    "Q.AAAA.Q",   // …blue circuit below: aim INTO a portal to reach the far pocket
  ]},
  { id: "hell-4", biome: "hell", descendInterval: 10, legend: { A: "hell_basic", B: "hell_tough", O: "hell_obsidian", G: "hell_teleporter_green" }, rows: [
    "AAAAAAAA",   // THE PRESS: the whole furnace descends every 10s — overrun loses
    "ABBAABBA",
    "O.AAAA.O",
    "O.ABBA.O",
    "G.AAAA.G",   // green escape circuit rides down with the press
    "..AAAA..",
    "...AA...",
  ]},
  { id: "hell-5", biome: "hell", legend: { A: "hell_basic", B: "hell_tough", O: "hell_obsidian", S: "hell_lava_spawner", L: "hell_lava" }, rows: [
    "..AAAA..",   // THE BREACH: crack the moat, kill the spawners before lava pools
    ".OO..OO.",
    ".OBBBBO.",
    ".O.SS.O.",
    ".OBBBBO.",
    ".OO..OO.",
    "..AAAA..",
    "L......L",
  ]},
  { id: "hell-6", biome: "hell", legend: { A: "hell_basic", B: "hell_tough", S: "hell_ballspawner", P: "hell_teleporter", Q: "hell_teleporter_blue", G: "hell_teleporter_green" }, rows: [
    "P.ABBA.Q",   // THE CIRCUIT: three colour-paired loops around guarded spawners
    "..ASSA..",
    "G.ABBA.G",
    "..AAAA..",
    "Q.ABBA.P",
    "..AAAA..",
  ]},
  { id: "hell-7", biome: "hell", descendInterval: 12, legend: { A: "hell_basic", B: "hell_tough", O: "hell_obsidian", S: "hell_ballspawner", L: "hell_lava" }, rows: [
    "AAAAAAAA",   // FULL FURNACE: the press returns with spawners and lava floor
    "OABBBBAO",
    "A.ASSA.A",
    "OABBBBAO",
    "AAAAAAAA",
    "L......L",
  ]},
  { id: "hell-boss", biome: "hell", boss: true, legend: { A: "hell_basic", B: "hell_tough", O: "hell_obsidian", D: "hell_demon_boss" }, rows: [
    "O......O",
    ".AABBAA.",
    ".AB..BA.",
    ".ABDDBA.",
    ".ABDDBA.",
    ".AB..BA.",
    ".AABBAA.",
    "..AAAA..",
  ]},

  // ════ CAVERNS — The Mine (CHAIN): bomb veins + ceilings; floors + timer ═════
  { id: "caverns-1", biome: "caverns", legend: { A: "cavern_basic", C: "cavern_tough", L: "cavern_stalactite" }, rows: [
    "ALA.ALA.",   // ceiling spikes: pass beneath to trigger, drop them ON the rocks
    "A.A.A.A.",
    "A.A.A.C.",
    "CLA.A.L.",
    "A...C...",
    "C..L....",
  ]},
  { id: "caverns-2", biome: "caverns", legend: { A: "cavern_basic", C: "cavern_tough", R: "cavern_rock", X: "cavern_bomb", K: "cavern_cart" }, rows: [
    "RR.AA.RR",
    "R..XX..R",   // first bombs: one good hit unzips the middle
    "R.CCCC.R",
    "A..XX..A",
    "A.RAAR.A",
    "K.R..R.K",
    "AAA..AAA",
  ]},
  { id: "caverns-3", biome: "caverns", timeLimit: 75, legend: { A: "cavern_basic", R: "cavern_rock", X: "cavern_bomb" }, rows: [
    "R.AAAA.R",   // DEMOLITION: 75s on the clock — chain the veins or fail
    "RXAAAAXR",
    "RRRAARRR",
    ".XAAAAX.",
    "RRRAARRR",
    "RXAAAAXR",
    "R.AAAA.R",
  ]},
  { id: "caverns-4", biome: "caverns", legend: { A: "cavern_basic", C: "cavern_tough", R: "cavern_rock", X: "cavern_bomb", L: "cavern_stalactite" },
    rows: [
      "AAAAAAAA",   // THE SHAFT, floor 1 of 3: clear it and drop deeper
      "A.LAAL.A",
      "CC....CC",
      "A.AAAA.A",
      "..C..C..",
      "...AA...",
    ],
    floors: [
      [
        "RXAAAAXR",  // floor 2: tighter, bombs in the seams
        ".CAAAAC.",
        "..XAAX..",
        "...AA...",
      ],
      [
        "CCCCCCCC",  // floor 3: the hard pan
        "CXACCAXC",
        "..CAAC..",
        "...CC...",
      ],
    ]},
  { id: "caverns-5", biome: "caverns", timeLimit: 90, legend: { A: "cavern_basic", C: "cavern_tough", X: "cavern_bomb", L: "cavern_stalactite", K: "cavern_cart" }, rows: [
    "ALAALAAL",   // COLLAPSE RUN: the ceiling comes with you — 90s on the clock
    "A.A.A.A.",
    "CCAACCAA",
    "A..XX..A",
    "K......K",
    "AA.AA.AA",
  ]},
  { id: "caverns-6", biome: "caverns", legend: { A: "cavern_basic", C: "cavern_tough", R: "cavern_rock", X: "cavern_bomb" },
    rows: [
      "RXAAAAXR",  // DEEP VEIN, floor 1 of 3
      "RRA..ARR",
      "AXAAAAXA",
      "..RAAR..",
      "...AA...",
      "...CC...",
    ],
    floors: [
      [
        "AXAAAAXA",  // floor 2
        "CCCAACCC",
        "..XAAX..",
        "...AA...",
      ],
      [
        "CXCAACXC",  // floor 3: the hard pan
        "CCCAACCC",
        "...AA...",
        "...CC...",
      ],
    ]},
  { id: "caverns-7", biome: "caverns", timeLimit: 100, legend: { A: "cavern_basic", C: "cavern_tough", R: "cavern_rock", X: "cavern_bomb", L: "cavern_stalactite", K: "cavern_cart" }, rows: [
    "LXALAXAL",   // THE MOTHERLODE: everything the mine has, on one clock
    "RA.AA.AR",
    "AXCAACXA",
    "R..XX..R",
    "K.AAAA.K",
    "..AAAA..",
  ]},
  { id: "caverns-boss", biome: "caverns", boss: true, legend: { A: "cavern_basic", B: "cavern_tough", R: "cavern_rock", G: "cavern_goblin_boss" }, rows: [
    "RR....RR",
    ".AABBAA.",
    ".ABGGBA.",
    ".ABGGBA.",
    ".ABBBBA.",
    ".A....A.",
    ".AABBAA.",
    "..R..R..",
  ]},

  // ════ WITCHLAND — The Haunt (RACE/PHASE): double board + guarded heart ══════
  { id: "village-1", biome: "village", legend: { A: "village_basic", B: "village_bat", G: "village_ghost" }, rows: [
    ".AA..AA.",
    ".AABBAA.",   // sleeping bats — wake them and they steal your ball
    "AA.GG.AA",
    "AA.GG.AA",   // first ghost pocket: the ball passes straight through
    ".AA..AA.",
    ".AA..AA.",
    "AA.AA.AA",
  ]},
  { id: "village-2", biome: "village", legend: { A: "village_basic", G: "village_ghost", E: "village_beholder", P: "village_portal", K: "village_cauldron" }, rows: [
    "GA.AA.AG",
    "A.AGGA.A",
    "PAA..AAP",   // portals flip your phase — the ghost half opens up
    "GA.EE.AG",
    ".AAKKAA.",   // cauldrons bubbling away at your mana
    "A.AGGA.A",
    "GA.AA.AG",
  ]},
  { id: "village-3", biome: "village", legend: { A: "village_basic", N: "village_necromant", G: "village_ghost" }, rows: [
    "........",
    "...AA...",
    "..AAAA..",
    "..A..A..",
    "..ANAA..",   // necromant centre: watch it raise the fallen
    "..AAAA..",
    "..GGGG..",   // ghost bricks below — they'll keep coming back
    "...GG...",
  ]},
  { id: "village-ghost", biome: "village", legend: { A: "village_basic", G: "village_ghost", N: "village_necromant", B: "village_bat" }, rows: [
    ".AAAAAA.",
    ".AGGGGA.",   // the double board: a ghost level inside the solid one
    ".AGGGGA.",
    "AGGNGGGA",   // the necromant hides on the ghost layer
    "AGGGGGGA",
    ".AGGGBA.",   // a bat lurks in the haunted layer
    ".AGGGGA.",
    ".AAAAAA.",
  ]},
  { id: "village-4", biome: "village", legend: { A: "village_basic", B: "village_tough", G: "village_ghost", K: "village_cauldron", N: "village_necromant" }, rows: [
    ".BBBBBB.",   // THE GUARDED HEART: phase in, silence the necromant, out-race it
    "BGGGGGGB",
    "BG.KK.GB",
    "BG.NN.GB",
    "BG....GB",
    "BGGGGGGB",
    ".BBBBBB.",
    "..A..A..",
  ]},
  { id: "village-5", biome: "village", legend: { A: "village_basic", G: "village_ghost", E: "village_beholder", K: "village_cauldron", B: "village_bat" }, rows: [
    ".AABBAA.",   // THE SEANCE: eyes, wings and cauldrons around a ghost ring
    "AG.EE.GA",
    "AGKAAKGA",
    "AG....GA",
    ".AABBAA.",
    "AA.AA.AA",
  ]},
  { id: "village-6", biome: "village", legend: { A: "village_basic", G: "village_ghost", E: "village_beholder", N: "village_necromant", P: "village_portal", B: "village_bat" }, rows: [
    "PA.GG.AP",   // WITCHING HOUR: the full haunt — phase in, silence the pair
    "AGGNNGGA",
    "AG.EE.GA",
    "AGGGGGGA",
    ".A.BB.A.",
    "AA.AA.AA",
  ]},
  { id: "village-boss", biome: "village", boss: true, legend: { A: "village_basic", B: "village_tough", W: "village_witch_boss" }, rows: [
    ".A.AA.A.",
    "AABBBBAA",
    ".ABWWBA.",
    ".ABWWBA.",
    "AABBBBAA",
    ".A.AA.A.",
    ".AB..BA.",
    ".AABBAA.",
  ]},

  // ════ HEAVEN — The Trial (CONVERT): colonnade + sanctum choice; escalation ══
  { id: "heaven-1", biome: "heaven", legend: { H: "heaven_basic", T: "heaven_tough", S: "heaven_statue", M: "heaven_melee_statue", R: "heaven_altar", V: "heaven_vase" }, rows: [
    "S.HHHH.S",
    "..HTTH..",
    "H.MHHM.H",   // first statues — hostile until you choose
    "H......H",
    "H.R..V.H",   // THE CHOICE: altar (ally them) left, vase (enrage + reward) right
    "S.HTTH.S",
    "S.HHHH.S",
  ]},
  { id: "heaven-2", biome: "heaven", legend: { H: "heaven_basic", T: "heaven_tough", S: "heaven_statue", W: "heaven_windmaster", D: "heaven_shield_statue", R: "heaven_altar", V: "heaven_vase" }, rows: [
    "H.STTS.H",
    "HS.TT.SH",
    ".H.WW.H.",   // the wind bends every shot near the centre
    "HD.SS.DH",
    ".H.TT.H.",
    "HR.TT.VH",
    "H.STTS.H",
  ]},
  { id: "heaven-3", biome: "heaven", legend: { H: "heaven_basic", T: "heaven_tough", M: "heaven_melee_statue", P: "heaven_column_top", C: "heaven_column_mid", B: "heaven_column_bottom", R: "heaven_altar", V: "heaven_vase" }, rows: [
    "P.HTTH.P",
    "C.HTTH.C",   // THE COLONNADE: crack the pillars top-down
    "B.MHHM.B",
    "..HTTH..",
    "P.R..V.P",
    "C.HTTH.C",
    "B.HHHH.B",
  ]},
  { id: "heaven-4", biome: "heaven", surviveTime: 60, escalateInterval: 12, legend: { H: "heaven_basic", T: "heaven_tough", S: "heaven_statue", M: "heaven_melee_statue", D: "heaven_shield_statue", R: "heaven_altar", V: "heaven_vase" }, rows: [
    "M.HTTH.M",   // THE TRIAL: survive 60s while the statues self-level every 12s —
    "H.DTTD.H",   // converting them (altar) or harvesting them (vase) is the answer
    "S......S",
    "H.R..V.H",
    "S......S",
    "H.MTTM.H",
    "D.HTTH.D",
  ]},
  { id: "heaven-5", biome: "heaven", legend: { H: "heaven_basic", T: "heaven_tough", S: "heaven_statue", M: "heaven_melee_statue", D: "heaven_shield_statue", W: "heaven_windmaster", P: "heaven_column_top", C: "heaven_column_mid", B: "heaven_column_bottom", R: "heaven_altar", V: "heaven_vase" }, rows: [
    "P.HTTH.P",   // TWIN TRIALS: wind in the colonnade, the choice at its heart
    "C.DWWD.C",
    "B.HTTH.B",
    "H.R..V.H",
    "S.MTTM.S",
    "H.HTTH.H",
  ]},
  { id: "heaven-6", biome: "heaven", surviveTime: 75, escalateInterval: 10, legend: { H: "heaven_basic", T: "heaven_tough", S: "heaven_statue", M: "heaven_melee_statue", D: "heaven_shield_statue", R: "heaven_altar", V: "heaven_vase" }, rows: [
    "M.DTTD.M",   // THE ASCENSION: hold 75s while the host self-levels every 10s
    "S.HTTH.S",
    "H.R..V.H",
    "D......D",
    "S.MTTM.S",
    "H.HTTH.H",
  ]},
  { id: "heaven-boss", biome: "heaven", boss: true, legend: { H: "heaven_basic", T: "heaven_tough", S: "heaven_statue", X: "heaven_angel_boss" }, rows: [
    "S.HHHH.S",
    ".HTTTTH.",
    ".HTXXTH.",
    ".HTXXTH.",
    ".HTTTTH.",
    "S.HHHH.S",
    "S......S",
  ]},
];

// ── Lint rules (docs/12 §2) ───────────────────────────────────────────────────

/** Every cell id used by a level (main rows + floors). */
function usedIds(lvl) {
  const ids = new Set();
  const scan = (rows) => {
    for (const r of rows) for (const ch of r) if (lvl.legend[ch]) ids.add(lvl.legend[ch]);
  };
  scan(lvl.rows);
  for (const f of lvl.floors ?? []) scan(f);
  return ids;
}

function countId(lvl, id) {
  let n = 0;
  const scan = (rows) => {
    for (const r of rows) for (const ch of r) if (lvl.legend[ch] === id) n++;
  };
  scan(lvl.rows);
  for (const f of lvl.floors ?? []) scan(f);
  return n;
}

/** Rule 1 — biome idiom markers (non-boss levels). */
function checkMarker(lvl) {
  const ids = usedIds(lvl);
  switch (lvl.biome) {
    case "hell": {
      const sameColorPair =
        countId(lvl, "hell_teleporter") >= 2 ||
        countId(lvl, "hell_teleporter_blue") >= 2 ||
        countId(lvl, "hell_teleporter_green") >= 2;
      return ids.has("hell_obsidian") || sameColorPair;
    }
    case "caverns":
      return countId(lvl, "cavern_bomb") >= 2 || countId(lvl, "cavern_stalactite") >= 3;
    case "village":
      return countId(lvl, "village_ghost") >= 4
        || ids.has("village_necromant") || ids.has("village_cauldron");
    case "heaven": {
      const colonnade = ids.has("heaven_column_top") && ids.has("heaven_column_bottom");
      const sanctum   = ids.has("heaven_altar") && ids.has("heaven_vase");
      return colonnade || sanctum;
    }
  }
  return false;
}

/** Element signature for the crescendo rule: block ids + pacing/objective fields. */
function elements(lvl) {
  const e = usedIds(lvl);
  for (const f of ["descendInterval", "timeLimit", "surviveTime", "escalateInterval"])
    if (lvl[f]) e.add(`field:${f}`);
  if (lvl.floors?.length) e.add("field:floors");
  return e;
}

let prevByBiome = {};
let count = 0;
for (const lvl of LEVELS) {
  const tag = lvl.id;

  // Width + pad.
  const padRows = (rows) => {
    for (const [i, r] of rows.entries())
      if (r.length !== COLS) throw new Error(`${tag} row ${i} is ${r.length} chars: "${r}"`);
    const out = [...rows];
    while (out.length < ROWS) out.push(".".repeat(COLS));
    if (out.length !== ROWS) throw new Error(`${tag} has ${out.length} rows`);
    return out;
  };
  const rows_data = padRows(lvl.rows);
  const floors = (lvl.floors ?? []).map(padRows);

  // Winnable.
  const hasKill = lvl.rows.some((r) => [...r].some((c) => lvl.legend[c] && DESTRUCTIBLE.has(lvl.legend[c])));
  if (!hasKill) throw new Error(`${tag} has no needToKill block — would win instantly`);

  // Rule 3 — block set.
  const prefix = BIOME_PREFIX[lvl.biome];
  for (const id of usedIds(lvl))
    if (!id.startsWith(prefix)) throw new Error(`${tag}: block "${id}" violates the ${lvl.biome} block-set rule`);

  // Rule 2 — exclusivity.
  const allowed = BIOME_FIELDS[lvl.biome];
  for (const f of ["descendInterval", "timeLimit", "surviveTime", "escalateInterval"])
    if (lvl[f] && !allowed.includes(f)) throw new Error(`${tag}: field "${f}" is not ${lvl.biome}'s pacing/objective`);
  if (lvl.floors?.length && !allowed.includes("floors")) throw new Error(`${tag}: floors are caverns-only`);

  // Rule 4 — depth: ≥6 occupied rows within rows 0-9; rows 12-13 clear.
  const occupied = rows_data.slice(0, 10).filter((r) => [...r].some((c) => lvl.legend[c])).length;
  if (occupied < 6) throw new Error(`${tag}: only ${occupied} occupied rows in rows 0-9 (depth rule needs ≥6)`);
  if (rows_data.slice(12).some((r) => [...r].some((c) => lvl.legend[c])))
    throw new Error(`${tag}: rows 12-13 must stay clear (paddle zone)`);

  if (!lvl.boss) {
    // Rule 1 — marker.
    if (!checkMarker(lvl)) throw new Error(`${tag}: missing its biome idiom marker (rule 1)`);
    // Rule 5 — crescendo vs the previous non-boss level of the biome.
    const prev = prevByBiome[lvl.biome];
    if (prev) {
      const prevEl = elements(prev);
      const added  = [...elements(lvl)].filter((e) => !prevEl.has(e));
      if (added.length === 0) throw new Error(`${tag}: adds nothing new over ${prev.id} (crescendo rule)`);
    }
    prevByBiome[lvl.biome] = lvl;
  }

  const json = { id: lvl.id, biome: lvl.biome, cols: COLS, rows: ROWS, rows_data, legend: lvl.legend };
  if (lvl.descendInterval)  json.descendInterval  = lvl.descendInterval;
  if (lvl.timeLimit)        json.timeLimit        = lvl.timeLimit;
  if (lvl.surviveTime)      json.surviveTime      = lvl.surviveTime;
  if (lvl.escalateInterval) json.escalateInterval = lvl.escalateInterval;
  if (floors.length)        json.floors           = floors;
  writeFileSync(join(OUT, `${lvl.id}.json`), JSON.stringify(json, null, 2) + "\n");
  count++;
}
console.log(`wrote ${count} levels to ${OUT} — all 5 identity-matrix lint rules pass`);
