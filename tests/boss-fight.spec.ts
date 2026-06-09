/**
 * boss-fight.spec.ts — P5b boss fight integration tests (mobile viewport).
 *
 * Covers:
 *   1. hell-boss: bossActive===true, #hud-boss-hp visible, hazards spawn.
 *   2. bossTelegraph event appears in the WebSocket event stream.
 */
import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat, waitForPhase } from "./helpers/game";

test("hell-boss: bossActive, boss HP bar visible, hazards spawn, telegraph fires", async ({ page }) => {
  // Install a WS event interceptor BEFORE the page initializes so we capture
  // all single-tick events from the very first connection.
  // Polling getState() misses single-tick events (sampler coverage ~17% of ticks).
  await page.addInitScript(() => {
    (window as any).__bossEventsIntercepted = [];
    const OrigWS = (window as any).WebSocket;
    function PatchedWS(this: any, ...args: any[]) {
      const ws = new OrigWS(...args);
      ws.addEventListener("message", (e: MessageEvent) => {
        try {
          const s = JSON.parse(e.data);
          if (s && Array.isArray(s.events)) {
            for (const ev of s.events) {
              if (ev.type === "bossTelegraph" || ev.type === "bossAttack") {
                (window as any).__bossEventsIntercepted.push({ tick: s.tick, type: ev.type });
              }
            }
          }
        } catch {}
      });
      return ws;
    }
    PatchedWS.prototype = OrigWS.prototype;
    PatchedWS.CONNECTING = OrigWS.CONNECTING;
    PatchedWS.OPEN  = OrigWS.OPEN;
    PatchedWS.CLOSING = OrigWS.CLOSING;
    PatchedWS.CLOSED  = OrigWS.CLOSED;
    (window as any).WebSocket = PatchedWS;
  });

  await openBattle(page, "hell-boss", 1);

  // 1. bossActive must be true immediately on load (boss blocks present).
  const s0 = await getState(page);
  expect(s0).toBeTruthy();
  expect(s0.bossActive, "bossActive should be true on hell-boss").toBe(true);
  expect(s0.bossMaxHp,  "bossMaxHp should be > 0").toBeGreaterThan(0);

  // 2. Boss HP bar must be visible in the DOM.
  await expect(page.locator("#hud-boss-hp")).toBeVisible();

  // 3. Keep the ball alive so the boss can fire hazards and telegraph events.
  await page.evaluate(() => {
    const g = (window as any).__game;
    const iv = setInterval(() => {
      const s = g?.getState();
      if (!s) return;
      if (s.phase === "Serving") { g.serve(); return; }
      if (s.phase === "Playing" && s.balls?.length > 0) {
        if (s.balls[0].y > s.boardH * 0.8) g.cheat("parkBallAbovePaddle");
      }
    }, 100);
    (window as any).__keepAliveIv = iv;
  });

  // 4. Wait until at least one hazard spawns (confirms boss is actively attacking).
  await page.waitForFunction(
    () => {
      const s = (window as any).__game?.getState();
      return s?.hazards?.length > 0;
    },
    null,
    { timeout: 20_000 },
  );

  // 5. Confirm the boss HP bar fill element exists.
  await expect(page.locator("#hud-boss-hp-fill")).toBeAttached();

  // 6. Poll accumulated WS events for a bossTelegraph or bossAttack.
  //    The WS interceptor catches 100% of events (not subject to sampler aliasing).
  await page.waitForFunction(
    () => ((window as any).__bossEventsIntercepted?.length ?? 0) > 0,
    null,
    { timeout: 10_000 },
  );

  const intercepted: { tick: number; type: string }[] = await page.evaluate(
    () => (window as any).__bossEventsIntercepted ?? [],
  );
  expect(intercepted.length, "Expected at least one bossTelegraph/bossAttack in WS stream").toBeGreaterThan(0);

  // Cleanup.
  await page.evaluate(() => clearInterval((window as any).__keepAliveIv));

  // 7. Boss can be won via cheat.
  await cheat(page, "winNow");
  await waitForPhase(page, "Won");
});
