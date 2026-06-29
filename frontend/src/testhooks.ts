import type { Connection, Snapshot } from "./net/Connection";
import { getLogs } from "./log";

declare global { interface Window { __game: GameTestApi } }
export interface GameTestApi {
  getState: () => Snapshot | null;
  cheat: (op: string, value?: number) => void;
  serve: () => void;
  castIgnite: () => void;
  castFireball: () => void;
  castFireWall: () => void;
  castTurret: () => void;
  castPhoenix: () => void;
  castSlot: (slot: number) => void;
  setPaddleX: (x: number) => void;
  runId: string;
  getLogs: () => unknown[];
}
export function installTestHooks(conn: Connection) {
  window.__game = {
    getState: () => conn.latest,
    cheat: (op, value = 0) => conn.cheat(op, value),
    serve: () => conn.serve(),
    castIgnite: () => conn.castIgnite(),
    castFireball: () => conn.castFireball(),
    castFireWall: () => conn.castFireWall(),
    castTurret: () => conn.castTurret(),
    castPhoenix: () => conn.castPhoenix(),
    castSlot: (slot) => conn.castSlot(slot),
    setPaddleX: (x) => conn.paddleX(x),
    runId: conn.runId,
    getLogs: () => getLogs(),
  };
}
