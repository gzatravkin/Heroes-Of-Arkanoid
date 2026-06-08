import type { Connection, Snapshot } from "./net/Connection";

declare global { interface Window { __game: GameTestApi } }
export interface GameTestApi {
  getState: () => Snapshot | null;
  cheat: (op: string, value?: number) => void;
  serve: () => void;
  castIgnite: () => void;
  castFireball: () => void;
  setPaddleX: (x: number) => void;
}
export function installTestHooks(conn: Connection) {
  window.__game = {
    getState: () => conn.latest,
    cheat: (op, value = 0) => conn.cheat(op, value),
    serve: () => conn.serve(),
    castIgnite: () => conn.castIgnite(),
    castFireball: () => conn.castFireball(),
    setPaddleX: (x) => conn.paddleX(x),
  };
}
