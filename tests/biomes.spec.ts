import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat, waitForPhase } from "./helpers/game";

const BATTLE_LEVELS = [
  "caverns-1",
  "village-1",
  "heaven-1",
  "hell-teleport",
  "village-ghost",
] as const;

for (const level of BATTLE_LEVELS) {
  test(`${level}: blocks present and level is winnable`, async ({ page }) => {
    await openBattle(page, level);
    const s = await getState(page);
    expect(s.blocks.length).toBeGreaterThan(0);
    await cheat(page, "winNow");
    await waitForPhase(page, "Won");
  });
}

test("menu: level-select grid has caverns-1 button and navigates to battle", async ({ page }) => {
  await page.goto("/?scene=menu");
  const cavernsBtn = page.locator('[data-level="caverns-1"]');
  await expect(cavernsBtn).toBeVisible();
  await cavernsBtn.click();
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.length).toBeGreaterThan(0);
});
