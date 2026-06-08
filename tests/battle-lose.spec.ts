import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat, waitForPhase } from "./helpers/game";

test("running out of balls loses the level", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await cheat(page, "loseNow", 0);
  await waitForPhase(page, "Lost");
});
