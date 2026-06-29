import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

// Shards (docs/04 §5): a meta-currency that drips even on dungeon death, spent on permanent unlocks.
test.beforeEach(async ({ page }) => { await page.request.post(`${API}/reset`); });
test.afterEach(async ({ page }) => { await page.request.post(`${API}/reset`); });

test("dungeon death drips shards — a failed run still earns permanent progress", async ({ page }) => {
  const chars0 = await (await page.request.get(`${API}/characters`)).json();
  expect(chars0.shards).toBe(0);

  await page.request.post(`${API}/dungeon/start?id=ember-depths`);
  const fail = await (await page.request.post(`${API}/dungeon/fail`)).json();
  expect(fail.shards).toBeGreaterThan(0);
});

test("shards permanently unlock a locked character", async ({ page }) => {
  const cost = (await (await page.request.get(`${API}/characters`)).json()).unlockCost as number;

  // Accumulate shards via repeated failed runs (all API, no gameplay).
  for (let i = 0; i < 15; i++) {
    await page.request.post(`${API}/dungeon/start?id=ember-depths`);
    await page.request.post(`${API}/dungeon/fail`);
  }

  const before = await (await page.request.get(`${API}/characters`)).json();
  expect(before.shards).toBeGreaterThanOrEqual(cost);
  expect(before.unlocked).not.toContain("paladin");

  const r = await (await page.request.post(`${API}/character/unlock?id=paladin`)).json();
  expect(r.ok).toBe(true);
  expect(r.unlocked).toContain("paladin");
  expect(r.shards).toBe(before.shards - cost);

  // A second unlock attempt with insufficient shards fails gracefully.
  const r2 = await (await page.request.post(`${API}/character/unlock?id=engineer`)).json();
  if (r.shards < cost) expect(r2.ok).toBe(false);
});
