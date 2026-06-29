import { test, expect } from "./helpers/fixtures";
import { openBattle, waitForPhase, cheat, getState } from "./helpers/game";

// FM6 playtest gate (docs/specs/2026-06-12-fire-mage-spells.md): cast each reworked
// Fire Mage spell in a live battle and capture a screenshot to review against the design.
// Phoenix must be a VISIBLE orbiting entity; the HUD HP bar must show real HP (T9 fix).

const API = "http://localhost:5080";

// These tests cast the Fire Mage kit — ensure the active character is fire_mage
// (another spec may have left the shared profile on a different class).
test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/character/select?id=fire_mage`);
});

test("Phoenix renders as a visible orbiting entity", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  await cheat(page, "clearAllButN", 18); // keep a field of blocks for it to sweep
  await cheat(page, "parkBallAbovePaddle"); // keep a ball alive + Playing so the cast lands
  await cheat(page, "setMana", 100);

  // Cast phoenix (its dedicated hardcoded test command, independent of the equipped loadout)
  // and confirm the entity exists in the snapshot.
  await page.waitForFunction(() => { (window as any).__game.castPhoenix(); return true; });
  await page.waitForFunction(
    () => ((window as any).__game.getState().phoenixes ?? []).length > 0,
    null, { timeout: 8000 });

  // Let it orbit a few frames, then capture.
  await page.waitForTimeout(600);
  await page.screenshot({ path: "playtest-phoenix.png" });

  const s = await getState(page);
  expect((s.phoenixes ?? []).length).toBeGreaterThan(0);
});

test("Fire Wall renders as a rising band", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  await cheat(page, "clearAllButN", 18);
  await cheat(page, "parkBallAbovePaddle"); // keep Playing so the cast lands
  await cheat(page, "setMana", 100);

  await page.waitForFunction(() => { (window as any).__game.castFireWall(); return true; });
  await page.waitForFunction(
    () => ((window as any).__game.getState().walls ?? []).length > 0,
    null, { timeout: 8000 });
  await page.waitForTimeout(400);
  await page.screenshot({ path: "playtest-firewall.png" });
});

test("HUD HP bar reflects real HP (T9 hp-rename drift fix)", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  await cheat(page, "setLives", 3);
  await page.waitForTimeout(200);

  // The HP count label in the HUD must show 3, not 0 (it previously read s.lives → undefined).
  const label = page.locator("#hud-lives .hud-bar-count");
  await expect(label).toHaveText("3", { timeout: 5000 });
  await page.screenshot({ path: "playtest-hud-full.png" });

  // Drop HP to 1 → the bar sprite should swap to the low (red) level.
  await cheat(page, "setLives", 1);
  await expect(label).toHaveText("1", { timeout: 5000 });
  await page.waitForTimeout(200);
  await page.screenshot({ path: "playtest-hud-low.png" });
});
