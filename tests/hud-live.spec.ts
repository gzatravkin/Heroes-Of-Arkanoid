import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat, waitForPhase } from "./helpers/game";

const API = "http://localhost:5080";

// Ensure fire_mage character is active before every test in this suite —
// some gallery/class-kit tests select other characters and may leave dirty state.
test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/character/select?id=fire_mage`);
});

test("HUD lives, balls and mana bar are present after battle opens", async ({ page }) => {
  await openBattle(page, "hell-1", 1);

  // Wait for HUD to receive at least one snapshot (data-lives set to non-zero or known value)
  await page.waitForFunction(
    () => document.querySelector("#hud-lives") !== null,
    null, { timeout: 10_000 }
  );

  // lives
  const livesEl = page.locator("#hud-lives");
  await expect(livesEl).toBeVisible({ timeout: 10_000 });
  expect(await livesEl.getAttribute("data-lives")).toBe("3");

  // spare balls
  const ballsEl = page.locator("#hud-balls");
  await expect(ballsEl).toBeVisible({ timeout: 10_000 });
  expect(await ballsEl.getAttribute("data-balls")).not.toBeNull();

  // mana bar container visible
  await expect(page.locator("#hud-mana")).toBeVisible({ timeout: 10_000 });
});

test("HUD mana fill and fireball affordability track setMana cheat", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  // Freeze ball velocity + mana regen + drain to 0 in one evaluate — single GPU stall.
  // fastForward:0 sets ball vel=(0,0) so it can't clear blocks during subsequent stalls.
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("fastForward", 0); // freeze ball
    g.cheat("freezeMana", 1);
    g.cheat("setMana", 0);
  });
  // waitForFunction has no GPU stall; confirms fill and affordability together.
  await page.waitForFunction(() => {
    const fill = document.querySelector("#hud-mana-fill") as HTMLElement | null;
    const fb   = document.querySelector("#hud-spell-fireball");
    if (!fill || !fb) return false;
    return parseFloat(fill.style.width ?? "100") < 25 && fb.classList.contains("unaffordable");
  }, null, { timeout: 10_000 });

  // Single evaluate — ball is frozen, so no block-clear risk during this GPU stall.
  await page.evaluate(() => (window as any).__game.cheat("setMana", 100));
  await page.waitForFunction(() => {
    const fill = document.querySelector("#hud-mana-fill") as HTMLElement | null;
    const fb   = document.querySelector("#hud-spell-fireball");
    if (!fill || !fb) return false;
    const w = parseFloat(fill.style.width ?? "0");
    return w > 90 && fb.classList.contains("affordable");
  }, null, { timeout: 10_000 });
});

test("HUD banner shows VICTORY on winNow cheat", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await cheat(page, "winNow", 0);
  await waitForPhase(page, "Won");
  await page.waitForFunction(() => {
    const b = document.querySelector("#hud-banner") as HTMLElement | null;
    return b && b.style.display !== "none";
  }, null, { timeout: 10_000 });
  const banner = page.locator("#hud-banner");
  await expect(banner).toBeVisible();
  await expect(banner).toContainText("VICTORY");
});

test("HUD banner shows DEFEAT on loseNow cheat", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await cheat(page, "loseNow", 0);
  await waitForPhase(page, "Lost");
  await page.waitForFunction(() => {
    const b = document.querySelector("#hud-banner") as HTMLElement | null;
    return b && b.style.display !== "none";
  }, null, { timeout: 10_000 });
  const banner = page.locator("#hud-banner");
  await expect(banner).toBeVisible();
  await expect(banner).toContainText("DEFEAT");
});
