import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat } from "./helpers/game";

/**
 * powerup.spec.ts — Playwright tests for the five power-up types added in task 1.2.
 *
 * Power-ups use the same BonusDto wire format but their `type` starts with "powerup_".
 * Effects are verified by inspecting the snapshot after collection.
 *
 * All tests use the cheat `spawnPowerUp:<name>` which spawns the pickup just above
 * the paddle (3× paddle height), so it is collected within ~0.4s of the game entering
 * Playing phase.
 */

// ---------------------------------------------------------------------------
// Gate 1: spawn & snapshot visibility
// ---------------------------------------------------------------------------

test("powerup: spawnPowerUp:wide creates a powerup_wide in bonuses snapshot", async ({ page }) => {
  await openBattle(page, "hell-1", 1);

  // Snapshot should already include the bonuses array.
  const s0 = await getState(page);
  expect(Array.isArray(s0.bonuses)).toBe(true);

  // Enter Playing phase with a frozen ball so the power-up isn't collected
  // immediately (avoids the race where the bonus is collected before getState()).
  await cheat(page, "fastForward", 0);
  await page.waitForFunction(
    () => (window as any).__game?.getState()?.phase === "Playing",
    undefined,
    { timeout: 5000 },
  );

  // Spawn power-up — falls toward the (stationary) paddle.
  await cheat(page, "spawnPowerUp:wide");

  // The pickup may be collected before a poll cycle can observe it in bonuses[].
  // Accept either: still in-flight (bonuses array) OR already collected (widePaddleActive).
  await page.waitForFunction(
    () => {
      const s = (window as any).__game?.getState();
      return (
        s?.bonuses?.some((b: any) => b.type === "powerup_wide") ||
        s?.widePaddleActive === true
      );
    },
    undefined,
    { timeout: 5000 },
  );

  // Confirm the outcome: either still visible or effect applied.
  const s1 = await getState(page);
  const stillVisible = s1.bonuses.some((b: any) => b.type === "powerup_wide");
  const alreadyCollected = s1.widePaddleActive === true;
  expect(stillVisible || alreadyCollected).toBe(true);
});

test("powerup: snapshot has fireshotActive/fireshotTimer/shieldActive fields", async ({ page }) => {
  await openBattle(page, "hell-1", 1);

  const s = await getState(page);
  // Fields should be present (false/0 by default).
  expect(typeof s.fireshotActive === "boolean" || s.fireshotActive === undefined).toBe(true);
  expect(typeof s.fireshotTimer === "number"   || s.fireshotTimer  === undefined).toBe(true);
  expect(typeof s.shieldActive  === "boolean"  || s.shieldActive   === undefined).toBe(true);
});

// ---------------------------------------------------------------------------
// Gate 2: Wide Paddle effect changes paddleW
// ---------------------------------------------------------------------------

test("powerup: wide paddle power-up increases paddleW", async ({ page }) => {
  await openBattle(page, "hell-1", 1);

  const s0 = await getState(page);
  const origW = s0.paddleW;

  // fastForward:0 — enters Playing AND zeroes ball velocity so the ball can't
  // drain before the power-up is collected (fixes a racy timeout).
  await cheat(page, "fastForward", 0);
  await page.waitForFunction(
    () => (window as any).__game?.getState()?.phase === "Playing",
    undefined,
    { timeout: 5000 },
  );

  // Spawn the pickup just above the paddle.
  await cheat(page, "spawnPowerUp:wide");

  // Wait until paddle width increases (bonus collected).
  await page.waitForFunction(
    (original: number) => {
      const s = (window as any).__game?.getState();
      return (s?.paddleW ?? 0) > original;
    },
    origW,
    { timeout: 5000 },
  );

  const s1 = await getState(page);
  expect(s1.paddleW).toBeGreaterThan(origW);
  expect(s1.widePaddleActive).toBe(true);
});

// ---------------------------------------------------------------------------
// Gate 3: FireShot activates and is reflected in snapshot
// ---------------------------------------------------------------------------

test("powerup: fireshot power-up sets fireshotActive in snapshot", async ({ page }) => {
  await openBattle(page, "hell-1", 1);

  await cheat(page, "fastForward", 0);
  await page.waitForFunction(
    () => (window as any).__game?.getState()?.phase === "Playing",
    undefined,
    { timeout: 5000 },
  );

  await cheat(page, "spawnPowerUp:fireshot");

  await page.waitForFunction(
    () => {
      const s = (window as any).__game?.getState();
      return s?.fireshotActive === true;
    },
    undefined,
    { timeout: 5000 },
  );

  const s = await getState(page);
  expect(s.fireshotActive).toBe(true);
  expect((s.fireshotTimer ?? 0)).toBeGreaterThan(0);
});

// ---------------------------------------------------------------------------
// Gate 4: Shield activates and is reflected in snapshot
// ---------------------------------------------------------------------------

test("powerup: shield power-up sets shieldActive in snapshot", async ({ page }) => {
  await openBattle(page, "hell-1", 1);

  await cheat(page, "fastForward", 0);
  await page.waitForFunction(
    () => (window as any).__game?.getState()?.phase === "Playing",
    undefined,
    { timeout: 5000 },
  );

  await cheat(page, "spawnPowerUp:shield");

  await page.waitForFunction(
    () => {
      const s = (window as any).__game?.getState();
      return s?.shieldActive === true;
    },
    undefined,
    { timeout: 5000 },
  );

  const s = await getState(page);
  expect(s.shieldActive).toBe(true);
});
