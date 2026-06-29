import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat, waitForPhase } from "./helpers/game";

test("hell-boss: has boss blocks and spawns hazards, then can be won", async ({ page }) => {
  await openBattle(page, "hell-boss", 1);

  // Verify at least one boss block exists in the snapshot.
  const s0 = await getState(page);
  expect(s0.blocks.some((b: any) => b.boss === true)).toBe(true);

  // Wait for the game to be in Playing state.
  await waitForPhase(page, "Playing");

  // Keep the ball alive: whenever the ball is about to drain, park it back above
  // the paddle so the game stays in Playing and the boss can fire hazards.
  // Also re-serve if the phase drops back to Serving.
  await page.evaluate(() => {
    const g = (window as any).__game;
    const interval = setInterval(() => {
      const s = g?.getState();
      if (!s) return;
      if (s.phase === "Serving") {
        g.serve();
        return;
      }
      if (s.phase === "Playing" && s.balls && s.balls.length > 0) {
        const ball = s.balls[0];
        // If ball is below the halfway point of the board and heading further down,
        // park it back above the paddle so it doesn't drain.
        if (ball.y > s.boardH * 0.8) {
          g.cheat("parkBallAbovePaddle");
        }
      }
    }, 100);
    (window as any).__autoServeInterval = interval;
  });

  // Wait for evidence the boss is attacking. The Demon's AimedShot is a FIST SLAM
  // (no hazard — it chips the paddle/column instead), so accept either a hazard in
  // flight (Rain/Spread rolled) or HP lost to a slam.
  await page.waitForFunction(
    () => {
      const s = (window as any).__game?.getState();
      return s && ((Array.isArray(s.hazards) && s.hazards.length > 0) || s.hp < 3);
    },
    null,
    { timeout: 20000 }
  );

  // Stop the keepalive interval.
  await page.evaluate(() => clearInterval((window as any).__autoServeInterval));

  // Cheat to win immediately.
  await cheat(page, "winNow");
  await waitForPhase(page, "Won");
});
