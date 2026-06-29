import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

test("season screen shows the theme, reward track and live event", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.request.post(`${API}/season/grant?tokens=250&event=70`); // default profile
  await page.goto("/?scene=season");
  await expect(page.locator(".se-root")).toBeVisible({ timeout: 10000 });
  await expect(page.locator(".theme")).not.toBeEmpty();
  await expect(page.locator(".event")).toBeVisible();
  expect(await page.locator(".tier").count()).toBeGreaterThanOrEqual(3);
});

test("claiming a season tier grants its reward (UI)", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.request.post(`${API}/season/grant?tokens=250&event=70`);
  await page.goto("/?scene=season");
  const claim = page.locator(".t-claim").first();
  await expect(claim).toBeVisible({ timeout: 5000 });
  await claim.click();
  // After claiming, at least one tier shows the claimed state.
  await expect(page.locator(".tier.claimed").first()).toBeVisible({ timeout: 5000 });
});

test("claiming the event milestone works (UI)", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.request.post(`${API}/season/grant?tokens=10&event=70`); // event milestone is 60
  await page.goto("/?scene=season");
  const claim = page.locator(".event .claim");
  await expect(claim).toBeVisible({ timeout: 5000 });
  await claim.click();
  await expect(page.locator(".event .done")).toBeVisible({ timeout: 5000 });
});
