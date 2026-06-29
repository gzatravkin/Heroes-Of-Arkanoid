import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Reckoning (design §3, NEW Paladin): a meter charged by HP LOST that auto-smites the board. Real play
// on hell-2 (emitter level): arm Reckoning, freeze the ball so the emitter hazards drain the paddle's HP
// (the only damage source), and watch the meter fill and smite the board. With the ball frozen, the ONLY
// thing that can destroy blocks is Reckoning's smite — so a drop in the block count is unambiguous.

const OUT = path.resolve(__dirname, "..", "test-results", "reckoning");

test("Reckoning smites the board as HP is lost (frozen ball ⇒ only the smite clears blocks)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `reckoning-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // Paladin with Reckoning equipped (signature Shield locks slot 0, so Reckoning is slot 1).
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=paladin&select=1&loadout=shield,reckoning,spear",
      { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-2&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");

  // Arm Reckoning, survive the barrage, freeze the ball so only the smite can clear blocks.
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("setMana", 100);
    g.castSlot(1);             // arm Reckoning
    g.cheat("setLives", 60);   // endure many hits → many smites
    g.cheat("freezeBall", 0);  // ball can't destroy blocks; emitter hazards drain HP
  });
  const before = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  fs.writeFileSync(path.join(OUT, "1-armed.png"), await page.screenshot());

  // Take a few hits (below the smite threshold) so the HUD charge bar is partially filled, then capture it.
  await page.evaluate(() => (window as any).__game.cheat("fastForward", 70));
  await page.waitForFunction(() => ((window as any).__game.getState().reckoningCharge ?? 0) > 0, null, { timeout: 8_000 });
  fs.writeFileSync(path.join(OUT, "1b-charging.png"), await page.screenshot());

  // Advance: emitters fire → hazards hit the paddle → HP lost → meter fills → board smitten.
  await page.evaluate(() => (window as any).__game.cheat("fastForward", 600));
  await page.waitForFunction((n) => (window as any).__game.getState().blocks.length < n, before, { timeout: 12_000 });
  await page.waitForTimeout(150);
  fs.writeFileSync(path.join(OUT, "2-smitten.png"), await page.screenshot());

  expect(await page.evaluate(() => (window as any).__game.getState().blocks.length)).toBeLessThan(before);
});
