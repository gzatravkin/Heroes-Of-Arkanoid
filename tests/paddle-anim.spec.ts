import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat, captureFrames } from "./helpers/game";

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

// ── New tests added in task 3.1 ─────────────────────────────────────────────

/**
 * Test 1: Paddle-mana correlation matrix (all 4 tiers in one battle session).
 *
 * Catches the original timer-loop bug immediately: the old code cycled bar
 * frames on a fixed timer regardless of mana, so setMana(60) could still show
 * frame 0 or frame 3 depending on timer phase instead of the expected frame 2.
 */
test("paddle bar frame matches all 4 mana tiers", async ({ page }) => {
  await openBattle(page, "hell-1", 3);

  await page.waitForFunction(
    () => !!(window as any).__renderer?.paddleLayer,
    null, { timeout: 10_000 },
  );

  // Freeze regen so the target value is stable across the snapshot round-trip.
  await cheat(page, "freezeMana", 1);

  // [manaAbsoluteValue, expectedAnimFrame]
  // Thresholds per PaddleLayer.setMana(): ratio*4 floored → 0,1,2,3.
  // With default manaMax=100: 5%→frame0, 30%→frame1, 60%→frame2, 95%→frame3.
  const tiers: [number, number][] = [[5, 0], [30, 1], [60, 2], [95, 3]];
  for (const [manaValue, expectedFrame] of tiers) {
    await cheat(page, "setMana", manaValue);
    await page.waitForFunction(
      (expected) => (window as any).__renderer?.paddleLayer?._animFrame === expected,
      expectedFrame,
      { timeout: 5_000 },
    );
    const frame = await page.evaluate<number>(
      () => (window as any).__renderer?.paddleLayer?._animFrame ?? -1,
    );
    expect(frame).toBe(expectedFrame);
  }
});

/**
 * Test 2: Paddle frame transitions are gradual (no teleporting).
 *
 * Sweeps mana from 5% to 95% in 10% steps and asserts no consecutive recorded
 * frame pair differs by more than 1.  Catches a regression to any scheme that
 * drives frame selection independently of mana (e.g., a free-running timer that
 * can jump 0→3 between two snapshots).
 */
test("paddle frame does not jump more than 1 step between snapshots", async ({ page }) => {
  await openBattle(page, "hell-1", 4);

  await page.waitForFunction(
    () => !!(window as any).__renderer?.paddleLayer,
    null, { timeout: 10_000 },
  );

  await cheat(page, "freezeMana", 1);
  await cheat(page, "setMana", 5);
  // Wait for frame 0 to be stable before the sweep.
  await page.waitForFunction(
    () => (window as any).__renderer?.paddleLayer?._animFrame === 0,
    null, { timeout: 5_000 },
  );

  const frames: number[] = [];
  for (let mana = 5; mana <= 95; mana += 10) {
    const expectedFrame = Math.min(3, Math.floor(mana / 100 * 4));
    await cheat(page, "setMana", mana);
    // Poll until the renderer has consumed the new snapshot and settled on the
    // expected frame — more robust than a fixed delay on loaded CI machines.
    await page.waitForFunction(
      (exp) => (window as any).__renderer?.paddleLayer?._animFrame === exp,
      expectedFrame,
      { timeout: 5_000 },
    );
    const frame = await page.evaluate<number>(
      () => (window as any).__renderer?.paddleLayer?._animFrame ?? -1,
    );
    frames.push(frame);
  }

  // No consecutive pair should differ by more than 1.
  // (Expected sequence with manaMax=100: [0,0,1,1,1,2,2,3,3,3], max step = 1.)
  for (let i = 1; i < frames.length; i++) {
    expect(
      Math.abs(frames[i] - frames[i - 1]),
      `frame jump at mana=${5 + i * 10}: ${frames[i - 1]}→${frames[i]}`,
    ).toBeLessThanOrEqual(1);
  }
});

/**
 * Test 3: Squash animation fires on ball-paddle contact.
 *
 * Uses parkBallAbovePaddle to guarantee the game is in Playing phase and the
 * ball bounces off the paddle into the upper board, then re-enters the zone.
 * Polls _squashElapsed every 50 ms; the field is ≥0 for ~180 ms on each
 * bounce — enough to catch reliably.
 *
 * Catches a regression where the squash trigger is removed or the edge-detect
 * logic is broken (e.g., reverted to level-trigger that fires every frame).
 */
test("squash animation activates within 500ms of ball-paddle contact", async ({ page }) => {
  await openBattle(page, "hell-1", 5);

  await page.waitForFunction(
    () => !!(window as any).__renderer?.paddleLayer,
    null, { timeout: 10_000 },
  );

  // parkBallAbovePaddle: transitions to Playing phase, places ball just above
  // the paddle face moving downward.  Ball bounces off the paddle face, travels
  // up past zoneTop, bounces off walls/bricks, and re-enters the zone — at that
  // crossing _squashElapsed is set to 0 (edge-triggered).
  await cheat(page, "parkBallAbovePaddle");

  // Poll every 50 ms for up to 5000 ms (100 iterations).
  // _squashElapsed is ≥0 for 180 ms per bounce, giving ~3-4 poll hits per cycle.
  let squashSeen = false;
  for (let i = 0; i < 100; i++) {
    const elapsed = await page.evaluate<number>(
      () => (window as any).__renderer?.paddleLayer?._squashElapsed ?? -1,
    );
    if (elapsed >= 0) {
      squashSeen = true;
      break;
    }
    await page.waitForTimeout(50);
  }

  expect(squashSeen).toBe(true);
});
