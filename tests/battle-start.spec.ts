import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, waitForPhase } from "./helpers/game";

test("battle starts: blocks present and ball serves into play", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  const s0 = await getState(page);
  expect(s0.blocks.length).toBeGreaterThan(0);
  await waitForPhase(page, "Playing");
  // ball spawns on the paddle (below the grid) and climbs into the board within a few ticks
  await page.waitForFunction(() => {
    const s = (window as any).__game.getState();
    return s && s.balls.length > 0 && s.balls[0].y < s.boardH;
  }, null, { timeout: 10000 });
});
