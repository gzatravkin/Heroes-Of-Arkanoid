import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat, waitForPhase } from "./helpers/game";

test("casting Fire Wall with mana creates a wall and consumes mana", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  await cheat(page, "setMana", 100);
  // Wait until snapshot reflects the setMana cheat.
  await page.waitForFunction(() => (window as any).__game.getState().mana >= 100);
  const before = (await getState(page)).mana;
  await page.evaluate(() => (window as any).__game.castFireWall());
  // Wait until at least one fire wall appears.
  await page.waitForFunction(() => (window as any).__game.getState().walls?.length > 0, null, { timeout: 8000 });
  const after = await getState(page);
  expect(after.mana).toBeLessThan(before);
});

test("Fire Wall hotbar slot becomes unaffordable when mana is zero", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  // Drain mana to zero so the slot goes unaffordable.
  await cheat(page, "setMana", 0);
  // Wait until the HUD reflects no mana (class update happens on next snapshot tick).
  const slot = page.locator("#hud-spell-firewall");
  await expect(slot).toHaveClass(/unaffordable/, { timeout: 8000 });
});
