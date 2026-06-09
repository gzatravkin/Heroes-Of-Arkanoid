import { test } from "./helpers/fixtures";
import type { Page } from "@playwright/test";
import * as path from "path";

/** Wait until the scene fade-in overlay has fully cleared so shots aren't dimmed. */
async function settle(page: Page) {
  await page.waitForFunction(() => {
    const o = document.querySelector("#scene-transition-overlay") as HTMLElement | null;
    return !o || parseFloat(getComputedStyle(o).opacity) < 0.05;
  }, null, { timeout: 5000 }).catch(() => {});
}

// Captures mobile (390×844) proof screenshots for docs/06-shell-flow-overhaul.md.
// Each move appends its shots here so the artifacts are reproducible.
const SHOTS = path.resolve(__dirname, "demo-screenshots");
const API = "http://localhost:5080";

test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/reset`);
});

// ── Move 1: collapsed menu (Continue + Campaign Map + docked icons) ───────────
test("shot: move1 home menu (fresh profile)", async ({ page }) => {
  await page.goto("/?scene=menu");
  await page.waitForSelector("#btn-continue");
  // let the furthest-node label resolve
  await page.waitForFunction(() =>
    document.querySelector("#continue-node-label")?.textContent === "Hell I");
  await settle(page);
  await page.screenshot({ path: path.join(SHOTS, "move1-home-fresh.png") });
});

test("shot: move1 home menu (after clearing Hell I → Continue advances)", async ({ page }) => {
  await page.request.post(`${API}/complete?level=hell-1`);
  await page.goto("/?scene=menu");
  await page.waitForFunction(() =>
    document.querySelector("#continue-node-label")?.textContent === "Hell II");
  await settle(page);
  await page.screenshot({ path: path.join(SHOTS, "move1-home-advanced.png") });
});
