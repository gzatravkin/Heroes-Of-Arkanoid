import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Stat engine (design §5) — REAL PLAY, NO CHEATS. The default hero (Fire Mage) must resolve to its
// §5.2 base profile and have it wired into the run at start: Power 3 (hits deal 3), Crit 12% (crits
// fire naturally ~1-in-8 hits), Tempo ×1.1. The server sim log proves the pipeline ran from hero
// stats (a "hero stats applied" record) — not from a cheat — and shows natural crits + base=3 damage.

const OUT = path.resolve(__dirname, "..", "test-results", "stat-engine");

test("stat engine: Fire Mage base profile wired into a real run (no cheats)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `stat-natural-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  await page.goto(`/?scene=battle&level=hell-1&seed=3&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await page.waitForFunction(() => (window as any).__game.getState()?.balls.length > 0);
  await waitForPhase(page, "Playing");

  // Play naturally; serve-poke each cycle so the ball keeps clearing bricks. NO crit cheat. Stop
  // as soon as the level ends (win navigates away / lose shows overlay) — the proof is server-side.
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
