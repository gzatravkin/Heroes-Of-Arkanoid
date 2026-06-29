import type { Snapshot } from "./Connection";
import { gameBridge } from "../wasm/bridge";
import { log } from "../log";

/**
 * WasmConnection — drop-in replacement for Connection that drives the sim
 * via the WASM GameBridge instead of a WebSocket.
 *
 * Public interface is identical to Connection so callers can swap with a
 * single import-line change.
 */
export class WasmConnection {
  latest: Snapshot | null = null;
  onSnapshot: ((s: Snapshot) => void) | null = null;
  readonly runId: string;

  private _closed = false;
  private _ready = false;
  private _readyCbs: Array<() => void> = [];
  private _pendingInputs: object[] = [];
  private _rafId = 0;
  private _lastPhase = "";
  private _bridge = gameBridge();

  constructor(level: string, seed: number, runId: string, mode = "") {
    this.runId = runId;
    const pid = (typeof localStorage !== "undefined" && localStorage.getItem("ark_pid")) || "default";

    log("wasm", "initSession", { level, seed, runId, mode });
    this._bridge.InitSession(level, seed, runId, pid, mode);

    // Mark ready on next microtask so callers have time to attach onSnapshot.
    Promise.resolve().then(() => {
      this._ready = true;
      this._readyCbs.forEach(cb => cb());
      this._readyCbs = [];
      this._startLoop();
    });
  }

  private _startLoop(): void {
    const tick = () => {
      if (this._closed) return;

      for (const cmd of this._pendingInputs) {
        this._bridge.EnqueueInput(JSON.stringify(cmd));
      }
      this._pendingInputs = [];

      const snapJson: string = this._bridge.Tick();
      if (!snapJson || snapJson === "{}") {
        this._rafId = requestAnimationFrame(tick);
        return;
      }
      const s = JSON.parse(snapJson) as Snapshot;
      this.latest = s;
      if (s.phase !== this._lastPhase) {
        log("phase", s.phase, { tick: s.tick, hp: s.hp });
        this._lastPhase = s.phase;
      }
      this.onSnapshot?.(s);
      this._rafId = requestAnimationFrame(tick);
    };
    this._rafId = requestAnimationFrame(tick);
  }

  private _send(cmd: object): void {
    log("cmd", (cmd as any).kind, cmd);
    this._pendingInputs.push(cmd);
  }

  paddleX(x: number): void          { this._send({ kind: "PaddleX", x }); }
  serve(): void                      { this._send({ kind: "Serve" }); }
  castIgnite(): void                 { this._send({ kind: "CastImbueIgnite" }); }
  castFireball(): void               { this._send({ kind: "CastFireball" }); }
  castFireWall(): void               { this._send({ kind: "CastFireWall" }); }
  castTurret(): void                 { this._send({ kind: "CastTurret" }); }
  castPhoenix(): void                { this._send({ kind: "CastPhoenix" }); }
  castSlot(slot: number): void       { this._send({ kind: "CastSlot", slot }); }
  cheat(op: string, value = 0): void { this._send({ kind: "Cheat", cheat: op, value }); }
  riftPick(id: string): void         { this._send({ kind: "RiftPick", riftMod: id }); }

  close(): void {
    this._closed = true;
    cancelAnimationFrame(this._rafId);
    this._bridge.CloseSession();
  }

  whenReady(cb: () => void): void {
    if (this._ready) cb(); else this._readyCbs.push(cb);
  }
}
