import { test, expect } from "./helpers/fixtures";
import { openBattle, waitForPhase, cheat, getState } from "./helpers/game";

test("multi-floor collapse surfaces the next floor's blocks", async ({ page }) => {
  await openBattle(page, "caverns-4", 1);
  await waitForPhase(page, "Playing");
  const s0 = await getState(page);
  const floor0 = s0.floor, count0 = s0.blocks.length;
  // Clear the current floor → the next should slide in.
  await cheat(page, "clearAllButN", 0);
  await page.waitForFunction((f) => {
    const s = (window as any).__game.getState();
    return s.floor > f && s.blocks.length > 0;
  }, floor0, { timeout: 6000 });
  const s1 = await getState(page);
  console.log(`floor ${floor0}->${s1.floor}, blocks ${count0}->${s1.blocks.length}, floorCount=${s1.floorCount}`);
  expect(s1.floor).toBeGreaterThan(floor0);
  expect(s1.blocks.length).toBeGreaterThan(0);
});
