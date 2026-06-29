import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

test("Ascend resets the campaign loop and bumps the prestige badge", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.request.post(`${API}/complete?level=heaven-boss&rift=none`); // become eligible
  page.on("dialog", (d) => d.accept()); // auto-accept the Ascend confirm

  await page.goto("/?scene=campaign");
  await expect(page.locator("#btn-ascend")).toBeVisible({ timeout: 10000 });
  await page.locator("#btn-ascend").click();

  // Prestige badge appears at P1.
  await expect(page.locator("#prestige-badge")).toContainText("P1", { timeout: 10000 });
  // Campaign progress was wiped: hell-1 unlocked, caverns-1 locked again.
  await expect(page.locator('[data-level="hell-1"]')).toHaveAttribute("data-state", "unlocked");
  await expect(page.locator('[data-level="caverns-1"]')).toHaveAttribute("data-state", "locked");
});
