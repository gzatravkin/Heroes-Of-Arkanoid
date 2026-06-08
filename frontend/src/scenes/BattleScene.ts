import { Renderer } from "../render/Renderer";
import { Connection } from "../net/Connection";
import { attachPaddleInput } from "../input/PaddleInput";
import { installTestHooks } from "../testhooks";

export function mountBattle(host: HTMLElement, level: string, seed: number) {
  const r = new Renderer(host);
  const conn = new Connection(level, seed);
  conn.onSnapshot = (s) => r.draw(s);
  attachPaddleInput(r.app.view as HTMLCanvasElement, conn, () => conn.latest);
  installTestHooks(conn);
  // auto-serve shortly after connect so the ball is live for tests/play
  conn.whenReady(() => setTimeout(() => conn.serve(), 300));
}
