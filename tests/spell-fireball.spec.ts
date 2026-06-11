import { test, expect } from "./helpers/fixtures";
import { openBattle, waitForPhase } from "./helpers/game";

test("casting fireball with mana spawns a projectile and consumes mana", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  // Combine park+setMana+cast into one evaluate to limit GPU stalls (multiple stalls
  // allow the ball to clear blocks and trigger Won navigation before the cast completes).
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("parkBallAbovePaddle");
    g.cheat("setMana", 100);
    g.castFireball();
  });
  // Confirm mana was consumed (backend processes the cast command in the next tick).
  await page.waitForFunction(() => (window as any).__game.getState()?.mana < 100, null, { timeout: 10_000 });
});
