import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat, waitForPhase } from "./helpers/game";

test("igniting the ball is visible in the snapshot after a deflect", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  await page.evaluate(() => (window as any).__game.castIgnite()); // arm
  await cheat(page, "parkBallAbovePaddle", 0);                    // ball -> just above paddle, moving down
  await page.waitForFunction(
    () => (window as any).__game.getState().balls.some((b: any) => b.ignited),
    null, { timeout: 8000 }
  );
  const s = await getState(page);
  expect(s.balls.some((b: any) => b.ignited)).toBeTruthy();
});
