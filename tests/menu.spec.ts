import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/reset`);
});

test("menu is one journey: Continue + Campaign Map + docked icons, no Play/Dungeons/Editor", async ({ page }) => {
  await page.goto("/?scene=menu");
  await expect(page.locator("#menu h1")).toHaveText("ARKANOID RPG");

  // Primary + secondary entries exist.
  await expect(page.locator("#btn-continue")).toBeVisible();
  await expect(page.locator("#btn-campaign")).toBeVisible();

  // Docked secondary destinations.
  for (const id of ["#btn-characters", "#btn-inventory", "#btn-skills", "#btn-achievements", "#btn-settings"]) {
    await expect(page.locator(id)).toBeVisible();
  }

  // The redundant / player-irrelevant entries are gone.
  await expect(page.locator("#btn-play")).toHaveCount(0);
  await expect(page.locator("#btn-dungeons")).toHaveCount(0);
  await expect(page.locator("#btn-editor")).toHaveCount(0);
  // No loose level-chip grid.
  await expect(page.locator(".menu-quick-grid")).toHaveCount(0);
});

test("clicking Continue enters a battle", async ({ page }) => {
  await page.goto("/?scene=menu");
  await page.locator("#btn-continue").click();
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.length).toBeGreaterThan(0);
});

test("Continue resumes the furthest playable node", async ({ page }) => {
  // Fresh profile → furthest playable node is hell-1.
  await page.goto("/?scene=menu");
  await expect(page.locator("#btn-continue")).toHaveAttribute("data-level", "hell-1");
  await expect(page.locator("#continue-node-label")).toHaveText("Hell I");

  // Clear hell-1 → frontier advances to hell-2.
  await page.request.post(`${API}/complete?level=hell-1`);
  await page.goto("/?scene=menu");
  await expect(page.locator("#btn-continue")).toHaveAttribute("data-level", "hell-2", { timeout: 10_000 });
  await expect(page.locator("#continue-node-label")).toHaveText("Hell II");
});
