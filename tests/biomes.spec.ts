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

test("menu → Campaign Map → unlocked node navigates into a battle", async ({ page }) => {
  await page.request.post("http://localhost:5080/reset");
  await page.goto("/?scene=menu");
  // Campaign Map is the level navigation now (the loose grid is gone).
  await page.locator("#btn-campaign").click();
  await page.waitForSelector('#campaign-map [data-level="hell-1"]');
  await page.locator('[data-level="hell-1"]').click();
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.length).toBeGreaterThan(0);
});
