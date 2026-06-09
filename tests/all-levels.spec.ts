import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat, waitForPhase } from "./helpers/game";

// Validates EVERY shipped level: it must parse/load (blocks present) and be winnable.
// A malformed generated level (bad block id, wrong dimensions, missing legend) makes the
// backend LevelLoader throw → the WS connection yields no blocks → this test fails. So this
// is the safety net for hand- or AI-generated levels.
const LEVELS = [
  "hell-1", "hell-2", "hell-teleport", "hell-boss",
  "caverns-1", "caverns-2", "caverns-boss",
  "village-1", "village-2", "village-ghost", "village-boss",
  "heaven-1", "heaven-2",
  "hell-winnable",
];

for (const level of LEVELS) {
  test(`level "${level}" loads and is winnable`, async ({ page }) => {
    await openBattle(page, level);
    const s = await getState(page);
    expect(s, `level ${level} produced no snapshot`).toBeTruthy();
    expect(s.blocks.length, `level ${level} has no blocks (failed to load?)`).toBeGreaterThan(0);
    // every level must have at least one need-to-kill block (otherwise it wins instantly / is empty)
    await cheat(page, "winNow");
    await waitForPhase(page, "Won");
  });
}
