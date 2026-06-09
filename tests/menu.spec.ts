import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/reset`);
});

test("menu is one journey: Continue + Campaign Map + docked icons, no Play/Dungeons/Editor", async ({ page }) => {
  await page.goto("/?scene=menu");
  await expect(page.locator("#menu h1")).toHaveText("ARKANOID RPG");

  // Primary + secondary entries exist.
  await expect(page.locator("#btn-continue")).toBeVisible();
  await expect(page.locator("#btn-campaign")).toBeVisible();

  // Docked secondary destinations.
  for (const id of ["#btn-characters", "#btn-inventory", "#btn-skills", "#btn-achievements", "#btn-settings"]) {
    await expect(page.locator(id)).toBeVisible();
  }

  // The redundant / player-irrelevant entries are gone.
  await expect(page.locator("#btn-play")).toHaveCount(0);
  await expect(page.locator("#btn-dungeons")).toHaveCount(0);
  await expect(page.locator("#btn-editor")).toHaveCount(0);
  // No loose level-chip grid.
  await expect(page.locator(".menu-quick-grid")).toHaveCount(0);
});

test("menu → Campaign Map → unlocked node navigates into a battle", async ({ page }) => {
  await page.goto("/?scene=menu");
  // Campaign Map is the level navigation now (the loose grid is gone).
  await page.locator("#btn-campaign").click();
  await page.waitForSelector('#campaign-map [data-level="hell-1"]');
  await page.locator('[data-level="hell-1"]').click();
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.length).toBeGreaterThan(0);
});

test("clicking Continue enters a battle", async ({ page }) => {
  await page.goto("/?scene=menu");
  await page.locator("#btn-continue").click();
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.length).toBeGreaterThan(0);
});

test("menu controls meet the ≥44px mobile touch-target floor (WCAG 2.5.5)", async ({ page }) => {
  await page.goto("/?scene=menu");
  const ids = ["#btn-continue", "#btn-campaign",
    "#btn-characters", "#btn-inventory", "#btn-skills", "#btn-achievements", "#btn-settings"];
  for (const id of ids) {
    const box = await page.locator(id).boundingBox();
    expect(box, `${id} has no box`).toBeTruthy();
    expect(box!.width,  `${id} width ≥ 44`).toBeGreaterThanOrEqual(44);
    expect(box!.height, `${id} height ≥ 44`).toBeGreaterThanOrEqual(44);
  }
});

test("every docked destination navigates to its scene", async ({ page }) => {
  const dest: Record<string, string> = {
    "#btn-characters":   "scene=characters",
    "#btn-inventory":    "scene=inventory",
    "#btn-skills":       "scene=skills",
    "#btn-achievements": "scene=achievements",
    "#btn-settings":     "scene=settings",
    "#btn-campaign":     "scene=campaign",
  };
  for (const [id, frag] of Object.entries(dest)) {
    await page.goto("/?scene=menu");
    await page.locator(id).click();
    await page.waitForURL(new RegExp(frag.replace("=", "=")), { timeout: 10_000 });
    expect(page.url()).toContain(frag);
  }
});

test("Continue resumes the furthest playable node", async ({ page }) => {
  // Fresh profile → furthest playable node is hell-1.
  await page.goto("/?scene=menu");
  await expect(page.locator("#btn-continue")).toHaveAttribute("data-level", "hell-1");
  await expect(page.locator("#continue-node-label")).toHaveText("Hell I");

  // Clear hell-1 → frontier advances to hell-2.
  await page.request.post(`${API}/complete?level=hell-1`);
  await page.goto("/?scene=menu");
  await expect(page.locator("#btn-continue")).toHaveAttribute("data-level", "hell-2", { timeout: 10_000 });
  await expect(page.locator("#continue-node-label")).toHaveText("Hell II");
});
