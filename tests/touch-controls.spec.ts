/**
 * P1 touch-control tests.
 * Verifies that:
 *   1. Drag on the play-area canvas moves the paddle (paddleX changes in state).
 *   2. Tapping a spell-slot button casts the spell (mana drops).
 * Both assertions must pass with rendering ON (no render-skip).
 */
import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat, waitForPhase } from "./helpers/game";

test("drag on canvas moves the paddle", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");

  const before = (await getState(page)).paddleX;

  // Simulate a touch drag across the middle of the viewport.
  const vw = 390;
  const vh = 844;
  await page.mouse.move(vw * 0.2, vh * 0.5);
  await page.mouse.down();
  await page.mouse.move(vw * 0.8, vh * 0.5, { steps: 10 });
  await page.mouse.up();

  const after = (await getState(page)).paddleX;
  // Paddle X should have changed meaningfully (more than 5 sim units).
  expect(Math.abs(after - before)).toBeGreaterThan(5);
});

test("tapping spell slot casts spell and consumes mana", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");

  // Give enough mana to cast fireball (costs 20).
  await cheat(page, "setMana", 100);
  await page.waitForFunction(() => (window as any).__game.getState().mana >= 100);

  const before = (await getState(page)).mana;

  // Tap the fireball slot element.
  await page.tap("#hud-spell-fireball");

  // Mana must decrease after the cast.
  await page.waitForFunction((m) => (window as any).__game.getState().mana < m, before, { timeout: 8000 });
  const after = (await getState(page)).mana;
  expect(after).toBeLessThan(before);
});
