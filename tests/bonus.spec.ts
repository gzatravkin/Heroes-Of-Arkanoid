import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat } from "./helpers/game";

/**
 * bonus.spec.ts — Playwright tests for the P6 falling bonus pickup system.
 *
 * The bonuses fall quickly (~130 px/s) and we need to catch them in the WebSocket
 * snapshot stream.  We spawn multiple bonuses via repeated cheat calls and latch
 * the first snapshot that contains one via page.evaluate.
 */

test("bonuses: spawnBonus cheat creates bonus visible in at least one snapshot", async ({ page }) => {
  await openBattle(page, "hell-1", 1);

  // Confirm initial snapshot has the bonuses field.
  const s0 = await getState(page);
  expect(Array.isArray(s0.bonuses)).toBe(true);

  // Install a latching listener on `conn.latest` via polling in-page.
  // We do NOT rely on snapshot callbacks (no `onSnapshot` hook exposed).
  // Instead we spawn many bonuses so the server always has some alive,
  // and we poll getState() rapidly.
  await page.evaluate(() => {
    // Spam the cheat every 100 ms so there are always fresh bonuses falling.
    (window as any).__bonusSpamInterval = setInterval(() => {
      (window as any).__game?.cheat("spawnBonus", 0);
    }, 100);
  });

  // Poll until at least one bonus appears in the snapshot (up to 5 s).
  await page.waitForFunction(
    () => {
      const s = (window as any).__game?.getState();
      return s && Array.isArray(s.bonuses) && s.bonuses.length > 0;
    },
    null,
    { timeout: 5000 },
  );

  // Stop spam.
  await page.evaluate(() => clearInterval((window as any).__bonusSpamInterval));

  const s1 = await getState(page);
  // Bonuses may have just been collected or fallen off — what matters is they appeared.
  // We verified it in waitForFunction; just assert the field exists.
  expect(Array.isArray(s1.bonuses)).toBe(true);
});

test("bonuses: snapshot always has bonuses array and temp-effect fields", async ({ page }) => {
  await openBattle(page, "hell-1", 1);

  const s = await getState(page);
  expect(Array.isArray(s.bonuses)).toBe(true);
  expect(typeof s.widePaddleActive).toBe("boolean");
  expect(typeof s.widePaddleTimer).toBe("number");
  expect(typeof s.slowBallActive).toBe("boolean");
  expect(typeof s.slowBallTimer).toBe("number");
});
