/**
 * achievement-unlocks.spec.ts — guards the boss / campaign-finale achievement wiring.
 *
 * `beat_boss` and `campaign_complete` were defined in the catalog but NEVER unlocked
 * by any code path (permanently unobtainable, including the tier-5 capstone). These
 * tests assert the trigger → identity contract so that regression can't slip back.
 *
 * Boss levels insta-attack the moment the game enters Playing, so an automated bot
 * is defeated before it can force a win. The `tutorial=1` flag forces the tutorial
 * overlay, which blocks the auto-serve and holds the game in Serving — the boss is
 * present (bossActive=true, so the flow latches `sawBoss`) but cannot attack. We
 * then `winNow` from Serving for a deterministic, defeat-free win.
 *
 * Mutates the shared default profile (reset per test); run with workers=1.
 */

import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

/** Win a boss level cleanly via the Serving-phase trick described above. */
async function winBossLevel(page: import("@playwright/test").Page, level: string) {
  await page.request.post(`${API}/reset`);
  const run = `${level}-ach-${Date.now()}`;
  await page.goto(`/?scene=battle&level=${level}&seed=1&from=campaign&tutorial=1&run=${run}`);
  await page.waitForFunction(() => !!(window as { __game?: { getState(): unknown } }).__game?.getState());
  // Boss must be present (latches sawBoss) AND we must still be in Serving (no attacks).
  await page.waitForFunction(
    () => {
      const s = (window as any).__game.getState();
      return s?.bossActive === true && s?.phase === "Serving";
    },
    null,
    { timeout: 8000 },
  );
  await page.evaluate(() => (window as any).__game.cheat("winNow", 0));
  await page.waitForFunction(
    () => (window as any).__game.getState()?.phase === "Won",
    null,
    { timeout: 8000 },
  );
}

/** A poll proxy over the profile's achievements — chain a matcher: `.toContain(id)`. */
function profileAchievements(page: import("@playwright/test").Page) {
  return expect.poll(
    async () => {
      const prof = await page.request.get(`${API}/profile`).then((r) => r.json());
      return (prof.achievements ?? []) as string[];
    },
    { timeout: 6000 },
  );
}

test.describe("achievement wiring", () => {
  test.afterAll(async ({ request }) => {
    // Leave the shared profile clean for other specs.
    await request.post(`${API}/reset`);
  });

  test("defeating a biome boss unlocks beat_boss", async ({ page }) => {
    await winBossLevel(page, "hell-boss");
    await profileAchievements(page).toContain("beat_boss");
  });

  test("defeating the heaven-boss finale unlocks campaign_complete", async ({ page }) => {
    await winBossLevel(page, "heaven-boss");
    // Poll until the finale unlock lands, then assert beat_boss came with it.
    await profileAchievements(page).toContain("campaign_complete");
    const a = await page.request.get(`${API}/profile`).then((r) => r.json());
    expect(a.achievements ?? []).toContain("beat_boss");
  });
});
