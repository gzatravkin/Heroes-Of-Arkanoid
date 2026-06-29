import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Lance of Dawn (design §3 rework of Spear): drops a temporary SOLID pillar at the paddle's column that
// the ball banks off — NOT a piercing projectile. Real play: cast it and a pillar of light appears in
// the play lane (snapshot.pillars), with the sim log showing it spawned.

const OUT = path.resolve(__dirname, "..", "test-results", "lance");

test("Lance of Dawn drops a solid pillar (no projectile)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `lance-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // Paladin with Lance of Dawn equipped (signature Shield locks slot 0 → Lance is slot 1).
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=paladin&select=1&loadout=spear,shield,duplicate",
      { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());

  // Cast Lance of Dawn — a solid pillar appears in the play lane (and no projectile).
  await page.evaluate(() => { const g = (window as any).__game; g.cheat("setMana", 100); g.castSlot(1); });
  await page.waitForFunction(() => ((window as any).__game.getState().pillars?.length ?? 0) > 0, null, { timeout: 8_000 });
  await page.waitForTimeout(200);
  fs.writeFileSync(path.join(OUT, "2-pillar.png"), await page.screenshot());

  expect(await page.evaluate(() => (window as any).__game.getState().pillars.length)).toBeGreaterThan(0);
  expect(await page.evaluate(() => (window as any).__game.getState().projectiles?.length ?? 0)).toBe(0);
});
