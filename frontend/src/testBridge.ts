// testBridge.ts — single typed window-global surface for teardown and Playwright.
// BattleScene installs it after mount; doMount calls teardown before the next scene.
import type { Renderer } from "./render/Renderer";
import type { Connection } from "./net/Connection";

declare global {
  interface Window {
    __bridge?: BattleBridge;
    __game: import("./testhooks").GameTestApi;
  }
}

export interface BattleBridge {
  renderer: Renderer;
  conn: Connection;
  detachInput: () => void;
}

export function installBridge(b: BattleBridge): void {
  window.__bridge = b;
}

export function teardownBridge(): void {
  const b = window.__bridge;
  if (!b) return;
  delete window.__bridge;
  try { b.detachInput(); } catch (_) {}
  try { b.conn.close(); } catch (_) {}
  try { b.renderer.app.destroy(false); } catch (_) {}
}
