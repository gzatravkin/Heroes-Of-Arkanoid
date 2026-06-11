import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat, waitForPhase } from "./helpers/game";
import type { Page } from "@playwright/test";

const API = "http://localhost:5080";

// Ensure fire_mage character so the hotbar IDs are predictable across test runs.
test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/character/select?id=fire_mage`);
});

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

  // Combine state + DOM checks in waitForFunction (no GPU stall).
  // fillPct uses locator.evaluate() which stalls the GPU for 12-15s under parallel load —
  // during that window the ball can fall off and decrement lives before the fill is read.

  // Pin the running max at 10 so 10/5/0 → 100/50/0%.
  await cheat(page, "setLives", 10);
  await page.waitForFunction(() => {
    const fillW = parseFloat((document.querySelector("#hud-lives-fill") as HTMLElement | null)?.style.width || "0");
    return (window as any).__game.getState()?.lives === 10 && fillW > 95;
  }, null, { timeout: 10_000 });

  await cheat(page, "setLives", 5);
  await page.waitForFunction(() => {
    const fillW = parseFloat((document.querySelector("#hud-lives-fill") as HTMLElement | null)?.style.width || "0");
    const lives = (window as any).__game.getState()?.lives;
    return lives === 5 && fillW > 45 && fillW < 55;
  }, null, { timeout: 10_000 });

  await cheat(page, "setLives", 0);
  await page.waitForFunction(() => {
    const fillW = parseFloat((document.querySelector("#hud-lives-fill") as HTMLElement | null)?.style.width || "0");
    return (window as any).__game.getState()?.lives === 0 && fillW < 5;
  }, null, { timeout: 10_000 });
});

test("spare-balls bar reflects the live spare-ball count (full / half / empty)", async ({ page }) => {
  await openBattle(page, "hell-1");
  await waitForPhase(page, "Playing");
  // Freeze the ball velocity so it never falls during GPU stalls from cheat() calls.
  // fastForward:0 sets ball vel=(0,0) without advancing ticks; ball stays stationary.
  await cheat(page, "fastForward", 0);

  const MAX = 10;
  // Full — use waitForFunction (no GPU stall) to check both snapshot AND bar together.
  await cheat(page, "setBalls", MAX);
  await page.waitForFunction((m) => {
    const s = (window as any).__game.getState();
    const fill = parseFloat((document.querySelector("#hud-balls-fill") as HTMLElement)?.style.width || "0");
    return s?.spareBalls === m && fill > 95;
  }, MAX, { timeout: 10_000 });

  // Half — setBalls(5) and confirm bar lands in the 40-60% band.
  await cheat(page, "setBalls", MAX / 2);
  await page.waitForFunction((m) => {
    const s = (window as any).__game.getState();
    const fill = parseFloat((document.querySelector("#hud-balls-fill") as HTMLElement)?.style.width || "0");
    return s?.spareBalls === m / 2 && fill > 40 && fill < 60;
  }, MAX, { timeout: 10_000 });

  // Empty.
  await cheat(page, "setBalls", 0);
  await page.waitForFunction(() => {
    const s = (window as any).__game.getState();
    const fill = parseFloat((document.querySelector("#hud-balls-fill") as HTMLElement)?.style.width || "0");
    return s?.spareBalls === 0 && fill < 5;
  }, null, { timeout: 10_000 });
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
