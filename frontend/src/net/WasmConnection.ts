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
  // Fixed-timestep accumulator: the sim always steps by FixedDt (1/60s) regardless of
  // display refresh rate. Without this, a 120Hz monitor runs the sim 2× too fast because
  // requestAnimationFrame fires twice as often as the sim's expected 60 Hz.
  private _accumulator = 0;
  private _lastRafTime = -1;
  private static readonly _fixedDt = 1 / 60;

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
    const fixedDt = WasmConnection._fixedDt;

    const tick = (now: number) => {
      if (this._closed) return;

      // Accumulate real elapsed time; cap at 100 ms to avoid a spiral-of-death
      // if the tab was backgrounded or the frame dropped badly.
      if (this._lastRafTime >= 0) {
        this._accumulator += Math.min((now - this._lastRafTime) / 1000, 0.1);
      }
      this._lastRafTime = now;

      let lastSnap: string | null = null;

      // Step the sim as many fixed-dt times as real time allows.
      while (this._accumulator >= fixedDt) {
        for (const cmd of this._pendingInputs) {
          this._bridge.EnqueueInput(JSON.stringify(cmd));
        }
        this._pendingInputs = [];

        const snapJson: string = this._bridge.Tick();
        this._accumulator -= fixedDt;

        if (snapJson && snapJson !== "{}") lastSnap = snapJson;
      }

      if (lastSnap) {
        const s = JSON.parse(lastSnap) as Snapshot;
        this.latest = s;
        if (s.phase !== this._lastPhase) {
          log("phase", s.phase, { tick: s.tick, hp: s.hp });
          this._lastPhase = s.phase;
        }
        this.onSnapshot?.(s);
      }

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
