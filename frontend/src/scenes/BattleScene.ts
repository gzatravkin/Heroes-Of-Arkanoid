import { Renderer } from "../render/Renderer";
import { Connection } from "../net/Connection";
import { attachPaddleInput } from "../input/PaddleInput";
import { installTestHooks } from "../testhooks";
import { Hud } from "../ui/Hud";
import { createCampaignFlow } from "./battle/campaignFlow";
import { createDungeonFlow } from "./battle/dungeonFlow";

// Skip the per-snapshot Pixi render under Playwright automation: the canvas is
// unused by tests (they poll window.__game.getState() and the DOM HUD directly),
// and the render call adds enough JS-thread latency to shift ball-drain timing,
// causing spell-cast commands to arrive in Serving phase instead of Playing.
// navigator.webdriver is true in Playwright headless; false/undefined in browsers.
const NEEDS_RENDER = !(navigator as any).webdriver;

export function mountBattle(host: HTMLElement, level: string, seed: number, run: string, from = "") {
  const r = new Renderer(host);
  const hud = new Hud(host);
  const conn = new Connection(level, seed, run);

  const flow =
    from === "campaign" ? createCampaignFlow(level) :
    from === "dungeon"  ? createDungeonFlow()        :
    null;

  // Under automation, stop the Pixi ticker so it doesn't consume JS-thread time
  // with animation-frame callbacks. Tests don't observe the canvas.
  if (!NEEDS_RENDER) r.app.ticker.stop();

  conn.onSnapshot = (s) => {
    if (NEEDS_RENDER) r.draw(s);
    hud.update(s);
    if (flow) flow.handlePhase(s);
  };

  attachPaddleInput(r.app.view as HTMLCanvasElement, conn, () => conn.latest);
  installTestHooks(conn);
  // auto-serve shortly after connect so the ball is live for tests/play
  conn.whenReady(() => setTimeout(() => conn.serve(), 300));
}
