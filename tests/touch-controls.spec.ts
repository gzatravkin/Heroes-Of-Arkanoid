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

  // Freeze regen so the post-cast mana drop is a stable, race-free observable
  // (regen otherwise refills the cost within ~1.8s — faster than snapshot
  // sampling can be trusted under parallel-worker load).
  await cheat(page, "freezeMana", 1);
  await cheat(page, "setMana", 100);
  await page.waitForFunction(() => (window as any).__game.getState().mana >= 100);

  const before = (await getState(page)).mana;

  // Tap the fireball slot until the cast registers. Under parallel-worker stress a
  // single tap can be lost between pointer dispatch and the WS command; with regen
  // frozen, re-tapping is safe (extra casts only lower mana further).
  await expect(async () => {
    await page.tap("#hud-spell-fireball");
    const m = (await getState(page)).mana;
    expect(m).toBeLessThan(before);
  }).toPass({ timeout: 10_000 });
});
