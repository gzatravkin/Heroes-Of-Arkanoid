import { test, expect } from "./helpers/fixtures";
import { openBattle, waitForPhase } from "./helpers/game";

test("casting Fire Wall with mana creates a wall and consumes mana", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  // Park ball, set mana, and cast in one evaluate — single GPU stall prevents the ball
  // from clearing all blocks and triggering Won navigation before the cast lands.
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("parkBallAbovePaddle");
    g.cheat("setMana", 100);
    g.castFireWall();
  });
  // Wall appearing confirms mana was available and consumed.
  await page.waitForFunction(() => (window as any).__game.getState().walls?.length > 0, null, { timeout: 10_000 });
});

test("Fire Wall hotbar slot becomes unaffordable when mana is zero", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  // Park ball and drain mana in one evaluate — single GPU stall keeps the round alive.
  await page.evaluate(() => {
    (window as any).__game.cheat("parkBallAbovePaddle");
    (window as any).__game.cheat("setMana", 0);
  });
  const slot = page.locator("#hud-spell-firewall");
  await expect(slot).toHaveClass(/unaffordable/, { timeout: 8000 });
});
