// Level generator (Phase C, 2026-06-15) — remakes all 48 campaign levels.
// Applies the corner/house structure system (digits 1-4 = basic corners, 5-8 = tough/2nd-style
// corners) as a design element while preserving each biome's mechanics + the teach→reinforce→combine
// arc + winnability rules. Validates: 14 rows × 8 chars, bottom 3 rows empty, every char mapped.
import { writeFileSync } from "node:fs";
import { join } from "node:path";

const OUT = join(process.cwd(), "config", "levels");

// ── Per-biome full legend (block id for every char a level may use) ──────────
const CORNERS = (b, tough) => ({
  "1": `${b}_corner_tl`, "2": `${b}_corner_tr`, "3": `${b}_corner_bl`, "4": `${b}_corner_br`,
  ...(tough ? { "5": `${tough}_tl`, "6": `${tough}_tr`, "7": `${tough}_bl`, "8": `${tough}_br` } : {}),
});
const LEG = {
  hell: {
    A: "hell_basic", B: "hell_tough", W: "hell_obsidian", E: "hell_ballspawner",
    T: "hell_teleporter", U: "hell_teleporter_blue", L: "hell_lava", S: "hell_lava_spawner",
    D: "hell_demon_boss", ...CORNERS("hell", "hell_tough_corner"),
  },
  caverns: {
    A: "cavern_basic", B: "cavern_tough", W: "cavern_rock", K: "cavern_stalactite",
    C: "cavern_cart", M: "cavern_bomb", U: "cavern_union", G: "cavern_goblin_boss",
    ...CORNERS("cavern", null),
  },
  village: {
    A: "village_basic", B: "village_tough", E: "village_beholder", H: "village_ghost",
    T: "village_bat", N: "village_necromant", P: "village_portal", C: "village_cauldron",
    X: "village_witch_boss", Y: "village_basic2", ...CORNERS("village", "village_tough_corner"),
  },
  heaven: {
    A: "heaven_basic", B: "heaven_tough", W: "heaven_statue", E: "heaven_melee_statue",
    M: "heaven_windmaster", S: "heaven_shield_statue", L: "heaven_altar", V: "heaven_vase",
    T: "heaven_column_top", I: "heaven_column_mid", J: "heaven_column_bottom",
    N: "heaven_angel_boss", ...CORNERS("heaven", "heaven_brick_corner"),
  },
};

const E = "........"; // empty row shorthand
function pad(rows) { while (rows.length < 14) rows.push(E); return rows; }

