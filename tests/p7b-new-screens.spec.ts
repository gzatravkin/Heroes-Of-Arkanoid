/**
 * p7b-new-screens.spec.ts — Tests for tutorial, achievements, and settings screens.
 * Mobile 390×844 viewport.
 */

import { test, expect } from "./helpers/fixtures";
import * as path from "path";
import * as fs from "fs";

const API = "http://localhost:5080";

// ── Tutorial overlay ──────────────────────────────────────────────────────────

test("tutorial overlay: shows when ?tutorial=1 is passed", async ({ page }) => {
  await page.request.post(`${API}/reset`);

  // ?tutorial=1 forces the tutorial even in Playwright (webdriver) mode
  await page.goto("/?scene=battle&level=hell-1&tutorial=1");
  await page.waitForSelector("#tutorial-overlay", { timeout: 15000 });
  await expect(page.locator("#tutorial-overlay")).toBeVisible();
});

test("tutorial overlay: can be dismissed with Skip", async ({ page }) => {
  await page.goto("/?scene=battle&level=hell-1&tutorial=1");
  await page.waitForSelector("#tutorial-overlay", { timeout: 15000 });

  // Click skip
  await page.locator(".tut-skip").click();
  await expect(page.locator("#tutorial-overlay")).not.toBeVisible({ timeout: 3000 });
});

test("tutorial overlay: can advance slides with Next", async ({ page }) => {
  await page.goto("/?scene=battle&level=hell-1&tutorial=1");
  await page.waitForSelector("#tutorial-overlay", { timeout: 15000 });

  // First slide: Next button exists
  await expect(page.locator("#tut-btn-next")).toBeVisible();
  await page.locator("#tut-btn-next").click();

  // Second slide still shows tutorial
  await expect(page.locator("#tutorial-overlay")).toBeVisible();

  // Advance to third (last) slide
  await page.locator("#tut-btn-next").click();

  // Final slide: Done button
  await expect(page.locator("#tut-btn-done")).toBeVisible();

  // Click Done → overlay disappears
  await page.locator("#tut-btn-done").click();
  await expect(page.locator("#tutorial-overlay")).not.toBeVisible({ timeout: 3000 });
});

// ── Achievements screen ───────────────────────────────────────────────────────

test("achievements screen: renders and lists achievements", async ({ page }) => {
  await page.request.post(`${API}/reset`);

  await page.goto("/?scene=achievements");
  await page.waitForSelector("#achievements-scene", { timeout: 10000 });

  const scene = page.locator("#achievements-scene");
  await expect(scene).toBeVisible();

  // Should have a grid of achievement cards
  const grid = page.locator("#ach-grid");
  await expect(grid).toBeVisible();
  const cards = grid.locator(".ach-card");
  await expect(cards).toHaveCount(13, { timeout: 5000 }); // 13 defined achievements

  // Summary shows 0/13 at start
  const summary = page.locator("#ach-summary");
  await expect(summary).toContainText("0 / 13");
});

test("achievements screen: reachable from menu via button", async ({ page }) => {
  await page.goto("/?scene=menu");
  await expect(page.locator("#btn-achievements")).toBeVisible();
  await page.locator("#btn-achievements").click();
  await page.waitForSelector("#achievements-scene", { timeout: 10000 });
  await expect(page.locator("#achievements-scene")).toBeVisible();
});

// ── Settings screen ───────────────────────────────────────────────────────────

test("settings screen: renders with all controls", async ({ page }) => {
  await page.goto("/?scene=settings");
  await page.waitForSelector("#settings-scene", { timeout: 10000 });

  const scene = page.locator("#settings-scene");
  await expect(scene).toBeVisible();

  // Key buttons
  await expect(page.locator("#set-btn-replay")).toBeVisible();
  await expect(page.locator("#set-btn-reset")).toBeVisible();

  // Audio and FX toggles (input is hidden inside CSS toggle; check the label/slider is visible)
  await expect(page.locator("label[for='set-toggle-audio']")).toBeVisible();
  await expect(page.locator("label[for='set-toggle-fx']")).toBeVisible();
});

test("settings screen: reachable from menu via button", async ({ page }) => {
  await page.goto("/?scene=menu");
  await expect(page.locator("#btn-settings")).toBeVisible();
  await page.locator("#btn-settings").click();
  await page.waitForSelector("#settings-scene", { timeout: 10000 });
  await expect(page.locator("#settings-scene")).toBeVisible();
});

test("settings screen: replay tutorial button opens tutorial", async ({ page }) => {
  await page.goto("/?scene=settings");
  await page.waitForSelector("#set-btn-replay", { timeout: 10000 });
  await page.locator("#set-btn-replay").click();
  await expect(page.locator("#tutorial-overlay")).toBeVisible({ timeout: 3000 });
});

// ── Skills screen ─────────────────────────────────────────────────────────────

test("skills screen: renders with spell cards and level badges", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.goto("/?scene=skills");
  await page.waitForSelector("#skills-scene", { timeout: 10000 });

  await expect(page.locator("#skills-scene")).toBeVisible();
  // Class tabs
  await expect(page.locator("#sk-tabs")).toBeVisible();
  // Spell cards
  const cards = page.locator(".sk-spell-card");
  await expect(cards.first()).toBeVisible();
  // Level badges
  const badges = page.locator(".sk-lvl-badge");
  await expect(badges.first()).toBeVisible();
});

// ── Demo screenshots (mobile 390×844) ────────────────────────────────────────

const SCREENSHOT_DIR = path.join(__dirname, "demo-screenshots");

test("demo screenshot: p7-tutorial", async ({ page }) => {
  await page.goto("/?scene=battle&level=hell-1&tutorial=1");
  await page.waitForSelector("#tutorial-overlay", { timeout: 15000 });

  fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, "p7-tutorial.png") });
});

test("demo screenshot: p7-achievements", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.goto("/?scene=achievements");
  await page.waitForSelector("#ach-grid", { timeout: 10000 });
  await page.waitForTimeout(500);

  fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, "p7-achievements.png") });
});

test("demo screenshot: p7-settings", async ({ page }) => {
  await page.goto("/?scene=settings");
  await page.waitForSelector("#settings-scene", { timeout: 10000 });
  await page.waitForTimeout(300);

  fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, "p7-settings.png") });
});

test("demo screenshot: p7-skills", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.goto("/?scene=skills");
  await page.waitForSelector("#sk-spell-grid", { timeout: 10000 });
  await page.waitForTimeout(800);

  fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, "p7-skills.png") });
});
