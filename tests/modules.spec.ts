import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

test("modules screen shows 4 slots + inventory; equip fills a slot", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  // Craft a couple of modules (cheat) so the inventory + slots are exercisable.
  await page.request.post(`${API}/modules/craft?def=void_ball`);
  await page.request.post(`${API}/modules/craft?def=cannon_module`);

  await page.goto("/?scene=modules");
  await expect(page.locator(".mod-root")).toBeVisible({ timeout: 10000 });
  await expect(page.locator(".slot")).toHaveCount(4);            // core/paddle/ball/field
  expect(await page.locator(".m-card").count()).toBeGreaterThanOrEqual(2);

  // Equip the first inventory module → its Equip button flips to "Equipped".
  const firstEquip = page.locator(".m-card .b.eq").first();
  await firstEquip.click();
  await expect(page.locator(".m-card .b.eq.on").first()).toBeVisible({ timeout: 5000 });
});

test("rerolling a module spends Module Cores (API)", async ({ page }) => {
  const pid = `mod${Date.now()}`;
  const h = { "X-Profile-Id": pid };
  await page.request.post(`${API}/modules/craft?def=void_ball`, { headers: h }); // grants 100 cores + a module
  const m = await (await page.request.get(`${API}/modules`, { headers: h })).json();
  const id = m.owned[0].id;
  const before = m.moduleCores;
  const r = await (await page.request.post(`${API}/modules/reroll?id=${id}`, { headers: h })).json();
  expect(r.ok).toBe(true);
  expect(r.moduleCores).toBe(before - m.rerollCost);
});
