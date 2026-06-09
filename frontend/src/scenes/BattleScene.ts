import { Renderer } from "../render/Renderer";
import { Connection } from "../net/Connection";
import { attachPaddleInput } from "../input/PaddleInput";
import { installTestHooks } from "../testhooks";
import { Hud } from "../ui/Hud";
import { createCampaignFlow } from "./battle/campaignFlow";
import { createDungeonFlow } from "./battle/dungeonFlow";

export function mountBattle(host: HTMLElement, level: string, seed: number, run: string, from = "") {
  const r = new Renderer(host);
  const hud = new Hud(host);
  const conn = new Connection(level, seed, run);

  const flow =
    from === "campaign" ? createCampaignFlow(level) :
    from === "dungeon"  ? createDungeonFlow()        :
    null;

  conn.onSnapshot = (s) => {
    r.draw(s);
    hud.update(s);
    if (flow) flow.handlePhase(s);
  };

  attachPaddleInput(r.app.view as HTMLCanvasElement, conn, () => conn.latest);
  installTestHooks(conn);
  // auto-serve shortly after connect so the ball is live for tests/play
  conn.whenReady(() => setTimeout(() => conn.serve(), 300));
}
