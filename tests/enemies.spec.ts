import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat } from "./helpers/game";
import * as path from "path";

const SHOTS = path.resolve(__dirname, "demo-screenshots");

// Use the deterministic fastForward cheat (freezes balls, advances N sim-ticks) so the
// emitter fires in sim-time regardless of the wall-clock tick rate under parallel load.
async function emitsHazard(page: import("@playwright/test").Page, level: string, name: string) {
  await openBattle(page, level);
  await cheat(page, "fastForward", 150); // ~2.5s sim → first emitter shot is mid-flight
  // Wait for the post-cheat snapshot: a hazard is in flight, or one already hit the paddle (HP lost).
  await page.waitForFunction(() => {
    const s = (window as any).__game.getState();
    return s && (s.hazards.length > 0 || s.lives < 3);
  }, null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, name) });
}

test("Hell ball-spawner emits hazards at the paddle", async ({ page }) => {
  await emitsHazard(page, "hell-2", "enemy-hell-spawner.png");
});

test("Witchland beholder fires at the ball", async ({ page }) => {
  await emitsHazard(page, "village-2", "enemy-beholder.png");
});

test("Heaven melee statue fires", async ({ page }) => {
  await emitsHazard(page, "heaven-1", "enemy-heaven-statue.png");
});

test("Caverns bombs present (chain-explode block)", async ({ page }) => {
  await openBattle(page, "caverns-2");
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.length).toBeGreaterThan(0);
  await page.screenshot({ path: path.join(SHOTS, "enemy-caverns-bombs.png") });
});

test("Caverns stalactites fall as hazards", async ({ page }) => {
  await openBattle(page, "caverns-1");
  await cheat(page, "dropStalactites", 4);
  await cheat(page, "fastForward", 20); // let them descend a little for the shot
  await page.waitForFunction(
    () => ((window as any).__game.getState()?.hazards ?? []).some((h: any) => h.kind === "stalactite"),
    null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-stalactites.png") });
});
