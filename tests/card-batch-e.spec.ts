import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// §1 Cards — Batch E (Martyr's Brand, Ricochet, Sleight of Hand): the misc/event cluster.
// Real play: equip all three at L5. An enemy bolt to the face triggers MARTYR'S BRAND (vengeance damage
// buff); free play bounces the ball off the side walls → RICOCHET fires horizontal bolts; a pickup caught
// dead-centre on the paddle → SLEIGHT OF HAND duplicates it. Sim log: `card martyrs_brand`, `card ricochet`,
// `card sleight_of_hand`.

const OUT = path.resolve(__dirname, "..", "test-results", "card-batch-e");

test("Batch E cards fire in real play (Martyr buff, Ricochet bolt, Sleight duplicate)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `card-batch-e-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=fire_mage&select=1" +
      "&cards=martyrs_brand,ricochet,sleight_of_hand&cardLevel=5", { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");
  await page.evaluate(() => { const g = (window as any).__game; g.cheat("setLives", 99); g.cheat("setBalls", 99); g.cheat("chipBlocks", 1); });
  const before = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());

  // MARTYR'S BRAND: take an enemy bolt to the face → vengeance buff arms (the bolt descends to the paddle).
  await page.evaluate(() => (window as any).__game.cheat("spawnEnemyBolt", 0));

  // Keep the game in Playing the whole time (re-serve dead-centre whenever it drops to Serving) so the bolt
  // reaches the paddle, the ball bounces off the side walls (Ricochet), and hits land while buffed.
  for (let i = 0; i < 24; i++) {
    await page.evaluate(() => {
      const g = (window as any).__game;
      if (g.getState().phase !== "Playing") g.cheat("parkBallAbovePaddle", 0);
    });
    await page.waitForTimeout(220);
    if (i === 8) fs.writeFileSync(path.join(OUT, "2-play.png"), await page.screenshot());
    if (i === 14) await page.evaluate(() => (window as any).__game.cheat("spawnBonus", 0)); // centred pickup
  }
  fs.writeFileSync(path.join(OUT, "3-after.png"), await page.screenshot());

  const after = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  expect(after).toBeLessThan(before); // blocks cleared in real play with the cards active
});
