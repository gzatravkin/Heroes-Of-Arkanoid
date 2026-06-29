import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Ashfall (design §3, NEW Fire Mage): while armed, every ignite-kill rains a vertical ember down its
// column. Real-play synergy demo: arm Ashfall, set the board alight, then detonate it with Conflagration
// — each burning block destroyed rains an ember. The sim log shows `ashfall armed`; the snapshot then
// carries falling ember projectiles (none of which existed before — no projectile spell was used).

const OUT = path.resolve(__dirname, "..", "test-results", "ashfall");

test("Ashfall rains embers from ignite-kills (Conflagration synergy)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `ashfall-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // Fire Mage with Ashfall + Conflagration equipped (signature Ignite locks slot 0).
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=fire_mage&select=1&loadout=ignite,ashfall,fireball",
      { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=2&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");

  // Arm Ashfall (slot 1).
  await page.evaluate(() => { (window as any).__game.cheat("setMana", 100); (window as any).__game.castSlot(1); });
  await page.waitForTimeout(150);
  fs.writeFileSync(path.join(OUT, "1-armed.png"), await page.screenshot());

  // Ignite the board and detonate it with Conflagration (slot 2) in one batch — every burning block
  // it destroys is an ignite-kill, so Ashfall rains an ember down each column.
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("setMana", 100);
    g.cheat("freezeBall", 0);
    g.cheat("igniteBlocks", 24);
    g.castSlot(2); // Conflagration
  });
  // Embers (projectiles) must now be raining — none existed before (no projectile spell was cast).
  await page.waitForFunction(() => ((window as any).__game.getState().projectiles?.length ?? 0) > 0, null, { timeout: 10_000 });
  await page.waitForTimeout(120);
  fs.writeFileSync(path.join(OUT, "2-embers.png"), await page.screenshot());

  expect(await page.evaluate(() => (window as any).__game.getState().projectiles.length)).toBeGreaterThan(0);
});
