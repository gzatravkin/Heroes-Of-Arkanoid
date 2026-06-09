import { test, expect } from "./helpers/fixtures";
import { openBattle, cheat } from "./helpers/game";
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

test("Witchland bat grabs the ball, then a flyaway bat departs upward", async ({ page }) => {
  await openBattle(page, "village-1");
  // Drive the ball into the first bat block (public collision path via cheat).
  const batId = await page.evaluate(() => {
    const s = (window as any).__game.getState();
    return s.blocks.find((b: any) => b.sprite === "BatSleeping")?.id;
  });
  expect(batId, "village-1 must contain a bat block").toBeTruthy();
  await cheat(page, "ballToBlock", batId);
  await cheat(page, "fastForward", 10); // collide + grab
  // Hold expires (BatHoldTime 2s @120Hz) → harmless flyaway hazard kind="bat" appears.
  await cheat(page, "fastForward", 260);
  await page.waitForFunction(() => {
    const s = (window as any).__game.getState();
    return s && s.hazards.some((h: any) => h.kind === "bat");
  }, null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-bat-flyaway.png") });
});

test("Witch boss casts magic bolts (witchmagic hazards)", async ({ page }) => {
  await openBattle(page, "village-boss");
  expect((await page.evaluate(() => (window as any).__game.getState())).bossActive).toBeTruthy();
  // Keep lives topped up so the frozen ball can't lose the fight while the boss
  // drains the paddle — otherwise the game ends before we can observe the volley.
  await cheat(page, "setLives", 99);
  // Advance sim-time deterministically so the boss telegraphs + fires magic volleys.
  await cheat(page, "fastForward", 130);
  // Wait until a magic bolt is in the open mid-field (below the top block rows, above
  // the paddle) so the proof screenshot shows it clearly rather than under the boss.
  await page.waitForFunction(() => {
    const s = (window as any).__game.getState();
    if (!s) return false;
    const lo = s.boardH * 0.3, hi = s.boardH * 0.6;
    return s.hazards.some((h: any) => h.kind === "witchmagic" && h.y > lo && h.y < hi);
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
  await openBattle(page, "heaven-1");
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
  // Spawner interval 1.8s, telegraph window 0.5s → charging from 1.3s (78 ticks @60Hz).
  await cheat(page, "fastForward", 85);
  const charging = await page.evaluate(() =>
    (window as any).__game.getState().blocks.some((b: any) => b.sprite === "HellBallSpawner" && b.charging));
  expect(charging, "spawner must flag charging inside the telegraph window").toBeTruthy();
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
  await openBattle(page, "village-ghost");
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
  await page.screenshot({ path: path.join(SHOTS, "enemy-deathmark.png") }); // sphere over the corpse
  // The necromant revives it (delay default → fast-forward well past it).
  await cheat(page, "fastForward", 400);
  await page.waitForFunction((id) =>
    (window as any).__game.getState().blocks.some((b: any) => b.id === id), brickId, { timeout: 8000 });
});

test("killing an enemy block always drops a bonus (danger pays)", async ({ page }) => {
  await openBattle(page, "hell-2");
  const spawnerId = await page.evaluate(() =>
    (window as any).__game.getState().blocks.find((b: any) => b.sprite === "HellBallSpawner")?.id);
  expect(spawnerId).toBeTruthy();
  // Spawner has 6 HP — drive six hits into it.
  for (let i = 0; i < 6; i++) {
    await cheat(page, "ballToBlock", spawnerId);
    await cheat(page, "fastForward", 3);
  }
  await page.waitForFunction(() =>
    (window as any).__game.getState().bonuses.length > 0, null, { timeout: 8000 });
  await page.screenshot({ path: path.join(SHOTS, "enemy-danger-pays.png") });
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
