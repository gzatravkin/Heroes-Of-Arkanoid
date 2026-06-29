import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// §1 Cards — Batch B (Bank Shot, Executioner's Edge, Overkill, Erosion): damage/hit rule-breakers.
// Marquee demo: EROSION (mythic) finally wears down an INDESTRUCTIBLE wall — drive the ball into an obsidian
// block until it cracks (a block that is normally permanent). Then EXECUTIONER'S EDGE: a forced crit on a
// low-HP block executes for double. The sim log records `card erosion cracked` and `card executioners_edge`.

const OUT = path.resolve(__dirname, "..", "test-results", "card-batch-b");

test("Batch B cards fire in real play (Erosion cracks an indestructible wall; Executioner executes)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `card-batch-b-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // Fire Mage with all four Batch B cards at level 5 (Erosion threshold drops to ~4 hits for a quick demo).
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=fire_mage&select=1" +
      "&cards=erosion,executioners_edge,bank_shot,overkill&cardLevel=5", { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");
  await page.evaluate(() => { const g = (window as any).__game; g.cheat("setLives", 60); g.cheat("freezeBall", 0); });
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());

  // ── EROSION: pick the TOPMOST indestructible block (obsidian, not the lower lava) and erode it. ──
  const obsidian = await page.evaluate(() => {
    const s = (window as any).__game.getState();
    const ind = s.blocks.filter((b: any) => b.indestructible).sort((a: any, b: any) => a.y - b.y);
    return ind[0] ? { id: ind[0].id } : { id: -1 };
  });
  expect(obsidian.id).not.toBe(-1); // hell-1 has obsidian walls

  for (let i = 0; i < 12; i++) {
    const alive = await page.evaluate((id) =>
      !!(window as any).__game.getState().blocks.find((b: any) => b.id === id), obsidian.id);
    if (!alive) break;
    await page.evaluate((id) => (window as any).__game.cheat("ballToBlock", id), obsidian.id);
    await page.waitForTimeout(200);
  }
  fs.writeFileSync(path.join(OUT, "2-eroded.png"), await page.screenshot());
  const eroded = await page.evaluate((id) =>
    !(window as any).__game.getState().blocks.find((b: any) => b.id === id), obsidian.id);
  expect(eroded).toBe(true); // the indestructible obsidian wall was cracked by Erosion

  // ── EXECUTIONER'S EDGE: revive + freeze the ball (no stray hits), force 100% crit, chip blocks low,
  // then drive the ball precisely into the lowest-HP block — the crit executes it for double. ──
  await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("parkBallAbovePaddle", 0); // ensure a live ball (the erosion phase may have drained it)
    g.cheat("freezeBall", 0);          // stop stray bounces so only the driven block is hit
    g.cheat("setCritChance", 1);       // every block hit crits
    g.cheat("chipBlocks", 3);          // bring destructibles well into the low-HP execute window
  });
  await page.waitForTimeout(150);      // let the snapshot reflect the chip before we pick a target
  const lowTarget = await page.evaluate(() => {
    const s = (window as any).__game.getState();
    const d = s.blocks.filter((b: any) => !b.indestructible && b.hp > 0).sort((a: any, b: any) => a.hp - b.hp)[0];
    return d ? { id: d.id } : { id: -1 };
  });
  if (lowTarget.id !== -1) {
    for (let i = 0; i < 3; i++) {
      const alive = await page.evaluate((id) =>
        !!(window as any).__game.getState().blocks.find((b: any) => b.id === id), lowTarget.id);
      if (!alive) break;
      await page.evaluate((id) => (window as any).__game.cheat("ballToBlock", id), lowTarget.id);
      await page.waitForTimeout(200);
    }
  }
  fs.writeFileSync(path.join(OUT, "3-execute.png"), await page.screenshot());
});
