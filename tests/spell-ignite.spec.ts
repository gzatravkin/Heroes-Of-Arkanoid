import { test, expect } from "./helpers/fixtures";
import { openBattle, getState } from "./helpers/game";

test("igniting the ball is visible in the snapshot after a deflect", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  // arm ignite, then wait until any ball reports ignited=true (it imbues on the next paddle bounce)
  await page.evaluate(() => (window as any).__game.castIgnite());
  await page.waitForFunction(
    () => (window as any).__game.getState().balls.some((b: any) => b.ignited),
    null, { timeout: 8000 }
  );
  const s = await getState(page);
  expect(s.balls.some((b: any) => b.ignited)).toBeTruthy();
});
