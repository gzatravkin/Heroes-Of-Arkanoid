import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat, waitForPhase } from "./helpers/game";

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

  // drain mana to 0 → fill ~0%, fireball unaffordable.
  // Mana regen (14/s) refills continuously, so a post-wait re-read races regen
  // under parallel-worker load (TOCTOU). The waitForFunction observation IS the
  // assertion: capture what it saw via JSON return.
  await cheat(page, "setMana", 0);
  const drained = await page.waitForFunction(() => {
    const fill = document.querySelector("#hud-mana-fill") as HTMLElement | null;
    const fb   = document.querySelector("#hud-spell-fireball");
    if (!fill || !fb) return null;
    const w = parseFloat(fill.style.width ?? "100");
    return w < 25 && fb.classList.contains("unaffordable")
      ? JSON.stringify({ w, unaffordable: true }) : null;
  }, null, { timeout: 10_000 });
  const drainedSeen = JSON.parse((await drained.jsonValue()) as string);
  expect(drainedSeen.w).toBeLessThan(25);
  expect(drainedSeen.unaffordable).toBe(true);

  // set mana to 100 → fill ~100%, fireball affordable (no regen race upward: 100 is max)
  await cheat(page, "setMana", 100);
  await page.waitForFunction(() => {
    const fill = document.querySelector("#hud-mana-fill") as HTMLElement | null;
    const fb   = document.querySelector("#hud-spell-fireball");
    if (!fill || !fb) return false;
    const w = parseFloat(fill.style.width ?? "0");
    return w > 90 && fb.classList.contains("affordable");
  }, null, { timeout: 10_000 });

  const widthStr100 = await page.locator("#hud-mana-fill").evaluate((el: HTMLElement) => el.style.width);
  expect(parseFloat(widthStr100)).toBeGreaterThan(90);
  await expect(page.locator("#hud-spell-fireball")).toHaveClass(/affordable/);
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
