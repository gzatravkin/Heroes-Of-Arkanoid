import { test } from "./helpers/fixtures";
import { openBattle, cheat, getState } from "./helpers/game";

// Visual verification of the Witchland ghost-phase rework (docs/2026-06-16-village-ghost-rework-spec.md):
// the renderer must communicate which layer the ball can hit RIGHT NOW. We park a stable ball, screenshot
// the NORMAL phase (ghost blocks faint, physical solid), then force the ball into GHOST phase and screenshot
// again (ball uses the BallGhost sprite + glow, ghost blocks solidify, physical dims).
const SHOTS = "demo-screenshots";

async function phaseShots(page: any, level: string) {
  await openBattle(page, level);
  await cheat(page, "parkBallAbovePaddle"); // → Playing, ball stable and out of the block field
  await cheat(page, "freezeBall");
  await page.waitForTimeout(350);
  await page.screenshot({ path: `${SHOTS}/${level}-A-normal.png` });

  await cheat(page, "setGhost", 1);
  await cheat(page, "freezeBall");
  await page.waitForTimeout(350);
  const s = await getState(page);
  // sanity: the sim really flipped phase
  if (!s.balls.every((b: any) => b.ghost)) throw new Error(`${level}: setGhost did not phase the ball`);
  await page.screenshot({ path: `${SHOTS}/${level}-B-ghost.png` });
}

test("village-6 phase (ghost intro)",   async ({ page }) => { await phaseShots(page, "village-6"); });
test("village-9 phase (ghost necromant)", async ({ page }) => { await phaseShots(page, "village-9"); });
test("village-11 phase (climax)",       async ({ page }) => { await phaseShots(page, "village-11"); });
