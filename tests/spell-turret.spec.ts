import { test } from "./helpers/fixtures";
import { openBattle, waitForPhase } from "./helpers/game";

// Design: the Fire Mage turret is paddle-mounted and fires a bolt on each ball-catch,
// NOT on a timer. See docs/specs/2026-06-12-fire-mage-spells.md.
test("casting Turret fires a bolt when the ball is deflected by the paddle", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");

  await page.waitForFunction(() => { (window as any).__game?.cheat("setMana", 100); return true; });
  await page.waitForFunction(() => { (window as any).__game?.castTurret(); return true; });
  await page.waitForFunction(
    () => (window as any).__game.getState().turretActive === true, null, { timeout: 8000 });

  // Drive the ball down into the paddle → the catch fires a turret bolt.
  await page.waitForFunction(() => { (window as any).__game?.cheat("parkBallAbovePaddle"); return true; });
  await page.waitForFunction(
    () => ((window as any).__game.getState().projectiles ?? []).some((p: any) => p.kind === "turret"),
    null, { timeout: 10_000 },
  );
});
