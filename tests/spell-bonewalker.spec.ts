import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Bonewalker (design §3 rework of "Skeleton"): a summoned minion that WALKS THE ROOFTOPS of the block
// field, meleeing whatever block it stands on — NOT a paddle turret that sprays bone bolts on a timer.
// Real play: freeze the ball (so the ONLY block damage is the walker), summon it, and watch a skeletal
// figure stride along the top of the field chipping blocks. The sim log shows `bonewalker summon`.

const OUT = path.resolve(__dirname, "..", "test-results", "bonewalker");

test("Bonewalker strides the rooftops and melees blocks (not a turret)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `bonewalker-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // Necromancer with Bonewalker equipped (signature Raise locks slot 0 → Bonewalker is slot 1).
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=necromancer&select=1&loadout=skeleton,raise,drain",
      { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");

  // Freeze the ball so the walker's melee is the ONLY source of block damage.
  await page.evaluate(() => { const g = (window as any).__game; g.cheat("setMana", 100); g.cheat("freezeBall", 0); });
  const beforeHp = await page.evaluate(() =>
    (window as any).__game.getState().blocks.reduce((a: number, b: any) => a + b.hp, 0));
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());

  // Summon the Bonewalker — a skeletal minion perches on the rooftops.
  await page.evaluate(() => (window as any).__game.castSlot(1));
  await page.waitForFunction(() => ((window as any).__game.getState().minions?.length ?? 0) > 0,
    null, { timeout: 8_000 });
  const m0 = await page.evaluate(() => {
    const m = (window as any).__game.getState().minions[0];
    return { kind: m.kind, x: m.x };
  });
  fs.writeFileSync(path.join(OUT, "2-summon.png"), await page.screenshot());

  // Let it stride along the top of the field, meleeing what it walks over.
  await page.waitForTimeout(1700);
  fs.writeFileSync(path.join(OUT, "3-walking.png"), await page.screenshot());

  const after = await page.evaluate(() => {
    const s = (window as any).__game.getState();
    return {
      hpSum:   s.blocks.reduce((a: number, b: any) => a + b.hp, 0),
      minionX: s.minions?.[0]?.x ?? null,
      bullets: (s.projectiles ?? []).filter((p: any) => p.kind === "skeleton_bullet").length,
    };
  });

  expect(m0.kind).toBe("bonewalker");          // a minion entity, not a turret
  expect(after.bullets).toBe(0);               // it NEVER sprays bone bolts like the old skeleton
  expect(after.hpSum).toBeLessThan(beforeHp);  // it meleed rooftop blocks (ball frozen → only the walker)
  if (after.minionX !== null)
    expect(Math.abs(after.minionX - m0.x)).toBeGreaterThan(10); // it strode horizontally
});
