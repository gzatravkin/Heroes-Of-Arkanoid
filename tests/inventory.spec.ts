import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

// Reset profile before AND after each test so state is clean
// and doesn't pollute subsequent tests that load a profile.
test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/reset`);
});

test.afterEach(async ({ page }) => {
  await page.request.post(`${API}/reset`);
});

test("inventory page loads with item grid and equipped slots", async ({ page }) => {
  await page.goto("/?scene=inventory");
  await expect(page.locator("#inventory-root")).toBeVisible({ timeout: 8000 });
  // Equipped row present
  await expect(page.locator("#inv-equipped-row")).toBeVisible({ timeout: 5000 });
  // Item grid has cards
  const cards = page.locator("#inv-grid .inv-card");
  await expect(cards.first()).toBeVisible({ timeout: 8000 });
  const count = await cards.count();
  expect(count).toBeGreaterThanOrEqual(10); // at least 10 items in catalog
});

test("crystals display reflects profile", async ({ page }) => {
  // Complete a level to earn crystals
  await page.request.post(`${API}/complete?level=hell-1`);
  await page.goto("/?scene=inventory");
  await expect(page.locator("#inv-crystals")).toBeVisible({ timeout: 8000 });
  const text = await page.locator("#inv-crystals").textContent();
  expect(text).toMatch(/💎\s*\d+/);
});

test("buy an item raises owned tier and deducts crystals", async ({ page }) => {
  // Give enough crystals: each unique level gives 10 crystals on first clear.
  // Cheapest item is 40 crystals — complete 6 different levels (60 crystals total).
  const levels = ["hell-1", "hell-teleport", "caverns-1", "village-1", "village-ghost", "heaven-1"];
  for (const lvl of levels) {
    await page.request.post(`${API}/complete?level=${lvl}`);
  }

  await page.goto("/?scene=inventory");
  await expect(page.locator("#inv-grid .inv-card").first()).toBeVisible({ timeout: 8000 });

  // Record initial crystal count
  const crystalsBefore = await page.locator("#inv-crystals").textContent();
  const numBefore = parseInt((crystalsBefore ?? "0").replace(/[^0-9]/g, ""), 10);

  // Find first Buy button that's not disabled
  const buyBtn = page.locator(".inv-buy-btn:not([disabled])").first();
  await expect(buyBtn).toBeVisible({ timeout: 5000 });
  const costText = await buyBtn.getAttribute("data-cost");
  const cost = parseInt(costText ?? "0", 10);

  await buyBtn.click();

  // Crystal count should drop
  await page.waitForFunction(
    ([id, before, c]: [string, number, number]) => {
      const el = document.getElementById(id);
      if (!el) return false;
      const n = parseInt((el.textContent ?? "0").replace(/[^0-9]/g, ""), 10);
      return n === before - c;
    },
    ["inv-crystals", numBefore, cost] as [string, number, number],
    { timeout: 5000 }
  );

  // The card should now show a tier badge T1
  const tierBadge = page.locator(".inv-tier-badge").first();
  await expect(tierBadge).toBeVisible({ timeout: 3000 });
  await expect(tierBadge).toContainText("T1");
});

test("equip and unequip an item updates equipped slots", async ({ page }) => {
  // Give crystals via distinct level first clears (10 each, cheapest item costs 40)
  const levels4 = ["hell-1", "hell-teleport", "caverns-1", "village-1", "village-ghost"];
  for (const lvl of levels4) await page.request.post(`${API}/complete?level=${lvl}`);

  // Buy via API directly so we don't depend on UI buy flow
  const profileRes = await page.request.get(`${API}/items`);
  const itemsData = await profileRes.json();
  // Find an affordable item
  const crystals: number = itemsData.crystals;
  const affordable = (itemsData.items as any[]).find((it: any) => it.ownedTier === 0 && it.cost[0] <= crystals);
  if (!affordable) {
    test.skip();
    return;
  }

  await page.request.post(`${API}/item/buy?id=${affordable.id}`);

  await page.goto("/?scene=inventory");
  await expect(page.locator("#inv-grid .inv-card").first()).toBeVisible({ timeout: 8000 });

  // Find and click Equip on the now-owned item
  const card = page.locator(`.inv-card[data-item-id="${affordable.id}"]`);
  await expect(card).toBeVisible({ timeout: 5000 });
  const equipBtn = card.locator(".inv-equip-btn");
  await expect(equipBtn).toBeVisible({ timeout: 3000 });
  expect(await equipBtn.getAttribute("data-equipped")).toBe("false");

  await equipBtn.click();

  // Equipped row should now show the item
  await expect(page.locator("#inv-equipped-row .inv-equip-slot-filled")).toBeVisible({ timeout: 5000 });

  // Button should now say Unequip
  await expect(card.locator(".inv-equip-btn")).toHaveAttribute("data-equipped", "true", { timeout: 3000 });

  // Unequip
  await card.locator(".inv-equip-btn").click();
  await expect(page.locator("#inv-equipped-row .inv-equip-slot-filled")).toHaveCount(0, { timeout: 5000 });
});

test("menu has Inventory button linking to ?scene=inventory", async ({ page }) => {
  await page.goto("/");
  const invBtn = page.locator("#btn-inventory");
  await expect(invBtn).toBeVisible({ timeout: 5000 });
  await invBtn.click();
  await expect(page).toHaveURL(/scene=inventory/, { timeout: 5000 });
});

test("GET /items returns catalog with ownedTier and crystals", async ({ page }) => {
  const res = await page.request.get(`${API}/items`);
  expect(res.status()).toBe(200);
  const data = await res.json();
  expect(data.items).toBeDefined();
  expect(data.items.length).toBeGreaterThan(0);
  expect(typeof data.crystals).toBe("number");
  // After reset: all ownedTier === 0
  for (const item of data.items) {
    expect(item.ownedTier).toBe(0);
    expect(item.equipped).toBe(false);
  }
});

test("POST /item/buy decrements crystals and raises tier", async ({ page }) => {
  // Give enough crystals: complete 5 unique levels → 50 crystals (enough for cheapest items at 40)
  const fiveLevels = ["hell-1", "hell-teleport", "caverns-1", "village-1", "village-ghost"];
  for (const lvl of fiveLevels) await page.request.post(`${API}/complete?level=${lvl}`);

  const itemsRes = await page.request.get(`${API}/items`);
  const itemsData = await itemsRes.json();
  const crystals: number = itemsData.crystals;
  const affordable = (itemsData.items as any[]).find((it: any) => it.cost[0] <= crystals);
  if (!affordable) return;

  const before = crystals;
  const cost = affordable.cost[0];

  const buyRes = await page.request.post(`${API}/item/buy?id=${affordable.id}`);
  const buyData = await buyRes.json();

  expect(buyData.ok).toBe(true);
  expect(buyData.ownedTier).toBe(1);
  expect(buyData.crystals).toBe(before - cost);
});

test("POST /item/equip caps at 3 slots", async ({ page }) => {
  // Give enough crystals to buy 4 items. Cheapest items cost 40-60 crystals each.
  // Complete many unique levels to accumulate crystals (10 per first clear).
  // We need ~240 crystals for 4 × 60 max-cost tier-1 items.
  const allLevels = [
    "hell-1", "hell-teleport", "caverns-1", "village-1", "village-ghost", "heaven-1",
    "level-a", "level-b", "level-c", "level-d", "level-e", "level-f",
    "level-g", "level-h", "level-i", "level-j", "level-k", "level-l",
    "level-m", "level-n", "level-o", "level-p", "level-q", "level-r",
  ];
  for (const lvl of allLevels) await page.request.post(`${API}/complete?level=${lvl}`);

  const itemsRes = await page.request.get(`${API}/items`);
  const data = await itemsRes.json();

  // Buy 4 cheapest items we can afford
  const sorted = (data.items as any[]).slice().sort((a: any, b: any) => a.cost[0] - b.cost[0]);
  const items = sorted.slice(0, 4);
  for (const it of items) {
    await page.request.post(`${API}/item/buy?id=${it.id}`);
  }

  // Equip first 3 — all ok
  for (let i = 0; i < 3; i++) {
    const r = await page.request.post(`${API}/item/equip?id=${items[i].id}`);
    const d = await r.json();
    expect(d.ok).toBe(true);
  }

  // 4th equip should fail
  const r4 = await page.request.post(`${API}/item/equip?id=${items[3].id}`);
  const d4 = await r4.json();
  expect(d4.ok).toBe(false);
  expect(d4.equipped.length).toBe(3);
});
