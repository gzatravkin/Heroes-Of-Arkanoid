import { test, expect } from "./helpers/fixtures";
import { cheat, waitForPhase } from "./helpers/game";

const API = "http://localhost:5080";

// Force rifts deterministically for the whole context (players roll probabilistically).
test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.addInitScript(() => localStorage.setItem("ark_rift_mode", "force"));
});

/** Clear hell-1 from the campaign, then dismiss the reward overlay (which carries the rift). */
async function clearHell1AndContinue(page: import("@playwright/test").Page) {
  await page.goto("/?scene=campaign");
  await page.waitForSelector('[data-level="hell-1"]');
  await page.locator('[data-level="hell-1"]').click();
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await page.waitForFunction(() => (window as any).__game.getState()?.balls.length > 0);
  await cheat(page, "winNow");
  await waitForPhase(page, "Won");
  await expect(page.locator("#reward-overlay")).toBeVisible({ timeout: 5000 });
  await page.locator("#btn-continue").click();
}

test("a rift opens after a campaign clear and Descend starts a dungeon run", async ({ page }) => {
  await clearHell1AndContinue(page);

  const banner = page.locator("#rift-banner");
  await expect(banner).toBeVisible({ timeout: 5000 });
  await expect(banner).toContainText("Rift");
  await expect(banner).toContainText("floors");

  await page.locator("#btn-rift-descend").click();

  // Descend → dungeon run scene with an active floor.
  await page.waitForURL(/scene=dungeon/, { timeout: 10_000 });
  await expect(page.locator("#dungeon-floor-progress")).toBeVisible({ timeout: 10_000 });

  // Server confirms an active run exists.
  const state = await page.request.get(`${API}/dungeon/state`).then((r) => r.json());
  expect(state.active).toBe(true);
});

test("Skip dismisses the rift and leaves campaign progress intact", async ({ page }) => {
  await clearHell1AndContinue(page);

  await expect(page.locator("#rift-banner")).toBeVisible({ timeout: 5000 });
  await page.locator("#btn-rift-skip").click();

  // Banner gone, URL cleaned back to the plain campaign.
  await expect(page.locator("#rift-banner")).toBeHidden({ timeout: 3000 });
  await expect(page).toHaveURL(/scene=campaign$/);

  // Campaign progress untouched: hell-1 completed, hell-2 unlocked.
  await expect(page.locator('[data-level="hell-1"]')).toHaveAttribute("data-state", "completed");
  await expect(page.locator('[data-level="hell-2"]')).toHaveAttribute("data-state", "unlocked");
});

test("the Dungeons menu is gone — dungeon runs are reached only via rifts", async ({ page }) => {
  await page.goto("/?scene=menu");
  await expect(page.locator("#btn-dungeons")).toHaveCount(0);
});
