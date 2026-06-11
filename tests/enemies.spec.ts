import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat, waitForPhase } from "./helpers/game";
import * as path from "path";

const SHOTS = path.resolve(__dirname, "demo-screenshots");

// Use the deterministic fastForward cheat (freezes balls, advances N sim-ticks) so the
// emitter fires in sim-time regardless of the wall-clock tick rate under parallel load.
// `expectedKind` asserts the hazard carries its missile-art tag (renderer draws the
// original missile sprite from it — HellBallMissile / BeholderMissile / heaven Missile).
async function emitsHazard(
  page: import("@playwright/test").Page, level: string, name: string, expectedKind: string,
) {
  await openBattle(page, level);
  await cheat(page, "fastForward", 150); // ~2.5s sim → first emitter shot is mid-flight
  // Wait for the post-cheat snapshot: a kind-tagged hazard is in flight.
  await page.waitForFunction((kind) => {
    const s = (window as any).__game.getState();
    return s && s.hazards.some((h: any) => h.kind === kind);
  }, expectedKind, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, name) });
}

test("Hell ball-spawner emits hellball missiles at the paddle", async ({ page }) => {
  await emitsHazard(page, "hell-2", "enemy-hell-spawner.png", "hellball");
});

test("Witchland beholder fires beholder missiles at the ball", async ({ page }) => {
  await emitsHazard(page, "village-2", "enemy-beholder.png", "beholdermissile");
});

test("Heaven melee statue fires heaven missiles", async ({ page }) => {
  await emitsHazard(page, "heaven-1", "enemy-heaven-statue.png", "heavenmissile");
});

test("Heaven boss finale renders with the Heaven rig (not a fallback)", async ({ page }) => {
  await openBattle(page, "heaven-boss");
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.bossActive).toBeTruthy();
  // The renderer must build the Heaven rig (HeavenBoss + Globe art), not the Demon fallback.
  await page.waitForFunction(() => (window as any).__bossRigType === "Heaven", null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-heaven-boss.png") });
});

test("Witchland bat carries the ball toward the drain (costs a spare if unanswered)", async ({ page }) => {
  await openBattle(page, "village-1");
  // Drive the ball into the first bat block (public collision path via cheat).
  const batId = await page.evaluate(() => {
    const s = (window as any).__game.getState();
    return s.blocks.find((b: any) => b.sprite === "BatSleeping")?.id;
  });
  expect(batId, "village-1 must contain a bat block").toBeTruthy();
  const sparesBefore = await page.evaluate(() => (window as any).__game.getState().spareBalls);
  await cheat(page, "ballToBlock", batId);
  await cheat(page, "fastForward", 10); // collide → the bat snatches the ball
  await page.waitForFunction(() => {
    const s = (window as any).__game.getState();
    return s && s.hazards.some((h: any) => h.kind === "bat");
  }, null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-bat-flyaway.png") }); // bat carrying the ball
  // Unanswered, the carrier reaches the drain — the stolen ball costs a spare.
  await cheat(page, "fastForward", 1200);
  await page.waitForFunction((n) =>
    (window as any).__game.getState().spareBalls < n, sparesBefore, { timeout: 8000 });
});

test("Witch boss casts magic bolts (witchmagic hazards)", async ({ page }) => {
  await openBattle(page, "village-boss");
  expect((await page.evaluate(() => (window as any).__game.getState())).bossActive).toBeTruthy();
  // Keep lives topped up so the frozen ball can't lose the fight while the boss
  // drains the paddle — otherwise the game ends before we can observe the volley.
  await cheat(page, "setLives", 99);
  // Advance sim-time deterministically so the boss telegraphs + fires magic volleys.
  // (Her AimedShot is now the grab-hand, so magic bolts only come from Rain/Spread —
  // give the roll a few extra cycles and accept a bolt anywhere below the boss row.)
  await cheat(page, "fastForward", 400);
  await page.waitForFunction(() => {
    const s = (window as any).__game.getState();
    if (!s) return false;
    return s.hazards.some((h: any) => h.kind === "witchmagic" && h.y > s.boardH * 0.12);
  }, null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-witch-magic.png") });
});

test("Caverns bombs present (chain-explode block)", async ({ page }) => {
  await openBattle(page, "caverns-2");
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.length).toBeGreaterThan(0);
  await page.screenshot({ path: path.join(SHOTS, "enemy-caverns-bombs.png") });
});

test("Witchland ghost portal toggles the ball's phase", async ({ page }) => {
  await openBattle(page, "village-2");
  expect((await page.evaluate(() => (window as any).__game.getState()))
    .blocks.some((b: any) => b.sprite === "Portal")).toBeTruthy();
  await page.screenshot({ path: path.join(SHOTS, "enemy-ghost-portal.png") });
});

test("Heaven shield statue shields neighbours (immune flash)", async ({ page }) => {
  await openBattle(page, "heaven-2");
  expect((await page.evaluate(() => (window as any).__game.getState()))
    .blocks.some((b: any) => b.sprite === "HeavenDefender")).toBeTruthy();
  // fast-forward past a shield pulse, then a block should report shielded
  await cheat(page, "fastForward", 240);
  await page.waitForFunction(
    () => ((window as any).__game.getState()?.blocks ?? []).some((b: any) => b.shielded),
    null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-shield-statue.png") });
});

test("Heaven windmaster present (deflects the ball)", async ({ page }) => {
  await openBattle(page, "heaven-2");
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.some((b: any) => b.sprite === "WindMaster2"),
    "windmaster block should be in the level").toBeTruthy();
  await page.screenshot({ path: path.join(SHOTS, "enemy-windmaster.png") });
});

test("Witchland necromant present (revives destroyed blocks)", async ({ page }) => {
  await openBattle(page, "village-ghost");
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.blocks.some((b: any) => b.sprite === "VillageDeath"),
    "necromant block should be in the level").toBeTruthy();
  await page.screenshot({ path: path.join(SHOTS, "enemy-necromant.png") });
});

