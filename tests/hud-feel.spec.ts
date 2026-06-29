import { test, expect } from "./helpers/fixtures";
import { openBattle, waitForPhase, cheat } from "./helpers/game";

test("HUD: spell slot flashes when it becomes castable", async ({ page }) => {
  await page.request.post("http://localhost:5080/character/select?id=fire_mage");
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  await cheat(page, "parkBallAbovePaddle");
  await cheat(page, "freezeMana", 1);
  await cheat(page, "setMana", 0);
  await page.waitForTimeout(150);
  // Listen for the flash class being applied when mana jumps up.
  await cheat(page, "setMana", 100);
  // Poll briefly for any slot to gain the spell-ready class.
  const flashed = await page.waitForFunction(() =>
    !!document.querySelector(".hud-spell-slot.spell-ready"), null, { timeout: 2000 }
  ).then(() => true).catch(() => false);
  await page.screenshot({ path: "spell-ready.png" });
  expect(flashed).toBe(true);
});
