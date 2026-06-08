import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat, waitForPhase } from "./helpers/game";

test("clearing the last block wins the level", async ({ page }) => {
  await openBattle(page, "hell-winnable", 1); // level has a single block
  await cheat(page, "winNow", 0);             // pre-setup forces the win path
  await waitForPhase(page, "Won");
});