// ── Slice 1 (docs/11 §6 item 1): readability + danger-pays ────────────────────

test("emitter telegraphs before firing (charging flag + flash)", async ({ page }) => {
  await openBattle(page, "hell-2");
  // The spawner is charging for 0.5s of every 1.8s cycle — poll the live sim until
  // the window comes around (single-sample timing is phase-dependent).
  await page.waitForFunction(() =>
    (window as any).__game.getState()?.blocks
      .some((b: any) => b.sprite === "HellBallSpawner" && b.charging), null, { timeout: 20_000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-telegraph-charging.png") });
});

test("altar allies the statues — Active art state + held fire", async ({ page }) => {
  await openBattle(page, "heaven-1");
  const altarId = await page.evaluate(() => {
    const s = (window as any).__game.getState();
    return s.blocks.find((b: any) => b.sprite === "HeavenAltarV2")?.id;
  });
  expect(altarId, "heaven-1 must contain an altar").toBeTruthy();
  await cheat(page, "ballToBlock", altarId);
  await cheat(page, "fastForward", 5);
  await page.waitForFunction(() =>
    (window as any).__game.getState().blocks.some((b: any) => b.allied), null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-statues-allied.png") });
});

test("windmaster aura radius is exposed and drawn", async ({ page }) => {
  await openBattle(page, "heaven-2");
  const s = await page.evaluate(() => (window as any).__game.getState());
  expect(s.windRadius, "snapshot must carry the wind radius for the aura").toBeGreaterThan(0);
  expect(s.blocks.some((b: any) => b.sprite === "WindMaster2")).toBeTruthy();
  await page.screenshot({ path: path.join(SHOTS, "enemy-windmaster-aura.png") });
});

test("necromant marks and revives a killed block (end-to-end)", async ({ page }) => {
  // village-4 keeps TWO necromants behind a tough ring — the live ball can't
  // realistically cancel the revive by killing both during real-time gaps.
  await openBattle(page, "village-4");
  const brickId = await page.evaluate(() =>
    (window as any).__game.getState().blocks.find((b: any) => b.sprite === "VillageStandart")?.id);
  expect(brickId).toBeTruthy();
  // Kill the brick (hp 2). A driven ball can resolve against a NEIGHBOUR first
  // (list-order collision), so loop driven hits until the target id is gone.
  let dead = false;
  for (let i = 0; i < 12 && !dead; i++) {
    await cheat(page, "ballToBlock", brickId);
    await cheat(page, "fastForward", 3);
    dead = await page.evaluate((id) =>
      !(window as any).__game.getState().blocks.some((b: any) => b.id === id), brickId);
  }
  expect(dead, "target brick must die within 12 driven hits").toBeTruthy();
  const necroAlive = await page.evaluate(() =>
    (window as any).__game.getState().blocks.some((b: any) => b.sprite === "VillageDeath"));
  expect(necroAlive, "a necromant must still be alive for the revive").toBeTruthy();
  await page.screenshot({ path: path.join(SHOTS, "enemy-deathmark.png") }); // sphere over the corpse
  // The necromant revives it (delay default → fast-forward well past it).
  await cheat(page, "fastForward", 400);
  await page.waitForFunction((id) =>
    (window as any).__game.getState().blocks.some((b: any) => b.id === id), brickId, { timeout: 8000 });
});

test("killing an enemy block always drops a bonus (danger pays)", async ({ page }) => {
  await openBattle(page, "hell-2");
  await waitForPhase(page, "Playing");
  // Locate spawner and kill it (6 HP) in one evaluate — single GPU stall instead of 13.
  // parkBallAbovePaddle prevents Won navigation during the GPU stall.
  const killed = await page.evaluate(() => {
    const g = (window as any).__game;
    g.cheat("parkBallAbovePaddle"); // keep phase=Playing during the stall
    const block = g.getState().blocks.find((b: any) => b.sprite === "HellBallSpawner");
    if (!block) return false;
    for (let i = 0; i < 6; i++) {
      g.cheat("ballToBlock", block.id);
      g.cheat("fastForward", 3);
    }
    return true;
  });
  expect(killed, "hell-2 must contain a HellBallSpawner block").toBeTruthy();
  await page.waitForFunction(() =>
    (window as any).__game.getState().bonuses.length > 0, null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-danger-pays.png") });
});

test("Witchland cauldron bubbles in village-2 (mana siphon enemy)", async ({ page }) => {
  await openBattle(page, "village-2");
  const present = await page.evaluate(() =>
    (window as any).__game.getState().blocks.some((b: any) => b.sprite === "Kotelok1"));
  expect(present, "village-2 must contain cauldrons").toBeTruthy();
  await page.screenshot({ path: path.join(SHOTS, "enemy-cauldron.png") });
});

test("Hell lava spawner creeps new lava cells over time", async ({ page }) => {
  await openBattle(page, "hell-5");

  // Spawner only activates after taking its first hit — drive the ball into it first.
  const spawnerId = await page.evaluate(() => {
    const s = (window as any).__game.getState();
    return s.blocks.find((b: any) => b.sprite === "LavaSpowner")?.id ?? -1;
  });
  expect(spawnerId, "hell-5 must contain a LavaSpowner block").toBeGreaterThan(-1);

  // Deal exactly 1 HP without ball physics — ballToBlock oscillates (1px overlap keeps
  // the ball inside the block forever, killing the spawner in 2 ticks before we can poll).
  await cheat(page, "damageBlock", spawnerId);
  // Wait for the damage to land in the snapshot (hp 2→1, spawner still alive).
  await page.waitForFunction(
    (id) => {
      const s = (window as any).__game.getState();
      const blk = s?.blocks.find((b: any) => b.id === id);
      return blk && blk.hp < blk.maxHp;
    },
    spawnerId,
    { timeout: 5_000 },
  );

  const before = await page.evaluate(() => (window as any).__game.getState().blocks.length);
  // Two creep intervals (6s each @60Hz) — the field should grow.
  await cheat(page, "fastForward", 800);
  await page.waitForFunction((n) =>
    (window as any).__game.getState().blocks.length > n, before, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-lava-creep.png") });
});

test("Caverns stalactites fall as hazards", async ({ page }) => {
  await openBattle(page, "caverns-1");
  await cheat(page, "dropStalactites", 4);
  await cheat(page, "fastForward", 20); // let them descend a little for the shot
  await page.waitForFunction(
    () => ((window as any).__game.getState()?.hazards ?? []).some((h: any) => h.kind === "stalactite"),
    null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-stalactites.png") });
});
