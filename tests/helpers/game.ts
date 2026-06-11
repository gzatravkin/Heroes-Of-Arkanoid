import { Page, expect } from "@playwright/test";

/** Captures N screenshots at intervalMs apart, returns array of Buffers. */
export async function captureFrames(page: Page, n: number, intervalMs: number): Promise<Buffer[]> {
  const frames: Buffer[] = [];
  for (let i = 0; i < n; i++) {
    frames.push(await page.screenshot());
    if (i < n - 1) await page.waitForTimeout(intervalMs);
  }
  return frames;
}

/** Open a battle pre-set to a given level/seed and wait until the sim is streaming. */
export async function openBattle(page: Page, level = "hell-1", seed = 1) {
  const run = `${level}-${seed}-${Date.now()}`;
  await page.goto(`/?scene=battle&level=${level}&seed=${seed}&run=${run}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await page.waitForFunction(() => (window as any).__game.getState()?.balls.length > 0);
}

export async function getState(page: Page) {
  return await page.evaluate(() => (window as any).__game.getState());
}

export async function cheat(page: Page, op: string, value = 0) {
  // waitForFunction does not trigger a GPU stall (unlike page.evaluate); cheat calls are fire-and-forget.
  await page.waitForFunction(
    ([o, v]) => { (window as any).__game?.cheat(o, v); return true; },
    [op, value] as const,
  );
}

/** Wait until the snapshot satisfies a predicate (polls the live sim state). */
export async function waitForPhase(page: Page, phase: string) {
  // When waiting for Playing, poke serve on each poll cycle so the game doesn't
  // get stuck behind the tutorial overlay or a timing race. Backend Serve() is
  // guarded by `if (Phase != Serving) return`, so repeated calls are no-ops.
  await page.waitForFunction(
    (p) => {
      if (p === "Playing") (window as any).__game?.serve?.();
      return (window as any).__game?.getState()?.phase === p;
    },
    phase,
    { timeout: 10_000 },
  );
}
