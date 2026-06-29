import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Lich's Gaze (design §3 rework of Skeletal Mage): a slow lighthouse beam at the paddle that sweeps the
// board, cursing every block it crosses (cursed blocks take bonus ball damage). NO projectile fan.
// Real play: cast it, watch the beam sweep and curse blocks (purple), with the sim log showing it armed.

const OUT = path.resolve(__dirname, "..", "test-results", "lichgaze");

test("Lich's Gaze sweeps a beam that curses blocks (no projectiles)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `lichgaze-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // Necromancer with Lich's Gaze equipped (signature Raise locks slot 0 → Lich's Gaze is slot 1).
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=necromancer&select=1&loadout=mage,raise,decay",
      { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");

  // Cast Lich's Gaze; freeze the ball so the sweep plays out on a stable board.
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("setMana", 100);
    g.cheat("freezeBall", 0);
    g.castSlot(1); // Lich's Gaze
  });
  // The beam must exist (a ray, not a projectile fan).
  await page.waitForFunction(() => !!(window as any).__game.getState().lichBeam, null, { timeout: 8_000 });
  expect(await page.evaluate(() => (window as any).__game.getState().projectiles?.length ?? 0)).toBe(0);
  fs.writeFileSync(path.join(OUT, "1-beam.png"), await page.screenshot());

  // As the beam sweeps, blocks it crosses become cursed (purple).
  await page.waitForFunction(
    () => ((window as any).__game.getState().blocks?.filter((b: any) => b.cursed).length ?? 0) > 0,
    null, { timeout: 8_000 });
  await page.waitForTimeout(400);
  fs.writeFileSync(path.join(OUT, "2-cursed.png"), await page.screenshot());

  const cursed = await page.evaluate(() =>
    (window as any).__game.getState().blocks.filter((b: any) => b.cursed).length);
  expect(cursed).toBeGreaterThan(0);
});