// ── Level definitions ────────────────────────────────────────────────────────
// Each: id -> { biome, rows[ up to 14 ], descend? }. House/structure shapes use corner digits.
const L = {
  // ===================== HELL =====================
  "hell-1":   { biome:"hell", rows:[E,E,"..1AA2..","..AAAA..","..AAAA..","..3AA4.."] },
  "hell-2":   { biome:"hell", rows:[E,".1AAAA2.",".ABBBBA.",".ABBBBA.",".3AAAA4."] },
  "hell-3":   { biome:"hell", rows:[E,"1A2..1A2","AWA..AWA","3A4..3A4"] },
  "hell-4":   { biome:"hell", rows:["......E.",E,".1AA2...",".AAAA...",".3AA4..."] },
  "hell-5":   { biome:"hell", rows:["..E..E..",E,"BB.AA.BB","AA.AA.AA","AA.AA.AA"] },
  "hell-6":   { biome:"hell", rows:[E,"T.1AA2.T","..AAAA..","..AAAA..","..3AA4.."] },
  "hell-7":   { biome:"hell", rows:["..E..E..",E,"BB.AA.BB","BB.AA.BB","AA.AA.AA","AA.AA.AA","T......T"] },
  "hell-8":   { biome:"hell", rows:[E,".1AAAA2.",".AAAAAA.",".3AAAA4.",E,E,E,E,"LL.SS.LL"] },
  "hell-9":   { biome:"hell", rows:[E,".1AAAA2.",".ABBBBA.",".ABBBBA.",".ABBBBA.",".3AAAA4.",E,E,"L.S..S.L"] },
  "hell-10":  { biome:"hell", rows:[".E....E.",E,"W.1AA2.W","..ABBA..","W.3AA4.W","..AAAA..","..BBBB..","T......T","..LSSL.."] },
  "hell-11":  { biome:"hell", rows:[".E.WW.E.","A.1AA2.A","W.BBBB.W","A.BBBB.A","A.AAAA.A","T.AAAA.T","U.3AA4.U","L.S..S.L"] },
  "hell-boss":{ biome:"hell", descend:14, rows:["..W..W..","..BDDB..","..BDDB..","..3BB4..",E,".1AAAA2.",".3AAAA4."] },

  // ===================== CAVERNS (channels) =====================
  "caverns-1":   { biome:"caverns", rows:[E,E,"..1AA2..","..AAAA..","..AAAA..","..3AA4.."] },
  "caverns-2":   { biome:"caverns", rows:[E,".A.AA.A.",".B.BB.B.",".B.BB.B.",".A.AA.A."] },
  "caverns-3":   { biome:"caverns", rows:[E,"W.1AA2.W","W.B..B.W","W.B..B.W","W.3AA4.W"] },
  "caverns-4":   { biome:"caverns", rows:[E,"..K..K..",E,".A.AA.A.",".A.AA.A.",".A.AA.A."] },
  "caverns-5":   { biome:"caverns", rows:[E,"..K..K..",".A.AA.A.",".A.AA.A.",".B.BB.B.",".B.BB.B.","C......C"] },
  "caverns-6":   { biome:"caverns", rows:[E,".AA..AA.",".AM..MA.",".AA..AA.",".AA..AA."] },
  "caverns-7":   { biome:"caverns", rows:[E,"..K..K..","AA.AA.AA","AA.MM.AA","AA.BB.AA","AA.AA.AA","C......C"] },
  "caverns-8":   { biome:"caverns", rows:[E,E,".UUUUUU.",".A.AA.A.",".A.AA.A.",".UUUUUU.",".A.AA.A."] },
  "caverns-9":   { biome:"caverns", rows:[E,".1AAAA2.",".AUUUUA.",".ABBBBA.",".AUUUUA.",".3AAAA4."] },
  "caverns-10":  { biome:"caverns", rows:[E,"...KK...",".UU..UU.",".AA..AA.",".MA..AM.",".BB..BB.","WAA..AAW","WC....CW"] },
  "caverns-11":  { biome:"caverns", rows:["W.K..K.W","UU.AA.UU","AA.MM.AA","BB.AA.BB","AA.UU.AA","MA.BB.AM","BB.AA.BB","AA.AA.AA",".C....C."] },
  "caverns-boss":{ biome:"caverns", descend:14, rows:[E,".W.GG.W.","...GG...",".B.AA.B.",".A.BB.A.","..A..A.."] },

  // ===================== VILLAGE (wooden) =====================
  "village-1":   { biome:"village", rows:[E,E,"..1AA2..","..AAAA..","..AAAA..","..3AA4.."] },
  "village-2":   { biome:"village", rows:[E,".5BBBB6.",".AAAAAA.",".3AAAA4."] },
  "village-3":   { biome:"village", rows:[E,"BB.12.BB","BB.AA.BB","..3AA4.."] },
  "village-4":   { biome:"village", rows:[E,"...E....","..A..A..",".AA..AA.",".AA..AA.","..A..A.."] },
  "village-5":   { biome:"village", rows:[E,"..E..E..","AA.BB.AA","AA.BB.AA",".A.AA.A."] },
  "village-6":   { biome:"village", rows:[E,"..AAAA..",".THHHHT.","..AAAA..","...HH..."] },
  "village-7":   { biome:"village", rows:[E,"..E..E..","BB.HH.BB","AA.HH.AA","TA.AA.AT",".A.HH.A.",".A.AA.A."] },
  "village-8":   { biome:"village", rows:[E,"..1AA2..",".AANNAA.","PAAAAAAP","..3AA4.."] },
  "village-9":   { biome:"village", rows:[E,".5BBBB6.","B.CAAC.B","B.NAAN.B","B.AAAA.B",".7AAAA8."] },
  "village-10":  { biome:"village", rows:[E,".E....E.","..HHHH..","B.AAAA.B","T.ANNA.T","..ACCA..","P.AAAA.P","...HH..."] },
  "village-11":  { biome:"village", rows:[E,"..E..E..","BB.HH.BB","AA.AA.AA","NA.HH.AN","AA.CC.AA","BA.AA.AB","TA.HH.AT","PA.AA.AP","...AA..."] },
  "village-boss":{ biome:"village", descend:14, rows:[E,"...XX...","...XX...","..3BB4..",".E.AA.E.",".1AAAA2.",".3AAAA4."] },

  // ===================== HEAVEN (temple columns) =====================
  "heaven-1":   { biome:"heaven", rows:[E,E,"..1AA2..","..AAAA..","..AAAA..","..3AA4.."] },
  "heaven-2":   { biome:"heaven", rows:[E,".5BBBB6.",".AAAAAA.",".3AAAA4."] },
  "heaven-3":   { biome:"heaven", rows:[E,".T.AA.T.",".I.BB.I.",".J.AA.J.","W..AA..W"] },
  "heaven-4":   { biome:"heaven", rows:["...E....",".AA..AA.",".AA..AA.",".AA..AA.","..A..A.."] },
  "heaven-5":   { biome:"heaven", rows:[".E....E.","..1AA2..","..ABBA..","..ABBA..","..3AA4.."] },
  "heaven-6":   { biome:"heaven", rows:[E,".M.12.M.",".M.AA.M.","..AAAA..","..3AA4.."] },
  "heaven-7":   { biome:"heaven", rows:["..AAAA..",".AEAAEA.",".A.BB.A.","MA.BB.AM","MA.AA.AM","...AA..."] },
  "heaven-8":   { biome:"heaven", rows:[E,".1AAAA2.",".AAAAAA.",".3AAAA4.","..SLLS.."] },
  "heaven-9":   { biome:"heaven", rows:["T.5BB6.T","I.BAAB.I","J.BAAB.J","W.SAAS.W","..AVVA..","..7AA8.."] },
  "heaven-10":  { biome:"heaven", rows:["WE.AA.EW","..TAAT..","..IMMI..","..JAAJ..","..SAAS..","...LL...","...VV..."] },
  "heaven-11":  { biome:"heaven", rows:[".E.WW.E.","T.BAAB.T","I.MAAM.I","J.BAAB.J","A.SAAS.A","A.ALLA.A","A.AVVA.A","..ABBA..","...AA..."] },
  "heaven-boss":{ biome:"heaven", descend:14, rows:["..W..W..","..ANNA..","..ANNA..","..3BB4..",".1AAAA2.",".3AAAA4."] },
};

// ── Build + validate + write ─────────────────────────────────────────────────
let count = 0;
for (const [id, def] of Object.entries(L)) {
  const rows = pad([...def.rows]);
  const base = LEG[def.biome];
  if (rows.length !== 14) throw new Error(`${id}: ${rows.length} rows (need 14)`);
  rows.forEach((r, i) => { if (r.length !== 8) throw new Error(`${id} row ${i} is "${r}" (${r.length} chars, need 8)`); });
  for (const i of [11, 12, 13]) if (rows[i] !== E) throw new Error(`${id} row ${i} must be empty (dodge zone)`);
  const used = new Set();
  for (const r of rows) for (const ch of r) if (ch !== ".") used.add(ch);
  const legend = {};
  for (const ch of [...used].sort()) {
    if (!(ch in base)) throw new Error(`${id}: char '${ch}' not in ${def.biome} legend`);
    legend[ch] = base[ch];
  }
  const out = { id, biome: def.biome, cols: 8, rows: 14, rows_data: rows, legend };
  if (def.descend) out.descendInterval = def.descend;
  writeFileSync(join(OUT, `${id}.json`), JSON.stringify(out, null, 2) + "\n");
  count++;
}
console.log(`wrote ${count} levels`);
