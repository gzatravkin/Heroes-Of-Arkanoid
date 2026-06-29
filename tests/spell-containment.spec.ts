import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Containment Field (design §3 rework of Radiation): a field that SUPPRESSES enemy emitters caught
// inside it (they can't fire) and melts the blocks within. Real play on hell-2 (a hell ball-spawner
// level): the emitter fires hazards; deploy the field onto it and the hazards stop. The sim log shows
// the field cast at the emitter's position; the on-screen hazards drain to zero (no new ones spawn).

const OUT = path.resolve(__dirname, "..", "test-results", "containment");

test("Containment Field suppresses an enemy emitter (no more hazards)", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  const runId = `containment-${Date.now()}`;
  fs.writeFileSync(path.join(OUT, "runid.txt"), runId);

  // Play the Engineer with Containment Field equipped (its reworked Radiation, slot 0).
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    await fetch("http://localhost:5080/reset", { method: "POST" });
    await fetch("http://localhost:5080/dev/hero?hero=engineer&select=1&loadout=radiation,overload,lightning",
      { method: "POST" });
  });

  await page.goto(`/?scene=battle&level=hell-2&seed=1&run=${runId}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");

  // Survive the barrage (the frozen ball can't deflect during fastForward), give mana.
  await page.evaluate(() => { (window as any).__game.cheat("setLives", 50); (window as any).__game.cheat("setMana", 100); });
  // Let the emitter fire: hazards must appear (proves the emitter is active — the control).
  await page.evaluate(() => (window as any).__game.cheat("fastForward", 200));
  await page.waitForFunction(() => ((window as any).__game.getState()?.hazards?.length ?? 0) > 0, null, { timeout: 10_000 });
  fs.writeFileSync(path.join(OUT, "1-emitter-firing.png"), await page.screenshot());

  // Deploy the Containment Field — it auto-targets the nearest emitter and suppresses it.
  // (Engineer's signature Overload locks slot 0, so the drafted Containment Field is slot 1.)
  await page.evaluate(() => { (window as any).__game.cheat("setMana", 100); (window as any).__game.castSlot(1); });
  await page.waitForTimeout(200);
  fs.writeFileSync(path.join(OUT, "2-field-deployed.png"), await page.screenshot());

  // Advance time with the field up: existing hazards fall off and NO new ones spawn → hazards drain to 0.
  await page.evaluate(() => (window as any).__game.cheat("fastForward", 200));
  await page.waitForFunction(() => ((window as any).__game.getState()?.hazards?.length ?? 0) === 0, null, { timeout: 10_000 });
  fs.writeFileSync(path.join(OUT, "3-suppressed.png"), await page.screenshot());

  expect(await page.evaluate(() => (window as any).__game.getState().hazards.length)).toBe(0);
});
