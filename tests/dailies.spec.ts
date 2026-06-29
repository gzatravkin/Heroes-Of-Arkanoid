import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

test("daily board renders 3 missions with a streak meter", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.goto("/?scene=daily");
  await expect(page.locator(".daily-root")).toBeVisible({ timeout: 10000 });
  expect(await page.locator(".mission").count()).toBe(3);
  await expect(page.locator(".streak .pip")).toHaveCount(7);
});

test("completing a mission lets you claim it for a reward", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  // Complete every metric via the cheat record endpoint (default profile).
  for (const metric of ["blocks_destroyed", "levels_won", "battles_played"]) {
    await page.request.post(`${API}/daily/record?metric=${metric}&amount=100000`);
  }
  await page.goto("/?scene=daily");
  const claim = page.locator(".claim").first();
  await expect(claim).toBeVisible({ timeout: 5000 });
  await claim.click();
  await expect(page.locator(".flash")).toContainText("Gems", { timeout: 5000 });
});
