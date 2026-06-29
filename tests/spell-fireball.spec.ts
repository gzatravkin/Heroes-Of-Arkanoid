import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Conflagration (design §3 rework of Fireball): NOT a projectile — it detonates every block the
// player has set on fire, all at once, chaining the flames onward. This exercises it in real play:
// ignite a swathe of blocks and detonate them in one cast. The sim log records `conflagration
// detonated=N`, blocks are destroyed by the burst (live count drops), and no projectile spawns.

const OUT = path.resolve(__dirname, "..", "test-results", "conflagration");

test("Conflagration detonates the board's burning blocks (no projectile)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `conflagration-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  await page.goto(`/?scene=battle&level=hell-1&seed=6&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot()); // intact board

  // Freeze the ball, ignite a swathe, and detonate — ALL in one message batch so the server drains
  // them before the next tick: the blocks are guaranteed alight when the burst lands (no burn-DoT or
  // fire-spread clears them first), so Conflagration detonates the whole lit cluster at once.
  const before = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("freezeBall", 0);
    g.cheat("setMana", 100);
    g.cheat("igniteBlocks", 24);
    g.castFireball(); // = Conflagration
  });
  // The detonation destroys blocks → the live block count drops, with no projectile involved.
  await page.waitForFunction((n) => (window as any).__game.getState().blocks.length < n, before, { timeout: 10_000 });
  await page.waitForTimeout(150);
  fs.writeFileSync(path.join(OUT, "2-detonation.png"), await page.screenshot());

  const after = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  expect(after).toBeLessThan(before);
  expect(await page.evaluate(() => (window as any).__game.getState().projectiles?.length ?? 0)).toBe(0);
});
