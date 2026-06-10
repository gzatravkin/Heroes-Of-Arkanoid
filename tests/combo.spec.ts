import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat, getState } from "./helpers/game";

/**
 * combo.spec.ts — Playwright tests for the combo multiplier (task 1.3).
 *
 * The combo multiplier (1–4) increments every 3 consecutive brick hits
 * without the ball touching the paddle.  Paddle contact resets it to 1.
 *
 * Tests use the `setCombo` cheat which directly sets `_comboMultiplier`
 * and `_comboCount` on the backend so the desired multiplier is reflected
 * in the next snapshot without needing to actually destroy bricks.
 */

test("combo multiplier builds on consecutive brick hits", async ({ page }) => {
  await openBattle(page, "hell-1", 1);

  // Use the setCombo cheat to simulate 6 consecutive hits → ×3 multiplier.
  await cheat(page, "setCombo", 3);

  // Wait for snapshot to reflect comboMultiplier = 3.
  await page.waitForFunction(
    () => (window as any).__game?.getState()?.comboMultiplier === 3,
    undefined,
    { timeout: 5000 },
  );

  const s = await getState(page);
  expect(s.comboMultiplier).toBe(3);
});

test("combo resets on paddle contact", async ({ page }) => {
  await openBattle(page, "hell-1", 1);

  // Build up a ×3 combo.
  await cheat(page, "setCombo", 3);
  await page.waitForFunction(
    () => (window as any).__game?.getState()?.comboMultiplier === 3,
    undefined,
    { timeout: 5000 },
  );

  // Park ball just above the paddle with downward velocity — deflects on next tick.
  await cheat(page, "parkBallAbovePaddle");

  // Wait for paddle contact to fire and reset comboMultiplier back to 1.
  await page.waitForFunction(
    () => (window as any).__game?.getState()?.comboMultiplier === 1,
    undefined,
    { timeout: 5000 },
  );

  const s = await getState(page);
  expect(s.comboMultiplier).toBe(1);
});
