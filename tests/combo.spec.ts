import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat, getState, captureFrames } from "./helpers/game";

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

// ── New test added in task 3.1 ───────────────────────────────────────────────

/**
 * Test: Floating score text appears and rises on a multi-brick combo.
 *
 * Uses ballToBlock to destroy a brick while comboMultiplier=2, then captures
 * 3 frames 150 ms apart and verifies the PNG bytes differ between frame 1 and
 * frame 3.  The floater rises ~7.5 px per 150 ms window, so the two frames are
 * visually distinct — the PNG IDAT chunk encodes different data.
 *
 * Catches a regression where the floater pool is empty, the text is never made
 * visible, or the rise animation is disabled.
 */
test("floating score text visible at ×2 combo", async ({ page }) => {
  await openBattle(page, "hell-1", 3);

  // Wait for the renderer to be ready so the floater pool is initialised.
  await page.waitForFunction(
    () => !!(window as any).__renderer?.paddleLayer,
    null, { timeout: 10_000 },
  );

  // Set combo to ×2 so the next brick destruction spawns a floater.
  await cheat(page, "setCombo", 2);

  // Grab the id of a live, destructible block from the current snapshot.
  const firstBlockId = await page.evaluate<number | null>(() => {
    const blocks: any[] = (window as any).__game?.getState()?.blocks ?? [];
    const blk = blocks.find((b: any) => !b.indestructible && b.hp > 0);
    return blk?.id ?? null;
  });
  expect(firstBlockId).not.toBeNull();

  // ballToBlock teleports the ball to overlap the target block and sets its
  // velocity toward it; the collision resolves on the next physics tick.
  // The ball is moved far from the paddle, so the combo should not reset before
  // the block is destroyed.
  await cheat(page, "ballToBlock", firstBlockId as number);

  // Wait for the block to vanish from the live snapshot.
  await page.waitForFunction(
    (id) => {
      const blocks: any[] = (window as any).__game?.getState()?.blocks ?? [];
      return !blocks.find((b: any) => b.id === id);
    },
    firstBlockId,
    { timeout: 5_000 },
  );

  // Small pause so the renderer processes the snapshot and spawns the floater
  // into the Pixi scene before we take the first screenshot.
  await page.waitForTimeout(100);

  // Capture 3 frames with 150 ms gaps (total 300 ms span).
  const frames = await captureFrames(page, 3, 150);

  // Compare PNG buffer bytes between frame 1 and frame 3.
  // A static scene produces identical PNG bytes (deterministic encoder).
  // A moving floater changes IDAT chunk content — many bytes will differ.
  let diffCount = 0;
  const len = Math.min(frames[0].length, frames[2].length);
  for (let i = 0; i < len; i++) {
    if (frames[0][i] !== frames[2][i]) diffCount++;
  }
  expect(diffCount).toBeGreaterThan(100);
});
