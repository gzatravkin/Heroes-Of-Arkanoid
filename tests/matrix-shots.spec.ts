import { test } from "./helpers/fixtures";
import { openBattle, cheat } from "./helpers/game";
import * as path from "path";

const SHOTS = path.resolve(__dirname, "demo-screenshots");

// Identity-matrix showcase levels (docs/12): one shot per new idiom/pacing/objective
// level — the greyscale-test evidence set.
const CASES: Array<[string, string, number]> = [
  ["hell-4",    "matrix-hell-press.png",        120], // descending furnace + green circuit
  ["hell-5",    "matrix-hell-breach.png",       500], // moat + lava creep underway
  ["caverns-3", "matrix-caverns-demolition.png", 60], // bomb veins + TIME countdown
  ["caverns-4", "matrix-caverns-shaft.png",      30], // floor 1/3 indicator
  ["village-4", "matrix-village-heart.png",      60], // guarded heart double-board
  ["heaven-3",  "matrix-heaven-colonnade.png",   30], // column stacks
  ["heaven-4",  "matrix-heaven-trial.png",      120], // SURVIVE timer + escalation
];

for (const [level, shot, ff] of CASES) {
  test(`matrix level ${level} renders its identity`, async ({ page }) => {
    await openBattle(page, level);
    await cheat(page, "fastForward", ff);
    await page.waitForTimeout(400); // let ambience/HUD settle
    await page.screenshot({ path: path.join(SHOTS, shot) });
  });
}
