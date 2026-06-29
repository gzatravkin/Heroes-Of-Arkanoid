import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Bone Golem (design §3 fix): a summoned BODYGUARD minion that rises from the paddle, climbs a single
// column bulldozing its blocks, and TANKS ENEMY FIRE (soaks hazards that would reach the paddle) — NOT a
// fat piercing projectile. Real play: freeze the ball, summon the golem, drop an enemy bolt onto it (the
// golem bodies it so the player takes no damage), and watch it climb + bulldoze its column. Sim log: `golem summon`.

const OUT = path.resolve(__dirname, "..", "test-results", "golem");

test("Bone Golem climbs a column, bulldozes it, and bodies enemy fire", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `golem-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // Necromancer with Bone Golem equipped (signature Raise locks slot 0 → Golem is slot 1).
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=necromancer&select=1&loadout=golem,raise,drain",
      { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-1&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");

  // Freeze the ball so the golem's bulldozing is the ONLY source of block damage.
  await page.evaluate(() => { const g = (window as any).__game; g.cheat("setMana", 100); g.cheat("freezeBall", 0); });
  const beforeHp = await page.evaluate(() =>
    (window as any).__game.getState().blocks.reduce((a: number, b: any) => a + b.hp, 0));
  const playerHp0 = await page.evaluate(() => (window as any).__game.getState().hp);
  fs.writeFileSync(path.join(OUT, "1-before.png"), await page.screenshot());

  // Summon the Bone Golem — a bony bodyguard rises in the paddle's column.
  await page.evaluate(() => (window as any).__game.castSlot(1));
  await page.waitForFunction(() => ((window as any).__game.getState().minions?.length ?? 0) > 0,
    null, { timeout: 8_000 });
  const m0 = await page.evaluate(() => {
    const m = (window as any).__game.getState().minions[0];
    return { kind: m.kind, y: m.y, hp: m.hp, maxHp: m.maxHp };
  });
  fs.writeFileSync(path.join(OUT, "2-summon.png"), await page.screenshot());

  // Drop an enemy bolt onto the golem — it should body the shot (player takes no damage).
  await page.evaluate(() => (window as any).__game.cheat("spawnEnemyBolt", 0));
  await page.waitForTimeout(500);
  const tanked = await page.evaluate(() => {
    const s = (window as any).__game.getState();
    return { golemHp: s.minions?.[0]?.hp ?? null, playerHp: s.hp, hazards: (s.hazards ?? []).length };
  });
  fs.writeFileSync(path.join(OUT, "3-tank.png"), await page.screenshot());

  // Let it climb + bulldoze its column.
  await page.waitForTimeout(2200);
  fs.writeFileSync(path.join(OUT, "4-climb.png"), await page.screenshot());

  const after = await page.evaluate(() => {
    const s = (window as any).__game.getState();
    return {
      hpSum: s.blocks.reduce((a: number, b: any) => a + b.hp, 0),
      golemProj: (s.projectiles ?? []).filter((p: any) => p.kind === "golem").length,
      minionY: s.minions?.[0]?.y ?? null,
    };
  });

  expect(m0.kind).toBe("golem");               // a minion entity…
  expect(m0.maxHp).toBeGreaterThan(0);         // …with a fire-soak HP pool
  expect(after.golemProj).toBe(0);             // NOT a piercing projectile
  // It tanked the enemy bolt: golem soaked HP, the player took NO damage.
  if (tanked.golemHp !== null) expect(tanked.golemHp).toBeLessThan(m0.hp);
  expect(tanked.playerHp).toBe(playerHp0);
  // It climbed (y decreased from spawn) and bulldozed its column (block HP dropped, ball frozen).
  if (after.minionY !== null) expect(after.minionY).toBeLessThan(m0.y);
  expect(after.hpSum).toBeLessThan(beforeHp);
});
