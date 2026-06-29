import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Tesla Grid (design §3, NEW Engineer): side-wall bounces charge both walls → a horizontal lightning
// curtain fries the frontmost row. Real play: arm it, freeze the ball (so the ONLY block damage is the
// curtain), and pulse both walls — the front rows are eaten and the sim log shows `tesla curtain`.

const OUT = path.resolve(__dirname, "..", "test-results", "tesla");

test("Tesla Grid fires a lightning curtain when both walls are charged", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `tesla-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // Engineer with Tesla Grid equipped (signature Overload locks slot 0 → Tesla is slot 1).
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=engineer&select=1&loadout=tesla,overload,lightning",
      { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");

  // Arm Tesla, freeze the ball so the curtain is the only block-damage source.
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("setMana", 100);
    g.castSlot(1);            // arm Tesla Grid
    g.cheat("freezeBall", 0);
  });
  const before = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());

  // Pulse both walls a few times — each charged pair fires a curtain that eats the new front row.
  await page.evaluate(() => (window as any).__game.cheat("teslaPulse", 0));
  await page.waitForTimeout(90);
  fs.writeFileSync(path.join(OUT, "2-curtain.png"), await page.screenshot()); // catch the arc FX
  // Space pulses past the 0.5s curtain cooldown so each one fires a fresh curtain.
  for (let i = 0; i < 5; i++) { await page.evaluate(() => (window as any).__game.cheat("teslaPulse", 0)); await page.waitForTimeout(560); }
  await page.waitForFunction((n) => (window as any).__game.getState().blocks.length < n, before, { timeout: 10_000 });
  fs.writeFileSync(path.join(OUT, "3-after.png"), await page.screenshot());

  expect(await page.evaluate(() => (window as any).__game.getState().blocks.length)).toBeLessThan(before);
});
