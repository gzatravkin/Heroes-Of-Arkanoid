import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat, waitForPhase } from "./helpers/game";

test("casting Turret with mana activates turret and fires bullets", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  await cheat(page, "setMana", 100);
  // Wait until snapshot reflects the setMana cheat.
  await page.waitForFunction(() => (window as any).__game.getState().mana >= 100);
  const before = (await getState(page)).mana;
  await page.evaluate(() => (window as any).__game.castTurret());
  // Wait until turretActive is true.
  await page.waitForFunction(() => (window as any).__game.getState().turretActive === true, null, { timeout: 8000 });
  // Wait until at least one turret bullet (id >= 10000) exists.
  await page.waitForFunction(
    () => (window as any).__game.getState().balls.some((b: any) => b.id >= 10000),
    null,
    { timeout: 10000 }
  );
  const after = await getState(page);
  expect(after.mana).toBeLessThan(before);
});
