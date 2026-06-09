import { test } from "./helpers/fixtures";
import type { Page } from "@playwright/test";
import { openBattle, cheat } from "./helpers/game";
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

// ── Move 3: rift banner over the campaign map ─────────────────────────────────
test("shot: move3 rift banner over campaign map", async ({ page }) => {
  await page.goto("/?scene=campaign&rift=ember-depths&riftFloors=3&riftName=Ember%20Depths");
  await page.waitForSelector('#campaign-map [data-level="hell-1"]');
  await page.waitForSelector("#rift-banner.rift-banner-in");
  await settle(page);
  // let the slide-in transform finish
  await page.waitForFunction(() => {
    const b = document.querySelector("#rift-banner") as HTMLElement | null;
    return !!b && b.getBoundingClientRect().top > 40;
  });
  await page.screenshot({ path: path.join(SHOTS, "move3-rift-banner.png") });
});

// ── Move 4: HUD bars (HP / spare-balls / mana / boss) at 100 / 50 / 0% fill ───
for (const [name, lives, balls, mana] of [
  ["full", 10, 10, 100], ["half", 5, 5, 50], ["empty", 0, 0, 0],
] as const) {
  test(`shot: move4 value bars ${name} (HP/balls/mana)`, async ({ page }) => {
    await openBattle(page, "hell-1");
    await cheat(page, "setLives", lives);
    await cheat(page, "setBalls", balls);
    await cheat(page, "setMana", mana);
    await page.waitForFunction((lv) => (window as any).__game.getState()?.lives === lv, lives);
    await page.screenshot({ path: path.join(SHOTS, `move4-bars-${name}.png`) });
  });
}

for (const [name, hp] of [["full", 100], ["half", 50], ["empty", 0]] as const) {
  test(`shot: move4 boss bar ${name}`, async ({ page }) => {
    await openBattle(page, "hell-boss");
    await page.waitForSelector("#hud-boss-hp");
    await cheat(page, "setBossHp", hp);
    await page.waitForFunction((h) => {
      const w = parseFloat((document.querySelector("#hud-boss-hp-fill") as HTMLElement)?.style.width || "-1");
      return Math.abs(w - h) < 6;
    }, hp);
    await page.screenshot({ path: path.join(SHOTS, `move4-boss-${name}.png`) });
  });
}
