import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, waitForPhase } from "./helpers/game";

test("battle starts: blocks present and ball serves into play", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  const s0 = await getState(page);
  expect(s0.blocks.length).toBeGreaterThan(0);
  await waitForPhase(page, "Playing");           // auto-serve kicked in
  const s1 = await getState(page);
  expect(s1.balls[0].y).toBeLessThan(s0.boardH); // ball is on the board
});
