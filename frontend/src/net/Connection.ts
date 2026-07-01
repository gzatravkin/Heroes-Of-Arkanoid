import { log } from "../log";

export interface Snapshot {
  tick: number; phase: string; hp: number; spareBalls: number; gold?: number;
  mana: number; manaMax: number; boardW: number; boardH: number; biome: string;
  paddleX: number; paddleW: number; paddleH: number; paddleInvuln?: boolean; cellSize: number;
  balls: { id: number; x: number; y: number; ignited: boolean; decayed?: boolean; ghost?: boolean; summoned?: boolean; radiusScale?: number }[];
  projectiles?: { id: number; x: number; y: number; kind: string }[];
  blocks: { id: number; x: number; y: number; hp: number; maxHp: number; sprite: string; ballPhases: boolean; teleporter: boolean; indestructible: boolean; boss?: boolean; flipX?: boolean; flipY?: boolean; shielded?: boolean; charging?: boolean; allied?: boolean; level?: number; burning?: boolean; cursed?: boolean; union?: boolean; elite?: boolean }[];
  /** Lich's Gaze (§3) sweeping beam, or absent. */
  lichBeam?: { x: number; y: number; angle: number; len: number };
  /** Lance of Dawn (§3) temporary solid pillars (center x/y + size). */
  pillars?: { x: number; y: number; w: number; h: number }[];
  /** Bonewalker / Bone Golem (§3) summoned minions (center x/y + size, kind, golem soak hp, bonewalker lifeFrac). */
  minions?: { id: number; kind: string; x: number; y: number; w: number; h: number; hp: number; maxHp: number; lifeFrac?: number }[];
  /** Twin Soul Core (§2) tether segment between the two twins, or absent. */
  twinTether?: { x1: number; y1: number; x2: number; y2: number };
  hazards: { x: number; y: number; kind?: string; warming?: boolean }[];
  /** Fire-Mage phoenixes orbiting balls (visible entities). */
  phoenixes?: { id: number; x: number; y: number; angle: number }[];
  events: { type: string; x: number; y: number; extra?: number }[];
  walls: { y: number; width: number }[];
  turretActive: boolean;
  /** Tesla Grid (§3 Engineer): armed = spell is active this level; left/right = wall charged. */
  teslaArmed?: boolean;
  teslaLeftCharged?: boolean;
  teslaRightCharged?: boolean;
  activeRelics: { id: string; name: string; icon: string }[];
  bossActive: boolean;
  bossHp: number;
  bossMaxHp: number;
  burningBlockCount: number;
  bonuses: { id: number; x: number; y: number; type: string; icon: string }[];
  widePaddleActive: boolean;
  widePaddleTimer: number;
  slowBallActive: boolean;
  slowBallTimer: number;
  // P6 additions
  barriers: { y: number; centerX: number; width: number; kind: string }[];
  zones: { x: number; y: number; radius: number }[];
  skeletonActive: boolean;
  spellDrainActive: boolean;
  // P7 additions
  /** Extra crystals to add to the level-completion reward (from equipped treasure items). */
  crystalBonus?: number;
  /** WindMaster push radius in world units (renderer draws the aura at this size). */
  windRadius?: number;
  /** Objective timer (docs/12): "" | "survive" | "limit", with seconds remaining. */
  timerMode?: string;
  timeLeft?: number;
  /** Multi-floor collapse progress (1-based; floorCount 1 = single floor). */
  floor?: number;
  floorCount?: number;
  /** Continuous Rift (2026-06-16): depth-reward banked so far + the next milestone depth (3/5/7/10). */
  isRift?: boolean;
  riftReward?: number;
  riftNextMilestone?: number;
  /** §8 mid-floor draft: when true the sim is paused and the player must pick one of draftChoices. */
  awaitingDraft?: boolean;
  draftChoices?: { id: string; name: string; desc: string }[];
  /** Grid dimensions in cells (populated from Grid.Cols / Grid.Rows). */
  cols?: number;
  rows?: number;
  /** Blocks destroyed this level — the backend uses this to drive +5%/20-brick speed escalation. */
  bricksDestroyedThisLevel?: number;
  // --- Power-up active states (task 1.2) ---
  fireshotActive?: boolean;
  fireshotTimer?: number;
  shieldActive?: boolean;
  // --- Combo multiplier (task 1.3) ---
  /** Consecutive-hit streak multiplier: 1 (none) … 4 (max). Resets on paddle contact. */
  comboMultiplier?: number;
  /** Reckoning (§3 Paladin) meter fill 0..1 (0 when not armed) — drives the HUD charge bar. */
  reckoningCharge?: number;
  /** Equipped spell loadout, ordered (slot 0 = signature). Drives the hotbar; the index
   *  here is exactly the slot CastSlot uses. Empty/absent ⇒ HUD falls back to /characters. */
  loadout?: { id: string; name: string; icon: string; manaCost: number; level: number; signature: boolean }[];
  /** Dungeon miniboss floor — HUD shows a warning banner (docs/04 §6.2). */
  minibossFloor?: boolean;
}

export class Connection {
  private ws: WebSocket;
  latest: Snapshot | null = null;
  onSnapshot: ((s: Snapshot) => void) | null = null;
  readonly runId: string;
  private lastPhase = "";

  constructor(level: string, seed: number, runId: string, mode = "") {
    this.runId = runId;
    // Profile namespace: lets parallel test workers isolate their backend save.
    // Browsers can't set WS headers, so it rides the query string. Defaults to "default".
    const pid = (typeof localStorage !== "undefined" && localStorage.getItem("ark_pid")) || "default";
    const modeQ = mode ? `&mode=${encodeURIComponent(mode)}` : "";
    this.ws = new WebSocket(`ws://localhost:5080/ws?level=${level}&seed=${seed}&run=${runId}&pid=${encodeURIComponent(pid)}${modeQ}`);
    log("net", "connecting", { level, seed, runId });
    this.ws.onopen = () => log("net", "open");
    this.ws.onclose = () => log("net", "close");
    this.ws.onerror = () => log("net", "error");
    this.ws.onmessage = (e) => {
      const s = JSON.parse(e.data) as Snapshot;
      this.latest = s;
      if (s.phase !== this.lastPhase) {
        log("phase", s.phase, { tick: s.tick, hp: s.hp, balls: s.spareBalls });
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
  castPhoenix() { this.send({ kind: "CastPhoenix" }); }
  castSlot(slot: number) { this.send({ kind: "CastSlot", slot }); }
  cheat(op: string, value = 0) { this.send({ kind: "Cheat", cheat: op, value }); }
  riftPick(id: string) { this.send({ kind: "RiftPick", riftMod: id }); }
  close() { this.ws.close(); }
  whenReady(cb: () => void) {
    if (this.ws.readyState === 1) cb(); else this.ws.addEventListener("open", cb, { once: true });
  }
}
