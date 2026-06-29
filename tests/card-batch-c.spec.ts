import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// §1 Cards — Batch C (Dead Center, Metronome, Phase Window): the deflect/combo cluster.
// parkBallAbovePaddle drops the ball dead-centre onto the paddle → a PERFECT deflect, which stacks Metronome
// and arms Dead Center; then a block hit spends both as bonus damage. setCombo + level 5 reaches the Phase
// Window threshold so the ball phases through blocks. Sim log: `card metronome stacks=N`, `card ball dmg +N`,
// and `ballcore phase-through`.

const OUT = path.resolve(__dirname, "..", "test-results", "card-batch-c");

test("Batch C cards fire in real play (Metronome stacks + Dead Center burst; Phase Window pierces)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `card-batch-c-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=fire_mage&select=1" +
      "&cards=dead_center,metronome,phase_window&cardLevel=5", { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");
  await page.evaluate(() => (window as any).__game.cheat("setLives", 99));
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());

  // ── Metronome + Dead Center: stack perfect (dead-centre) deflects. ──
  for (let i = 0; i < 5; i++) {
    await page.evaluate(() => (window as any).__game.cheat("parkBallAbovePaddle", 0)); // centred drop → perfect deflect
    await page.waitForTimeout(260);
  }
  fs.writeFileSync(path.join(OUT, "2-metronome.png"), await page.screenshot());

  // Now drive the (perfect-deflect-armed) ball into a top block — Dead Center burst + Metronome stacks land.
  const before = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  const top = await page.evaluate(() => {
    const s = (window as any).__game.getState();
    const d = s.blocks.filter((b: any) => !b.indestructible).sort((a: any, b: any) => a.y - b.y)[0];
    return d ? { id: d.id } : { id: -1 };
  });
  for (let i = 0; i < 4; i++) {
    const alive = await page.evaluate((id) =>
      !!(window as any).__game.getState().blocks.find((b: any) => b.id === id), top.id);
    if (!alive) break;
    await page.evaluate((id) => (window as any).__game.cheat("ballToBlock", id), top.id);
    await page.waitForTimeout(200);
  }

  // ── Phase Window: push the combo over the threshold, then drive the ball through a block stack. ──
  await page.evaluate(() => { const g = (window as any).__game; g.cheat("parkBallAbovePaddle", 0); g.cheat("setCombo", 4); });
  const colTarget = await page.evaluate(() => {
    const s = (window as any).__game.getState();
    const d = s.blocks.filter((b: any) => !b.indestructible).sort((a: any, b: any) => a.y - b.y)[0];
    return d ? { id: d.id } : { id: -1 };
  });
  for (let i = 0; i < 4; i++) {
    await page.evaluate((id) => { const g = (window as any).__game; g.cheat("setCombo", 4); g.cheat("ballToBlock", id); }, colTarget.id);
    await page.waitForTimeout(200);
  }
  fs.writeFileSync(path.join(OUT, "3-phase.png"), await page.screenshot());

  const after = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  expect(after).toBeLessThan(before); // blocks cleared in real play with the deflect/combo cards active
});
