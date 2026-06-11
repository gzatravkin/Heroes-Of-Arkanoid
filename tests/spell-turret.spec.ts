import { test } from "./helpers/fixtures";
import { openBattle, waitForPhase } from "./helpers/game";

test("casting Turret with mana activates turret and fires bullets", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  // Park ball, set mana, and cast in one evaluate — single GPU stall prevents the ball
  // from clearing all blocks and triggering Won navigation before the cast lands.
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("parkBallAbovePaddle");
    g.cheat("setMana", 100);
    g.castTurret();
  });
  await page.waitForFunction(() => (window as any).__game.getState().turretActive === true, null, { timeout: 8000 });
  await page.waitForFunction(
    () => (window as any).__game.getState().balls.some((b: any) => b.id >= 10000),
    null,
    { timeout: 10_000 }
  );
  // turret activating and firing confirms mana was consumed.
});
