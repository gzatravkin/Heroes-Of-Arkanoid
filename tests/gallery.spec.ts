import { test } from "./helpers/fixtures";
import type { Page } from "@playwright/test";
import { openBattle, cheat } from "./helpers/game";
import * as path from "path";

// Visual showcase: expanded UI + spell screenshots (requested for review).
// All shots land in demo-screenshots/ (wiped each run by global-setup).
const SHOTS = path.resolve(__dirname, "demo-screenshots");
const API = "http://localhost:5080";

async function settle(page: Page) {
  await page.waitForFunction(() => {
    const o = document.querySelector("#scene-transition-overlay") as HTMLElement | null;
    return !o || parseFloat(getComputedStyle(o).opacity) < 0.05;
  }, null, { timeout: 5000 }).catch(() => {});
}

test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/reset`);
});

// ── UI screens ────────────────────────────────────────────────────────────────
const UI: [scene: string, sel: string, name: string][] = [
  ["characters", "#character-scene", "ui-characters"],
  ["inventory",  "#inventory-root",  "ui-inventory"],
  ["skills",     "#skills-scene",    "ui-skills"],
  ["campaign",   "#campaign-map [data-level='hell-1']", "ui-campaign"],
];
for (const [scene, sel, name] of UI) {
  test(`shot: ${name}`, async ({ page }) => {
    await page.goto(`/?scene=${scene}`);
    await page.waitForSelector(sel, { timeout: 10_000 });
    await settle(page);
    await page.screenshot({ path: path.join(SHOTS, `${name}.png`) });
  });
}

test("shot: ui-dungeon-run", async ({ page }) => {
  await page.request.post(`${API}/dungeon/start?id=ember-depths`);
  await page.goto("/?scene=dungeon");
  await page.waitForSelector("#dungeon-floor-progress", { timeout: 10_000 });
  await settle(page);
  await page.screenshot({ path: path.join(SHOTS, "ui-dungeon-run.png") });
});

// ── Fire Mage spells in action ──────────────────────────────────────────────────
const FIRE_SPELLS: [name: string, cast: string][] = [
  ["spell-ignite",   "castIgnite"],
  ["spell-fireball", "castFireball"],
  ["spell-firewall", "castFireWall"],
  ["spell-turret",   "castTurret"],
];
for (const [name, cast] of FIRE_SPELLS) {
  test(`shot: ${name}`, async ({ page }) => {
    await openBattle(page, "hell-1");
    await cheat(page, "setMana", 100);
    await cheat(page, "parkBallAbovePaddle"); // keep the round alive while casting
    await page.evaluate((c) => (window as any).__game[c](), cast);
    await page.waitForTimeout(500); // let the effect render
    await page.screenshot({ path: path.join(SHOTS, `${name}.png`) });
  });
}

// ── Other classes' signature spells ─────────────────────────────────────────────
const CLASS_SPELLS: [char: string, slot: number, name: string][] = [
  ["paladin",     0, "spell-paladin-shield"],
  ["engineer",    0, "spell-engineer-lightning"],
  ["necromancer", 1, "spell-necromancer-skeleton"],
];
for (const [char, slot, name] of CLASS_SPELLS) {
  test(`shot: ${name}`, async ({ page }) => {
    await page.request.post(`${API}/character/select?id=${char}`);
    await openBattle(page, "hell-1");
    await cheat(page, "setMana", 100);
    await cheat(page, "parkBallAbovePaddle");
    await page.evaluate((s) => (window as any).__game.castSlot(s), slot);
    await page.waitForTimeout(500);
    await page.screenshot({ path: path.join(SHOTS, `${name}.png`) });
  });
}
