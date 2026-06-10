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

  // Freeze regen so the post-cast mana drop is a stable, race-free observable
  // (regen otherwise refills the cost within ~1.8s — faster than snapshot
  // sampling can be trusted under parallel-worker load).
  await cheat(page, "freezeMana", 1);
  await cheat(page, "setMana", 100);
  await page.waitForFunction(() => (window as any).__game.getState().mana >= 100);

  // Ensure Playing phase: hell-1 physics are fast; the ball can drain while we
  // set up mana. SpellSystem rejects casts in Serving phase, so we must be in
  // Playing to observe a mana drop.  Re-serve via __game.serve() until Playing.
  // freeze keeps mana at 100 across phase transitions.
  await page.waitForFunction(() => {
    const g = (window as any).__game;
    if (!g) return false;
    const phase = g.getState()?.phase;
    if (phase === "Serving") g.serve();
    return phase === "Playing";
  }, null, { timeout: 10_000 });

  const before = (await getState(page)).mana;

  // Tap the fireball slot until the cast registers.
  // force:true is required: the mana-fill bar (a flex sibling of the hotbar) updates
  // its CSS width on every ~30fps snapshot, keeping the hotbar's bounding box
  // perpetually "moving" in Playwright's stability check — which polls for up to 8s
  // before deciding the element is stable enough to tap. By that time the ball is
  // lost and SpellSystem (Playing-phase gate) rejects the cast.
  // force:true dispatches the touch events immediately without waiting for stability.
  // With regen frozen, re-tapping is safe (extra casts only lower mana further).
  await expect(async () => {
    // Re-serve if ball was lost since the last check.
    await page.waitForFunction(() => {
      const g = (window as any).__game;
      if (!g) return false;
      const phase = g.getState()?.phase;
      if (phase === "Serving") g.serve();
      return phase === "Playing";
    }, null, { timeout: 5_000 });
    await page.locator("#hud-spell-fireball").tap({ force: true });
    const m = (await getState(page)).mana;
    expect(m).toBeLessThan(before);
  }).toPass({ timeout: 15_000 });
});
