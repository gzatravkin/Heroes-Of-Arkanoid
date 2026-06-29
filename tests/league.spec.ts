import { test, expect } from "./helpers/fixtures";
import { waitForPhase, cheat } from "./helpers/game";

const API = "http://localhost:5080";

test("league ladder renders with a bot cohort and the player marked", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.goto("/?scene=league");
  await expect(page.locator(".lg-root")).toBeVisible({ timeout: 10000 });
  await expect(page.locator(".row")).toHaveCount(30);          // player + 29 bots
  await expect(page.locator(".row.me")).toHaveCount(1);         // exactly one "you"
  await expect(page.locator("#btn-play-trial")).toBeVisible();
});

test("playing the Weekly Trial submits a server-authoritative score to the ladder", async ({ page }) => {
  await page.request.post(`${API}/reset`);
  await page.goto("/?scene=league");
  await expect(page.locator("#btn-play-trial")).toBeVisible({ timeout: 10000 });
  await page.locator("#btn-play-trial").click();

  // Trial battle (server picks the level + weekly seed; mode=trial).
  await page.waitForFunction(() => !!(window as any).__game?.getState(), null, { timeout: 15000 });
  await page.waitForFunction(() => (window as any).__game.getState()?.balls?.length > 0, null, { timeout: 10000 });
  await page.waitForFunction(() => { (window as any).__game?.serve?.(); return true; });
  await waitForPhase(page, "Playing");
  await cheat(page, "winNow");
  await waitForPhase(page, "Won");

  // trialFlow returns to the league; the server already scored the run.
  await page.waitForURL("**/?scene=league*", { timeout: 12000 });
  await expect(page.locator(".lg-root")).toBeVisible({ timeout: 10000 });
  // My score is now the win bonus (>0), placing me at rank #1 over the Wood bots.
  const myScore = page.locator(".row.me .score");
  await expect(myScore).not.toHaveText("0", { timeout: 8000 });
});
