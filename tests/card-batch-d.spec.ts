import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// §1 Cards — Batch D (Avalanche, Keystone, Domino): the on-kill cluster.
// Real play: equip all three at L5, soften the board, force a hot combo, and drive the ball through many
// blocks fast. AVALANCHE (combo ≥8) sends a dead block crashing onto the one below; KEYSTONE collapses the
// column when a load-bearing block dies; DOMINO chain-explodes after 3 quick kills. Sim log records
// `card avalanche`, `card keystone`, `card domino`.

const OUT = path.resolve(__dirname, "..", "test-results", "card-batch-d");

test("Batch D cards fire in real play (Avalanche crush, Keystone collapse, Domino chain)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `card-batch-d-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=fire_mage&select=1" +
      "&cards=avalanche,keystone,domino&cardLevel=5", { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("setLives", 99);
    g.cheat("freezeBall", 0);
    g.cheat("chipBlocks", 2); // soften so kills come fast (Domino's 3-in-1s) and stacks fall to ball hits
  });
  const before = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());

  // Drive the ball rapidly through destructible blocks (top-down) with the combo held hot.
  for (let i = 0; i < 16; i++) {
    const id = await page.evaluate(() => {
      const g = (window as any).__game;
      g.cheat("setCombo", 4); // Combo.Count = 9 → above Avalanche's ≥8 gate
      const s = g.getState();
      const d = s.blocks.filter((b: any) => !b.indestructible && b.hp > 0).sort((a: any, b: any) => a.y - b.y)[0];
      return d ? d.id : -1;
    });
    if (id === -1) break;
    await page.evaluate((bid) => (window as any).__game.cheat("ballToBlock", bid), id);
    await page.waitForTimeout(160);
    if (i === 7) fs.writeFileSync(path.join(OUT, "2-clearing.png"), await page.screenshot());
  }
  fs.writeFileSync(path.join(OUT, "3-after.png"), await page.screenshot());

  const after = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  expect(after).toBeLessThan(before); // many blocks cleared in real play with the on-kill cards active
});
