/**
 * P1 touch-control tests.
 * Verifies that:
 *   1. Drag on the play-area canvas moves the paddle (paddleX changes in state).
 *   2. Tapping a spell-slot button casts the spell (mana drops).
 * Both assertions must pass with rendering ON (no render-skip).
 */
import { test, expect } from "./helpers/fixtures";
import { openBattle, waitForPhase } from "./helpers/game";

test("drag on canvas moves the paddle", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");

  // Read paddleX before the drag via waitForFunction (wrapping in object avoids
  // the falsy-zero problem; also avoids GPU stall from page.evaluate).
  const beforeHandle = await page.waitForFunction(() => ({
    value: (window as any).__game?.getState()?.paddleX ?? 0,
  }));
  const before = (await beforeHandle.jsonValue()).value;

  // Simulate a touch drag across the middle of the viewport.
  const vw = 390;
  const vh = 844;
  await page.mouse.move(vw * 0.2, vh * 0.5);
  await page.mouse.down();
  await page.mouse.move(vw * 0.8, vh * 0.5, { steps: 10 });
  await page.mouse.up();

  // waitForFunction instead of getState() — no GPU stall.
  await page.waitForFunction(
    (b) => Math.abs(((window as any).__game?.getState()?.paddleX ?? 0) - b) > 5,
    before,
    { timeout: 10_000 },
  );
});

test("tapping spell slot casts spell and consumes mana", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  // Freeze ball + mana regen + set full mana in one evaluate (single GPU stall).
  // fastForward:0 freezes ball velocity so it can't clear blocks during subsequent stalls.
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("fastForward", 0); // freeze ball
    g.cheat("freezeMana", 1);
    g.cheat("setMana", 100);
  });
  await page.waitForFunction(() => (window as any).__game.getState().mana >= 100);

  // Tap the fireball slot until the cast registers.
  // force:true bypasses Playwright's stability check — the mana bar's CSS width
  // updates every ~30fps snapshot, which keeps the hotbar's bounding box "moving"
  // and would stall the tap for 8s otherwise.
  await expect(async () => {
    await page.locator("#hud-spell-fireball").tap({ force: true });
    await page.waitForFunction(() => (window as any).__game.getState().mana < 100, null, { timeout: 5_000 });
  }).toPass({ timeout: 15_000 });
});
