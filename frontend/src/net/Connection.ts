export interface Snapshot {
  tick: number; phase: string; lives: number; spareBalls: number;
  mana: number; manaMax: number; boardW: number; boardH: number;
  paddleX: number; paddleW: number; paddleH: number; cellSize: number;
  balls: { id: number; x: number; y: number; ignited: boolean }[];
  blocks: { id: number; x: number; y: number; hp: number; maxHp: number; sprite: string }[];
  events: { type: string; x: number; y: number }[];
}

export class Connection {
  private ws: WebSocket;
  latest: Snapshot | null = null;
  onSnapshot: ((s: Snapshot) => void) | null = null;

  constructor(level: string, seed: number) {
    this.ws = new WebSocket(`ws://localhost:5080/ws?level=${level}&seed=${seed}`);
    this.ws.onmessage = (e) => {
      const s = JSON.parse(e.data) as Snapshot;
      this.latest = s;
      this.onSnapshot?.(s);
    };
  }
  private send(o: object) { if (this.ws.readyState === 1) this.ws.send(JSON.stringify(o)); }
  paddleX(x: number) { this.send({ kind: "PaddleX", x }); }
  serve() { this.send({ kind: "Serve" }); }
  castIgnite() { this.send({ kind: "CastImbueIgnite" }); }
  castFireball() { this.send({ kind: "CastFireball" }); }
  cheat(op: string, value = 0) { this.send({ kind: "Cheat", cheat: op, value }); }
  whenReady(cb: () => void) {
    if (this.ws.readyState === 1) cb(); else this.ws.addEventListener("open", cb, { once: true });
  }
}
