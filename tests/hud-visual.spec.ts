/**
 * Visual regression for the HUD bars (HP, spare-balls, mana).
 *
 * These tests snapshot only the bar elements — not the game canvas — so they
 * are not affected by non-deterministic block layouts or particle effects.
 * Golden images live in hud-visual.spec.ts-snapshots/ and must be committed.
 *
 * To regenerate baselines after an intentional visual change:
 *   cd tests && npx playwright test hud-visual --update-snapshots
 */
import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat } from "./helpers/game";

const API = "http://localhost:5080";

test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/character/select?id=fire_mage`);
});

for (const [label, lives, balls, mana] of [
  ["full", 10, 10, 100],
  ["half",  5,  5,  50],
  ["low",   1,  1,   5],
] as const) {
  test(`HUD bars visual — ${label} fill`, async ({ page }) => {
    await openBattle(page, "hell-1");

    // Freeze mana regen so fill stays stable during the snapshot.
    await page.evaluate(() => (window as any).__game.cheat("freezeMana", 1));

    await page.evaluate(
      ([lv, bl, mn]) => {
        const g = (window as any).__game;
        g.cheat("setLives", lv);
        g.cheat("setBalls", bl);
        g.cheat("setMana", mn);
      },
      [lives, balls, mana] as const,
    );

    // Wait for all three values to land in the sim state simultaneously.
    await page.waitForFunction(
      ([lv, mn]) => {
        const s = (window as any).__game.getState();
        return s?.lives === lv && s?.mana <= mn + 2;
      },
      [lives, mana] as const,
      { timeout: 10_000 },
    );

    // Snapshot the top-left panel (HP + spare-balls bars).
    await expect(page.locator(".hud-top-left")).toHaveScreenshot(
      `hud-topleft-${label}.png`,
      { maxDiffPixelRatio: 0.03 },
    );

    // Snapshot the bottom mana bar.
    await expect(page.locator("#hud-mana")).toHaveScreenshot(
      `hud-mana-${label}.png`,
      { maxDiffPixelRatio: 0.03 },
    );
  });
}
