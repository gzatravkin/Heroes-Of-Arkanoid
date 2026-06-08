import { test, expect } from "./helpers/fixtures";
import { openBattle, getState } from "./helpers/game";

test("HUD state exposes lives, spare balls, and mana", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  const s = await getState(page);
  expect(s.lives).toBeGreaterThan(0);
  expect(s.spareBalls).toBeGreaterThanOrEqual(0);
  expect(s.manaMax).toBeGreaterThan(0);
});
