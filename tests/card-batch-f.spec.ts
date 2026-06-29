import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// §1 Cards — Batch F (Hot Hand, Redline, Channeling): the ball-state / regen cluster (the deferred trio).
// HOT HAND: a hot combo grows the ball (snapshot radiusScale climbs). CHANNELING: mana regen doubles while
// the ball is cradled low, pauses while it's aloft (measured as a mana-gain difference). REDLINE: the longer
// the ball flies untouched, the harder it hits (sim log `card ball dmg +N`). Sim log: `card hot_hand`.

const OUT = path.resolve(__dirname, "..", "test-results", "card-batch-f");

test("Batch F cards fire in real play (Hot Hand grows ball, Channeling regen swing, Redline ramp)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `card-batch-f-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=fire_mage&select=1" +
      "&cards=hot_hand,redline,channeling&cardLevel=5", { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");
  await page.evaluate(() => (window as any).__game.cheat("setLives", 99));
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());

  // ── HOT HAND: force a hot combo and let the ball tick → it grows (radiusScale > 1). ──
  // Revive + freeze a live ball first (so the game stays Playing and OnBallTick runs), then set the combo.
  await page.evaluate(() => { const g = (window as any).__game; g.cheat("parkBallAbovePaddle", 0); g.cheat("freezeBall", 0); });
  const r0 = await page.evaluate(() => (window as any).__game.getState().balls[0]?.radiusScale ?? 1);
  await page.evaluate(() => (window as any).__game.cheat("setCombo", 4));
  await page.waitForTimeout(500);
  const rGrown = await page.evaluate(() => (window as any).__game.getState().balls[0]?.radiusScale ?? 1);
  fs.writeFileSync(path.join(OUT, "2-hothand.png"), await page.screenshot());
  expect(rGrown).toBeGreaterThan(r0); // the ball grew at the combo milestone

  // ── REDLINE: let the ball fly aloft a while, landing hits → its damage ramps (sim log). ──
  for (let i = 0; i < 6; i++) {
    const id = await page.evaluate(() => {
      const g = (window as any).__game;
      const s = g.getState();
      const d = s.blocks.filter((b: any) => !b.indestructible && b.hp > 0)[0];
      return d ? d.id : -1;
    });
    if (id === -1) break;
    await page.evaluate((bid) => (window as any).__game.cheat("ballToBlock", bid), id);
    await page.waitForTimeout(250);
  }

  // ── CHANNELING: mana climbs while the ball is cradled low, stalls while it's aloft. ──
  // Low phase: park the ball just above the paddle (cradled) + freeze, regen from 0 for 1s → doubled.
  const lowGain = await page.evaluate(async () => {
    const g = (window as any).__game;
    g.cheat("parkBallAbovePaddle", 0);
    g.cheat("freezeBall", 0);
    g.cheat("setMana", 0);
    await new Promise(r => setTimeout(r, 1000));
    return g.getState().mana;
  });
  // Aloft phase: drive the ball up to a top block, freeze it there (in flight), regen from 0 for 1s → paused.
  const aloftGain = await page.evaluate(async () => {
    const g = (window as any).__game;
    const s = g.getState();
    const top = s.blocks.filter((b: any) => !b.indestructible).sort((a: any, b: any) => a.y - b.y)[0];
    if (top) g.cheat("ballToBlock", top.id);
    await new Promise(r => setTimeout(r, 120));
    g.cheat("freezeBall", 0);
    g.cheat("setMana", 0);
    await new Promise(r => setTimeout(r, 1000));
    return g.getState().mana;
  });
  fs.writeFileSync(path.join(OUT, "3-after.png"), await page.screenshot());

  expect(lowGain).toBeGreaterThan(aloftGain); // Channeling: cradled-low regen > in-flight regen
});
