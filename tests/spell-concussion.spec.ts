import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Concussion Charge (design §3 rework of Rocket): a damage-less utility blast at the paddle that
// KNOCKS BACK balls (a save) and YANKS nearby pickups toward you. Real play: spawn a pickup, offset
// the paddle so the pickup is off to the side, then detonate — the pickup is pulled toward the paddle
// and the in-range ball is knocked. The sim log records `concussion knockedBalls=N yankedPickups=M`.

const OUT = path.resolve(__dirname, "..", "test-results", "concussion");

test("Concussion Charge yanks a pickup toward the paddle (and knocks the ball, no damage)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `concussion-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=engineer&select=1&loadout=rocket,overload,lightning",
      { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");

  // Park balls in range; capture the board before the blast.
  await page.evaluate(() => { (window as any).__game.cheat("setMana", 100); (window as any).__game.cheat("parkBallAbovePaddle", 0); });
  const paddleX = await page.evaluate(() => (window as any).__game.getState().paddleX);
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());

  // Spawn the pickup (120px right of the paddle) and detonate in ONE batch: the server drains both
  // before the next tick, so the FRESH in-range pickup is yanked the same tick (no time to fall off).
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("spawnBonusRight", 0);
    g.castSlot(1); // Concussion Charge — knocks balls (log) + yanks the pickup leftward toward the paddle
  });
  const spawnX = paddleX + 120;
  // The pickup is pulled toward the paddle → its x drops below where it spawned.
  await page.waitForFunction(
    (x0) => { const b = (window as any).__game.getState().bonuses?.[0]; return !!b && b.x < x0 - 6; },
    spawnX, { timeout: 10_000 });
  await page.waitForTimeout(120);
  fs.writeFileSync(path.join(OUT, "2-after.png"), await page.screenshot());

  expect(await page.evaluate(() => (window as any).__game.getState().bonuses[0].x)).toBeLessThan(spawnX);
});
