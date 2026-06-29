import { test, expect } from "@playwright/test";
import { openBattle, waitForPhase, cheat } from "./helpers/game";

test("no console errors while exercising all the new render/feel features", async ({ page }) => {
  const errors: string[] = [];
  page.on("console", m => { if (m.type() === "error") errors.push(m.text()); });
  page.on("pageerror", e => errors.push("pageerror: " + e.message));

  await page.request.post("http://localhost:5080/character/select?id=fire_mage");
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");
  await cheat(page, "setMana", 100);
  // Exercise: phoenix (entity+layer), firewall (burning blocks), turret, fireball, ignite.
  await page.waitForFunction(() => { const g=(window as any).__game; g.castPhoenix(); g.castFireWall(); g.castTurret(); g.castFireball(); g.castIgnite(); return true; });
  // Combo floaters + chip sparks + danger vignette.
  await cheat(page, "setCombo", 4);
  await cheat(page, "chipBlocks", 1);
  await cheat(page, "setLives", 1);   // danger vignette
  await page.waitForTimeout(1500);    // let everything animate
  await cheat(page, "setLives", 2);
  await page.waitForTimeout(300);

  expect(errors, `console errors:\n${errors.join("\n")}`).toEqual([]);
});
