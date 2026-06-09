import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat } from "./helpers/game";
import type { Page } from "@playwright/test";

/** parseFloat of a fill element's inline width (kept as a plain percentage). */
async function fillPct(page: Page, sel: string): Promise<number> {
  return page.locator(sel).evaluate((el) => parseFloat((el as HTMLElement).style.width) || 0);
}

/** Read the 9-slice frame metrics of a bar's track (first child of the outer). */
async function trackMetrics(page: Page, barId: string) {
  return page.locator(`${barId} > div`).first().evaluate((el) => {
    const s = getComputedStyle(el);
    return {
      src: s.borderImageSource,
      left: parseFloat(s.borderLeftWidth),
      right: parseFloat(s.borderRightWidth),
      top: parseFloat(s.borderTopWidth),
      bottom: parseFloat(s.borderBottomWidth),
    };
  });
}

test("value bars are symmetric 3-slice: border-image frame with caps pinned to both ends", async ({ page }) => {
  await openBattle(page, "hell-1");
  for (const barId of ["#hud-mana", "#hud-lives", "#hud-balls"]) {
    const m = await trackMetrics(page, barId);
    expect(m.src, `${barId} frame uses a border-image sprite`).not.toBe("none");
    expect(m.left, `${barId} left cap present`).toBeGreaterThan(0);
    expect(m.right, `${barId} right cap present`).toBeGreaterThan(0);
    // Caps are symmetric left↔right and top↔bottom.
    expect(Math.abs(m.left - m.right), `${barId} caps symmetric L/R`).toBeLessThanOrEqual(1);
    expect(Math.abs(m.top - m.bottom), `${barId} caps symmetric T/B`).toBeLessThanOrEqual(1);
  }
});

test("HP bar tracks 100 / 50 / 0% fill", async ({ page }) => {
  await openBattle(page, "hell-1"); // no boss → Lives is stable (only boss hazards reduce it)

  // Pin the running max at 10 so 10/5/0 → 100/50/0%.
  await cheat(page, "setLives", 10);
  await page.waitForFunction(() => (window as any).__game.getState()?.lives === 10);
  expect(await fillPct(page, "#hud-lives-fill")).toBeGreaterThan(95);

  await cheat(page, "setLives", 5);
  await page.waitForFunction(() => (window as any).__game.getState()?.lives === 5);
  const lv = await fillPct(page, "#hud-lives-fill");
  expect(lv).toBeGreaterThan(45); expect(lv).toBeLessThan(55);

  await cheat(page, "setLives", 0);
  await page.waitForFunction(() => (window as any).__game.getState()?.lives === 0);
  expect(await fillPct(page, "#hud-lives-fill")).toBeLessThan(5);
});

test("spare-balls bar reflects the live spare-ball count (full / half / empty)", async ({ page }) => {
  await openBattle(page, "hell-1");

  // Read the live count and the fill together so a stray drain can't desync the
  // assertion — the bar must always equal spareBalls / maxBalls.
  const read = () => page.evaluate(() => ({
    balls: (window as any).__game.getState().spareBalls as number,
    fill: parseFloat((document.querySelector("#hud-balls-fill") as HTMLElement).style.width) || 0,
  }));

  // Pin the running max at 10 (default StartBalls is small, so clean halves need a wider scale).
  const MAX = 10;
  await cheat(page, "setBalls", MAX);
  await page.waitForFunction((m) => (window as any).__game.getState().spareBalls === m, MAX);
  expect((await read()).fill).toBeGreaterThan(95);          // full

  // Half. A stray drain can nudge the count by 1, so assert the bar matches the live
  // value exactly AND lands in the half band.
  await cheat(page, "setBalls", MAX / 2);
  await page.waitForFunction((m) => (window as any).__game.getState().spareBalls <= m / 2, MAX);
  const half = await read();
  expect(half.fill).toBeCloseTo((half.balls / MAX) * 100, 0); // bar matches live value
  expect(half.fill).toBeGreaterThan(35); expect(half.fill).toBeLessThan(55); // ~half

  // Empty.
  await cheat(page, "setBalls", 0);
  await page.waitForFunction(() => (window as any).__game.getState().spareBalls === 0);
  expect((await read()).fill).toBeLessThan(5);
});

test("boss bar tracks 100 / 50 / 0% fill", async ({ page }) => {
  await openBattle(page, "hell-boss");
  await expect(page.locator("#hud-boss-hp")).toBeVisible({ timeout: 10_000 });

  await cheat(page, "setBossHp", 100);
  await page.waitForFunction(() => parseFloat(
    (document.querySelector("#hud-boss-hp-fill") as HTMLElement)?.style.width || "0") > 95);

  await cheat(page, "setBossHp", 50);
  await page.waitForFunction(() => {
    const w = parseFloat((document.querySelector("#hud-boss-hp-fill") as HTMLElement)?.style.width || "0");
    return w > 45 && w < 55;
  });

  await cheat(page, "setBossHp", 0);
  await page.waitForFunction(() => parseFloat(
    (document.querySelector("#hud-boss-hp-fill") as HTMLElement)?.style.width || "100") < 5);
});
