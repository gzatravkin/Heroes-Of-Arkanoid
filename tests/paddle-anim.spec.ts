import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat } from "./helpers/game";

// Helper: set mana via cheat (same pattern as hud-live.spec.ts).
// We freeze mana regen first so the target value is stable across the
// snapshot round-trip, then set it to the desired absolute value.
async function setManaStable(page: Parameters<typeof cheat>[0], value: number) {
  await cheat(page, "freezeMana", 1);
  await cheat(page, "setMana", value);
}

test("paddle bar shows frame 0 at low mana (5)", async ({ page }) => {
  await openBattle(page, "hell-1", 1);

  // Wait for the renderer to be mounted and paddleLayer accessible.
  await page.waitForFunction(
    () => !!(window as any).__renderer?.paddleLayer,
    null, { timeout: 10_000 },
  );

  await setManaStable(page, 5);

  // Wait for the snapshot to arrive and paddleLayer.setMana() to run.
  await page.waitForFunction(
    () => (window as any).__renderer?.paddleLayer?._animFrame === 0,
    null, { timeout: 10_000 },
  );

  const frame = await page.evaluate(() => (window as any).__renderer?.paddleLayer?._animFrame);
  expect(frame).toBe(0);
});

test("paddle bar shows frame 3 at full mana (95)", async ({ page }) => {
  await openBattle(page, "hell-1", 2);

  // Wait for the renderer to be mounted and paddleLayer accessible.
  await page.waitForFunction(
    () => !!(window as any).__renderer?.paddleLayer,
    null, { timeout: 10_000 },
  );

  await setManaStable(page, 95);

  // Wait for the snapshot to arrive and paddleLayer.setMana() to run.
  await page.waitForFunction(
    () => (window as any).__renderer?.paddleLayer?._animFrame === 3,
    null, { timeout: 10_000 },
  );

  const frame = await page.evaluate(() => (window as any).__renderer?.paddleLayer?._animFrame);
  expect(frame).toBe(3);
});
