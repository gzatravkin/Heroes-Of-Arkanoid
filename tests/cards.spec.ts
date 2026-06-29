import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";
const uid = () => `card${Date.now()}_${Math.floor(Math.random() * 1e6)}`;

test("cards screen renders the full catalog with starter cards equipped", async ({ page }) => {
  await page.request.post(`${API}/reset`); // default profile → NewDefault (molten_core equipped)
  await page.goto(`/?scene=cards`);
  await expect(page.locator(".cards-root")).toBeVisible({ timeout: 10000 });
  expect(await page.locator(".card").count()).toBeGreaterThanOrEqual(12);
  // At least one card is equipped (the starter molten_core), shown by the Equipped button.
  await expect(page.locator(".btn.equip.on").first()).toBeVisible({ timeout: 5000 });
});

test("equipping a card respects the slot cap (API)", async ({ page }) => {
  const pid = uid();
  const h = { "X-Profile-Id": pid };
  // New profile starts with molten_core equipped (1/3 slots). Grant + equip two more to fill, third fails.
  for (const c of ["arcane_battery", "quickstart", "vigor"]) {
    await page.request.post(`${API}/cards/grant?id=${c}`, { headers: h });
  }
  await page.request.post(`${API}/cards/equip?id=arcane_battery`, { headers: h });
  await page.request.post(`${API}/cards/equip?id=quickstart`, { headers: h }); // now 3/3
  const full = await (await page.request.post(`${API}/cards/equip?id=vigor`, { headers: h })).json();
  expect(full.ok).toBe(false); // slots full
  expect(full.equipped.length).toBe(3);
});
