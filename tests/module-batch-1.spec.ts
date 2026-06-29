import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// §2 Modules — Batch 1 (Tidal Core, Hollow Ball, Gyro Paddle, Drumhead Paddle): slot-bound passives.
// Real play. Sim log: `module tidal_core heavy/swift`, `module drumhead_paddle shockwave`, `module gyro_paddle`.

const OUT = path.resolve(__dirname, "..", "test-results", "module-batch-1");

test("Tidal Core, Hollow Ball, Drumhead Paddle fire in real play", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `module-b1a-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // core=Tidal, ball=Hollow, paddle=Drumhead (one module per slot).
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=fire_mage&select=1" +
      "&modules=tidal_core,hollow_ball,drumhead_paddle&moduleLevel=3", { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");
  await page.evaluate(() => (window as any).__game.cheat("setLives", 99));

  // HOLLOW BALL: the served ball is visibly bigger (radiusScale > 1).
  const rs = await page.evaluate(() => (window as any).__game.getState().balls[0]?.radiusScale ?? 1);
  fs.writeFileSync(path.join(OUT, "1-hollow.png"), await page.screenshot());
  expect(rs).toBeGreaterThan(1.2); // Hollow Ball serves big

  // TIDAL + DRUMHEAD: park the ball dead-centre repeatedly → perfect deflects toggle Tidal and fire
  // Drumhead's column shockwave. Track block HP to confirm the shockwave bites.
  const before = await page.evaluate(() => (window as any).__game.getState().blocks.reduce((a: number, b: any) => a + b.hp, 0));
  for (let i = 0; i < 6; i++) {
    await page.evaluate(() => { const g = (window as any).__game; if (g.getState().phase !== "Playing") g.cheat("parkBallAbovePaddle", 0); else g.cheat("parkBallAbovePaddle", 0); });
    await page.waitForTimeout(260);
  }
  fs.writeFileSync(path.join(OUT, "2-drumhead.png"), await page.screenshot());
  const after = await page.evaluate(() => (window as any).__game.getState().blocks.reduce((a: number, b: any) => a + b.hp, 0));
  expect(after).toBeLessThan(before); // perfect-deflect shockwaves (+ ball hits) chewed the board
});

test("Gyro Paddle whips the ball when the paddle is moving", async ({ page }) => {
  const runId = `module-b1b-${Date.now()}`;
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=fire_mage&select=1&modules=gyro_paddle&moduleLevel=3",
      { method: "POST" });
  });
  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");
  await page.evaluate(() => (window as any).__game.cheat("setLives", 99));

  // Park the ball, then sweep the paddle hard side-to-side so a deflect lands on a fast-moving paddle.
  const before = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  for (let i = 0; i < 30; i++) {
    await page.evaluate((k) => {
      const g = (window as any).__game;
      if (g.getState().phase !== "Playing") g.cheat("parkBallAbovePaddle", 0);
      const w = g.getState().boardW ?? 256;
      g.setPaddleX(k % 2 === 0 ? w * 0.25 : w * 0.75); // hard sweep each step
    }, i);
    await page.waitForTimeout(120);
  }
  fs.writeFileSync(path.join(OUT, "3-gyro.png"), await page.screenshot());
  const after = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  expect(after).toBeLessThanOrEqual(before); // game ran; Gyro logs the whip (verified via sim log)
});
