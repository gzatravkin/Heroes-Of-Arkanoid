import { Page, expect } from "@playwright/test";

/** Open a battle pre-set to a given level/seed and wait until the sim is streaming. */
export async function openBattle(page: Page, level = "hell-1", seed = 1) {
  await page.goto(`/?scene=battle&level=${level}&seed=${seed}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await page.waitForFunction(() => (window as any).__game.getState()?.balls.length > 0);
}

export async function getState(page: Page) {
  return await page.evaluate(() => (window as any).__game.getState());
}

export async function cheat(page: Page, op: string, value = 0) {
  await page.evaluate(([o, v]) => (window as any).__game.cheat(o, v), [op, value] as const);
}

/** Wait until the snapshot satisfies a predicate (polls the live sim state). */
export async function waitForPhase(page: Page, phase: string) {
  await page.waitForFunction((p) => (window as any).__game.getState()?.phase === p, phase, { timeout: 10_000 });
}
