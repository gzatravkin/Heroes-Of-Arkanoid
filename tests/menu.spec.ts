import { test, expect } from "./helpers/fixtures";

test("menu renders with a play button", async ({ page }) => {
  await page.goto("/?scene=menu");
  await expect(page.locator("#menu h1")).toHaveText("ARKANOID RPG");
  await expect(page.locator("#btn-play")).toBeVisible();
});

test("clicking play enters a battle", async ({ page }) => {
  await page.goto("/?scene=menu");
  await page.locator("#btn-play").click();
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.length).toBeGreaterThan(0);
});
