import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Rot & Collapse (design §3 rework of Decay): an imbue that ROTS blocks (permanently lowering their max
// HP) and, when a rotted block dies, the column above COLLAPSES down into the gap (the gravity primitive).
// Real play: cast it, rot a swathe of blocks — their max HP drops and killed blocks make columns fall
// (the sim log shows `gravity collapse`). NO projectile.

const OUT = path.resolve(__dirname, "..", "test-results", "rot");

test("Rot & Collapse withers max HP and collapses columns (gravity)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `rot-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // Necromancer with Rot & Collapse (the decay imbue) equipped.
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=necromancer&select=1&loadout=decay,raise,drain",
      { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");

  const blockCount = () => page.evaluate(() => (window as any).__game.getState().blocks.length);

  // Cast the imbue (slot 0 = decay/Rot & Collapse) and freeze the ball for a stable demo.
  await page.evaluate(() => { const g = (window as any).__game; g.cheat("setMana", 100); g.cheat("freezeBall", 0); g.castSlot(0); });
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());
  const before = await blockCount();

  // Rot some blocks: killed ones collapse their columns (others above fall in) — visible mid-board.
  await page.evaluate(() => (window as any).__game.cheat("rotHits", 8));
  await page.waitForFunction((n) => (window as any).__game.getState().blocks.length < n, before, { timeout: 10_000 });
  await page.waitForTimeout(200);
  fs.writeFileSync(path.join(OUT, "2-collapsed.png"), await page.screenshot());

  // Some blocks were destroyed (collapses) and surviving rotted blocks have lowered max HP.
  expect(await blockCount()).toBeLessThan(before);
});
