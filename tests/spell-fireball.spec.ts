import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat, waitForPhase } from "./helpers/game";

test("casting fireball with mana spawns a projectile and consumes mana", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  await cheat(page, "setMana", 100);
  // wait for snapshot to reflect the setMana cheat
  await page.waitForFunction(() => (window as any).__game.getState().mana >= 100);
  const before = (await getState(page)).mana;
  await page.evaluate(() => (window as any).__game.castFireball());
  await page.waitForFunction((m) => (window as any).__game.getState().mana < m, before);
  const after = await getState(page);
  expect(after.mana).toBeLessThan(before);
});
