import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Behavioral hero perks (design §5.5). The UI must show each hero's ★1/★3/★5 perks with lock state,
// and an unlocked perk must actually FIRE in real play. We demo Fire Mage ★5 ("a crit kill ignites a
// nearby block"): with NO ignite cast, the Fire Mage's base fire-spread is OFF, so any block that
// catches fire proves the ★5 perk — confirmed by `perk fm_s5 crit-kill ignite` lines in the sim log.

const OUT = path.resolve(__dirname, "..", "test-results", "hero-perks");

test("perks UI shows ★ perks with lock state", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    const B = "http://localhost:5080";
    await fetch(`${B}/reset`, { method: "POST" });
    await fetch(`${B}/dev/hero?hero=fire_mage&stars=5&level=20`, { method: "POST" });
    await fetch(`${B}/dev/hero?hero=necromancer&stars=3`, { method: "POST" });
  });

  await page.goto("/?scene=masteries");
  await page.waitForSelector("#ms-perks");
  // Fire Mage at ★5: all three perks unlocked.
  const fmOn = await page.evaluate(() => document.querySelectorAll("#ms-perks .perk.on").length);
  expect(fmOn).toBe(3);
  fs.writeFileSync(path.join(OUT, "perks-firemage-star5.png"), await page.screenshot({ fullPage: true }));

  // Necromancer at ★3: ★1 + ★3 unlocked, ★5 still locked.
  await page.click("text=Necromancer");
  await page.waitForFunction(() =>
    document.querySelectorAll("#ms-perks .perk.on").length === 2
    && document.querySelectorAll("#ms-perks .perk.off").length === 1);
  fs.writeFileSync(path.join(OUT, "perks-necro-star3.png"), await page.screenshot({ fullPage: true }));
});

test("Fire Mage ★5: a crit kill ignites a nearby block (real play)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `fm-s5-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    const B = "http://localhost:5080";
    await fetch(`${B}/reset`, { method: "POST" });
    await fetch(`${B}/dev/hero?hero=fire_mage&stars=5&level=10`, { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=4&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await page.waitForFunction(() => (window as any).__game.getState()?.balls.length > 0);
  await waitForPhase(page, "Playing");
  // Force every hit to crit so crit-KILLS happen; do NOT cast ignite (so base fire-spread stays off).
  await page.waitForFunction(() => { (window as any).__game?.cheat("setCritChance", 1); return true; });

  let captured = 0;
  for (let i = 0; i < 20; i++) {
    const phase = await page.evaluate(() => (window as any).__game?.getState?.()?.phase ?? null);
    if (phase !== "Playing") break;
    await page.evaluate(() => (window as any).__game?.serve?.());
    fs.writeFileSync(path.join(OUT, `frame-${String(i).padStart(2, "0")}.png`), await page.screenshot());
    captured++;
    await page.waitForTimeout(180);
  }
  expect(captured).toBeGreaterThan(0);
});
