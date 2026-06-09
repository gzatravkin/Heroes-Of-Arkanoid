import { log } from "../log";

export interface Snapshot {
  tick: number; phase: string; lives: number; spareBalls: number;
  mana: number; manaMax: number; boardW: number; boardH: number; biome: string;
  paddleX: number; paddleW: number; paddleH: number; cellSize: number;
  balls: { id: number; x: number; y: number; ignited: boolean; decayed?: boolean }[];
  blocks: { id: number; x: number; y: number; hp: number; maxHp: number; sprite: string; ballPhases: boolean; teleporter: boolean; indestructible: boolean; boss?: boolean; flipX?: boolean; flipY?: boolean }[];
  hazards: { x: number; y: number }[];
  events: { type: string; x: number; y: number }[];
  walls: { y: number; width: number }[];
  turretActive: boolean;
  activeRelics: { id: string; name: string; icon: string }[];
  bossActive: boolean;
  bossHp: number;
  bossMaxHp: number;
  bonuses: { id: number; x: number; y: number; type: string; icon: string }[];
  widePaddleActive: boolean;
  widePaddleTimer: number;
  slowBallActive: boolean;
  slowBallTimer: number;
  // P6 additions
  barriers: { y: number; centerX: number; width: number }[];
  zones: { x: number; y: number; radius: number }[];
  skeletonActive: boolean;
  drainActive: boolean;
  // P7 additions
  /** Extra crystals to add to the level-completion reward (from equipped treasure items). */
  treasureBonus?: number;
}

export class Connection {
  private ws: WebSocket;
  latest: Snapshot | null = null;
  onSnapshot: ((s: Snapshot) => void) | null = null;
  readonly runId: string;
  private lastPhase = "";

  constructor(level: string, seed: number, runId: string) {
    this.runId = runId;
    // Profile namespace: lets parallel test workers isolate their backend save.
    // Browsers can't set WS headers, so it rides the query string. Defaults to "default".
    const pid = (typeof localStorage !== "undefined" && localStorage.getItem("ark_pid")) || "default";
    this.ws = new WebSocket(`ws://localhost:5080/ws?level=${level}&seed=${seed}&run=${runId}&pid=${encodeURIComponent(pid)}`);
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
  castSlot(slot: number) { this.send({ kind: "CastSlot", slot }); }
  cheat(op: string, value = 0) { this.send({ kind: "Cheat", cheat: op, value }); }
  whenReady(cb: () => void) {
    if (this.ws.readyState === 1) cb(); else this.ws.addEventListener("open", cb, { once: true });
  }
}
