import { test, expect } from "./helpers/fixtures";
import { openBattle } from "./helpers/game";

const API = "http://localhost:5080";

test.beforeEach(async ({ page }) => {
  await page.request.post(`${API}/character/select?id=fire_mage`);
});

// ── Ball-follow ───────────────────────────────────────────────────────────────
// During the Serving phase the ball must sit directly above the paddle center.
// Regression: previously the ball stayed at its initial spawn x while the paddle
// moved, making it impossible to aim the serve.
test("ball tracks paddle x during serving phase", async ({ page }) => {
  await openBattle(page, "hell-1");
  // openBattle waits for balls.length > 0 — game is still in Serving (not yet served).

  const boardW: number = await page.evaluate(
    () => (window as any).__game.getState()?.boardW ?? 400,
  );

  for (const frac of [0.2, 0.5, 0.8]) {
    const targetX = Math.round(boardW * frac);
    await page.evaluate((x) => window.__game.setPaddleX(x), targetX);

    // Wait for the round-trip: paddle settles near targetX AND ball x matches paddle x.
    await page.waitForFunction(
      (x) => {
        const s = (window as any).__game.getState();
        if (!s || s.phase !== "Serving" || !s.balls?.length) return false;
        return Math.abs(s.paddleX - x) < 8 && Math.abs(s.balls[0].x - s.paddleX) < 4;
      },
      targetX,
      { timeout: 5_000 },
    );

    const s = await page.evaluate(() => (window as any).__game.getState());
    expect(
      Math.abs(s.balls[0].x - s.paddleX),
      `ball x (${s.balls[0].x.toFixed(1)}) should match paddle x (${s.paddleX.toFixed(1)}) at ${Math.round(frac * 100)}% of board`,
    ).toBeLessThan(4);
  }
});

// ── SPA routing ───────────────────────────────────────────────────────────────
// The atlas (sprite sheet) must be fetched exactly once per browser session.
// Regression: without SPA routing every scene change triggered a full page reload,
// causing the atlas to re-download on every navigation (~2 s loading screen).
test("atlas is fetched exactly once regardless of scene navigations", async ({ page }) => {
  let atlasHits = 0;
  page.on("request", (req) => {
    if (req.url().includes("/atlas/")) atlasHits++;
  });

  // Initial load — atlas.json + atlas.png = 2 requests expected.
  await page.goto("/?scene=menu");
  await page.waitForFunction(
    () => (window as any).__atlas?.ready === true,
    null,
    { timeout: 10_000 },
  );
  const hitsAfterInit = atlasHits;
  expect(hitsAfterInit, "atlas must load at least once on startup").toBeGreaterThanOrEqual(1);

  // SPA navigate to campaign — triggers the popstate handler registered in main.ts.
  await page.evaluate(() => {
    window.history.pushState({}, "", "/?scene=campaign");
    window.dispatchEvent(new PopStateEvent("popstate"));
  });
  await page.waitForSelector("#campaign-map", { timeout: 8_000 });

  // SPA navigate back to menu.
  await page.evaluate(() => {
    window.history.pushState({}, "", "/?scene=menu");
    window.dispatchEvent(new PopStateEvent("popstate"));
  });
  await page.waitForSelector("#btn-continue", { timeout: 5_000 });

  expect(
    atlasHits,
    "atlas must not be re-fetched on SPA navigation — full page reload has regressed",
  ).toBe(hitsAfterInit);
});
