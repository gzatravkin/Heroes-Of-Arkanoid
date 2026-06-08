import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/reset`);
});

test("upgrade panel: upgrade fireball spell costs 1 point", async ({ page }) => {
  // Grant points by completing hell-1
  await page.request.post(`${API}/complete?level=hell-1`);

  await page.goto("/?scene=campaign");
  await page.waitForSelector("#btn-upgrade");

  // Open upgrade panel
  await page.locator("#btn-upgrade").click();
  await page.waitForSelector("#upgrade-panel");
  await expect(page.locator("#upgrade-panel")).toBeVisible();

  // Read initial spell level and points
  const initialLevel = await page.locator("#spell-level-fireball").textContent();
  expect(Number(initialLevel)).toBe(1);

  const pointsBefore = await page.locator("#profile-points").textContent();
  const pointsNumBefore = Number(pointsBefore?.replace(/\D+/g, "").trim());
  expect(pointsNumBefore).toBeGreaterThan(0);

  // Click upgrade fireball
  await page.locator("#btn-upgrade-fireball").click();

  // Wait for the level to update
  await expect(page.locator("#spell-level-fireball")).toHaveText("2", { timeout: 3000 });

  // Points should have decreased by 1
  const pointsAfter = await page.locator("#profile-points").textContent();
  const pointsNumAfter = Number(pointsAfter?.replace(/\D+/g, "").trim());
  expect(pointsNumAfter).toBe(pointsNumBefore - 1);
});
