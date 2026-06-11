import { test } from "./helpers/fixtures";
import { openBattle, waitForPhase } from "./helpers/game";

test("igniting the ball is visible in the snapshot after a deflect", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  // Arm ignite + park ball + advance 3 ticks via waitForFunction (no GPU stall each).
  // fastForward:3 ensures the ball has definitely deflected off the paddle and been ignited
  // before the poll below, even under high parallel load.
  await page.waitForFunction(() => { (window as any).__game?.castIgnite(); return true; });
  await page.waitForFunction(() => { (window as any).__game?.cheat("parkBallAbovePaddle"); return true; });
  await page.waitForFunction(() => { (window as any).__game?.cheat("fastForward", 3); return true; });
  await page.waitForFunction(
    () => (window as any).__game?.getState()?.balls.some((b: any) => b.ignited),
    null, { timeout: 15_000 }
  );
  // waitForFunction passing confirms at least one ball is ignited.
});
