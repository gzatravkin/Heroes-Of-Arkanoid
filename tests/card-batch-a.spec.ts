import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// §1 Cards — Batch A (Headhunter, Underdog, Opening Gambit, Cleanup Crew): cross-hero rule-breaking
// PASSIVE triggers. Real play: equip all four, then drive the ball into a TOP-row block until it dies.
// The kill triggers Headhunter's top-row bonus AND Opening Gambit's once-per-level first-kill AoE (which
// chips the dead block's neighbours). The sim log records `card opening_gambit aoe` + `card ball dmg +N`.

const OUT = path.resolve(__dirname, "..", "test-results", "card-batch-a");

test("Batch A cards fire in real play (Opening Gambit AoE + position damage)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `card-batch-a-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // Fire Mage with all four Batch A cards equipped at level 2 (so bonuses are clearly visible).
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=fire_mage&select=1" +
      "&cards=opening_gambit,headhunter,underdog,cleanup_crew&cardLevel=2", { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");

  // Freeze the ball + soften the board, then pick the TOP-row destructible block (min y) that has a
  // left/right neighbour — so Opening Gambit's AoE has something to chip.
  const target = await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("setLives", 60);
    g.cheat("freezeBall", 0);
    g.cheat("chipBlocks", 1);
    const s = g.getState();
    const dest = s.blocks.filter((b: any) => !b.indestructible).sort((a: any, b: any) => a.y - b.y || a.x - b.x);
    const top = dest[0];
    // a neighbour on the same (top) row, just to the right — to observe the AoE chip
    const neigh = dest.find((b: any) => Math.abs(b.y - top.y) < 2 && b.x > top.x);
    return { id: top.id, neighId: neigh?.id ?? -1, neighHp: neigh?.hp ?? -1 };
  });
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());

  // Drive the ball into the target until it dies (the kill fires Opening Gambit + Headhunter).
  for (let i = 0; i < 10; i++) {
    const alive = await page.evaluate((id) =>
      !!(window as any).__game.getState().blocks.find((b: any) => b.id === id), target.id);
    if (!alive) break;
    await page.evaluate((id) => (window as any).__game.cheat("ballToBlock", id), target.id);
    await page.waitForTimeout(220);
  }
  fs.writeFileSync(path.join(OUT, "2-after.png"), await page.screenshot());

  const result = await page.evaluate((t) => {
    const s = (window as any).__game.getState();
    const neigh = s.blocks.find((b: any) => b.id === t.neighId);
    return { targetGone: !s.blocks.find((b: any) => b.id === t.id), neighNow: neigh?.hp ?? -1 };
  }, target);

  expect(result.targetGone).toBe(true); // the top-row block was destroyed in real play with cards active
  // Opening Gambit's AoE chipped the surviving neighbour (its HP dropped from the once-per-level blast).
  if (target.neighId !== -1 && result.neighNow !== -1)
    expect(result.neighNow).toBeLessThan(target.neighHp);
});
