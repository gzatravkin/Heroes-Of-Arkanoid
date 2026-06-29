import { test, expect } from "./helpers/fixtures";
import { waitForPhase, cheat } from "./helpers/game";

const API = "http://localhost:5080";

test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/reset`);
  // This spec covers campaign progression only — suppress the probabilistic rift
  // so a random rift banner can't interleave (rifts are covered by rift.spec.ts).
  await page.addInitScript(() => localStorage.setItem("ark_rift_mode", "none"));
});

test("campaign map: hell-1 unlocked, caverns-1 locked after reset", async ({ page }) => {
  await page.goto("/?scene=campaign");
  await page.waitForSelector("#campaign-map [data-level]");
  await expect(page.locator('[data-level="hell-1"]')).toHaveAttribute("data-state", "unlocked");
  await expect(page.locator('[data-level="caverns-1"]')).toHaveAttribute("data-state", "locked");
});

test("campaign win flow: complete hell-1 → reward overlay → hell-2 unlocked", async ({ page }) => {
  // Navigate to campaign and enter hell-1 from campaign
  await page.goto("/?scene=campaign");
  await page.waitForSelector('[data-level="hell-1"]');
  await page.locator('[data-level="hell-1"]').click();

  // Wait for battle to be active
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await page.waitForFunction(() => (window as any).__game.getState()?.balls.length > 0);
  // Poke serve so winNow fires even if auto-serve timer hasn't fired yet.
  // waitForFunction avoids a GPU stall (unlike page.evaluate).
  await page.waitForFunction(() => { (window as any).__game?.serve?.(); return true; });

  // Cheat to win
  await cheat(page, "winNow");
  await waitForPhase(page, "Won");

  // Reward overlay should appear with some exp text
  await expect(page.locator("#reward-overlay")).toBeVisible({ timeout: 5000 });
  await expect(page.locator("#reward-exp")).toContainText("EXP");

  // Click continue to go back to campaign
  await page.locator("#btn-continue").click();

  // Now on campaign: hell-1 completed, hell-2 (its successor) unlocked
  await page.waitForSelector('#campaign-map [data-level="hell-1"]');
  await expect(page.locator('[data-level="hell-1"]')).toHaveAttribute("data-state", "completed");
  await expect(page.locator('[data-level="hell-2"]')).toHaveAttribute("data-state", "unlocked");
});

test("campaign defeat flow: losing shows the defeat overlay with a teaching tip", async ({ page }) => {
  const run = `defeat-${Date.now()}`;
  await page.goto(`/?scene=battle&level=hell-1&seed=1&from=campaign&run=${run}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await cheat(page, "loseNow");

  await page.waitForSelector("#defeat-overlay", { timeout: 5000 });
  // The death screen teaches: a gold TIP label + a non-empty tip line.
  await expect(page.locator("#defeat-overlay .ov-tip-label")).toHaveText("TIP");
  const tip = (await page.locator("#defeat-overlay .ov-tip").textContent()) ?? "";
  expect(tip.replace("TIP", "").trim().length).toBeGreaterThan(10);
  // Retry + Map are still the two actions.
  await expect(page.locator("#btn-retry")).toBeVisible();
  await expect(page.locator("#btn-map")).toBeVisible();
});
