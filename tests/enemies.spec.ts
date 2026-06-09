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

test("Heaven boss finale renders", async ({ page }) => {
  await openBattle(page, "heaven-boss");
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.bossActive).toBeTruthy();
  await page.screenshot({ path: path.join(SHOTS, "enemy-heaven-boss.png") });
});

test("Witch boss casts magic bolts (witchmagic hazards)", async ({ page }) => {
  await openBattle(page, "village-boss");
  expect((await page.evaluate(() => (window as any).__game.getState())).bossActive).toBeTruthy();
  // Keep lives topped up so the frozen ball can't lose the fight while the boss
  // drains the paddle — otherwise the game ends before we can observe the volley.
  await cheat(page, "setLives", 99);
  // Advance sim-time deterministically so the boss telegraphs + fires magic volleys.
  await cheat(page, "fastForward", 130);
  // Wait until a magic bolt is in the open mid-field (below the top block rows, above
  // the paddle) so the proof screenshot shows it clearly rather than under the boss.
  await page.waitForFunction(() => {
    const s = (window as any).__game.getState();
    if (!s) return false;
    const lo = s.boardH * 0.3, hi = s.boardH * 0.6;
    return s.hazards.some((h: any) => h.kind === "witchmagic" && h.y > lo && h.y < hi);
  }, null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-witch-magic.png") });
});

test("Caverns bombs present (chain-explode block)", async ({ page }) => {
  await openBattle(page, "caverns-2");
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.length).toBeGreaterThan(0);
  await page.screenshot({ path: path.join(SHOTS, "enemy-caverns-bombs.png") });
});

test("Witchland ghost portal toggles the ball's phase", async ({ page }) => {
  await openBattle(page, "village-2");
  expect((await page.evaluate(() => (window as any).__game.getState()))
    .blocks.some((b: any) => b.sprite === "Portal")).toBeTruthy();
  await page.screenshot({ path: path.join(SHOTS, "enemy-ghost-portal.png") });
});

test("Heaven shield statue shields neighbours (immune flash)", async ({ page }) => {
  await openBattle(page, "heaven-1");
  expect((await page.evaluate(() => (window as any).__game.getState()))
    .blocks.some((b: any) => b.sprite === "HeavenDefender")).toBeTruthy();
  // fast-forward past a shield pulse, then a block should report shielded
  await cheat(page, "fastForward", 240);
  await page.waitForFunction(
    () => ((window as any).__game.getState()?.blocks ?? []).some((b: any) => b.shielded),
    null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-shield-statue.png") });
});

test("Heaven windmaster present (deflects the ball)", async ({ page }) => {
  await openBattle(page, "heaven-2");
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.some((b: any) => b.sprite === "WindMaster2"),
    "windmaster block should be in the level").toBeTruthy();
  await page.screenshot({ path: path.join(SHOTS, "enemy-windmaster.png") });
});

test("Witchland necromant present (revives destroyed blocks)", async ({ page }) => {
  await openBattle(page, "village-ghost");
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.some((b: any) => b.sprite === "VillageDeath"),
    "necromant block should be in the level").toBeTruthy();
  await page.screenshot({ path: path.join(SHOTS, "enemy-necromant.png") });
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
