import { log } from "../log";

export interface Snapshot {
  tick: number; phase: string; lives: number; spareBalls: number;
  mana: number; manaMax: number; boardW: number; boardH: number;
  paddleX: number; paddleW: number; paddleH: number; cellSize: number;
  balls: { id: number; x: number; y: number; ignited: boolean }[];
  blocks: { id: number; x: number; y: number; hp: number; maxHp: number; sprite: string; ballPhases: boolean; teleporter: boolean; indestructible: boolean; boss?: boolean }[];
  hazards: { x: number; y: number }[];
  events: { type: string; x: number; y: number }[];
  walls: { y: number; width: number }[];
  turretActive: boolean;
  activeRelics: { id: string; name: string; icon: string }[];
}

export class Connection {
  private ws: WebSocket;
  latest: Snapshot | null = null;
  onSnapshot: ((s: Snapshot) => void) | null = null;
  readonly runId: string;
  private lastPhase = "";

  constructor(level: string, seed: number, runId: string) {
    this.runId = runId;
    this.ws = new WebSocket(`ws://localhost:5080/ws?level=${level}&seed=${seed}&run=${runId}`);
    log("net", "connecting", { level, seed, runId });
    this.ws.onopen = () => log("net", "open");
    this.ws.onclose = () => log("net", "close");
    this.ws.onerror = () => log("net", "error");
    this.ws.onmessage = (e) => {
      const s = JSON.parse(e.data) as Snapshot;
      this.latest = s;
      if (s.phase !== this.lastPhase) {
        log("phase", s.phase, { tick: s.tick, lives: s.lives, balls: s.spareBalls });
        this.lastPhase = s.phase;
      }
      this.onSnapshot?.(s);
    };
  }
  private send(o: any) { if (this.ws.readyState === 1) { log("cmd", o.kind, o); this.ws.send(JSON.stringify(o)); } }
  paddleX(x: number) { this.send({ kind: "PaddleX", x }); }
  serve() { this.send({ kind: "Serve" }); }
  castIgnite() { this.send({ kind: "CastImbueIgnite" }); }
  castFireball() { this.send({ kind: "CastFireball" }); }
  castFireWall() { this.send({ kind: "CastFireWall" }); }
  castTurret() { this.send({ kind: "CastTurret" }); }
  cheat(op: string, value = 0) { this.send({ kind: "Cheat", cheat: op, value }); }
  whenReady(cb: () => void) {
    if (this.ws.readyState === 1) cb(); else this.ws.addEventListener("open", cb, { once: true });
  }
}
