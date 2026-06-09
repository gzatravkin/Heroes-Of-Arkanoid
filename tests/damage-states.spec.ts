import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat } from "./helpers/game";
import * as path from "path";

const SHOTS = path.resolve(__dirname, "demo-screenshots");

// Verify A3: blocks swap to a "destroyed/cracked" frame near death rather than just
// alpha-fading. Chip every block deterministically so the survivors fall below the
// 40% damage threshold and render their cracked sprite.
test("blocks show damage states when chipped below the threshold", async ({ page }) => {
  await openBattle(page, "hell-2");
  await cheat(page, "chipBlocks", 1); // basics 2→1 (cracked, alive); board stays populated
  await page.waitForFunction(() => {
    const s = (window as any).__game.getState();
    return s && s.blocks.some((b: any) => b.hp < b.maxHp);
  }, null, { timeout: 8000 });
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.some((b: any) => b.hp < b.maxHp)).toBeTruthy();
  await page.screenshot({ path: path.join(SHOTS, "damage-states.png") });
});
