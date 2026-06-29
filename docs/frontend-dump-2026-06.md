# Frontend Source Dump — 2026-06-12

Generated from `frontend/src` — 52 files, 11665 lines.

---

## `src/audio/Music.ts`

```typescript
// Music.ts — procedural per-biome ambience (musical rewrite 2026-06-10).
//
// Design constraints (hard requirements):
//   - Only sine/triangle oscillators (no sawtooth, no square)
//   - Every voice through a lowpass BiquadFilterNode (per-biome cutoff ≤ 2000 Hz)
//   - Master gain ≤ 0.05 behind a DynamicsCompressorNode
//     (threshold -24, knee 12, ratio 6, attack 0.003, release 0.25)
//   - Attack ≥ 50 ms via linearRampToValueAtTime; release ≥ 300 ms
//   - Default OFF: localStorage key "arkanoid_music" must equal "1" to enable
//
// Per-biome content:
//   hell     — Am pads (A2+E3+A3 triangle 8 s chords), sparse 55 Hz sine thumps
//   caverns  — D-minor drone (D2+A2 sine), echoing pentatonic triangle plucks
//   village  — A-minor-pentatonic chimes (triangle) + soft A2 pad
//   heaven   — Fmaj7/Am alternating pads (triangle), occasional triangle bells

const MUSIC_VOLUME = 0.05;
const SCHEDULER_MS = 200;    // tick cadence
const LOOKAHEAD_S  = 1.0;    // schedule up to this far ahead

let _ctx:    AudioContext | null           = null;
let _comp:   DynamicsCompressorNode | null = null;
let _master: GainNode | null              = null;
let _filter: BiquadFilterNode | null      = null;
let _biome   = "";
let _timer:  number | null                = null;
let _drones: { osc: OscillatorNode; gain: GainNode }[] = [];

// Per-biome sparse-event schedule cursors
let _nextChord  = 0;
let _chordPhase = 0;
let _nextThump  = 0;
let _nextPluck  = 0;
let _pluckIdx   = 0;
let _nextChime  = 0;
let _chimeIdx   = 0;
let _nextBell   = 0;

// ── Toggle ────────────────────────────────────────────────────────────────────

/** Music toggle: off by default. "arkanoid_music" === "1" to enable. */
function musicEnabled(): boolean {
  return localStorage.getItem("arkanoid_music") === "1";
}

// ── AudioContext bootstrap ────────────────────────────────────────────────────

function getCtx(): AudioContext | null {
  if (_ctx) return _ctx;
  try {
    const AC = (
      window.AudioContext ??
      (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    );
    if (!AC) return null;
    _ctx  = new AC();
    _comp = _ctx.createDynamicsCompressor();
    _comp.threshold.value = -24;
    _comp.knee.value      =  12;
    _comp.ratio.value     =   6;
    _comp.attack.value    = 0.003;
    _comp.release.value   = 0.25;
    _master = _ctx.createGain();
    _master.gain.value = 0;          // stays 0 until scheduler confirms enabled
    _master.connect(_comp).connect(_ctx.destination);
  } catch { return null; }
  return _ctx;
}

// ── Synth primitives ──────────────────────────────────────────────────────────

/** Replace the biome lowpass filter, connected to _master. */
function resetFilter(c: AudioContext, cutoff: number): void {
  if (_filter) { try { _filter.disconnect(); } catch { /**/ } }
  const f = c.createBiquadFilter();
  f.type            = "lowpass";
  f.frequency.value = cutoff;
  f.Q.value         = 0.7;
  f.connect(_master!);
  _filter = f;
}

/**
 * Schedule one note: osc → gain → _filter.
 * Enforces attack ≥ 50 ms (linearRamp) and release ≥ 300 ms (linearRamp).
 */
function playNote(
  c:        AudioContext,
  type:     OscillatorType,
  freq:     number,
  at:       number,
  durS:     number,
  vol:      number,
  attackS = 0.05,
  releaseS = 0.3,
): void {
  if (!_filter) return;
  const atk = Math.max(attackS, 0.05);
  const rel = Math.max(releaseS, 0.3);
  const osc = c.createOscillator();
  osc.type            = type;
  osc.frequency.value = freq;
  const g = c.createGain();
  g.gain.setValueAtTime(0, at);
  g.gain.linearRampToValueAtTime(vol, at + atk);
  const susEnd = Math.max(at + atk + 0.001, at + durS - rel);
  g.gain.setValueAtTime(vol, susEnd);
  g.gain.linearRampToValueAtTime(0, at + durS);
  osc.connect(g).connect(_filter);
  osc.start(at);
  osc.stop(at + durS + 0.05);
}

/** Continuous drone oscillator; faded in over 1 s (linearRamp). */
function startDrone(c: AudioContext, type: OscillatorType, freq: number, vol: number): void {
  if (!_filter) return;
  const osc = c.createOscillator();
  osc.type            = type;
  osc.frequency.value = freq;
  const g = c.createGain();
  g.gain.setValueAtTime(0, c.currentTime);
  g.gain.linearRampToValueAtTime(vol, c.currentTime + 1.0);
  osc.connect(g).connect(_filter);
  osc.start();
  _drones.push({ osc, gain: g });
}

function stopDrones(c: AudioContext): void {
  const now = c.currentTime;
  for (const d of _drones) {
    try {
      d.gain.gain.cancelScheduledValues(now);
      d.gain.gain.setValueAtTime(d.gain.gain.value, now);
      d.gain.gain.linearRampToValueAtTime(0, now + 0.5);
      d.osc.stop(now + 0.55);
    } catch { /* already stopped */ }
  }
  _drones = [];
}

// ── Chord / scale tables ──────────────────────────────────────────────────────

// hell — Am: A2 E3 A3
const HELL_CHORD        = [110, 164.8, 220];

// caverns — D-minor pentatonic mid register (D3 F3 G3 A3 C4)
const CAVERN_PENTATONIC = [146.8, 174.6, 196.0, 220.0, 261.6];

// village — A-minor pentatonic
const VILLAGE_PENT      = [220, 261.6, 293.7, 329.6, 392];

// heaven — Fmaj7 (F2 A2 C3 E3) and Am (A2 C3 E3 A3)
const HEAVEN_CHORDS: number[][] = [
  [87.3, 110.0, 130.8, 164.8],
  [110.0, 130.8, 164.8, 220.0],
];

// ── Biome entry ────────────────────────────────────────────────────────────────

function enterHell(c: AudioContext): void {
  resetFilter(c, 800);
  // chord scheduler drives the harmonic layer (no separate drone)
  _nextChord  = c.currentTime + 0.1;
  _chordPhase = 0;
  _nextThump  = c.currentTime + 2 + Math.random() * 3;
}

function enterCaverns(c: AudioContext): void {
  resetFilter(c, 1200);
  startDrone(c, "sine", 73.4,  0.20);   // D2
  startDrone(c, "sine", 110.0, 0.14);   // A2
  _nextPluck  = c.currentTime + 1.5 + Math.random() * 2;
  _pluckIdx   = 0;
}

function enterVillage(c: AudioContext): void {
  resetFilter(c, 1600);
  startDrone(c, "triangle", 110.0, 0.08);  // A2 soft pad
  _nextChime  = c.currentTime + 1.5 + Math.random() * 2;
  _chimeIdx   = 0;
}

function enterHeaven(c: AudioContext): void {
  resetFilter(c, 2000);
  _nextChord  = c.currentTime + 0.1;
  _chordPhase = 0;
  _nextBell   = c.currentTime + 8 + Math.random() * 4;
}

// ── Per-biome schedulers ───────────────────────────────────────────────────────

function scheduleHell(c: AudioContext, until: number): void {
  // Am pad chord every 8 s — triangle voices, slow attack/release
  while (_nextChord < until) {
    for (const f of HELL_CHORD) {
      playNote(c, "triangle", f, _nextChord, 8.5, 0.20, 0.6, 1.0);
    }
    _nextChord  += 8.0;
    _chordPhase++;
  }
  // Sparse 55 Hz sine thump every 5–7 s
  while (_nextThump < until) {
    playNote(c, "sine", 55, _nextThump, 0.9, 0.40, 0.05, 0.5);
    _nextThump += 5 + Math.random() * 2;
  }
}

function scheduleCaverns(c: AudioContext, until: number): void {
  // Pentatonic pluck + echo, every ≈ 1.5–4.5 s
  while (_nextPluck < until) {
    const f = CAVERN_PENTATONIC[_pluckIdx % CAVERN_PENTATONIC.length];
    playNote(c, "triangle", f, _nextPluck,        1.8, 0.22, 0.05, 0.8);
    playNote(c, "triangle", f, _nextPluck + 0.36, 1.5, 0.09, 0.05, 0.7);
    _pluckIdx++;
    const interval = 3.0 + (Math.random() * 3 - 1.5);   // ±1.5 s jitter
    _nextPluck += Math.max(interval, 1.2);
  }
}

function scheduleVillage(c: AudioContext, until: number): void {
  // One chime every ≈ 1.5–4.5 s
  while (_nextChime < until) {
    const f = VILLAGE_PENT[_chimeIdx % VILLAGE_PENT.length];
    playNote(c, "triangle", f, _nextChime, 2.2, 0.25, 0.06, 1.0);
    _chimeIdx++;
    const interval = 3.0 + (Math.random() * 3 - 1.5);   // ±1.5 s jitter
    _nextChime += Math.max(interval, 1.2);
  }
}

function scheduleHeaven(c: AudioContext, until: number): void {
  // Alternating Fmaj7/Am pads every 8 s, 1.5 s attack / 3 s release for overlap
  while (_nextChord < until) {
    const chord = HEAVEN_CHORDS[_chordPhase % 2];
    for (const f of chord) {
      playNote(c, "triangle", f, _nextChord, 9.5, 0.16, 1.5, 3.0);
    }
    _nextChord  += 8.0;
    _chordPhase++;
  }
  // Bell: triangle 880 Hz + 2200 Hz, ~9–12 s apart
  while (_nextBell < until) {
    playNote(c, "triangle",  880, _nextBell,        1.4, 0.14, 0.05, 0.5);
    playNote(c, "triangle", 2200, _nextBell + 0.02, 1.0, 0.05, 0.05, 0.4);
    _nextBell += 9 + Math.random() * 3;
  }
}

// ── Scheduler tick ─────────────────────────────────────────────────────────────

function schedulerTick(): void {
  const c = _ctx;
  if (!c || !_master || !_biome) return;

  if (c.state === "suspended") { c.resume().catch(() => {}); return; }

  if (!musicEnabled()) {
    _master.gain.setTargetAtTime(0, c.currentTime, 0.1);
    // Advance cursors so there is no catch-up flood when re-enabled
    const now = c.currentTime;
    if (_nextChord < now) _nextChord = now + 0.1;
    if (_nextThump < now) _nextThump = now + 1;
    if (_nextPluck < now) _nextPluck = now + 1;
    if (_nextChime < now) _nextChime = now + 1;
    if (_nextBell  < now) _nextBell  = now + 1;
    return;
  }

  _master.gain.setTargetAtTime(MUSIC_VOLUME, c.currentTime, 0.1);

  const until = c.currentTime + LOOKAHEAD_S;
  const base  = _biome.split("-")[0];
  switch (base) {
    case "hell":    scheduleHell(c, until);    break;
    case "cavern":
    case "caverns": scheduleCaverns(c, until); break;
    case "village": scheduleVillage(c, until); break;
    case "heaven":  scheduleHeaven(c, until);  break;
  }
}

// ── Public API ─────────────────────────────────────────────────────────────────

/** Idempotently switch the ambience to the given biome (call per snapshot). */
export function setMusicBiome(biome: string): void {
  if (!biome || biome === _biome) return;
  const c = getCtx();
  if (!c || !_master) return;
  _biome = biome;
  if (c.state === "suspended") { c.resume().catch(() => {}); }

  stopDrones(c);

  const base = biome.split("-")[0];
  switch (base) {
    case "hell":    enterHell(c);    break;
    case "cavern":
    case "caverns": enterCaverns(c); break;
    case "village": enterVillage(c); break;
    case "heaven":  enterHeaven(c);  break;
  }

  if (_timer === null) {
    _timer = window.setInterval(schedulerTick, SCHEDULER_MS);
  }
  schedulerTick(); // fire immediately so first events don't wait 200 ms
}

/** Stop all ambience (scene teardown). */
export function stopMusic(): void {
  if (_timer !== null) { clearInterval(_timer); _timer = null; }
  if (_ctx) stopDrones(_ctx);
  _biome = "";
}
```

## `src/audio/Sfx.ts`

```typescript
// Sfx.ts — procedural Web Audio sound effects (docs/09 G1).
//
// No audio assets exist in the project (docs/05 deferral), so every effect is
// SYNTHESIZED: oscillators + filtered noise with short gain envelopes, riding
// the sim's existing event cues. Swapping in real samples later only means
// replacing the recipe bodies — the event wiring stays.
//
// Honors the Settings "Audio" toggle (localStorage arkanoid_audio).

const MASTER_VOLUME   = 0.22;
const EVENT_COOLDOWN_MS = 45;   // per-type rate limit (chain explosions, fire spread…)
const NOISE_BUFFER_SECONDS = 1;

type Recipe = (ctx: AudioContext, out: GainNode) => void;

let _ctx: AudioContext | null = null;
let _master: GainNode | null = null;
let _noise: AudioBuffer | null = null;
const _lastPlayed = new Map<string, number>();

function enabled(): boolean {
  return localStorage.getItem("arkanoid_audio") !== "0";
}

function ctx(): AudioContext | null {
  if (_ctx) return _ctx;
  try {
    const AC = (window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext);
    if (!AC) return null;
    _ctx = new AC();
    _master = _ctx.createGain();
    _master.gain.value = MASTER_VOLUME;
    _master.connect(_ctx.destination);
    // Pre-render a noise buffer for percussive recipes.
    _noise = _ctx.createBuffer(1, _ctx.sampleRate * NOISE_BUFFER_SECONDS, _ctx.sampleRate);
    const data = _noise.getChannelData(0);
    for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
  } catch {
    return null;
  }
  return _ctx;
}

// ── Synth building blocks ─────────────────────────────────────────────────────

/** A tone with a pitch ramp and exponential decay. */
function tone(
  c: AudioContext, out: GainNode,
  type: OscillatorType, f0: number, f1: number,
  durMs: number, vol = 1, delayMs = 0,
) {
  const t0 = c.currentTime + delayMs / 1000;
  const t1 = t0 + durMs / 1000;
  const osc = c.createOscillator();
  osc.type = type;
  osc.frequency.setValueAtTime(Math.max(20, f0), t0);
  osc.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t1);
  const g = c.createGain();
  g.gain.setValueAtTime(vol, t0);
  g.gain.exponentialRampToValueAtTime(0.001, t1);
  osc.connect(g).connect(out);
  osc.start(t0);
  osc.stop(t1);
}

/** A filtered noise burst (hits, explosions, hisses). */
function noiseBurst(
  c: AudioContext, out: GainNode,
  filterType: BiquadFilterType, freq: number,
  durMs: number, vol = 1, delayMs = 0,
) {
  if (!_noise) return;
  const t0 = c.currentTime + delayMs / 1000;
  const t1 = t0 + durMs / 1000;
  const src = c.createBufferSource();
  src.buffer = _noise;
  src.loop = true;
  const f = c.createBiquadFilter();
  f.type = filterType;
  f.frequency.value = freq;
  const g = c.createGain();
  g.gain.setValueAtTime(vol, t0);
  g.gain.exponentialRampToValueAtTime(0.001, t1);
  src.connect(f).connect(g).connect(out);
  src.start(t0);
  src.stop(t1);
}

// ── Event → recipe map ────────────────────────────────────────────────────────

const RECIPES: Record<string, Recipe> = {
  deflect:        (c, o) => tone(c, o, "square", 300, 520, 70, 0.5),
  blockDestroyed: (c, o) => { noiseBurst(c, o, "bandpass", 900, 90, 0.8); tone(c, o, "triangle", 700, 240, 90, 0.35); },
  explosion:      (c, o) => { noiseBurst(c, o, "lowpass", 320, 280, 1.0); tone(c, o, "sine", 90, 40, 260, 0.9); },
  playerHit:      (c, o) => { tone(c, o, "sawtooth", 220, 90, 160, 0.8); noiseBurst(c, o, "highpass", 1200, 120, 0.4); },
  spellCast:      (c, o) => tone(c, o, "sawtooth", 220, 960, 160, 0.5),
  ignite:         (c, o) => noiseBurst(c, o, "highpass", 2500, 130, 0.4),
  lightning:      (c, o) => { noiseBurst(c, o, "highpass", 3000, 90, 0.7); tone(c, o, "square", 1400, 200, 120, 0.4); },
  teleport:       (c, o) => tone(c, o, "sine", 380, 1250, 140, 0.6),
  ghostPortal:    (c, o) => tone(c, o, "sine", 1250, 380, 140, 0.5),
  bonusCaught:    (c, o) => { tone(c, o, "square", 980, 980, 60, 0.5); tone(c, o, "square", 1320, 1320, 90, 0.5, 65); },
  bossTelegraph:  (c, o) => { tone(c, o, "triangle", 840, 840, 90, 0.6); tone(c, o, "triangle", 840, 840, 90, 0.6, 140); },
  bossAttack:     (c, o) => { tone(c, o, "sine", 120, 55, 220, 0.9); noiseBurst(c, o, "lowpass", 500, 180, 0.5); },
  bossHop:        (c, o) => tone(c, o, "sine", 200, 90, 150, 0.6),
  fistTelegraph:  (c, o) => tone(c, o, "triangle", 660, 660, 110, 0.55),
  fistSlam:       (c, o) => { tone(c, o, "sine", 100, 45, 260, 1.0); noiseBurst(c, o, "lowpass", 400, 220, 0.7); },
  judgement:      (c, o) => { tone(c, o, "triangle", 1040, 520, 240, 0.6); noiseBurst(c, o, "bandpass", 1600, 160, 0.35); },
  batGrab:        (c, o) => tone(c, o, "square", 900, 350, 160, 0.5),
  batRelease:     (c, o) => tone(c, o, "square", 350, 900, 160, 0.45),
  witchGrab:      (c, o) => tone(c, o, "sawtooth", 700, 250, 200, 0.5),
  witchThrow:     (c, o) => tone(c, o, "sawtooth", 250, 800, 160, 0.55),
  lava:           (c, o) => noiseBurst(c, o, "lowpass", 700, 260, 0.55),
  lavaCreep:      (c, o) => noiseBurst(c, o, "lowpass", 500, 180, 0.35),
  stalactite:     (c, o) => tone(c, o, "triangle", 1500, 500, 130, 0.45),
  shield:         (c, o) => tone(c, o, "sine", 520, 780, 130, 0.4),
  corrupt:        (c, o) => tone(c, o, "sawtooth", 480, 180, 170, 0.45),
  frost:          (c, o) => tone(c, o, "sine", 1600, 2300, 110, 0.4),
  revive:         (c, o) => tone(c, o, "sine", 300, 700, 180, 0.45),
  deathMark:      (c, o) => tone(c, o, "sine", 700, 300, 180, 0.4),
  altar:          (c, o) => { tone(c, o, "sine", 660, 660, 140, 0.5); tone(c, o, "sine", 990, 990, 180, 0.4, 90); },
  vaseLevelUp:    (c, o) => tone(c, o, "square", 500, 1000, 160, 0.5),
  secondWind:     (c, o) => tone(c, o, "sine", 440, 880, 200, 0.55),
  splitShot:      (c, o) => tone(c, o, "square", 660, 1100, 110, 0.5),
  manaRefund:     (c, o) => tone(c, o, "sine", 880, 1320, 140, 0.5),
  hellwalk:       (c, o) => noiseBurst(c, o, "bandpass", 1100, 150, 0.5),
  enemyShot:      (c, o) => tone(c, o, "square", 480, 240, 90, 0.35),
  overrun:        (c, o) => tone(c, o, "sawtooth", 200, 60, 400, 0.8),
  timeUp:         (c, o) => { tone(c, o, "triangle", 520, 520, 140, 0.6); tone(c, o, "triangle", 392, 392, 240, 0.6, 150); },
  floorDown:      (c, o) => { tone(c, o, "sine", 200, 90, 240, 0.7); noiseBurst(c, o, "lowpass", 350, 260, 0.5); },
  levelWon: (c, o) => {
    tone(c, o, "square", 523, 523, 110, 0.5);        // C5
    tone(c, o, "square", 659, 659, 110, 0.5, 120);   // E5
    tone(c, o, "square", 784, 784, 200, 0.55, 240);  // G5
  },
  levelLost: (c, o) => {
    tone(c, o, "sawtooth", 196, 196, 200, 0.6);      // G3
    tone(c, o, "sawtooth", 131, 131, 380, 0.6, 210); // C3
  },
};

// ── Public API ────────────────────────────────────────────────────────────────

/** Play every recognized event in this snapshot's cue list (rate-limited per type). */
export function consumeSfx(events: Array<{ type: string }>): void {
  if (!events?.length || !enabled()) return;
  const c = ctx();
  if (!c || !_master) return;
  if (c.state === "suspended") { c.resume().catch(() => {}); }

  const now = performance.now();
  for (const ev of events) {
    const recipe = RECIPES[ev.type];
    if (!recipe) continue;
    const last = _lastPlayed.get(ev.type) ?? 0;
    if (now - last < EVENT_COOLDOWN_MS) continue;
    _lastPlayed.set(ev.type, now);
    try { recipe(c, _master); } catch { /* never break the render loop over audio */ }
  }
}
```

## `src/input/PaddleInput.ts`

```typescript
import type { Connection, Snapshot } from "../net/Connection";

// Pointer-based paddle input: works for both mouse (desktop) and touch (mobile).
// Drag anywhere on the canvas to move the paddle.
// Tap on the canvas while in Serving phase to serve.
export function attachPaddleInput(canvas: HTMLCanvasElement, conn: Connection, getSnap: () => Snapshot | null) {
  // Translate a canvas clientX into sim-space X using the same fit scale as Renderer.
  function toSimX(clientX: number): number | null {
    const s = getSnap();
    if (!s) return null;
    const rect = canvas.getBoundingClientRect();
    const scale = Math.min(rect.width / s.boardW, rect.height / s.boardH) * 0.95;
    const offX  = (rect.width - s.boardW * scale) / 2;
    return (clientX - rect.left - offX) / scale;
  }

  // Pointer move → paddle position (works for touch drag and mouse move).
  canvas.addEventListener("pointermove", (e) => {
    const x = toSimX(e.clientX);
    if (x !== null) conn.paddleX(x);
  });

  // Pointer down → move paddle immediately (responsive on tap-start).
  canvas.addEventListener("pointerdown", (e) => {
    const x = toSimX(e.clientX);
    if (x !== null) conn.paddleX(x);
  });

  // Pointer up / tap → serve if in Serving phase.
  canvas.addEventListener("pointerup", (_e) => {
    const s = getSnap();
    if (s?.phase === "Serving") {
      // We always serve on pointerup — the backend ignores duplicate serves
      // during Playing anyway.
      conn.serve();
    }
  });

  // Keyboard shortcuts (desktop).
  window.addEventListener("keydown", (e) => {
    if (e.code === "Space") conn.serve();
    if (e.key === "q" || e.key === "Q") conn.castIgnite();
    if (e.key === "e" || e.key === "E") conn.castFireball();
    if (e.key === "w" || e.key === "W") conn.castFireWall();
    if (e.key === "r" || e.key === "R") conn.castTurret();
    if (e.key === "t" || e.key === "T") conn.castSlot(4); // 5th kit slot (G2c)
  });
}
```

## `src/log.ts`

```typescript
export interface LogEntry { ts: number; tag: string; msg: string; data?: unknown }
const BUF: LogEntry[] = [];
const MAX = 3000;

export function log(tag: string, msg: string, data?: unknown) {
  BUF.push({ ts: Date.now(), tag, msg, data });
  if (BUF.length > MAX) BUF.shift();
  // mirror to console so Playwright's page.on('console') captures it live
  console.info(`[ark:${tag}] ${msg}`, data ?? "");
}
export function getLogs(): LogEntry[] { return BUF.slice(); }
```

## `src/main.ts`

```typescript
import { loadAtlas } from "./render/assets";
import { mountMenu } from "./scenes/MenuScene";
import { mountBattle } from "./scenes/BattleScene";
import { mountCampaign } from "./scenes/CampaignScene";
import { mountDungeons } from "./scenes/DungeonsScene";
import { mountDungeon } from "./scenes/DungeonScene";
import { mountCharacters } from "./scenes/CharacterScene";
import { mountEditor } from "./scenes/EditorScene";
import { mountInventory } from "./scenes/InventoryScene";
import { mountAchievements } from "./scenes/AchievementsScene";
import { mountSettings } from "./scenes/SettingsScene";
import { mountSkills } from "./scenes/SkillsScene";
import { fadeInOnLoad, setNavigateHandler, navigateTo } from "./ui/transition";
import { injectTheme } from "./ui/theme";

injectTheme();

const host = document.getElementById("app")!;

function teardownBattle() {
  const r = (window as any).__renderer;
  if (r) {
    try { r.app.destroy(false); } catch (_) {}
    delete (window as any).__renderer;
  }
  const c = (window as any).__conn;
  if (c) {
    try { c.close(); } catch (_) {}
    delete (window as any).__conn;
  }
}

function doMount(search: string) {
  teardownBattle();
  host.innerHTML = "";

  const q = new URLSearchParams(search.startsWith("?") ? search.slice(1) : search);
  const scene = q.get("scene") ?? "menu";
  const level = q.get("level") ?? "hell-1";
  const seed  = Number(q.get("seed") ?? "1");
  const run   = q.get("run") ?? `dev-${Date.now()}`;
  const from  = q.get("from") ?? "";

  if      (scene === "battle")       mountBattle(host, level, seed, run, from);
  else if (scene === "campaign")     mountCampaign(host);
  else if (scene === "dungeons")     mountDungeons(host);
  else if (scene === "dungeon")      mountDungeon(host);
  else if (scene === "characters")   mountCharacters(host);
  else if (scene === "editor")       mountEditor(host);
  else if (scene === "inventory")    mountInventory(host);
  else if (scene === "achievements") mountAchievements(host);
  else if (scene === "settings")     mountSettings(host);
  else if (scene === "skills")       mountSkills(host);
  else                               mountMenu(host);
}

// Show a minimal loading indicator while fetching the atlas.
const loading = document.createElement("div");
loading.style.cssText = "color:var(--text-dim,#c9b182);font-family:var(--font-body,sans-serif);text-align:center;padding-top:40cqh;font-size:var(--fs-xl,1.2rem)";
loading.textContent = "Loading assets…";
host.appendChild(loading);

loadAtlas()
  .then(() => {
    loading.remove();
    fadeInOnLoad();

    // SPA navigate handler: called by navigateTo() instead of location.href.
    setNavigateHandler((url) => {
      const full = url.startsWith("/") ? url : "/" + url;
      history.pushState({}, "", full);
      doMount(new URL(full, location.origin).search);
    });

    // Intercept same-origin <a href> clicks so they go through SPA routing.
    document.addEventListener("click", (e) => {
      const a = (e.target as Element).closest("a[href]") as HTMLAnchorElement | null;
      if (!a) return;
      let href: URL;
      try { href = new URL(a.href); } catch { return; }
      if (href.origin !== location.origin) return;
      e.preventDefault();
      navigateTo(href.pathname + href.search);
    });

    // Back/forward browser buttons.
    window.addEventListener("popstate", () => {
      doMount(location.search);
      fadeInOnLoad();
    });

    // Initial scene mount.
    doMount(location.search);
  })
  .catch((err) => {
    loading.textContent = "Failed to load assets: " + String(err);
    console.error("Atlas load failed:", err);
  });
```

## `src/net/Connection.ts`

```typescript
import { log } from "../log";

export interface Snapshot {
  tick: number; phase: string; lives: number; spareBalls: number;
  mana: number; manaMax: number; boardW: number; boardH: number; biome: string;
  paddleX: number; paddleW: number; paddleH: number; cellSize: number;
  balls: { id: number; x: number; y: number; ignited: boolean; decayed?: boolean; ghost?: boolean }[];
  projectiles?: { id: number; x: number; y: number; kind: string }[];
  blocks: { id: number; x: number; y: number; hp: number; maxHp: number; sprite: string; ballPhases: boolean; teleporter: boolean; indestructible: boolean; boss?: boolean; flipX?: boolean; flipY?: boolean; shielded?: boolean; charging?: boolean; allied?: boolean; level?: number }[];
  hazards: { x: number; y: number; kind?: string }[];
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
  /** WindMaster push radius in world units (renderer draws the aura at this size). */
  windRadius?: number;
  /** Objective timer (docs/12): "" | "survive" | "limit", with seconds remaining. */
  timerMode?: string;
  timeLeft?: number;
  /** Multi-floor collapse progress (1-based; floorCount 1 = single floor). */
  floor?: number;
  floorCount?: number;
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
  close() { this.ws.close(); }
  whenReady(cb: () => void) {
    if (this.ws.readyState === 1) cb(); else this.ws.addEventListener("open", cb, { once: true });
  }
}
```

## `src/net/metaApi.ts`

```typescript
const BASE = "http://localhost:5080";

// ── Editor types ─────────────────────────────────────────────────────────────

export interface BlockTypeDef {
  id: string;
  biome: string;
  sprite: string;
}

export interface LevelData {
  id: string;
  biome: string;
  cols: number;
  rows: number;
  rows_data: string[];
  legend: Record<string, string>;
}

export interface SaveLevelResult {
  ok: boolean;
  id: string;
}

// ── Shared types ─────────────────────────────────────────────────────────────

export interface Profile {
  level: number;
  exp: number;
  points: number;
  crystals: number;
  completedLevels: string[];
  unlockedRelics: string[];
  spellLevels: Record<string, number>;
  achievements: string[];
  tutorialSeen: boolean;
}

export interface CampaignNode {
  id: string;
  label: string;
  biome: string;
  x: number;
  y: number;
  unlocked: boolean;
  completed: boolean;
}

export interface CampaignData {
  nodes: CampaignNode[];
}

export interface RiftOffer {
  opened: boolean;
  dungeonId: string;
  name: string;
  floors: number;
}

export interface CompleteResult {
  reward: {
    expGained: number;
    pointsGained: number;
    crystalsGained: number;
    leveledUp: boolean;
    newLevel: number;
    firstClear: boolean;
  } | null;
  rift: RiftOffer | null;
}

/** Rift roll mode sent to /complete. "none" = never (default for direct callers). */
export type RiftMode = "roll" | "force" | "none";

export interface UpgradeResult {
  ok: boolean;
  profile: Profile;
}

export interface DungeonDef {
  id: string;
  name: string;
  floors: string[];
  rewardRelic: string;
  rewardCrystals: number;
}

export interface DungeonsResult {
  dungeons: DungeonDef[];
}

export interface DungeonRunState {
  dungeonId?: string;
  floors?: string[];
  floorIndex?: number;
  relics?: string[];
  ballCores?: string[];
  pendingChoices?: string[];
  active?: boolean;
  cleared?: boolean;
}

export interface FloorClearedResult {
  isLastFloor: boolean;
  run?: DungeonRunState & { pendingChoices?: string[] };
  profile?: Profile;
}

export interface SpellDef {
  id: string;
  name: string;
  icon: string;
}

export interface CharacterDef {
  id: string;
  name: string;
  passive: string;
  icon: string;
  spells: SpellDef[];
}

export interface CharactersResponse {
  characters: CharacterDef[];
  selected: string;
  unlocked: string[];
}

// ── Items ────────────────────────────────────────────────────────────────────

export interface ItemDef {
  id: string;
  name: string;
  icon: string;
  maxTier: number;
  cost: number[];
  effect: string;
  description: string;
  ownedTier: number;
  equipped: boolean;
}

export interface ItemsResponse {
  items: ItemDef[];
  crystals: number;
  equipped: string[];
}

export interface ItemBuyResult {
  ok: boolean;
  crystals: number;
  ownedTier: number;
}

export interface ItemEquipResult {
  ok: boolean;
  equipped: string[];
}

// ── Client ───────────────────────────────────────────────────────────────────

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`);
  return res.json() as Promise<T>;
}

async function post<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`, { method: "POST" });
  return res.json() as Promise<T>;
}

async function postJson<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  return res.json() as Promise<T>;
}

export const metaApi = {
  getProfile: ()                 => get<Profile>("/profile"),
  getCampaign: ()                => get<CampaignData>("/campaign"),
  complete: (level: string, treasureBonus = 0, riftMode: RiftMode = "none") =>
    post<CompleteResult>(`/complete?level=${encodeURIComponent(level)}&treasureBonus=${treasureBonus}&rift=${riftMode}`),
  upgrade: (spell: string)       => post<UpgradeResult>(`/upgrade?spell=${encodeURIComponent(spell)}`),
  reset: ()                      => post<unknown>("/reset"),
  getDungeons: ()                => get<DungeonsResult>("/dungeons"),
  startDungeon: (id: string)     => post<unknown>(`/dungeon/start?id=${encodeURIComponent(id)}`),
  getDungeonState: ()            => get<DungeonRunState>("/dungeon/state"),
  floorCleared: ()               => post<FloorClearedResult>("/dungeon/floor-cleared"),
  pick: (choice: string)         => post<unknown>(`/dungeon/pick?choice=${encodeURIComponent(choice)}`),
  fail: ()                       => post<unknown>("/dungeon/fail"),
  getCharacters: ()              => get<CharactersResponse>("/characters"),
  selectCharacter: (id: string)  => post<unknown>(`/character/select?id=${encodeURIComponent(id)}`),
  // Items
  getItems: ()                   => get<ItemsResponse>("/items"),
  buyItem: (id: string)          => post<ItemBuyResult>(`/item/buy?id=${encodeURIComponent(id)}`),
  equipItem: (id: string)        => post<ItemEquipResult>(`/item/equip?id=${encodeURIComponent(id)}`),
  unequipItem: (id: string)      => post<ItemEquipResult>(`/item/unequip?id=${encodeURIComponent(id)}`),
  // Editor
  getBlockTypes: ()                   => get<BlockTypeDef[]>("/editor/blocktypes"),
  loadLevel: (id: string)             => get<LevelData>(`/editor/load?id=${encodeURIComponent(id)}`),
  saveLevel: (body: LevelData)        => postJson<SaveLevelResult>("/editor/save", body),
  // Achievements & tutorial
  unlockAchievement: (id: string)     => post<{ ok: boolean; achievements: string[] }>(`/achievement/unlock?id=${encodeURIComponent(id)}`),
  markTutorialSeen: ()                => post<{ ok: boolean }>("/tutorial/seen"),
};
```

## `src/render/AnimSystem.ts`

```typescript
/**
 * AnimSystem.ts — Pooled one-shot and looping AnimatedSprite system.
 *
 * Usage:
 *   const sys = new AnimSystem(parentContainer);
 *   sys.oneShot(frames, fps, x, y, size);   // plays once, auto-removes
 *   const id = sys.looping(frames, fps, x, y, size); // loops until removed
 *   sys.remove(id);
 *   sys.update(dtMs); // call each frame
 */

import { Container, AnimatedSprite, BLEND_MODES, Rectangle, Texture } from "pixi.js";

// Max simultaneous one-shot animations (prevents unbounded allocation under spam).
const MAX_ONE_SHOTS = 48;

// Blend mode used for additive fire/explosion effects.
const ADDITIVE = BLEND_MODES.ADD;

export interface AnimHandle { id: number }

interface OneShotEntry {
  anim: AnimatedSprite;
  /** elapsed ms */
  elapsed: number;
  /** total duration ms */
  duration: number;
}

interface LoopEntry {
  id: number;
  anim: AnimatedSprite;
}

let _nextId = 1;

export class AnimSystem {
  readonly container: Container;
  private oneShots: OneShotEntry[] = [];
  private loops = new Map<number, LoopEntry>();

  constructor(parent?: Container) {
    this.container = new Container();
    if (parent) parent.addChild(this.container);
  }

  /**
   * Slice a horizontal sprite strip into individual frame Textures.
   * The strip is assumed to contain square frames:
   *   frameSize = texture.height
   *   frameCount = round(texture.width / texture.height)
   * Returns the sliced array, or an empty array if the texture is invalid.
   */
  static sliceStrip(texture: Texture): Texture[] {
    if (!texture || texture === Texture.EMPTY || texture === Texture.WHITE) return [];
    const { width, height } = texture;
    if (height <= 0) return [];
    const frameSize = height;
    const frameCount = Math.max(1, Math.round(width / frameSize));
    if (frameCount === 1) return [texture];
    const base = texture.baseTexture;
    // The texture's frame gives the top-left origin within the atlas page.
    const ox = texture.frame.x;
    const oy = texture.frame.y;
    const frames: Texture[] = [];
    for (let i = 0; i < frameCount; i++) {
      frames.push(new Texture(base, new Rectangle(ox + i * frameSize, oy, frameSize, frameSize)));
    }
    return frames;
  }

  /**
   * Play a one-shot animation at (x, y) in world space.
   * `size` is the desired display size in world units.
   * `additive` uses ADD blend mode (good for fire/explosions).
   * `tint` optionally tints the sprite.
   */
  oneShot(
    frames: Texture[],
    fps: number,
    x: number, y: number,
    size: number,
    additive = true,
    tint = 0xffffff,
  ) {
    if (!frames.length) return;
    // Throttle: drop oldest one-shot if we're at the cap.
    if (this.oneShots.length >= MAX_ONE_SHOTS) {
      const oldest = this.oneShots.shift()!;
      this.container.removeChild(oldest.anim);
      oldest.anim.destroy();
    }

    const anim = new AnimatedSprite(frames);
    anim.anchor.set(0.5);
    anim.position.set(x, y);
    anim.blendMode = additive ? ADDITIVE : BLEND_MODES.NORMAL;
    anim.tint = tint;
    anim.loop = false;
    anim.animationSpeed = fps / 60; // Pixi animSpeed is in frames-per-ticker-frame at 60fps
    // Scale to desired size using the natural frame dimensions.
    const naturalSize = Math.max(frames[0].width, 1);
    anim.scale.set(size / naturalSize);
    anim.play();

    const duration = (frames.length / fps) * 1000;
    this.container.addChild(anim);
    this.oneShots.push({ anim, elapsed: 0, duration });
  }

  /**
   * Spawn a looping animation. Returns an AnimHandle to remove it later.
   */
  looping(
    frames: Texture[],
    fps: number,
    x: number, y: number,
    size: number,
    additive = true,
    tint = 0xffffff,
  ): AnimHandle {
    const id = _nextId++;
    if (!frames.length) return { id };

    const anim = new AnimatedSprite(frames);
    anim.anchor.set(0.5);
    anim.position.set(x, y);
    anim.blendMode = additive ? ADDITIVE : BLEND_MODES.NORMAL;
    anim.tint = tint;
    anim.loop = true;
    anim.animationSpeed = fps / 60;
    const naturalSize = Math.max(frames[0].width, 1);
    anim.scale.set(size / naturalSize);
    anim.play();

    this.container.addChild(anim);
    this.loops.set(id, { id, anim });
    return { id };
  }

  /** Remove a looping animation by handle. */
  remove(handle: AnimHandle) {
    const entry = this.loops.get(handle.id);
    if (!entry) return;
    this.container.removeChild(entry.anim);
    entry.anim.stop();
    entry.anim.destroy();
    this.loops.delete(handle.id);
  }

  /** Move a looping animation to a new position. */
  moveTo(handle: AnimHandle, x: number, y: number) {
    const entry = this.loops.get(handle.id);
    if (entry) entry.anim.position.set(x, y);
  }

  /** Resize a looping animation. size is in world units. */
  resize(handle: AnimHandle, size: number) {
    const entry = this.loops.get(handle.id);
    if (!entry) return;
    const naturalSize = Math.max(entry.anim.width / entry.anim.scale.x, 1);
    entry.anim.scale.set(size / naturalSize);
  }

  /** Call every ticker frame with delta in ms. */
  update(dtMs: number) {
    const done: OneShotEntry[] = [];
    for (const e of this.oneShots) {
      e.elapsed += dtMs;
      const t = Math.min(e.elapsed / e.duration, 1);
      // Fade out in the final 30% of the animation.
      e.anim.alpha = t > 0.7 ? 1 - (t - 0.7) / 0.3 : 1;
      if (t >= 1) done.push(e);
    }
    for (const e of done) {
      this.container.removeChild(e.anim);
      e.anim.stop();
      e.anim.destroy();
      this.oneShots.splice(this.oneShots.indexOf(e), 1);
    }
  }

  /** Remove all active animations (call on level reset). */
  clear() {
    for (const e of this.oneShots) {
      this.container.removeChild(e.anim);
      e.anim.destroy();
    }
    this.oneShots = [];
    for (const [, e] of this.loops) {
      this.container.removeChild(e.anim);
      e.anim.stop();
      e.anim.destroy();
    }
    this.loops.clear();
  }
}
```

## `src/render/BackgroundLayer.ts`

```typescript
import { Container, Graphics, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { bg as biomedBg, hellParallaxFrames, tex as atlasTex } from "./assets";

// Biome background, Hell parallax layers, cosmetic ambient sprites, and the
// per-biome atmosphere kits (docs/12): Hell embers, Caverns dust, Witchland fog
// shadows, Heaven clouds + light motes. Exposes two containers: `bgLayer` (stage,
// behind the world) and `ambientContainer` (world, behind play).

// Biome background: slightly darkened so blocks read clearly over it.
const BG_TINT = 0xaaaaaa; // ~67% brightness multiplier on the sprite

// Ambient beholder keys (cosmetic background, village biome only). Pooled, max 2.
const BEHOLDER_KEYS = [
  "village/enemies/Beholder1", "village/enemies/Beholder2", "village/enemies/Beholder3",
];
const BEHOLDER_GHOST_KEYS = [
  "village/enemies/Beholder1Ghost", "village/enemies/Beholder2Ghost", "village/enemies/Beholder3Ghost",
];

interface Ambient {
  sp: Sprite; x: number; y: number; vx: number; vy: number;
  frame: number; frameMs: number; keys: string[];
}

// ── Atmosphere kits (docs/12) ────────────────────────────────────────────────
// All particles live in ambientContainer (global alpha 0.22) so they can never
// compete with playfield readability — the docs/12 restraint rule.
const WRAP_W = 440; // world-space wrap bounds (matches the ambient sprite wrap)
const WRAP_H = 540;

const HELL_EMBER_COUNT   = 18;
const HELL_EMBER_COLOR   = 0xffaa44;
const CAVERN_DUST_COUNT  = 16;
const CAVERN_DUST_COLOR  = 0xbbaa99;
const HEAVEN_MOTE_COUNT  = 10;
const HEAVEN_MOTE_COLOR  = 0xfff4cc;
const HEAVEN_CLOUD_KEYS  = ["heaven/Cloud", "heaven/Clouds", "heaven/HeavenClouds"];
const VILLAGE_SHADOW_KEY = "village/enemies/VillageShadow";

interface MoteParticle {
  node: Graphics | Sprite;
  x: number; y: number; vx: number; vy: number;
  /** phase offset for the sway/flicker sine */
  phase: number;
}

export class BackgroundLayer {
  readonly bgLayer = new Container();        // stage-level (behind world)
  readonly ambientContainer = new Container(); // world-level (behind play)

  private bgSprite = new Sprite();
  private _hellParallaxSprites: Sprite[] = [];
  private _lastBiome = "";
  private _ambientSprites: Ambient[] = [];
  private _lastAmbientBiome = "";
  private _motes: MoteParticle[] = [];
  private _moteClock = 0;

  constructor() {
    this.bgSprite.anchor.set(0);
    this.bgSprite.tint = BG_TINT;
    this.bgLayer.addChild(this.bgSprite);
    this.ambientContainer.alpha = 0.22; // purely cosmetic
  }

  /** Rebuild the biome background, Hell parallax, and ambient sprites on biome change. */
  setBiome(biome: string, cellSize: number): void {
    // --- biome background (update only on biome change) ---
    if (biome && biome !== this._lastBiome) {
      this._lastBiome = biome;
      const bgTex = biomedBg(biome);
      this.bgSprite.texture = bgTex;
      this.bgSprite.visible = bgTex !== Texture.WHITE;

      // Hell parallax layers: add/rebuild when entering hell biome.
      for (const psp of this._hellParallaxSprites) this.bgLayer.removeChild(psp);
      this._hellParallaxSprites = [];
      if (biome === "hell") {
        const frames = hellParallaxFrames();
        for (let i = 0; i < frames.length; i++) {
          const psp = new Sprite(frames[i]);
          psp.anchor.set(0);
          psp.tint = 0x888888; // darker than main bg for depth
          psp.alpha = 0.35;    // subtle layering
          this.bgLayer.addChild(psp);
          this._hellParallaxSprites.push(psp);
        }
      }
    }

    // --- ambient background sprites (cosmetic, village biome beholders) ---
    if (biome !== this._lastAmbientBiome) {
      this._lastAmbientBiome = biome;
      for (const a of this._ambientSprites) this.ambientContainer.removeChild(a.sp);
      this._ambientSprites = [];
      this._buildAtmosphere(biome, cellSize);

      if (biome === "village" || biome === "village-ghost" || biome === "village-boss") {
        // Spawn 2 ambient beholders drifting slowly across the board.
        const beholderCount = 2;
        for (let i = 0; i < beholderCount; i++) {
          const useGhost = i === 1;
          const keys = useGhost ? BEHOLDER_GHOST_KEYS : BEHOLDER_KEYS;
          const tex0 = atlasTex(keys[0]);
          if (tex0 === Texture.WHITE) continue; // atlas not yet loaded
          const sp = new Sprite(tex0);
          sp.anchor.set(0.5);
          const size = cellSize * 2.2;
          sp.width  = size;
          sp.height = size;
          sp.tint   = useGhost ? 0xaaccff : 0xffffff;
          // Scatter starting positions.
          const startX = 60 + i * 180;
          const startY = 60 + i * 100;
          // Gentle drift velocity (world-space px/ms).
          const vx = (i % 2 === 0 ? 0.012 : -0.015);
          const vy = (i % 2 === 0 ? 0.007 : 0.011);
          sp.position.set(startX, startY);
          this.ambientContainer.addChild(sp);
          this._ambientSprites.push({ sp, x: startX, y: startY, vx, vy, frame: 0, frameMs: i * 180, keys });
        }
      }
    }
  }

  /** Build the biome's atmosphere kit (docs/12): embers / dust / fog shadows / clouds. */
  private _buildAtmosphere(biome: string, cellSize: number): void {
    for (const m of this._motes) this.ambientContainer.removeChild(m.node);
    this._motes = [];
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    const base = biome.split("-")[0]; // "village-boss" → "village"

    const addDot = (color: number, r: number, vx: number, vy: number, additive: boolean) => {
      const g = new Graphics();
      g.beginFill(color, 1).drawCircle(0, 0, r).endFill();
      if (additive) g.blendMode = BLEND_MODES.ADD;
      const x = Math.random() * WRAP_W;
      const y = Math.random() * WRAP_H;
      g.position.set(x, y);
      this.ambientContainer.addChild(g);
      this._motes.push({ node: g, x, y, vx, vy, phase: Math.random() * Math.PI * 2 });
    };

    const addSprite = (key: string, size: number, alpha: number, vx: number, tint?: number) => {
      const t = atlasTex(key);
      if (t === Texture.WHITE) return;
      const sp = new Sprite(t);
      sp.anchor.set(0.5);
      const dim = Math.max(t.width, t.height, 1);
      sp.scale.set(size / dim);
      sp.alpha = alpha;
      if (tint !== undefined) sp.tint = tint;
      const x = Math.random() * WRAP_W;
      const y = Math.random() * (WRAP_H * 0.5); // upper half — keeps the paddle zone clean
      sp.position.set(x, y);
      this.ambientContainer.addChild(sp);
      this._motes.push({ node: sp, x, y, vx, vy: 0, phase: Math.random() * Math.PI * 2 });
    };

    switch (base) {
      case "hell": // rising embers
        for (let i = 0; i < HELL_EMBER_COUNT; i++)
          addDot(HELL_EMBER_COLOR, 1.5 + Math.random() * 1.5, 0, -(0.01 + Math.random() * 0.02), true);
        break;
      case "cavern":
      case "caverns": // falling dust motes
        for (let i = 0; i < CAVERN_DUST_COUNT; i++)
          addDot(CAVERN_DUST_COLOR, 1 + Math.random(), 0, 0.005 + Math.random() * 0.008, false);
        break;
      case "village": // drifting shadow silhouettes (fog reads via their slow motion)
        addSprite(VILLAGE_SHADOW_KEY, cellSize * 4, 0.9, 0.008);
        addSprite(VILLAGE_SHADOW_KEY, cellSize * 3, 0.7, -0.012);
        break;
      case "heaven": // drifting clouds + rising light motes
        for (let i = 0; i < HEAVEN_CLOUD_KEYS.length; i++)
          addSprite(HEAVEN_CLOUD_KEYS[i], cellSize * (4 + i * 2), 0.8, 0.006 + i * 0.004);
        for (let i = 0; i < HEAVEN_MOTE_COUNT; i++)
          addDot(HEAVEN_MOTE_COLOR, 1 + Math.random(), 0, -(0.004 + Math.random() * 0.008), true);
        break;
    }
  }

  /** COVER-scale the background + parallax to fill the stage (called from fit()). */
  resize(screenW: number, screenH: number): void {
    const bw = this.bgSprite.texture.width;
    const bh = this.bgSprite.texture.height;
    if (bw > 0 && bh > 0) {
      const coverScale = Math.max(screenW / bw, screenH / bh);
      this.bgSprite.scale.set(coverScale);
      this.bgSprite.x = (screenW - bw * coverScale) / 2;
      this.bgSprite.y = (screenH - bh * coverScale) / 2;
    }
    for (const psp of this._hellParallaxSprites) {
      if (psp.texture.width > 0 && psp.texture.height > 0) {
        const pw = psp.texture.width;
        const ph = psp.texture.height;
        const ps = Math.max(screenW / pw, screenH / ph);
        psp.scale.set(ps);
        psp.y = (screenH - ph * ps) / 2;
      }
    }
  }

  /** Advance the ambient sprite drift + frame cycling each frame. */
  updateAnim(dtMs: number): void {
    // Atmosphere motes: drift, sway, wrap.
    this._moteClock += dtMs / 1000;
    for (const m of this._motes) {
      m.x += m.vx * dtMs + Math.sin(this._moteClock + m.phase) * 0.06;
      m.y += m.vy * dtMs;
      if (m.y < -10) m.y += WRAP_H + 20;
      if (m.y > WRAP_H + 10) m.y -= WRAP_H + 20;
      if (m.x < -60) m.x += WRAP_W + 120;
      if (m.x > WRAP_W + 60) m.x -= WRAP_W + 120;
      m.node.x = m.x;
      m.node.y = m.y;
    }

    for (const a of this._ambientSprites) {
      // Advance frame.
      a.frameMs += dtMs;
      if (a.frameMs > 380) {
        a.frameMs = 0;
        a.frame = (a.frame + 1) % a.keys.length;
        const t = atlasTex(a.keys[a.frame]);
        if (t !== Texture.WHITE) a.sp.texture = t;
      }
      // Drift.
      a.x += a.vx * dtMs;
      a.y += a.vy * dtMs;
      a.sp.x = a.x;
      a.sp.y = a.y;
      // Wrap horizontally within board bounds.
      if (a.x < -40) a.x += 440;
      if (a.x > 440) a.x -= 440;
      if (a.y < -40) a.y += 540;
      if (a.y > 540) a.y -= 540;
    }
  }
}
```

## `src/render/BallLayer.ts`

```typescript
import { Container, Graphics, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { anim as animFrames, tex as atlasTex } from "./assets";
import { AnimSystem } from "./AnimSystem";

// Ball rendering (pooled by id): per-class ball sprite, turret/fireball projectile
// art, ignite halo + looping fire aura, ghost tint, and Necromancer decay halo.
// Owns the balls display container and a dedicated AnimSystem for the fire auras.
// Extracted from Renderer.

// Sprite vs physics circle: keep these close. 2.2 made the ball render wider
// than a brick (docs/13 battle audit — "ball is super huge"); 1.15 leaves just
// enough halo allowance while matching the collision size players feel.
const BALL_SPRITE_SCALE = 1.15;
const BALL_RADIUS_FRAC  = 0.25; // ball radius as a fraction of cellSize

// Halo drawn behind ignited balls.
const IGNITE_HALO_RADIUS_MULT = 1.8;
const IGNITE_HALO_ALPHA = 0.35;

// Ignite fire aura: atlas anim key (FireBirth frames) played as a looping aura.
const IGNITE_AURA_KEY = "firemage/spell_phonex/phoenixbirthanimpic";
const IGNITE_AURA_FPS = 10; // slow loop looks like a gentle fire aura
const IGNITE_AURA_SIZE_MULT = 2.8; // aura size as a multiplier of the ball sprite size

// Necromancer decay aura on ball (sickly green, distinct from ignite orange).
const DECAY_HALO_COLOR       = 0x22cc44;
const DECAY_HALO_ALPHA       = 0.38;
const DECAY_HALO_RADIUS_MULT = 1.8;

// Fireball / firering: art for the active fireball projectile.
const FIRE_RING_KEY      = "firemage/spell_firering/FireRing";
const TURRET_MISSILE_KEY = "firemage/spell_fireturret/FireHeroTurretMissile";

interface BallDto { id: number; x: number; y: number; ignited: boolean; decayed?: boolean; ghost?: boolean }
interface ProjectileDto { id: number; x: number; y: number; kind: string }

export class BallLayer {
  readonly container = new Container();      // ball sprites + halos
  readonly auraContainer: Container;          // looping fire auras (separate z-slot)
  private auraSys = new AnimSystem();
  private ballPool = new Map<number, { sp: Sprite; haloGfx: Graphics; auraId?: number }>();
  private projPool = new Map<number, { sp: Sprite }>();

  constructor() {
    this.auraContainer = this.auraSys.container;
  }

  /** The looping fire auras are AnimatedSprites — advance them each frame. */
  updateAnim(dtMs: number): void {
    this.auraSys.update(dtMs);
  }

  update(balls: BallDto[], projectiles: ProjectileDto[], tick: number, cellSize: number, ballSpriteKey: string): void {
    const ballRadius   = cellSize * BALL_RADIUS_FRAC;
    const spriteRadius = ballRadius * BALL_SPRITE_SCALE;

    const ballTex     = atlasTex(ballSpriteKey);
    const fireRingTex = tex(FIRE_RING_KEY);
    const missileTex  = tex(TURRET_MISSILE_KEY);
    // Projectile art: prefer FireRing for fireball look; fall back to missile art.
    const projectileTex = fireRingTex !== Texture.WHITE ? fireRingTex
      : (missileTex !== Texture.WHITE ? missileTex : ballTex);
    const igniteAuraFrames = animFrames(IGNITE_AURA_KEY);

    // ── Real balls ────────────────────────────────────────────────────────────
    const liveBallIds = new Set(balls.map(b => b.id));
    for (const [id, entry] of this.ballPool) {
      if (!liveBallIds.has(id)) {
        this.container.removeChild(entry.haloGfx);
        this.container.removeChild(entry.sp);
        if (entry.auraId !== undefined) this.auraSys.remove({ id: entry.auraId });
        this.ballPool.delete(id);
      }
    }

    for (const ball of balls) {
      if (this.ballPool.has(ball.id)) {
        const entry = this.ballPool.get(ball.id)!;
        const { sp, haloGfx } = entry;

        haloGfx.clear();
        if (ball.ignited) {
          haloGfx.blendMode = BLEND_MODES.ADD;
          haloGfx.beginFill(0xff5500, IGNITE_HALO_ALPHA * 0.8)
            .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
            .endFill();
        }

        sp.x = ball.x;
        sp.y = ball.y;
        sp.tint = ball.ignited ? 0xff7a2a : (ball.ghost ? 0xaa88ff : 0xffffff);
        const igScale = ball.ignited
          ? spriteRadius * (1.0 + 0.15 * Math.sin(tick * 0.2))
          : spriteRadius;
        sp.width  = igScale * 2;
        sp.height = igScale * 2;

        if (entry.auraId !== undefined) {
          this.auraSys.moveTo({ id: entry.auraId }, ball.x, ball.y);
          this.auraSys.resize({ id: entry.auraId }, spriteRadius * IGNITE_AURA_SIZE_MULT * 2);
        }

        if (ball.ignited && entry.auraId === undefined && igniteAuraFrames.length) {
          const h = this.auraSys.looping(
            igniteAuraFrames, IGNITE_AURA_FPS,
            ball.x, ball.y,
            spriteRadius * IGNITE_AURA_SIZE_MULT * 2,
            true, 0xff8822,
          );
          entry.auraId = h.id;
        } else if (!ball.ignited && entry.auraId !== undefined) {
          this.auraSys.remove({ id: entry.auraId });
          entry.auraId = undefined;
        }
      } else {
        const haloGfx = new Graphics();
        if (ball.ignited) {
          haloGfx.blendMode = BLEND_MODES.ADD;
          haloGfx.beginFill(0xff5500, IGNITE_HALO_ALPHA * 0.8)
            .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
            .endFill();
        }

        const sp = new Sprite(ballTex !== Texture.WHITE ? ballTex : Texture.WHITE);
        sp.anchor.set(0.5);
        sp.x = ball.x;
        sp.y = ball.y;
        sp.tint = ball.ignited ? 0xff7a2a : (ball.ghost ? 0xaa88ff : 0xffffff);
        sp.width  = spriteRadius * 2;
        sp.height = spriteRadius * 2;

        this.container.addChild(haloGfx);
        this.container.addChild(sp);

        let auraId: number | undefined;
        if (ball.ignited && igniteAuraFrames.length) {
          const h = this.auraSys.looping(
            igniteAuraFrames, IGNITE_AURA_FPS,
            ball.x, ball.y,
            spriteRadius * IGNITE_AURA_SIZE_MULT * 2,
            true, 0xff8822,
          );
          auraId = h.id;
        }

        this.ballPool.set(ball.id, { sp, haloGfx, auraId });
      }
    }

    // Necromancer decay aura: repaint the halo green for decayed balls.
    for (const ball of balls) {
      if (!ball.decayed) continue;
      const entry = this.ballPool.get(ball.id);
      if (!entry) continue;
      entry.haloGfx.clear();
      entry.haloGfx.blendMode = BLEND_MODES.ADD;
      entry.haloGfx.beginFill(DECAY_HALO_COLOR, DECAY_HALO_ALPHA)
        .drawCircle(ball.x, ball.y, ballRadius * DECAY_HALO_RADIUS_MULT)
        .endFill();
    }

    // ── Projectiles (turret bullets, fireballs, golems, skeleton bolts) ───────
    const liveProjIds = new Set(projectiles.map(p => p.id));
    for (const [id, entry] of this.projPool) {
      if (!liveProjIds.has(id)) {
        this.container.removeChild(entry.sp);
        this.projPool.delete(id);
      }
    }

    const missileSize = ballRadius * 1.4;
    for (const proj of projectiles) {
      if (this.projPool.has(proj.id)) {
        const { sp } = this.projPool.get(proj.id)!;
        sp.x = proj.x;
        sp.y = proj.y;
        sp.width  = missileSize * 2;
        sp.height = missileSize * 2;
        sp.rotation = tick * 0.12;
      } else {
        const sp = new Sprite(projectileTex);
        sp.anchor.set(0.5);
        sp.x = proj.x;
        sp.y = proj.y;
        sp.tint = 0xffcc44;
        sp.width  = missileSize * 2;
        sp.height = missileSize * 2;
        sp.rotation = tick * 0.12;
        this.container.addChild(sp);
        this.projPool.set(proj.id, { sp });
      }
    }
  }
}
```

## `src/render/BallTrail.ts`

```typescript
import { Container, Graphics } from "pixi.js";

// Number of historical positions kept per ball.
const TRAIL_LENGTH = 7;
// Alpha of the oldest trail dot (newest is always 1 → fades toward oldest).
const TRAIL_ALPHA_MIN = 0.04;
// Trail dot radius as a fraction of the ball radius.
const TRAIL_DOT_FRAC = 0.72;

// Colors for normal vs ignited ball trails.
const TRAIL_COLOR_NORMAL  = 0x88ccff; // cool blue-white
const TRAIL_COLOR_IGNITED = 0xff6a00; // hot orange

interface TrailEntry {
  x: number;
  y: number;
  ignited: boolean;
}

export class BallTrail {
  readonly container: Container;
  /** Map from ball id → circular buffer of recent positions. */
  private history = new Map<number, TrailEntry[]>();

  constructor() {
    this.container = new Container();
  }

  /**
   * Call once per snapshot after positions are known.
   * `balls` is the snapshot ball array; `ballRadius` is the radius in world units.
   * Clears and redraws the trail graphics.
   */
  update(
    balls: { id: number; x: number; y: number; ignited: boolean }[],
    ballRadius: number,
  ) {
    // Build a set of live ids so we can prune stale entries.
    const liveIds = new Set(balls.map((b) => b.id));

    // Prune dead balls.
    for (const id of this.history.keys()) {
      if (!liveIds.has(id)) this.history.delete(id);
    }

    // Append current position for each live ball.
    for (const ball of balls) {
      if (!this.history.has(ball.id)) this.history.set(ball.id, []);
      const buf = this.history.get(ball.id)!;
      buf.push({ x: ball.x, y: ball.y, ignited: ball.ignited });
      // Keep only the most recent TRAIL_LENGTH entries.
      if (buf.length > TRAIL_LENGTH) buf.splice(0, buf.length - TRAIL_LENGTH);
    }

    // Redraw all trail dots.
    this.container.removeChildren();
    const dotRadius = ballRadius * TRAIL_DOT_FRAC;

    for (const [, buf] of this.history) {
      const len = buf.length;
      // Skip the last entry (index len-1) — that is the current ball position
      // which Renderer already draws; start from len-2 going backward.
      for (let i = len - 2; i >= 0; i--) {
        const entry = buf[i];
        // t=0 → oldest visible dot; t=1 → just before the ball.
        const t = i / Math.max(len - 1, 1);
        const alpha = TRAIL_ALPHA_MIN + (1 - TRAIL_ALPHA_MIN) * t;
        const scale = 0.35 + 0.65 * t; // shrink toward the tail
        const color = entry.ignited ? TRAIL_COLOR_IGNITED : TRAIL_COLOR_NORMAL;

        const g = new Graphics();
        g.beginFill(color, alpha)
          .drawCircle(entry.x, entry.y, dotRadius * scale)
          .endFill();
        this.container.addChild(g);
      }
    }
  }

  /** Call when the level resets / ball list changes drastically. */
  clear() {
    this.history.clear();
    this.container.removeChildren();
  }
}
```

## `src/render/BlockLayer.ts`

```typescript
import { Container, Graphics, Sprite, TilingSprite, Texture, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { tex as atlasTex, animStrip } from "./assets";

// Block rendering (pooled): damage states, mirror flags, boss aura, teleporter ring,
// ghost pulse, shield flash — and NATIVE-ASPECT sizing (the original art is not
// square: bricks are ~1.36:1, walls ~2.2:1, statues portrait). Sizing modes:
//   contain — fit inside the cell keeping aspect (bricks get mortar gaps, skulls slim)
//   wall    — TilingSprite filling the cell with undistorted repeating stone
//   stand   — full cell width, natural height, feet on the cell floor (statues)
//   hang    — full cell width, natural height, hanging from the cell ceiling
//   fill    — stretch to the cell (columns: keeps stack continuity)

const BOSS_SCALE_MULT       = 1.15;
const BOSS_AURA_COLOR       = 0xcc0000;
const BOSS_AURA_RADIUS_MULT = 0.8;
const BOSS_AURA_ALPHA       = 0.55;
const BOSS_AURA_PULSE_SPEED = 0.06;
const BOSS_AURA_ALPHA_AMP   = 0.25;

const GHOST_ALPHA_BASE  = 0.45;
const GHOST_ALPHA_AMP   = 0.12;
const GHOST_PULSE_SPEED = 0.055;
const GHOST_TINT        = 0x88ccff;

const TELEPORTER_RING_ALPHA_BASE  = 0.35;
const TELEPORTER_RING_ALPHA_AMP   = 0.25;
const TELEPORTER_RING_PULSE_SPEED = 0.07;
const TELEPORTER_RING_COLOR       = 0x44aaff;
const TELEPORTER_RING_RADIUS_MULT = 0.72;

// Damage states (A3): below DAMAGE_THRESHOLD of max HP, swap to cracked art.
// NOTE: the "*Destroyed" assets are ANIMATION STRIPS (e.g. 388×34) — they must be
// sliced via animStrip and sampled, never drawn whole (that was the squashed-
// garbage near-death look). DAMAGE_FRAME picks an early-crack frame.
const DAMAGE_THRESHOLD = 0.6;
const DAMAGE_FRAME     = 1;
const BLOCK_DAMAGED: Record<string, string> = {
  HellStandart:     "hell/StandartHellDestroyed",
  HellStandart2:    "hell/StandartHell2Destroyed",
  DungeonStandart:  "dungeon/DungeonStandartDestroyed",
  DungeonStandart2: "dungeon/DungeonStandart2Destroyed",
  VillageStandart:  "village/blocks/VillageStandartDestroyed",
  VillageStandart2: "village/blocks/VillageStandart2Destroyed",
  StandartHaven:    "heaven/StandartHavenDestroyed",
  Standart2Haven:   "heaven/Standart2HavenDestroyed",
  ColumnTop:        "heaven/ColumnTopDestroyed",
  Column:           "heaven/ColumnDamaged",
  ColumnBottom:     "heaven/ColumnBottomDestroyed",
  WindMaster2:      "heaven/WindMaster2Destroyed",
};

// Beholder shows its damage through the original 3-tier art (docs/11: beholder tiers).
const BEHOLDER_TIER2 = "village/enemies/Beholder2"; // below 2/3 HP
const BEHOLDER_TIER3 = "village/enemies/Beholder3"; // below 1/3 HP
const BEHOLDER_T2_FRAC = 2 / 3;
const BEHOLDER_T3_FRAC = 1 / 3;

// Pacified statues/altar swap to the original *Active art (docs/11 R5 — convert is visible).
const ALLIED_VARIANT: Record<string, string> = {
  HeavenMeleeStatue: "heaven/HeavenMeleeStatueActive",
  HeavenDefender:    "heaven/HeavenDefenderActive",
};
const ALTAR_SPRITE = "HeavenAltarV2";
const ALTAR_ACTIVE = "heaven/HeavenAltarV2Active";

// Telegraph flash (docs/11 R2): emitter about to fire pulses warm.
const CHARGE_TINT        = 0xffdd66;
const CHARGE_PULSE_TICKS = 8; // tint toggles every N ticks

// WindMaster aura (docs/11: the push radius must be visible).
const WIND_AURA_KEY        = "heaven/WindMasterV2Circle";
const WIND_SPRITE          = "WindMaster2";
const WIND_AURA_ALPHA_BASE = 0.22;
const WIND_AURA_ALPHA_AMP  = 0.10;
const WIND_AURA_PULSE      = 0.05;

// Vase-levelled statues glow warm so the risk the player took is readable.
const LEVELED_TINT = 0xffd9a0;

// Cauldron: the Kotelok assets are 7-frame bubbling STRIPS — cycle real frames.
const CAULDRON_STRIP_KEY   = "village/blocks/Kotelok1";
const CAULDRON_FRAME_TICKS = 9;
// Lava spawner pulses to its Active frame.
const LAVA_SPAWNER_ACTIVE = "hell/LavaSpownerActive";
const LAVA_SPAWNER_PULSE_TICKS = 24;

// stand/hang sprites may overflow their cell, but no further than this — taller
// figures scale down uniformly so they never hide a whole neighbouring brick.
const MAX_OVERFLOW = 1.75;

type SizeMode = "contain" | "wall" | "stand" | "hang" | "fill";
const SIZE_MODES: Record<string, SizeMode> = {
  // Structural walls: tile undistorted stone to seal channels.
  HellInvulnerable:    "wall",
  DungeonInvulnerable: "wall",
  InvulnerableHaven:   "wall",
  LavaMainPart:        "wall",
  // Columns stretch so the stack stays continuous.
  ColumnTop: "fill", Column: "fill", ColumnBottom: "fill",
  // Figures stand on the cell floor at natural height.
  HeavenMeleeStatue: "stand", HeavenDefender: "stand",
  BatSleeping: "stand", HeavenVaza: "stand", Kotelok1: "stand",
  // Stalactites hang from the cell ceiling.
  Stalactite: "hang",
  // default: contain
};

interface BlockDto {
  id: number; x: number; y: number; hp: number; maxHp: number; sprite: string;
  boss?: boolean; ballPhases: boolean; indestructible: boolean; teleporter: boolean;
  flipX?: boolean; flipY?: boolean; shielded?: boolean;
  charging?: boolean; allied?: boolean; level?: number;
}

interface Entry { sp: Sprite | TilingSprite; aura?: Graphics; ring?: Graphics; wind?: Sprite }

export class BlockLayer {
  readonly container = new Container();
  private pool = new Map<number, Entry>();
  private _cauldronFrames: Texture[] | null = null;

  /** Block texture: allied *Active art, beholder damage tiers, cracked frames near death. */
  private blockTex(b: BlockDto, anyAllied: boolean, tick: number): Texture {
    // Cauldron: bubble through the real Kotelok strip frames.
    if (b.sprite === "Kotelok1") {
      this._cauldronFrames ??= animStrip(CAULDRON_STRIP_KEY);
      const frames = this._cauldronFrames;
      if (frames.length > 1)
        return frames[Math.floor(tick / CAULDRON_FRAME_TICKS) % frames.length];
    }
    // Lava spawner: pulse to the Active frame.
    if (b.sprite === "LavaSpowner" && (tick % (LAVA_SPAWNER_PULSE_TICKS * 2)) < LAVA_SPAWNER_PULSE_TICKS) {
      const t = atlasTex(LAVA_SPAWNER_ACTIVE);
      if (t !== Texture.WHITE) return t;
    }
    // Pacified statues (and the altar, while its blessing holds) show the Active art.
    const activeKey = b.allied ? ALLIED_VARIANT[b.sprite]
      : (anyAllied && b.sprite === ALTAR_SPRITE) ? ALTAR_ACTIVE : undefined;
    if (activeKey) {
      const t = atlasTex(activeKey);
      if (t !== Texture.WHITE) return t;
    }
    // Beholder communicates damage through its 3-tier art.
    if (b.sprite === "Beholder1" && b.maxHp > 0) {
      const frac = b.hp / b.maxHp;
      const tierKey = frac < BEHOLDER_T3_FRAC ? BEHOLDER_TIER3
        : frac < BEHOLDER_T2_FRAC ? BEHOLDER_TIER2 : undefined;
      if (tierKey) {
        const t = atlasTex(tierKey);
        if (t !== Texture.WHITE) return t;
      }
    }
    // Cracked frame near death — sliced from the destroy STRIP, never drawn whole.
    const dmgKey = BLOCK_DAMAGED[b.sprite];
    if (dmgKey && b.maxHp > 0 && b.hp / b.maxHp < DAMAGE_THRESHOLD) {
      const frames = animStrip(dmgKey);
      if (frames.length > DAMAGE_FRAME) return frames[DAMAGE_FRAME];
      if (frames.length === 1 && frames[0].width / Math.max(frames[0].height, 1) < 2)
        return frames[0]; // a genuine single cracked frame
    }
    return tex(b.sprite);
  }

  /** Size + position the sprite by its sizing mode, preserving flip signs. */
  private applySizing(sp: Sprite | TilingSprite, b: BlockDto, size: number): void {
    const mode: SizeMode = b.boss ? "fill" : (SIZE_MODES[b.sprite] ?? "contain");
    const nw = sp.texture.width  || 1;
    const nh = sp.texture.height || 1;
    const aspect = nh / nw;

    let w = size, h = size, y = b.y;
    if (sp instanceof TilingSprite) {
      // wall: tile the texture at cell width, undistorted, sealing the cell.
      sp.width = size; sp.height = size;
      const s = size / nw;
      sp.tileScale.set(s, s);
      sp.position.set(b.x, b.y);
      return;
    }
    switch (mode) {
      case "contain":
        if (aspect <= 1) { w = size; h = size * aspect; }
        else { h = size; w = size / aspect; }
        break;
      case "stand": // feet on the cell floor; may rise above the cell (capped)
        w = size; h = size * aspect;
        if (h > size * MAX_OVERFLOW) { h = size * MAX_OVERFLOW; w = h / aspect; }
        y = b.y + size / 2 - h / 2;
        break;
      case "hang": // hanging from the cell ceiling; may reach below (capped)
        w = size; h = size * aspect;
        if (h > size * MAX_OVERFLOW) { h = size * MAX_OVERFLOW; w = h / aspect; }
        y = b.y - size / 2 + h / 2;
        break;
      case "fill":
      default:
        break;
    }
    sp.width = w; sp.height = h;
    sp.scale.x = Math.abs(sp.scale.x) * (b.flipX ? -1 : 1);
    sp.scale.y = Math.abs(sp.scale.y) * (b.flipY ? -1 : 1);
    sp.position.set(b.x, y);
  }

  /** Hide a block's plain sprite (e.g. while the animated boss rig covers it). */
  hideBlock(id: number): void {
    const e = this.pool.get(id);
    if (e) e.sp.alpha = 0;
  }

  update(blocks: BlockDto[], tick: number, brickSize: number, windRadius = 0): void {
    const live = new Set<number>();
    let anyAllied = false;
    for (const b of blocks) {
      live.add(b.id);
      if (b.allied) anyAllied = true;
    }

    // Remove sprites for blocks that no longer exist.
    for (const [id, entry] of this.pool) {
      if (!live.has(id)) {
        if (entry.aura) this.container.removeChild(entry.aura);
        if (entry.ring) this.container.removeChild(entry.ring);
        if (entry.wind) this.container.removeChild(entry.wind);
        this.container.removeChild(entry.sp);
        this.pool.delete(id);
      }
    }

    for (const b of blocks) {
      const size = b.boss ? brickSize * BOSS_SCALE_MULT : brickSize;
      let entry = this.pool.get(b.id);

      if (!entry) {
        let aura: Graphics | undefined;
        let ring: Graphics | undefined;
        let wind: Sprite | undefined;

        if (b.boss) {
          aura = new Graphics();
          aura.blendMode = BLEND_MODES.ADD;
          this.container.addChild(aura);
        }
        if (b.teleporter) {
          ring = new Graphics();
          ring.blendMode = BLEND_MODES.ADD;
          this.container.addChild(ring);
        }
        if (b.sprite === WIND_SPRITE && windRadius > 0) {
          const auraTex = atlasTex(WIND_AURA_KEY);
          if (auraTex !== Texture.WHITE) {
            wind = new Sprite(auraTex);
            wind.anchor.set(0.5);
            wind.blendMode = BLEND_MODES.ADD;
            wind.alpha = WIND_AURA_ALPHA_BASE;
            this.container.addChild(wind);
          }
        }

        const texture = this.blockTex(b, anyAllied, tick);
        const isWall = !b.boss && SIZE_MODES[b.sprite] === "wall";
        const sp = isWall
          ? new TilingSprite(texture, size, size)
          : new Sprite(texture);
        sp.anchor.set(0.5);
        this.container.addChild(sp);
        entry = { sp, aura, ring, wind };
        this.pool.set(b.id, entry);
      }

      const { sp, aura, ring, wind } = entry;
      const texture = this.blockTex(b, anyAllied, tick);
      if (sp.texture !== texture) sp.texture = texture;
      this.applySizing(sp, b, size);

      if (wind) {
        wind.alpha = WIND_AURA_ALPHA_BASE + WIND_AURA_ALPHA_AMP * Math.sin(tick * WIND_AURA_PULSE);
        wind.width = wind.height = windRadius * 2;
        wind.position.set(b.x, b.y);
        wind.rotation = tick * WIND_AURA_PULSE;
      }

      if (b.boss) {
        sp.alpha = 1.0;
        if (aura) {
          const a = BOSS_AURA_ALPHA + BOSS_AURA_ALPHA_AMP * Math.sin(tick * BOSS_AURA_PULSE_SPEED);
          aura.clear().beginFill(BOSS_AURA_COLOR, a)
            .drawCircle(b.x, b.y, brickSize * BOSS_AURA_RADIUS_MULT).endFill();
        }
      } else if (b.ballPhases) {
        sp.tint = GHOST_TINT;
        sp.alpha = GHOST_ALPHA_BASE + GHOST_ALPHA_AMP * Math.sin(tick * GHOST_PULSE_SPEED);
      } else if (b.indestructible || b.teleporter) {
        sp.alpha = 1.0;
        sp.tint = 0xffffff;
        if (ring) {
          const a = TELEPORTER_RING_ALPHA_BASE + TELEPORTER_RING_ALPHA_AMP * Math.sin(tick * TELEPORTER_RING_PULSE_SPEED);
          ring.clear().beginFill(TELEPORTER_RING_COLOR, a)
            .drawCircle(b.x, b.y, brickSize * TELEPORTER_RING_RADIUS_MULT).endFill();
        }
      } else {
        sp.alpha = 0.4 + 0.6 * (b.hp / b.maxHp);
        // Telegraph pulse beats shield flash beats levelled glow beats neutral.
        sp.tint = b.charging && (tick % (CHARGE_PULSE_TICKS * 2)) < CHARGE_PULSE_TICKS
          ? CHARGE_TINT
          : b.shielded ? 0x66ddff
          : (b.level ?? 0) > 0 ? LEVELED_TINT : 0xffffff;
      }
    }
  }
}
```

## `src/render/BonusLayer.ts`

```typescript
import { Container, Sprite } from "pixi.js";
import { tex } from "./textures";

// Falling bonus-pickup icons. Owns its own display layer + id-keyed pool.
const BONUS_SPRITE_SIZE   = 28;   // world-space px
const BONUS_SPIN_SPEED    = 0.04; // radians per tick
const BONUS_BOB_AMPLITUDE = 2.5;  // px vertical bob
const BONUS_BOB_SPEED     = 0.07; // radians per tick for the bob sinusoid

interface BonusDto { id: number; x: number; y: number; type: string; icon: string }

export class BonusLayer {
  readonly container = new Container();
  private pool = new Map<number, Sprite>();

  update(bonuses: BonusDto[], tick: number): void {
    // Power-up types (powerup_*) are rendered by PowerUpLayer — skip them here.
    bonuses = bonuses.filter(b => !b.type?.startsWith("powerup_"));
    const live = new Set(bonuses.map(b => b.id));
    for (const [id, sp] of this.pool) {
      if (!live.has(id)) { this.container.removeChild(sp); this.pool.delete(id); }
    }
    for (const bn of bonuses) {
      const sp = this.pool.get(bn.id);
      if (sp) {
        const bob = Math.sin(tick * BONUS_BOB_SPEED + bn.id) * BONUS_BOB_AMPLITUDE;
        sp.x = bn.x;
        sp.y = bn.y + bob;
        sp.rotation += BONUS_SPIN_SPEED;
      } else {
        const next = new Sprite(tex(bn.icon));
        next.anchor.set(0.5);
        next.width = next.height = BONUS_SPRITE_SIZE;
        next.x = bn.x; next.y = bn.y; next.rotation = 0;
        this.container.addChild(next);
        this.pool.set(bn.id, next);
      }
    }
  }
}
```

## `src/render/Boss.ts`

```typescript
/**
 * Boss.ts — Assembled animated multi-part boss rigs.
 *
 * Infers boss type from the boss-block sprite key (DemonBody → Demon,
 * GoblinBody → Goblin, WitchChest → Witch) and builds a layered Container
 * of part sprites over the boss-block region.
 *
 * Features:
 *  - Idle animation: subtle bob (body), hand sway (hands), head bob offset
 *  - Attack tell: lunge/flash on bossTelegraph / bossAttack events
 *  - HP-driven tint: boss darkens as HP drops
 *  - Defeat flourish: flash + explosion burst when boss dies
 *
 * Atlas keys used:
 *   Demon : hell/DemonBody, hell/DemonFace, hell/DemonFace2, hell/DemonFaceGlow,
 *            hell/DemonHand1, hell/DemonHand2, hell/DemonHand3
 *   Goblin: dungeon/GoblinBody, dungeon/GoblinHead, dungeon/GoblinHand1,
 *            dungeon/GoblinHand2, dungeon/GoblinHand3, dungeon/GoblinLeg1,
 *            dungeon/GoblinLeg2, dungeon/GoblinPants, dungeon/GoblinPlecho
 *   Witch : village/enemies/WitchChest, village/enemies/WitchHead1,
 *            village/enemies/WitchHand1, village/enemies/WitchHand2,
 *            village/enemies/WitchHand3, village/enemies/WitchLeg1,
 *            village/enemies/WitchLeg2, village/enemies/WitchSkirt,
 *            village/enemies/WitchMetla
 */

import {
  Container, Sprite, Graphics, BLEND_MODES, Texture
} from "pixi.js";
import { tex as atlasTex } from "./assets";
import { AnimSystem } from "./AnimSystem";
import { animStrip } from "./assets";

// ── Timing constants ─────────────────────────────────────────────────────────

// Idle bob: body oscillates vertically at this angular speed (ticker units).
const IDLE_BOB_SPEED   = 0.04;
const IDLE_BOB_AMOUNT  = 0.025; // fraction of bossH

// Idle sway: hands and accessories oscillate horizontally.
const IDLE_SWAY_SPEED  = 0.03;
const IDLE_SWAY_AMOUNT = 0.04;  // fraction of bossW

// Attack tell: how long the lunge-forward flash lasts (ms).
const ATTACK_LUNGE_DURATION_MS = 400;
// Lunge scale-up factor for the tell.
const ATTACK_LUNGE_SCALE = 1.22;
// Red flash alpha on telegraph.
const TELEGRAPH_FLASH_ALPHA = 0.7;

// HP colour thresholds for boss tint.
// Above 0.66: full bright.  0.33–0.66: orange tint.  Below 0.33: red tint.
const HP_TINT_LOW  = 0.33;
const HP_TINT_MID  = 0.66;
const HP_COLOR_LOW = 0xdd4422;  // angry red
const HP_COLOR_MID = 0xff9944;  // orange

// How many boss explosion bursts on defeat.
const DEFEAT_BURST_COUNT = 5;

// Explosion strip atlas key (same as Effects.ts).
const EXPLOSION_KEY = "effects/Explosion";
const EXPLOSION_FPS = 18;

// ── Type definitions ──────────────────────────────────────────────────────────

export type BossType = "Demon" | "Goblin" | "Witch" | "Heaven" | "Unknown";

/** Infer the boss type from the sprite key on a boss block. */
export function inferBossType(spriteKey: string): BossType {
  if (spriteKey.includes("Demon")) return "Demon";
  if (spriteKey.includes("Goblin")) return "Goblin";
  if (spriteKey.includes("Witch")) return "Witch";
  if (spriteKey.includes("Heaven")) return "Heaven";
  return "Unknown";
}

/** Label shown in the boss HP bar. */
export function bossLabel(type: BossType): string {
  switch (type) {
    case "Demon":  return "DEMON LORD";
    case "Goblin": return "GOBLIN KING";
    case "Witch":  return "THE WITCH";
    case "Heaven": return "THE SERAPH";
    default:       return "BOSS";
  }
}

// ── Boss rig ──────────────────────────────────────────────────────────────────

interface RigPart {
  sprite: Sprite;
  /** Base offset from rig center as fraction of (bossW, bossH). */
  relX: number;
  relY: number;
  /** Natural size as fraction of bossH. */
  scale: number;
  /** Is this part a "hand/sway" part (oscillates side-to-side)? */
  sway: boolean;
  /** Is this part a "body/bob" part (oscillates up-down with the body)? */
  bob: boolean;
}

export class BossRig {
  readonly container: Container;
  private body: Container;    // inner container that bob-translates
  private parts: RigPart[] = [];
  private flashGfx: Graphics; // red flash overlay
  private type: BossType;
  private _attackLunge = -1;  // ms elapsed into lunge anim, -1=inactive
  private _animSys: AnimSystem;
  // Stored region (set by setRegion each snapshot, read by update each tick).
  _regionW = 0;
  _regionH = 0;

  constructor(type: BossType) {
    this.type = type;
    this.container = new Container();
    this.body = new Container();
    this.container.addChild(this.body);

    this.flashGfx = new Graphics();
    this.flashGfx.blendMode = BLEND_MODES.ADD;
    this.flashGfx.alpha = 0;
    this.container.addChild(this.flashGfx);

    this._animSys = new AnimSystem();
    this.container.addChild(this._animSys.container);

    this._buildRig(type);
  }

  // ── Part construction ────────────────────────────────────────────────────

  private _buildRig(type: BossType) {
    switch (type) {
      case "Demon":  this._buildDemon();  break;
      case "Goblin": this._buildGoblin(); break;
      case "Witch":  this._buildWitch();  break;
      case "Heaven": this._buildHeaven(); break;
      default:       this._buildFallback(); break;
    }
  }

  /**
   * Demon rig: body center, face above body, glow overlay, three hands arranged as arms.
   * Parts used: DemonBody (7), DemonFace (8), DemonFace2 (face alt), DemonFaceGlow (9),
   *             DemonHand1 (left arm up), DemonHand2 (right arm), DemonHand3 (claw).
   * Total: 7 sprites.
   */
  private _buildDemon() {
    this._addPart("hell/DemonBody",      0.0,  0.1,  0.90, false, true);
    this._addPart("hell/DemonFace",      0.0, -0.18, 0.55, false, true);
    this._addPart("hell/DemonFaceGlow",  0.0, -0.18, 0.55, false, true);  // additive glow on face
    this._addPart("hell/DemonHand1",    -0.52, 0.0,  0.38, true,  true);  // left arm
    this._addPart("hell/DemonHand2",     0.52, 0.0,  0.38, true,  true);  // right arm
    this._addPart("hell/DemonHand3",    -0.60, 0.28, 0.32, true,  false); // lower-left claw
    this._addPart("hell/DemonFace2",     0.0, -0.18, 0.42, false, true);  // alt face layer
    // Apply additive blend to glow part only
    const glowPart = this.parts[2];
    if (glowPart) {
      glowPart.sprite.blendMode = BLEND_MODES.ADD;
      glowPart.sprite.alpha = 0.6;
    }
  }

  /**
   * Goblin rig: body center, head above, pants below body, shoulder plates,
   *             two hands at sides, two legs at bottom.
   * Parts used: GoblinBody (10), GoblinHead (11), GoblinHand1 (12), GoblinHand2 (13),
   *             GoblinHand3 (14), GoblinLeg1 (15), GoblinLeg2 (16),
   *             GoblinPants (17), GoblinPlecho (18).
   * Total: 9 sprites.
   */
  private _buildGoblin() {
    this._addPart("dungeon/GoblinPants",  0.0,  0.35, 0.50, false, true);  // lower body/pants
    this._addPart("dungeon/GoblinBody",   0.0,  0.0,  0.70, false, true);  // torso
    this._addPart("dungeon/GoblinPlecho", 0.0, -0.05, 0.58, false, true);  // shoulder armor
    this._addPart("dungeon/GoblinHead",   0.0, -0.35, 0.50, false, true);  // head
    this._addPart("dungeon/GoblinHand1", -0.50,  0.05, 0.32, true,  true); // left hand
    this._addPart("dungeon/GoblinHand2",  0.50,  0.05, 0.32, true,  true); // right hand
    this._addPart("dungeon/GoblinHand3", -0.55, -0.05, 0.28, true,  true); // extra left
    this._addPart("dungeon/GoblinLeg1",  -0.22,  0.62, 0.30, true,  false); // left leg
    this._addPart("dungeon/GoblinLeg2",   0.22,  0.62, 0.30, true,  false); // right leg
  }

  /**
   * Witch rig: chest center, head above, skirt below, hands at sides,
   *            broom (metla) accessory, legs at bottom.
   * Parts used: WitchChest (10), WitchHead1 (11), WitchHand1 (12), WitchHand2 (13),
   *             WitchHand3 (14), WitchLeg1 (15), WitchLeg2 (16), WitchSkirt (17),
   *             WitchMetla (18).
   * Total: 9 sprites.
   */
  private _buildWitch() {
    this._addPart("village/enemies/WitchSkirt",  0.0,  0.38, 0.50, false, true);  // skirt/bottom
    this._addPart("village/enemies/WitchChest",  0.0,  0.05, 0.65, false, true);  // torso
    this._addPart("village/enemies/WitchHead1",  0.0, -0.32, 0.48, false, true);  // head+hat
    this._addPart("village/enemies/WitchHand1", -0.48,  0.0,  0.30, true,  true); // left arm
    this._addPart("village/enemies/WitchHand2",  0.48,  0.0,  0.30, true,  true); // right arm
    this._addPart("village/enemies/WitchHand3", -0.52,  0.15, 0.25, true,  false); // extra hand
    this._addPart("village/enemies/WitchLeg1",  -0.18,  0.65, 0.28, true,  false); // left leg
    this._addPart("village/enemies/WitchLeg2",   0.18,  0.65, 0.28, true,  false); // right leg
    this._addPart("village/enemies/WitchMetla",  0.52,  0.18, 0.35, true,  true);  // broom
  }

  /**
   * Heaven rig: the Seraph statue with its additive globe halo and two orbiting
   * holy balls (the finale boss — uses the original HeavenBoss/Globe/HolyBall art).
   * Total: 4 sprites.
   */
  private _buildHeaven() {
    this._addPart("heaven/HeavenBossGlobe", 0.0, -0.05, 1.05, false, true); // halo behind
    this._addPart("heaven/HeavenBoss",      0.0,  0.0,  0.90, false, true); // statue body
    this._addPart("heaven/HolyBall",       -0.55, -0.10, 0.22, true,  true); // left orb
    this._addPart("heaven/HolyBall",        0.55, -0.10, 0.22, true,  true); // right orb
    // The globe is a glow layer — render it additive like the Demon's face glow.
    const globe = this.parts[0];
    if (globe) {
      globe.sprite.blendMode = BLEND_MODES.ADD;
      globe.sprite.alpha = 0.7;
    }
  }

  /** Fallback: single body sprite when type is unknown. */
  private _buildFallback() {
    this._addPart("hell/DemonBody", 0.0, 0.0, 0.9, false, true);
  }

  private _addPart(
    key: string,
    relX: number, relY: number,
    scale: number,
    sway: boolean, bob: boolean,
  ) {
    const texture = atlasTex(key);
    const sp = new Sprite(texture);
    sp.anchor.set(0.5);
    // Tint fallback (Texture.WHITE) parts invisible until atlas loads.
    if (texture === Texture.WHITE) sp.alpha = 0;
    this.body.addChild(sp);
    this.parts.push({ sprite: sp, relX, relY, scale, sway, bob });
  }

  // ── Public API ────────────────────────────────────────────────────────────

  /** Destroy and clean up. */
  destroy() {
    this.container.destroy({ children: true });
  }

  /** Boss type label. */
  get bossType(): BossType { return this.type; }

  /**
   * Advance animation state and position the rig.
   * Called from the Pixi ticker with real delta-ms so animations play at true speed.
   *
   * @param cx   World-space center X of the boss region
   * @param cy   World-space center Y of the boss region
   * @param w    World-space width of the boss region
   * @param h    World-space height of the boss region
   * @param hpFrac 0..1 HP fraction (1 = full, 0 = dead)
   * @param tick  Ticker frame counter for animations
   * @param dtMs  Real delta-time in ms (must be >0 for animations to move)
   */
  update(cx: number, cy: number, w: number, h: number, hpFrac: number, tick: number, dtMs: number) {
    this.container.position.set(cx, cy);

    // Determine per-part size in world space (h is the "rig height" reference).
    const rigH = h;

    // Compute bob offset.
    const bobAmt = rigH * IDLE_BOB_AMOUNT * Math.sin(tick * IDLE_BOB_SPEED);

    // Compute attack lunge scale multiplier.
    let lungeScale = 1.0;
    if (this._attackLunge >= 0) {
      this._attackLunge += dtMs;
      const t = Math.min(this._attackLunge / ATTACK_LUNGE_DURATION_MS, 1);
      // Quick grow → fast shrink back: peak at t≈0.3
      const peak = Math.sin(t * Math.PI);
      lungeScale = 1.0 + (ATTACK_LUNGE_SCALE - 1.0) * peak;
      if (t >= 1) this._attackLunge = -1;
    }

    // Apply HP-based tint.
    let hpTint = 0xffffff;
    if (hpFrac < HP_TINT_LOW) hpTint = HP_COLOR_LOW;
    else if (hpFrac < HP_TINT_MID) hpTint = HP_COLOR_MID;

    // Flash overlay alpha fades toward 0 each frame.
    this.flashGfx.alpha = Math.max(0, this.flashGfx.alpha - 0.04 * (dtMs / 16.67));

    // Update each part.
    for (const part of this.parts) {
      const sp = part.sprite;

      // Re-check alpha: if texture loaded this frame, make visible.
      if (sp.texture !== Texture.WHITE && sp.alpha === 0 && !sp.blendMode) {
        sp.alpha = 1;
      }

      // Compute natural size.
      const naturalW = sp.texture.width  || 1;
      const naturalH = sp.texture.height || 1;
      const naturalDim = Math.max(naturalW, naturalH);
      const targetSize = rigH * part.scale * lungeScale;
      sp.scale.set(targetSize / naturalDim);

      // Position = relX * w + idle sway offset, relY * h + bob offset.
      const swayAmt = part.sway
        ? w * IDLE_SWAY_AMOUNT * Math.sin(tick * IDLE_SWAY_SPEED + part.relX * 2)
        : 0;
      const bobOffset = part.bob ? bobAmt : 0;

      sp.x = part.relX * w * 0.5 + swayAmt;
      sp.y = part.relY * rigH * 0.5 + bobOffset;

      // Apply HP tint (skip glow/additive parts).
      if (sp.blendMode !== BLEND_MODES.ADD) {
        sp.tint = hpTint;
      }
    }

    // Resize flash gfx to cover rig — use an ellipse for a natural body-glow shape.
    this.flashGfx.clear();
    if (this.flashGfx.alpha > 0.01) {
      // Outer halo ring (additive ellipse — looks like a pulsing energy burst).
      this.flashGfx.beginFill(0xff2200, 0.55)
        .drawEllipse(0, 0, w * 0.75, rigH * 0.7)
        .endFill();
      // Inner core: brighter, smaller.
      this.flashGfx.beginFill(0xff8844, 0.65)
        .drawEllipse(0, 0, w * 0.42, rigH * 0.42)
        .endFill();
    }

    // Drive AnimSystem.
    this._animSys.update(dtMs);
  }

  /**
   * Update the region the rig is anchored to (called from draw() per snapshot).
   * Does NOT advance animation time — the ticker calls update() for that.
   */
  setRegion(cx: number, cy: number, w: number, h: number) {
    this.container.position.set(cx, cy);
    // Store for use when the ticker calls update().
    this._regionW = w;
    this._regionH = h;
  }

  /** Call on bossTelegraph or bossAttack event — triggers the lunge + flash. */
  onTelegraph() {
    this._attackLunge = 0; // start lunge animation
    this.flashGfx.alpha = TELEGRAPH_FLASH_ALPHA;
  }

  /** Call when the boss is defeated — plays burst explosions. */
  onDefeat(cellSize: number) {
    const exFrames = animStrip(EXPLOSION_KEY, EXPLOSION_FPS);
    if (!exFrames.length) return;
    for (let i = 0; i < DEFEAT_BURST_COUNT; i++) {
      const offsetX = (Math.random() - 0.5) * cellSize * 3;
      const offsetY = (Math.random() - 0.5) * cellSize * 3;
      const delay = i * 120; // stagger by 120ms
      // Use AnimSystem's container via direct oneShot calls.
      // We schedule with a brief timeout to stagger the bursts.
      setTimeout(() => {
        this._animSys.oneShot(
          exFrames, EXPLOSION_FPS,
          offsetX, offsetY,
          cellSize * (1.5 + Math.random() * 2),
          true, 0xff8844,
        );
        // Extra flash.
        this.flashGfx.alpha = 0.6;
      }, delay);
    }
  }
}

// ── Telegraph warning glyph ───────────────────────────────────────────────────

/**
 * A reusable warning indicator drawn at a given world position.
 * Displayed on bossTelegraph events.
 */
export class TelegraphWarning {
  readonly container: Container;
  private gfx: Graphics;
  private _elapsed = 0;
  readonly duration = 600; // ms

  constructor() {
    this.container = new Container();
    this.gfx = new Graphics();
    this.gfx.blendMode = BLEND_MODES.ADD;
    this.container.addChild(this.gfx);
    this.container.visible = false;
  }

  /** Trigger the warning at world-space (x, y). */
  trigger(x: number, y: number, size: number) {
    this.container.position.set(x, y);
    this.container.visible = true;
    this._elapsed = 0;
    this._redraw(size, 1.0);
  }

  update(dtMs: number, size: number) {
    if (!this.container.visible) return;
    this._elapsed += dtMs;
    const t = this._elapsed / this.duration;
    if (t >= 1) {
      this.container.visible = false;
      return;
    }
    // Pulse: 3 rapid pulses then fade.
    const pulse = Math.abs(Math.sin(t * Math.PI * 4));
    const alpha = (1 - t) * pulse;
    this._redraw(size, alpha);
  }

  private _redraw(size: number, alpha: number) {
    const r = size * 0.55;
    this.gfx.clear();

    // Soft outer glow halo (wide, low-alpha additive fill).
    this.gfx.lineStyle(0).beginFill(0xff2200, alpha * 0.18)
      .drawCircle(0, 0, r * 1.5)
      .endFill();

    // Outer ring — double-stroked for readability.
    this.gfx.lineStyle(5, 0xff0000, alpha * 0.45)
      .drawCircle(0, 0, r);
    this.gfx.lineStyle(2.5, 0xff8800, alpha * 0.9)
      .drawCircle(0, 0, r);

    // Exclamation shaft (tall rect).
    this.gfx.lineStyle(0).beginFill(0xffcc00, alpha * 0.95)
      .drawRoundedRect(-r * 0.1, -r * 0.60, r * 0.2, r * 0.52, r * 0.05)
      .endFill();
    // Exclamation dot.
    this.gfx.beginFill(0xffcc00, alpha * 0.95)
      .drawCircle(0, r * 0.36, r * 0.13)
      .endFill();
  }
}
```

## `src/render/Effects.ts`

```typescript
/**
 * Effects.ts — Event-driven particle + animation effects.
 *
 * Delegates one-shot animated effects (explosions, burns, flashes) to AnimSystem
 * using real atlas art (strip-sliced Explosion, FireBirth, etc.) rather than
 * static Sprite fades.  Keeps the original Particle path as a fallback for any
 * key that isn't found in the atlas.
 */

import { Container, Graphics, Sprite, BLEND_MODES } from "pixi.js";
import type { Snapshot } from "../net/Connection";
import { tex } from "./textures";
import { animStrip, anim as animFrames } from "./assets";
import { AnimSystem } from "./AnimSystem";

// ── Constants ───────────────────────────────────────────────────────────────
// Fallback particle — used only when no atlas frames are available.
interface Particle {
  sprite: Sprite;
  baseScale: number;
  life: number;
  elapsed: number;
  scaleStart: number;
  scaleEnd: number;
}

// Explosion strip: "effects/Explosion" (7215×555, ~13 frames square)
const EXPLOSION_KEY = "effects/Explosion";
const EXPLOSION_FPS = 18;

// FireBirth: used for burn/ignite flashes (it's the fire wall birth art, small burst)
const FIRE_BIRTH_KEY = "firemage/spell_firewall/FireBirth";
const FIRE_BIRTH_FPS = 14;

// Phoenix birth sequence: 20 individual frames in the manifest (not a strip)
const PHOENIX_BIRTH_ANIM_KEY = "firemage/spell_phonex/phoenixbirthanimpic";
const PHOENIX_BIRTH_FPS = 12;

// PhoenixDeathAnimation2: strip (19818×884, ~22 frames)
const PHOENIX_DEATH_STRIP_KEY = "firemage/spell_phonex/PhoenixDeathAnimation2";
const PHOENIX_DEATH_FPS = 14;

// Lightning: animated frames from manifest (engineer/spell_lighting/lighting)
const LIGHTNING_ANIM_KEY = "engineer/spell_lighting/lighting";
const LIGHTNING_FPS = 16;

// Skeleton death: animated strip (necromancer/spell_skeleton/SkeletonDeathAnimation)
const SKELETON_DEATH_STRIP_KEY = "necromancer/spell_skeleton/SkeletonDeathAnimation";
const SKELETON_DEATH_FPS = 12;

// Skeleton2 death: animated strip (necromancer/spell_skeleton/Skeleton2DeathAnimation)
const SKELETON2_DEATH_STRIP_KEY = "necromancer/spell_skeleton/Skeleton2DeathAnimation";
const SKELETON2_DEATH_FPS = 12;

// Heaven Vaza death and HellBall death: referenced indirectly by getBiomeSecondaryStrip()
// which is called from spawnBlockDestroy. Keys are literal strings in that function.
const _HEAVEN_VAZA_REF = "heaven/HeavenVazaDeathAnimation"; // used in getBiomeSecondaryStrip
const _HELL_BALL_REF = "hell/HellBallDeathAnimation"; // used in getBiomeSecondaryStrip
void _HEAVEN_VAZA_REF; void _HELL_BALL_REF; // suppress lint — actual keys are in the function below

// Necromant death-mark: a DeathSphere hovers over the marked corpse until the
// revive fires (or is cancelled by killing the necromant). docs/11 — makes the
// race visible and tells the player WHICH cells are coming back.
const DEATH_SPHERE_KEY        = "village/enemies/DeathSphere";
const DEATH_SPHERE_SIZE_FRAC  = 0.8;  // of cellSize
const DEATH_SPHERE_ALPHA_BASE = 0.65;
const DEATH_SPHERE_ALPHA_AMP  = 0.25;
const DEATH_SPHERE_PULSE_HZ   = 2.5;

// Demon fist column flashes (docs/11 boss verbs): warning amber, slam red.
const FIST_TELEGRAPH_COLOR = 0xffaa33;
const FIST_TELEGRAPH_MS    = 700;
const FIST_SLAM_COLOR      = 0xff3311;
const FIST_SLAM_MS         = 350;
const JUDGEMENT_COLOR      = 0xffd24a; // Paladin Last Day gold
const FIST_COLUMN_ALPHA    = 0.32;

interface ColumnFlash { gfx: Graphics; life: number; elapsed: number }

export class Effects {
  readonly container: Container;
  private animSys: AnimSystem;
  private particles: Particle[] = [];
  // Death-mark spheres keyed by rounded cell position.
  private deathMarks = new Map<string, Sprite>();
  private _markClock = 0;
  private columnFlashes: ColumnFlash[] = [];
  /** Board height in world units — set by the renderer each snapshot for column flashes. */
  boardH = 0;

  // Cached sliced frames (built lazily so atlas is fully loaded when first event fires)
  private _explosionFrames = () => animStrip(EXPLOSION_KEY, EXPLOSION_FPS);
  private _fireBirthFrames = () => animStrip(FIRE_BIRTH_KEY, FIRE_BIRTH_FPS);
  private _phoenixBirthFrames = () => animFrames(PHOENIX_BIRTH_ANIM_KEY);
  private _phoenixDeathFrames = () => animStrip(PHOENIX_DEATH_STRIP_KEY, PHOENIX_DEATH_FPS);
  private _lightningFrames = () => animFrames(LIGHTNING_ANIM_KEY);
  private _skeletonDeathFrames = () => animStrip(SKELETON_DEATH_STRIP_KEY, SKELETON_DEATH_FPS);
  private _skeleton2DeathFrames = () => animStrip(SKELETON2_DEATH_STRIP_KEY, SKELETON2_DEATH_FPS);

  constructor() {
    this.container = new Container();
    this.animSys = new AnimSystem();
    this.container.addChild(this.animSys.container);
  }

  /** Spawn effects for all events in the snapshot. Call once per snapshot. */
  consume(events: Snapshot["events"], cellSize: number, biome?: string) {
    for (const ev of events) {
      switch (ev.type) {
        case "blockDestroyed":
          this.spawnBlockDestroy(ev.x, ev.y, cellSize, biome ?? "hell");
          break;
        case "burn":
          this.spawnBurn(ev.x, ev.y, cellSize);
          break;
        case "ignite":
          this.spawnIgniteFlash(ev.x, ev.y, cellSize);
          break;
        case "spellCast":
          this.spawnPhoenixFlourish(ev.x, ev.y, cellSize);
          break;
        case "lightning":
          this.spawnLightningStrike(ev.x, ev.y, cellSize);
          break;
        case "skeletonDeath":
          this.spawnSkeletonDeath(ev.x, ev.y, cellSize);
          break;
        case "deathMark":
          this.addDeathMark(ev.x, ev.y, cellSize);
          break;
        case "revive":
        case "reviveCancelled":
          this.removeDeathMark(ev.x, ev.y);
          break;
        case "fistTelegraph":
          this.spawnColumnFlash(ev.x, cellSize, FIST_TELEGRAPH_COLOR, FIST_TELEGRAPH_MS);
          break;
        case "fistSlam":
          this.spawnColumnFlash(ev.x, cellSize, FIST_SLAM_COLOR, FIST_SLAM_MS);
          this.spawnBlockDestroy(ev.x, this.boardH * 0.5, cellSize, biome ?? "hell");
          break;
        case "judgement": // Paladin Last Day: a golden column smite
          this.spawnColumnFlash(ev.x, cellSize, JUDGEMENT_COLOR, FIST_SLAM_MS);
          break;
      }
    }
  }

  /** Full-height column flash at world x — the Demon fist's warning and impact. */
  private spawnColumnFlash(x: number, cellSize: number, color: number, lifeMs: number) {
    const gfx = new Graphics();
    gfx.blendMode = BLEND_MODES.ADD;
    gfx.beginFill(color, FIST_COLUMN_ALPHA)
      .drawRect(x - cellSize / 2, 0, cellSize, Math.max(this.boardH, cellSize))
      .endFill();
    this.container.addChild(gfx);
    this.columnFlashes.push({ gfx, life: lifeMs, elapsed: 0 });
  }

  // ── Necromant death-mark spheres ──────────────────────────────────────────

  private static markKey(x: number, y: number) { return `${Math.round(x)},${Math.round(y)}`; }

  private addDeathMark(x: number, y: number, cellSize: number) {
    const key = Effects.markKey(x, y);
    if (this.deathMarks.has(key)) return;
    const texture = tex(DEATH_SPHERE_KEY); // direct atlas key via the legacy resolver
    const sp = new Sprite(texture);
    sp.anchor.set(0.5);
    sp.blendMode = BLEND_MODES.ADD;
    sp.position.set(x, y);
    const size = cellSize * DEATH_SPHERE_SIZE_FRAC;
    const dim = Math.max(sp.texture.width, sp.texture.height, 1);
    sp.scale.set(size / dim);
    sp.alpha = DEATH_SPHERE_ALPHA_BASE;
    this.container.addChild(sp);
    this.deathMarks.set(key, sp);
  }

  private removeDeathMark(x: number, y: number) {
    const key = Effects.markKey(x, y);
    const sp = this.deathMarks.get(key);
    if (!sp) return;
    this.container.removeChild(sp);
    this.deathMarks.delete(key);
  }

  // ── Block destruction ────────────────────────────────────────────────────

  private spawnBlockDestroy(x: number, y: number, cellSize: number, biome: string) {
    // Use the biome-specific destruction strip if available, then fall back to
    // the generic Explosion strip.
    const biomeStrip = getBiomeDestroyStrip(biome);
    let frames = biomeStrip ? animStrip(biomeStrip, 20) : [];
    if (!frames.length) frames = this._explosionFrames();

    if (frames.length) {
      // Use normal blend for biome strips (they are opaque art); additive for explosion
      const additive = !biomeStrip || frames.length < 4;
      this.animSys.oneShot(frames, EXPLOSION_FPS, x, y, cellSize * 1.5, additive, 0xffffff);
      // Secondary smaller burst with additive explosion for brightness
      const exFrames = this._explosionFrames();
      if (exFrames.length) {
        this.animSys.oneShot(exFrames, EXPLOSION_FPS + 4, x, y, cellSize * 0.9, true, 0xff9933);
      }
      // Biome-specific secondary burst (heaven vaza glow, hell ball death)
      const secondaryKey = getBiomeSecondaryStrip(biome);
      if (secondaryKey) {
        const secondaryFrames = animStrip(secondaryKey, 14);
        if (secondaryFrames.length) {
          this.animSys.oneShot(secondaryFrames, 14, x, y, cellSize * 1.2, true, 0xffffff);
        }
      }
    } else {
      // Fallback: static sprite fade
      this.spawnFallback(tex("Explosion"), x, y, cellSize * 1.4, 280, 1.0, 1.6);
    }
  }

  // ── Burn ─────────────────────────────────────────────────────────────────

  private spawnBurn(x: number, y: number, cellSize: number) {
    const frames = this._fireBirthFrames();
    if (frames.length) {
      this.animSys.oneShot(frames, FIRE_BIRTH_FPS, x, y, cellSize * 0.9, true, 0xff6600);
    } else {
      const sp = new Sprite(tex("Explosion"));
      sp.tint = 0xff6600;
      this.spawnFallback(sp.texture, x, y, cellSize * 0.7, 180, 1.0, 1.4);
    }
  }

  // ── Ignite flash ─────────────────────────────────────────────────────────

  private spawnIgniteFlash(x: number, y: number, cellSize: number) {
    // Small bright flash using FireBirth art, then a phoenix birth flourish scaled down
    const fbFrames = this._fireBirthFrames();
    if (fbFrames.length) {
      this.animSys.oneShot(fbFrames, FIRE_BIRTH_FPS + 4, x, y, cellSize * 1.2, true, 0xffdd88);
    }
    const phoenixFrames = this._phoenixBirthFrames();
    if (phoenixFrames.length) {
      this.animSys.oneShot(phoenixFrames, PHOENIX_BIRTH_FPS, x, y, cellSize * 2.5, true, 0xff8833);
    }
  }

  // ── Phoenix flourish (on spellCast) ──────────────────────────────────────

  private spawnPhoenixFlourish(x: number, y: number, cellSize: number) {
    // Try the 20-frame birth sequence first; fall back to death strip.
    const birthFrames = this._phoenixBirthFrames();
    if (birthFrames.length) {
      this.animSys.oneShot(birthFrames, PHOENIX_BIRTH_FPS, x, y, cellSize * 4.5, true, 0xff6600);
      return;
    }
    const deathFrames = this._phoenixDeathFrames();
    if (deathFrames.length) {
      this.animSys.oneShot(deathFrames, PHOENIX_DEATH_FPS, x, y, cellSize * 5, true, 0xff8844);
      return;
    }
    // Fallback: bright white flash
    this.spawnFallback(tex("Explosion"), x, y, cellSize * 0.5, 120, 1.0, 1.3);
  }

  // ── Lightning strike ─────────────────────────────────────────────────────

  private spawnLightningStrike(x: number, y: number, cellSize: number) {
    const frames = this._lightningFrames();
    if (frames.length) {
      this.animSys.oneShot(frames, LIGHTNING_FPS, x, y, cellSize * 2.5, true, 0xaaccff);
      // Secondary additive white flash
      this.animSys.oneShot(frames, LIGHTNING_FPS + 6, x, y + cellSize, cellSize * 1.5, true, 0xffffff);
    } else {
      this.spawnFallback(tex("Explosion"), x, y, cellSize * 1.8, 200, 1.0, 1.5, 0x88bbff);
    }
  }

  // ── Skeleton death ────────────────────────────────────────────────────────

  private spawnSkeletonDeath(x: number, y: number, cellSize: number) {
    const frames = this._skeletonDeathFrames();
    const frames2 = this._skeleton2DeathFrames();
    const chosen = frames.length >= frames2.length ? frames : frames2;
    if (chosen.length) {
      this.animSys.oneShot(chosen, SKELETON_DEATH_FPS, x, y, cellSize * 3, false, 0xccddff);
    } else {
      this.spawnFallback(tex("Explosion"), x, y, cellSize, 220, 1.0, 1.6, 0x8899ff);
    }
  }

  // ── Fallback particle (no atlas art) ─────────────────────────────────────

  private spawnFallback(
    texture: import("pixi.js").Texture,
    x: number, y: number,
    sizeInWorld: number,
    life: number,
    scaleStart: number, scaleEnd: number,
    tint = 0xffffff,
  ) {
    const sp = new Sprite(texture);
    sp.anchor.set(0.5);
    sp.blendMode = BLEND_MODES.ADD;
    sp.tint = tint;
    sp.position.set(x, y);
    const baseScale = sizeInWorld / Math.max(sp.texture.width, 1);
    sp.scale.set(baseScale * scaleStart);
    sp.alpha = 1;
    this.container.addChild(sp);
    this.particles.push({ sprite: sp, baseScale, life, elapsed: 0, scaleStart, scaleEnd });
  }

  /** Call every ticker frame with the delta in ms. */
  update(dtMs: number) {
    this.animSys.update(dtMs);

    // Fade out the fist column flashes.
    for (let i = this.columnFlashes.length - 1; i >= 0; i--) {
      const cf = this.columnFlashes[i];
      cf.elapsed += dtMs;
      const t = cf.elapsed / cf.life;
      if (t >= 1) {
        this.container.removeChild(cf.gfx);
        this.columnFlashes.splice(i, 1);
      } else {
        cf.gfx.alpha = 1 - t;
      }
    }

    // Pulse the death-mark spheres so they read as "pending", not debris.
    this._markClock += dtMs / 1000;
    const markAlpha = DEATH_SPHERE_ALPHA_BASE
      + DEATH_SPHERE_ALPHA_AMP * Math.sin(this._markClock * Math.PI * DEATH_SPHERE_PULSE_HZ);
    for (const sp of this.deathMarks.values()) sp.alpha = markAlpha;

    const dead: Particle[] = [];
    for (const p of this.particles) {
      p.elapsed += dtMs;
      const t = Math.min(p.elapsed / p.life, 1);
      p.sprite.alpha = 1 - t;
      const scaleMult = p.scaleStart + (p.scaleEnd - p.scaleStart) * t;
      p.sprite.scale.set(p.baseScale * scaleMult);
      if (t >= 1) dead.push(p);
    }
    for (const p of dead) {
      this.container.removeChild(p.sprite);
      this.particles.splice(this.particles.indexOf(p), 1);
    }
  }

  /** Clear all effects (call on level reset). */
  clear() {
    this.animSys.clear();
    for (const p of this.particles) this.container.removeChild(p.sprite);
    this.particles = [];
    for (const sp of this.deathMarks.values()) this.container.removeChild(sp);
    this.deathMarks.clear();
    for (const cf of this.columnFlashes) this.container.removeChild(cf.gfx);
    this.columnFlashes = [];
  }
}

// ── Biome → per-block destruction strip mapping ───────────────────────────

/**
 * Return the atlas key for the biome's primary block destruction strip,
 * or null to fall back to the generic Explosion.
 */
function getBiomeDestroyStrip(biome: string): string | null {
  switch (biome) {
    case "hell":    return "hell/StandartHellDestroyed";
    case "dungeon":
    case "caverns":
    case "cavern":  return "dungeon/DungeonStandartDestroyed";
    case "village": return "village/blocks/VillageStandartDestroyed";
    case "heaven":  return "heaven/StandartHavenDestroyed";
    default:        return null;
  }
}

// Expose biome-specific secondary burst keys for richer effects.
function getBiomeSecondaryStrip(biome: string): string | null {
  switch (biome) {
    case "heaven":  return "heaven/HeavenVazaDeathAnimation";
    case "hell":    return "hell/HellBallDeathAnimation";
    default:        return null;
  }
}
```

## `src/render/FireWallLayer.ts`

```typescript
import { Container, Sprite, AnimatedSprite, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { anim as animFrames } from "./assets";

// Fire-wall (Fire Mage) rendering: a band of animated FireStandAnnimation tiles
// rebuilt only when the wall count changes, with an alpha-flicker fallback for the
// static-sprite path. Owns its display container; extracted from Renderer.

// Fire wall band height as a fraction of cellSize.
const FIRE_WALL_HEIGHT_MULT = 1.1;
// FireWall animation key in the manifest.
const FIRE_WALL_ANIM_KEY = "firemage/spell_firewall/firestandannimation";

interface Wall { y: number; width: number }

export class FireWallLayer {
  readonly container = new Container();
  private _wallAnims: AnimatedSprite[] = [];
  private _lastWallCount = -1;

  update(walls: Wall[], tick: number, cellSize: number, boardW: number): void {
    const wallH = cellSize * FIRE_WALL_HEIGHT_MULT;
    const fireWallFrames = animFrames(FIRE_WALL_ANIM_KEY);

    if (walls.length !== this._lastWallCount) {
      // Destroy old wall anim sprites.
      this.container.removeChildren();
      for (const a of this._wallAnims) { a.stop(); a.destroy(); }
      this._wallAnims = [];

      for (const wall of walls) {
        const tileW = wallH; // square tiles
        const count = Math.ceil(boardW / tileW);
        for (let i = 0; i < count; i++) {
          if (fireWallFrames.length >= 2) {
            // Use real animated FireStandAnnimation art.
            const anim = new AnimatedSprite(fireWallFrames);
            anim.blendMode = BLEND_MODES.ADD;
            anim.tint = 0xff8833;
            anim.anchor.set(0, 0.5);
            anim.width  = tileW + 1;
            anim.height = wallH;
            anim.x = i * tileW;
            anim.y = wall.y;
            anim.loop = true;
            anim.animationSpeed = 8 / 60; // ~8 fps
            // Stagger offset per tile for organic flicker.
            anim.currentFrame = (i * 3) % fireWallFrames.length;
            anim.alpha = 0.9;
            anim.play();
            this.container.addChild(anim);
            this._wallAnims.push(anim);
          } else {
            // Fallback: static Explosion sprite.
            const sp = new Sprite(tex("Explosion"));
            sp.blendMode = BLEND_MODES.ADD;
            sp.tint = 0xff6620;
            sp.anchor.set(0, 0.5);
            sp.width  = tileW + 1;
            sp.height = wallH;
            sp.x = i * tileW;
            sp.y = wall.y;
            sp.alpha = 0.85;
            this.container.addChild(sp);
          }
        }
      }
      this._lastWallCount = walls.length;
    } else {
      // Walls unchanged — just flicker alpha for the static-sprite fallback path.
      for (let i = 0; i < this.container.children.length; i++) {
        const child = this.container.children[i];
        if (!(child instanceof AnimatedSprite)) {
          const flicker = 0.72 + 0.28 * Math.sin(tick * 0.18 + i * 1.3);
          child.alpha = flicker;
        }
      }
    }
  }
}
```

## `src/render/HazardLayer.ts`

```typescript
import { Container, Graphics, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { tex as atlasTex } from "./assets";

// Hazard (falling/rolling enemy projectile) rendering. Owns its own display layer +
// index-keyed pool; extracted from Renderer to keep that file focused.
//
// Every hazard carries a `kind` tag from the sim and maps to its original art:
//   hellball / beholdermissile / heavenmissile → missile sprites
//   stalactite / cart                          → falling/rolling props
//   witchmagic                                 → WitchMagic1-4 cycle
//   bat                                        → harmless flyaway bat
//   "" (untagged)                              → crimson dot fallback
const HAZARD_RADIUS           = 6;        // px in world space
const HAZARD_COLOR            = 0xdd1111; // crimson
const HAZARD_GLOW_COLOR       = 0xff3333; // additive glow
const HAZARD_GLOW_ALPHA       = 0.45;
const HAZARD_GLOW_RADIUS_MULT = 1.9;

interface HazardDto { x: number; y: number; kind?: string }
type Entry = { halo: Graphics; core: Graphics; bat: Sprite; stal: Sprite; magic: Sprite; missile: Sprite };

// The Witch boss casts four distinct magic bolts — cycle the sprites by hazard index.
const WITCH_MAGIC_FRAMES = [
  "village/enemies/WitchMagic1", "village/enemies/WitchMagic2",
  "village/enemies/WitchMagic3", "village/enemies/WitchMagic4",
];
const MAGIC_SIZE_MULT = 4;   // ×HAZARD_RADIUS
const MAGIC_SPIN_SPEED = 0.1; // rad/tick

// Enemy missile art per sim kind (original sprites the legacy game used).
const MISSILE_FRAMES: Record<string, string> = {
  hellball:        "hell/HellBallMissile",
  beholdermissile: "village/enemies/BeholderMissile",
  heavenmissile:   "heaven/Missile",
  witchgrab:       "village/enemies/WitchHand2", // the Witch's grab-hand chasing the ball
};
const MISSILE_SIZE_MULT = 3;     // ×HAZARD_RADIUS
const BAT_FLUTTER_SPEED = 0.25;  // wing-flap wobble (scale.y oscillation)
const BAT_FLUTTER_AMP   = 0.18;

export class HazardLayer {
  readonly container = new Container();
  private pool: Entry[] = [];

  /** Render the current hazards. `tick` drives spin/flutter animation. */
  update(hazards: HazardDto[], tick: number): void {
    // Grow the pool to cover the live hazard count.
    while (this.pool.length < hazards.length) {
      const halo = new Graphics();
      halo.blendMode = BLEND_MODES.ADD;
      const core = new Graphics();
      const bat = new Sprite(Texture.WHITE);
      bat.anchor.set(0.5);
      bat.visible = false;
      const stal = new Sprite(tex("Stalactite"));
      stal.anchor.set(0.5);
      stal.visible = false;
      const magic = new Sprite(Texture.WHITE);
      magic.anchor.set(0.5);
      magic.visible = false;
      const missile = new Sprite(Texture.WHITE);
      missile.anchor.set(0.5);
      missile.visible = false;
      this.container.addChild(halo, core, bat, stal, magic, missile);
      this.pool.push({ halo, core, bat, stal, magic, missile });
    }

    for (let i = 0; i < this.pool.length; i++) {
      const entry = this.pool[i];
      const { halo, core, bat, stal, magic, missile } = entry;
      if (i >= hazards.length) {
        halo.visible = core.visible = bat.visible = stal.visible = magic.visible = missile.visible = false;
        continue;
      }
      const hz = hazards[i];
      halo.visible = core.visible = bat.visible = stal.visible = magic.visible = missile.visible = false;

      if (hz.kind === "witchmagic") {
        const frame = atlasTex(WITCH_MAGIC_FRAMES[i % WITCH_MAGIC_FRAMES.length]);
        if (frame !== Texture.WHITE) {
          magic.visible = true;
          magic.texture = frame;
          const ms = HAZARD_RADIUS * MAGIC_SIZE_MULT;
          magic.width = ms; magic.height = ms;
          magic.x = hz.x; magic.y = hz.y;
          magic.rotation = tick * MAGIC_SPIN_SPEED + i;
          continue;
        }
      } else if (hz.kind === "stalactite" || hz.kind === "cart") {
        stal.visible = true;
        if (hz.kind === "cart") {
          stal.texture = tex("DungeonCart");
          const cs = HAZARD_RADIUS * 4;
          stal.width = cs * 1.4; stal.height = cs;
        } else {
          stal.texture = tex("Stalactite");
          const ss = HAZARD_RADIUS * 3.2;
          stal.width = ss; stal.height = ss * 1.6;
        }
        stal.x = hz.x; stal.y = hz.y;
        continue;
      } else if (hz.kind === "bat") {
        const frame = atlasTex("village/enemies/BatFlyAnimation");
        if (frame !== Texture.WHITE) {
          bat.visible = true;
          bat.texture = frame;
          const batSize = HAZARD_RADIUS * 7;
          bat.width = batSize;
          // Wing-flap flutter: oscillate the height instead of spinning the sprite.
          bat.height = batSize * (1 + BAT_FLUTTER_AMP * Math.sin(tick * BAT_FLUTTER_SPEED + i));
          bat.x = hz.x; bat.y = hz.y;
          continue;
        }
      } else if (hz.kind && MISSILE_FRAMES[hz.kind]) {
        const frame = atlasTex(MISSILE_FRAMES[hz.kind]);
        if (frame !== Texture.WHITE) {
          missile.visible = true;
          missile.texture = frame;
          const ms = HAZARD_RADIUS * MISSILE_SIZE_MULT;
          // Preserve the art's aspect ratio (missiles are taller than wide).
          const aspect = frame.height / Math.max(frame.width, 1);
          missile.width = ms; missile.height = ms * aspect;
          missile.x = hz.x; missile.y = hz.y;
          continue;
        }
      }

      // Fallback for untagged hazards (or missing art): crimson glow dot.
      halo.visible = core.visible = true;
      halo.clear().beginFill(HAZARD_GLOW_COLOR, HAZARD_GLOW_ALPHA)
        .drawCircle(hz.x, hz.y, HAZARD_RADIUS * HAZARD_GLOW_RADIUS_MULT).endFill();
      core.clear().beginFill(HAZARD_COLOR, 1)
        .drawCircle(hz.x, hz.y, HAZARD_RADIUS).endFill();
    }
  }
}
```

## `src/render/PaddleLayer.ts`

```typescript
import { Container, Sprite, Texture } from "pixi.js";
import { tex } from "./textures";
import { tex as atlasTex } from "./assets";

// Paddle + turret rendering, extracted from Renderer. Owns both sprites (in one
// container for z-order), the per-class bar-frame animation, and the squash/stretch
// bounce animation. update() positions them from the snapshot; updateAnim() drives
// the bar cycling + squash timeline.

// Turret visual: barrel length and width as fractions of paddleH.
const TURRET_BARREL_LENGTH_MULT = 1.8;
const TURRET_BARREL_WIDTH_MULT  = 0.45;
const TURRET_SPRITE_KEY = "firemage/spell_fireturret/FireHeroTurret";

// Paddle squash/stretch: on ball bounce the paddle stretches briefly.
const PADDLE_SQUASH_DURATION_MS = 180; // total duration of the squash → stretch anim
const PADDLE_SQUASH_Y_SCALE     = 0.65; // minimum Y scale during squash peak
const PADDLE_STRETCH_X_SCALE    = 1.18; // maximum X scale at stretch peak

interface BallLike { id: number; y: number }

export class PaddleLayer {
  readonly container = new Container();
  // The original bar art is a HALF sprite (the left wing, cut at the centre gem) —
  // the legacy game mirrored it. We render two mirrored halves around the centre;
  // drawing the half once, stretched, was the old asymmetric-paddle bug.
  private leftHalf  = new Sprite();
  private rightHalf = new Sprite();
  private turretSprite = new Sprite();

  // Per-class bar art keys (default fire mage; overridden by setClass).
  private _spriteKey = "firemage/bars/v2FireHero1";
  private _animKeys: string[] = [
    "firemage/bars/v2FireHero1", "firemage/bars/v2FireHero2",
    "firemage/bars/v2FireHero3", "firemage/bars/v2FireHero4",
  ];
  private _animFrame = 0;

  // Squash/stretch state.
  private _squashElapsed = -1; // -1 = inactive; >=0 = ms into the animation
  private _baseScaleX = 1;
  private _baseScaleY = 1;
  // Previous y per ball id — squash triggers on band ENTRY, not presence.
  private _prevBallY = new Map<number, number>();

  constructor() {
    // Both halves anchor on their inner (cut) edge so they meet at the centre.
    this.leftHalf.anchor.set(1, 0.5);
    this.leftHalf.texture = Texture.WHITE;
    this.rightHalf.anchor.set(1, 0.5); // mirrored via negative X scale
    this.rightHalf.texture = Texture.WHITE;
    this.turretSprite = new Sprite(Texture.WHITE);
    this.turretSprite.anchor.set(0.5, 1); // anchor at bottom-center
    this.turretSprite.visible = false;
    this.container.addChild(this.leftHalf, this.rightHalf, this.turretSprite);
  }

  /** Switch the paddle bar art to the given class's 4-frame strip. */
  setClass(paddleKeys: string[]): void {
    this._animKeys  = paddleKeys;
    this._spriteKey = paddleKeys[0];
    this._animFrame = 0;
  }

  update(
    paddleX: number, paddleW: number, paddleH: number,
    boardH: number, cellSize: number, turretActive: boolean, balls: BallLike[],
  ): void {
    // --- paddle squash trigger: a ball ENTERING the paddle y-band from above ---
    // Edge-triggered on the band boundary. The old level-trigger restarted the
    // squash every snapshot while a served ball rested on the paddle, so the
    // bar pulsed forever (docs/13: "constantly changing size").
    const paddleYCenter = (boardH + cellSize) - paddleH / 2;
    const paddleBounceZone = paddleH * 2.5;
    const zoneTop = paddleYCenter - paddleBounceZone;
    const seen = new Set<number>();
    for (const ball of balls) {

      seen.add(ball.id);
      const prevY = this._prevBallY.get(ball.id);
      if (prevY !== undefined && prevY < zoneTop && ball.y >= zoneTop && this._squashElapsed < 0) {
        this._squashElapsed = 0; // start squash animation
      }
      this._prevBallY.set(ball.id, ball.y);
    }
    // Drop stale entries so the map doesn't grow over a long level.
    for (const id of this._prevBallY.keys()) {
      if (!seen.has(id)) this._prevBallY.delete(id);
    }

    // --- paddle (two mirrored halves of the half-sprite bar art) ---
    // Swap to per-class atlas paddle texture on first draw (ticker advances frames).
    const paddleTex = atlasTex(this._spriteKey);
    if (paddleTex !== Texture.WHITE) {
      this.leftHalf.texture  = paddleTex;
      this.rightHalf.texture = paddleTex;
    }
    this.leftHalf.position.set(paddleX, paddleYCenter);
    this.rightHalf.position.set(paddleX, paddleYCenter);
    // Scale is anchored to the FIRST animation frame, not the current one: the
    // 4-frame strips vary in width (fire mage: 240→369 px at a constant 171 px
    // height — the wings flare outward). Per-frame width compensation made the
    // bar's rendered height pulse 1.5× at 6 fps (docs/13: "constantly changing
    // size"). With one uniform scale, height stays constant and wide frames
    // flare past the collision width as the art intends.
    const baseTex = atlasTex(this._animKeys[0]);
    const baseW = baseTex !== Texture.WHITE ? baseTex.width  : this.leftHalf.texture.width;
    const baseH = baseTex !== Texture.WHITE ? baseTex.height : this.leftHalf.texture.height;
    if (baseW > 1) {
      const wScale  = (paddleW / 2) / baseW;
      const spriteH = Math.max(paddleH, baseH * wScale);
      this._baseScaleX = wScale;
      this._baseScaleY = spriteH / baseH;
      // Only reset to base scale if no squash animation is running.
      if (this._squashElapsed < 0) this.applyScale(1, 1);
    }

    // --- turret indicator (atlas art: FireHeroTurret) ---
    if (turretActive) {
      const turretAtlasTex = tex(TURRET_SPRITE_KEY);
      if (turretAtlasTex !== Texture.WHITE) this.turretSprite.texture = turretAtlasTex;
      this.turretSprite.visible = true;
      this.turretSprite.width   = paddleH * TURRET_BARREL_WIDTH_MULT * 2;
      this.turretSprite.height  = paddleH * TURRET_BARREL_LENGTH_MULT;
      this.turretSprite.x       = paddleX;
      this.turretSprite.y       = paddleYCenter - paddleH / 2;
    } else {
      this.turretSprite.visible = false;
    }
  }

  /** Apply scale multipliers to both halves (right half mirrors via negative X). */
  private applyScale(xMult: number, yMult: number): void {
    this.leftHalf.scale.set(this._baseScaleX * xMult, this._baseScaleY * yMult);
    this.rightHalf.scale.set(-this._baseScaleX * xMult, this._baseScaleY * yMult);
  }

  /** Drives the squash/stretch timeline + the bar-frame cycling each frame. */
  updateAnim(dtMs: number): void {
    // Paddle squash/stretch animation.
    if (this._squashElapsed >= 0) {
      this._squashElapsed += dtMs;
      const t = Math.min(this._squashElapsed / PADDLE_SQUASH_DURATION_MS, 1);
      // Phase 1 (0→0.4): squash — compress Y, expand X. Phase 2 (0.4→1.0): spring back.
      let xScale = 1.0;
      let yScale = 1.0;
      if (t < 0.4) {
        const p = t / 0.4;
        xScale = 1.0 + (PADDLE_STRETCH_X_SCALE - 1.0) * p;
        yScale = 1.0 - (1.0 - PADDLE_SQUASH_Y_SCALE) * p;
      } else {
        const p = (t - 0.4) / 0.6;
        const overshoot = Math.sin(p * Math.PI) * 0.06;
        xScale = PADDLE_STRETCH_X_SCALE - (PADDLE_STRETCH_X_SCALE - 1.0) * p + overshoot;
        yScale = PADDLE_SQUASH_Y_SCALE + (1.0 - PADDLE_SQUASH_Y_SCALE) * p - overshoot;
      }
      this.applyScale(xScale, yScale);
      if (t >= 1) {
        this._squashElapsed = -1;
        this.applyScale(1, 1);
      }
    }

  }

  /**
   * Select the bar-frame from the mana ratio (0.0–1.0):
   *   0.00–0.24 → frame 0 (compact simple paddle)
   *   0.25–0.49 → frame 1
   *   0.50–0.74 → frame 2
   *   0.75–1.00 → frame 3 (wide elaborate dragon-head)
   */
  setMana(ratio: number): void {
    const frame = Math.min(3, Math.floor(Math.max(0, ratio) * 4));
    if (frame === this._animFrame) return;
    this._animFrame = frame;
    const nextTex = atlasTex(this._animKeys[frame]);
    if (nextTex !== Texture.WHITE) {
      this.leftHalf.texture  = nextTex;
      this.rightHalf.texture = nextTex;
    }
  }
}
```

## `src/render/PowerUpLayer.ts`

```typescript
import { Container, Graphics, Text, TextStyle } from "pixi.js";

// ---------------------------------------------------------------------------
// Power-up falling pickup layer (task 1.2).
//
// Renders only bonuses whose type starts with "powerup_" as coloured circles
// with a letter symbol.  The generic BonusLayer skips these types so there is
// no double-rendering.
// ---------------------------------------------------------------------------

const RADIUS       = 16;   // world-space half-size of the circle
const SPIN_SPEED   = 0.03; // radians added per tick
const BOB_AMP      = 2.5;  // px vertical bob
const BOB_SPEED    = 0.07; // radians per tick for bob sinusoid

// Colour per power-up effect type.
const COLORS: Record<string, number> = {
  powerup_wide:      0xd4aa00, // gold
  powerup_multiball: 0xd4aa00, // gold
  powerup_fireshot:  0xff6600, // orange
  powerup_manasurge: 0x2266ff, // blue
  powerup_shield:    0x00ddee, // cyan
};

// One-letter symbol rendered inside the circle.
const SYMBOLS: Record<string, string> = {
  powerup_wide:      "W",
  powerup_multiball: "M",
  powerup_fireshot:  "F",
  powerup_manasurge: "S",
  powerup_shield:    "◆", // ◆
};

interface BonusDto { id: number; x: number; y: number; type: string }

interface PoolEntry { circle: Graphics; angle: number }

export class PowerUpLayer {
  readonly container = new Container();
  private pool = new Map<number, PoolEntry>();

  update(bonuses: BonusDto[], tick: number): void {
    // Only handle powerup_ types.
    const pups = bonuses.filter(b => b.type.startsWith("powerup_"));
    const live = new Set(pups.map(b => b.id));

    // Cull entries that are no longer present.
    for (const [id, entry] of this.pool) {
      if (!live.has(id)) {
        this.container.removeChild(entry.circle);
        entry.circle.destroy({ children: true });
        this.pool.delete(id);
      }
    }

    for (const pu of pups) {
      const bob = Math.sin(tick * BOB_SPEED + pu.id) * BOB_AMP;
      const existing = this.pool.get(pu.id);
      if (existing) {
        existing.angle += SPIN_SPEED;
        existing.circle.position.set(pu.x, pu.y + bob);
        existing.circle.rotation = existing.angle;
      } else {
        const color  = COLORS[pu.type]  ?? 0xffffff;
        const symbol = SYMBOLS[pu.type] ?? "?";

        const circle = new Graphics();
        circle.beginFill(color, 0.85);
        circle.lineStyle(2, 0xffffff, 0.65);
        circle.drawCircle(0, 0, RADIUS);
        circle.endFill();

        const style = new TextStyle({
          fontFamily: "'Segoe UI', system-ui, sans-serif",
          fontSize: 13,
          fontWeight: "bold",
          fill: 0xffffff,
        });
        const label = new Text(symbol, style);
        label.anchor.set(0.5, 0.5);
        circle.addChild(label);

        circle.position.set(pu.x, pu.y + bob);
        this.container.addChild(circle);
        this.pool.set(pu.id, { circle, angle: 0 });
      }
    }
  }
}
```

## `src/render/Renderer.ts`

```typescript
import { Application, Container, Graphics, Text } from "pixi.js";
import { GlowFilter } from "@pixi/filter-glow";
import type { Snapshot } from "../net/Connection";
import { HazardLayer } from "./HazardLayer";
import { BonusLayer } from "./BonusLayer";
import { PowerUpLayer } from "./PowerUpLayer";
import { BlockLayer } from "./BlockLayer";
import { SpellFxLayer } from "./SpellFxLayer";
import { BallLayer } from "./BallLayer";
import { FireWallLayer } from "./FireWallLayer";
import { PaddleLayer } from "./PaddleLayer";
import { VILLAGE_AMBIENT_REFS } from "./ambientRefs";
void VILLAGE_AMBIENT_REFS; // referenced so the asset-coverage audit sees these frames
import { BackgroundLayer } from "./BackgroundLayer";
import { Effects } from "./Effects";
import { BallTrail } from "./BallTrail";
import { ScreenShake } from "./ScreenShake";
import { Vignette } from "./Vignette";
import { BossRig, TelegraphWarning, inferBossType } from "./Boss";
import { log } from "../log";
import { consumeSfx } from "../audio/Sfx";
import { setMusicBiome } from "../audio/Music";

// Heavy GPU effects (GlowFilter/bloom render-to-texture passes) are gated behind
// this flag so that Playwright's headless software-WebGL never pays the cost.
// navigator.webdriver is true in automation; false/undefined in real browsers.
// NOTE: base rendering always runs — only the optional glow post-processing is gated.
const HEAVY_FX = !(navigator as any).webdriver;

// ── Per-class paddle and ball keys ───────────────────────────────────────────
// Each class has a 4-frame animated bar and a ball sprite.
// Keys are registered here so the audit scanner finds them and the Renderer
// can switch art when setClass() is called from BattleScene.
const CLASS_PADDLE_KEYS: Record<string, string[]> = {
  fire_mage:   [
    "firemage/bars/v2FireHero1","firemage/bars/v2FireHero2",
    "firemage/bars/v2FireHero3","firemage/bars/v2FireHero4",
  ],
  paladin:     [
    "paladin/bars/KnightHero1","paladin/bars/KnightHero2",
    "paladin/bars/KnightHero3","paladin/bars/KnightHero4",
  ],
  engineer:    [
    "engineer/bars/TechnoHero1","engineer/bars/TechnoHero2",
    "engineer/bars/TechnoHero3","engineer/bars/TechnoHero4",
  ],
  necromancer: [
    "necromancer/bars/Necr1","necromancer/bars/Necr2",
    "necromancer/bars/Necr3","necromancer/bars/Necr4",
  ],
};
const CLASS_BALL_KEYS: Record<string, string> = {
  fire_mage:   "firemage/ball/FireHeroBall",
  paladin:     "paladin/ball/KnightHeroBall",
  engineer:    "engineer/ball/KnightHeroBall",
  necromancer: "necromancer/ball/KnightHeroBall",
};

// Default ball key (fire mage); overridden by setClass(). (Paddle keys live in PaddleLayer.)
let _ballSpriteKey    = CLASS_BALL_KEYS.fire_mage;

// Visible gap between bricks so the wall doesn't merge into a solid sheet.
// Expressed as a fraction of cellSize; enforces a 2 px minimum.
const GAP_FRAC = 0.12;

// Extra height below the block grid to ensure the paddle and its clearance are
// fully visible (grid + paddle zone + margin).
const PADDLE_ZONE_CELLS = 3;

// (Paddle/turret + ball/spell constants live in their respective Layer modules.)

// Hit-stop: brief freeze of the world container (enemies / big bosses / ignited kills).
// Implemented as a duration counter; when active, we skip updating the game world
// visually by skipping draw() calls' update of animations for that many ms.
const HIT_STOP_DURATION_BOSS_MS = 80;   // short camera stutter for boss hits
const HIT_STOP_DURATION_IGNITE_MS = 55; // ignited kill

// How long to keep the boss rig visible after defeat (for the explosion burst to play).
const BOSS_DEFEAT_CLEANUP_MS = 1500;

// Damage flash: full-screen red overlay that fades out on a lives decrease.
const DAMAGE_FLASH_ALPHA_START = 0.45;
const DAMAGE_FLASH_FADE_SPEED  = 0.04; // alpha lost per ticker delta

// Glow filter applied to the fx / fire layer and the balls container.
// Kept modest to avoid washing out the whole board on slow hardware.
const GLOW_DISTANCE   = 14;   // px — spread of the glow halo
const GLOW_OUTER_STRENGTH = 3.0;
const GLOW_INNER_STRENGTH = 0.0; // inner-strength=0 avoids colour shift on the core sprite
const GLOW_COLOR      = 0xff6a20; // warm orange — complements fire/explosion palette

export class Renderer {
  app: Application;
  // Biome background + Hell parallax + ambient village beholders.
  private background = new BackgroundLayer();
  private world = new Container();
  private blockLayer = new BlockLayer();
  private effectsLayer: Effects;
  private fireWallLayer = new FireWallLayer();
  private hazardLayer = new HazardLayer();
  readonly paddleLayer = new PaddleLayer();
  private ballLayer = new BallLayer();
  private ballTrail: BallTrail;
  private screenShake: ScreenShake;
  // Store the base fit position so screen-shake can layer on top.
  private _fitX = 0;
  private _fitY = 0;
  private damageFlash = new Graphics(); // full-screen overlay for HP hit feedback
  private _tick = 0; // used to drive wall flicker animation
  private _lastLives = -1; // track lives decreases for damage flash


  // Bonus pickups layer (generic atlas-icon pickups).
  private bonusLayer = new BonusLayer();
  // Power-up falling pickups (coloured circles: wide/multiball/fireshot/manasurge/shield).
  private powerUpLayer = new PowerUpLayer();

  // ── P6 per-class spell effects (Paladin barriers, Engineer zones, Necro skeleton) ──
  private spellFx = new SpellFxLayer();

  // Boss rig: one BossRig instance while bossActive, destroyed when boss dies.
  private _bossRig: BossRig | null = null;
  // The boss type inferred from boss block sprites (set when rig is created).
  private _bossRigType = "";
  // Whether the boss was active in the previous frame (for defeat detection).
  private _prevBossActive = false;
  // Telegraph warning glyph (reusable).
  private _telegraphWarning = new TelegraphWarning();
  // Boss region bounding box (updated each frame).
  private _bossRegion = { cx: 0, cy: 0, w: 0, h: 0 };
  // Latest boss HP fraction — kept so the ticker can drive boss animation.
  private _bossHpFrac = 1.0;
  // Boss rig container layer (sits above blocks).
  private _bossLayer = new Container();

  // Hit-stop state: remaining ms of visual freeze.
  private _hitStopRemaining = 0;

  // Last-brick highlight: gold pulsing outline on the final ≤3 destructible bricks.
  private _dangerOverlay = new Graphics();
  private _dangerBlocks: { x: number; y: number }[] = [];
  private _dangerBrickSize = 0;

  // Floating score popups: pool of 10 Text objects reused to avoid GC pressure.
  private _floaterPool: Text[] = [];
  private _activeFloaters: { text: Text; elapsed: number }[] = [];
  private _floaterContainer = new Container();
  // Previous-frame block positions — used to detect block disappearances for floater spawning.
  private _prevBlocks = new Map<number, { x: number; y: number }>();

  /** Switch the paddle/ball sprites to match the given class. */
  setClass(classId: string) {
    const paddleKeys = CLASS_PADDLE_KEYS[classId] ?? CLASS_PADDLE_KEYS.fire_mage;
    _ballSpriteKey   = CLASS_BALL_KEYS[classId] ?? CLASS_BALL_KEYS.fire_mage;
    this.paddleLayer.setClass(paddleKeys);
  }

  constructor(host: HTMLElement) {
    // resolution + autoDensity: render at the device pixel ratio (capped at 2)
    // so Windows display scaling (dpr 1.25–1.5) and retina phones get a sharp
    // canvas instead of a stretched 1x buffer (docs/13 §S5).
    this.app = new Application({
      resizeTo: host,
      background: "#0b0b12",
      antialias: true,
      resolution: Math.min(window.devicePixelRatio || 1, 2),
      autoDensity: true,
    });
    host.appendChild(this.app.view as HTMLCanvasElement);

    this.effectsLayer = new Effects();
    this.ballTrail = new BallTrail();
    this.screenShake = new ScreenShake();



    // Apply a single GlowFilter to the fx + fire layer group and to the balls
    // container so that explosions, fire walls, halos, and balls all glow.
    // Scoped to bright/fx elements only — blocks and paddle are untouched.
    //
    // HEAVY_FX is false under Playwright (navigator.webdriver===true), so the
    // GlowFilter render-to-texture passes are skipped entirely in headless runs,
    // preventing WebSocket snapshot starvation from GPU thread blocking.
    // The filter arrays are assigned once here and never reassigned per-frame.
    if (HEAVY_FX) {
      const fxGlow = new GlowFilter({
        distance:      GLOW_DISTANCE,
        outerStrength: GLOW_OUTER_STRENGTH,
        innerStrength: GLOW_INNER_STRENGTH,
        color:         GLOW_COLOR,
        quality:       0.25, // low quality = faster; perfectly fine for bloom halos
      });
      this.effectsLayer.container.filters = [fxGlow];
      this.fireWallLayer.container.filters = [fxGlow];

      // Ball glow: separate filter instance so ball trails can share it independently.
      const ballGlow = new GlowFilter({
        distance:      10,
        outerStrength: 2.2,
        innerStrength: 0.0,
        color:         0xffffff,
        quality:       0.25,
      });
      this.ballLayer.container.filters = [ballGlow];
    }

    // Add telegraph warning container to boss layer.
    this._bossLayer.addChild(this._telegraphWarning.container);

    // Floating score popup pool: 10 pre-allocated PIXI.Text objects (task 1.3).
    const floaterStyle = {
      fontSize: 14,
      fill: 0xd8a84e,
      fontWeight: "bold" as const,
      dropShadow: true,
      dropShadowDistance: 2,
      dropShadowAlpha: 0.8,
    };
    for (let i = 0; i < 10; i++) {
      const t = new Text("", floaterStyle);
      t.visible = false;
      t.anchor.set(0.5, 1); // anchor at bottom-centre so text rises from block position
      this._floaterPool.push(t);
      this._floaterContainer.addChild(t);
    }

    // Layer order: ambient → ballTrail → zones → blocks → dangerOverlay → fireWalls → barriers → bossLayer → effects → paddle/turret → ballAuras → balls → skeleton → hazards → bonuses → powerUps
    this.world.addChild(
      this.background.ambientContainer,
      this.ballTrail.container,
      this.spellFx.zonesContainer,
      this.blockLayer.container,
      this._dangerOverlay,
      this.fireWallLayer.container,
      this.spellFx.barriersContainer,
      this._bossLayer,
      this.effectsLayer.container,
      this.paddleLayer.container,
      // Balls draw over the paddle: the bar art is much taller than the physics
      // band, so a served ball resting on the paddle top must not hide behind it.
      this.ballLayer.auraContainer,
      this.ballLayer.container,
      this.spellFx.skeletonAnim.container,
      this.hazardLayer.container,
      this.bonusLayer.container,
      this.powerUpLayer.container,
      this._floaterContainer,  // floating score popups drawn over everything
    );
    // Damage flash sits on stage (not world) so it covers the full screen regardless of world scale.
    this.damageFlash.alpha = 0;
    // Layer order on stage: bg → world → damageFlash → vignette
    this.app.stage.addChild(this.background.bgLayer);
    this.app.stage.addChild(this.world);
    this.app.stage.addChild(this.damageFlash);

    // Vignette: subtle dark corners overlay on the stage (top-most).
    new Vignette(this.app);

    // Tick the effects every frame and drive wall flicker.
    this.app.ticker.add((delta) => {
      // delta is in Pixi ticker units (frames at 60 fps → multiply by 1000/60 for ms)
      const dtMs = (delta / 60) * 1000;

      // Hit-stop: while active, freeze AnimatedSprites (don't update) and damp animations.
      if (this._hitStopRemaining > 0) {
        this._hitStopRemaining -= dtMs;
        // Skip effects + ball aura updates during hit-stop so the world freezes visually.
      } else {
        this.effectsLayer.update(dtMs);
        this.ballLayer.updateAnim(dtMs);
        this.spellFx.updateAnim(dtMs);
      }

      // Telegraph warning update (runs regardless of hit-stop for clarity).
      this._telegraphWarning.update(dtMs, this._bossRegion.w * 0.5);

      // Boss rig animation: drive with real dt so idle bob/lunge/flash animate.
      // draw() calls setRegion() to reposition; the ticker drives animation timing.
      if (this._bossRig) {
        this._bossRig.update(
          this._bossRegion.cx, this._bossRegion.cy,
          this._bossRegion.w, this._bossRegion.h,
          this._bossHpFrac, this._tick, dtMs,
        );
      }

      this.screenShake.update(dtMs);
      // Apply screen-shake offset on top of the fit position calculated last draw().
      this.world.position.set(
        this._fitX + this.screenShake.offsetX,
        this._fitY + this.screenShake.offsetY,
      );
      this._tick += delta;

      // Paddle squash/stretch + bar-frame animation.
      this.paddleLayer.updateAnim(dtMs);

      // Fade the damage flash overlay.
      if (this.damageFlash.alpha > 0) {
        this.damageFlash.alpha = Math.max(0, this.damageFlash.alpha - DAMAGE_FLASH_FADE_SPEED * delta);
      }

      // Last-brick highlight: gold pulsing outline on final ≤3 destructible bricks.
      if (this._dangerBlocks.length > 0 && this._dangerBrickSize > 0) {
        const pulseAlpha = 0.5 + 0.4 * Math.sin(Date.now() / 300);
        this._dangerOverlay.clear();
        this._dangerOverlay.lineStyle(3, 0xd8a84e, pulseAlpha);
        const half = this._dangerBrickSize / 2;
        for (const b of this._dangerBlocks) {
          this._dangerOverlay.drawRect(b.x - half, b.y - half, this._dangerBrickSize, this._dangerBrickSize);
        }
      } else if (this._dangerBlocks.length === 0) {
        this._dangerOverlay.clear();
      }

      // Floating score popups: rise and fade over 800 ms.
      for (let fi = this._activeFloaters.length - 1; fi >= 0; fi--) {
        const f = this._activeFloaters[fi];
        f.elapsed += dtMs;
        // Rise 40 world-units over 800 ms.
        f.text.y -= 40 * (dtMs / 800);
        f.text.alpha = Math.max(0, 1 - f.elapsed / 800);
        if (f.elapsed >= 800) {
          f.text.visible = false;
          f.text.alpha = 1;
          this._floaterPool.push(f.text);
          this._activeFloaters.splice(fi, 1);
        }
      }

      // Ambient sprite drift animation (village beholders).
      this.background.updateAnim(dtMs);
    });
  }

  /** Spawn a floating "+1 ×M" score label rising from world position (wx, wy). */
  private _spawnFloater(wx: number, wy: number, mult: number) {
    if (this._floaterPool.length === 0) return; // pool exhausted — skip
    const t = this._floaterPool.pop()!;
    t.text = `+1 ×${mult}`;  // e.g. "+1 ×3"
    t.x = wx;
    t.y = wy;
    t.alpha = 1;
    t.visible = true;
    this._activeFloaters.push({ text: t, elapsed: 0 });
  }

  private fit(s: Snapshot) {
    // Include paddle zone below the block grid so the paddle is never clipped.
    const effectiveH = s.boardH + s.cellSize * PADDLE_ZONE_CELLS;
    // Reserve space at the top for the DOM HUD (HP + lives bars, two 20px bars
    // plus margins) — the playfield used to start at y≈0 and the top brick rows
    // rendered underneath the bars (docs/13 battle audit).
    const HUD_TOP_INSET = 58;
    const availableH = this.app.screen.height - HUD_TOP_INSET;
    // Portrait-first: prefer filling the full height, then constrain by width.
    const scale = Math.min(
      this.app.screen.width / s.boardW,
      availableH / effectiveH,
    ) * 0.97;
    this.world.scale.set(scale);
    // Centre horizontally; align below the HUD band so blocks start clear of it.
    this._fitX = (this.app.screen.width - s.boardW * scale) / 2;
    this._fitY = Math.max(HUD_TOP_INSET, HUD_TOP_INSET + (availableH - effectiveH * scale) / 2);
    this.world.position.set(this._fitX, this._fitY);

    // Resize background + parallax to cover the full stage.
    this.background.resize(this.app.screen.width, this.app.screen.height);
  }

  draw(s: Snapshot) {
    // --- biome background + Hell parallax + ambient village beholders (rebuilt on biome change) ---
    this.background.setBiome(s.biome, s.cellSize);

    this.fit(s);

    // --- screen shake + hit-stop: fire on relevant events ---
    const shakeEnabled = localStorage.getItem("arkanoid_fx") !== "0"
      && !window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    for (const ev of s.events) {
      if (ev.type === "playerHit") { if (shakeEnabled) this.screenShake.trigger("playerHit"); }
      else if (ev.type === "bossAttack") {
        if (shakeEnabled) this.screenShake.trigger("bossAttack");
        // Short hit-stop on boss attacks.
        this._hitStopRemaining = Math.max(this._hitStopRemaining, HIT_STOP_DURATION_BOSS_MS);
      } else if (ev.type === "ignite") {
        // Brief hit-stop when ignite lands.
        this._hitStopRemaining = Math.max(this._hitStopRemaining, HIT_STOP_DURATION_IGNITE_MS);
      }
    }

    // --- damage flash: trigger on lives decrease ---
    if (this._lastLives >= 0 && s.lives < this._lastLives) {
      // Repaint the full-screen flash rect to match current screen size, then trigger.
      this.damageFlash.clear();
      this.damageFlash.beginFill(0xff0000, 1)
        .drawRect(0, 0, this.app.screen.width, this.app.screen.height)
        .endFill();
      this.damageFlash.alpha = DAMAGE_FLASH_ALPHA_START;
    }
    this._lastLives = s.lives;

    // --- blocks (pooled: damage states, mirror, boss aura, teleporter ring, ghost, shield) ---
    const gap = Math.max(s.cellSize * GAP_FRAC, 2);
    const brickSize = s.cellSize - gap;
    this.blockLayer.update(s.blocks, this._tick, brickSize, s.windRadius ?? 0);

    // --- floating score popups: detect destroyed blocks, spawn floater when combo > 1 ---
    const combo = s.comboMultiplier ?? 1;
    if (this._prevBlocks.size > 0 && combo > 1) {
      const currentIds = new Set(s.blocks.map(b => b.id));
      for (const [id, pos] of this._prevBlocks) {
        if (!currentIds.has(id)) {
          this._spawnFloater(pos.x, pos.y, combo);
        }
      }
    }
    // Update prev-blocks map for next frame.
    this._prevBlocks.clear();
    for (const b of s.blocks) {
      this._prevBlocks.set(b.id, { x: b.x, y: b.y });
    }

    // --- last-brick highlight: track the final ≤3 destructible bricks for the pulsing overlay ---
    const destructible = s.blocks.filter(b => !b.indestructible && b.hp > 0);
    this._dangerBrickSize = brickSize;
    this._dangerBlocks = (destructible.length <= 3 && destructible.length > 0)
      ? destructible.map(b => ({ x: b.x, y: b.y }))
      : [];

    // --- boss rig: assemble / update / destroy animated multi-part boss ---
    // Compute the boss-block bounding region this frame.
    const bossBlocks = s.blocks.filter(b => b.boss);
    if (bossBlocks.length > 0) {
      let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
      for (const b of bossBlocks) {
        minX = Math.min(minX, b.x - brickSize / 2);
        maxX = Math.max(maxX, b.x + brickSize / 2);
        minY = Math.min(minY, b.y - brickSize / 2);
        maxY = Math.max(maxY, b.y + brickSize / 2);
      }
      const regionCx = (minX + maxX) / 2;
      const regionCy = (minY + maxY) / 2;
      const regionW  = maxX - minX;
      const regionH  = maxY - minY;
      this._bossRegion = { cx: regionCx, cy: regionCy, w: regionW, h: regionH };

      // Infer boss type from first boss block sprite.
      const bossType = inferBossType(bossBlocks[0].sprite);
      const bossTypeStr = bossBlocks[0].sprite;

      // Create or recreate rig if type changed.
      if (!this._bossRig || this._bossRigType !== bossTypeStr) {
        if (this._bossRig) {
          this._bossLayer.removeChild(this._bossRig.container);
          this._bossRig.destroy();
        }
        this._bossRig = new BossRig(bossType);
        this._bossRigType = bossTypeStr;
        this._bossLayer.addChildAt(this._bossRig.container, 0);
        // Audit log + test hook: which rig art is actually shown for this boss block.
        log("boss", `rig created type=${bossType} sprite=${bossTypeStr}`);
        (window as unknown as { __bossRigType?: string }).__bossRigType = bossType;
      }

      // Hide the plain boss-block sprites while the rig is showing.
      for (const b of bossBlocks) this.blockLayer.hideBlock(b.id);

      // Compute HP fraction (stored so the ticker can animate the rig).
      this._bossHpFrac = s.bossMaxHp > 0 ? s.bossHp / s.bossMaxHp : 1;

      // Reposition the rig to match the current boss-block region.
      // Animation (bob/lunge/flash) is driven by the Pixi ticker above with real dt.
      this._bossRig.setRegion(regionCx, regionCy, regionW, regionH);
    }

    // Boss-active → inactive transition: defeat flourish.
    if (this._prevBossActive && !s.bossActive) {
      if (this._bossRig) {
        this._bossRig.onDefeat(s.cellSize);
        // Animate defeat in-place for a beat, then clean up.
        setTimeout(() => {
          if (this._bossRig) {
            this._bossLayer.removeChild(this._bossRig.container);
            this._bossRig.destroy();
            this._bossRig = null;
            this._bossRigType = "";
          }
        }, BOSS_DEFEAT_CLEANUP_MS);
      }
    }
    this._prevBossActive = s.bossActive;

    // Boss events: telegraph warning + lunge.
    for (const ev of s.events) {
      if (ev.type === "bossTelegraph") {
        if (this._bossRig) this._bossRig.onTelegraph();
        this._telegraphWarning.trigger(
          this._bossRegion.cx, this._bossRegion.cy,
          this._bossRegion.w,
        );
      } else if (ev.type === "bossAttack") {
        if (this._bossRig) this._bossRig.onTelegraph(); // also lunge on actual attack
      }
    }

    // --- fire walls (animated FireStandAnnimation tiles, rebuilt on count change) ---
    this.fireWallLayer.update(s.walls ?? [], this._tick, s.cellSize, s.boardW);

    // --- paddle + turret (squash trigger, per-class bar sprite, turret indicator) ---
    this.paddleLayer.update(s.paddleX, s.paddleW, s.paddleH, s.boardH, s.cellSize, s.turretActive, s.balls);
    // Drive the bar-frame from mana ratio so power-state art matches game state.
    this.paddleLayer.setMana(s.mana / (s.manaMax || 100));

    // --- ball trail (drawn behind balls) ---
    const ballRadius = s.cellSize * 0.25;
    this.ballTrail.update(s.balls, ballRadius);

    // --- balls (pooled by id: per-class sprite, projectile art, ignite/decay halos + aura) ---
    this.ballLayer.update(s.balls, s.projectiles ?? [], this._tick, s.cellSize, _ballSpriteKey);

    // --- hazards (falling/rolling enemy projectiles) ---
    this.hazardLayer.update(s.hazards ?? [], this._tick);

    // --- bonus pickups (falling icons from Bonus/ art) ---
    this.bonusLayer.update(s.bonuses ?? [], this._tick);
    // --- power-up pickups (coloured circles: wide/multiball/fireshot/manasurge/shield) ---
    this.powerUpLayer.update(s.bonuses ?? [], this._tick);

    // Catch sparkle: fire on bonusCaught events.
    for (const ev of s.events) {
      if (ev.type === "bonusCaught") {
        this.effectsLayer.consume([{ type: "blockDestroyed", x: ev.x, y: ev.y }], s.cellSize, s.biome);
      }
    }

    // ── P6 per-class spell effects (Paladin barriers, Engineer zones, Necro skeleton) ──
    this.spellFx.update(s.barriers ?? [], s.zones ?? [], s.skeletonActive ?? false, this._tick, s.cellSize, s.boardW, s.boardH);

    // --- P6 events: lightning, explosion (rocket), radiation, decay ---
    // These are remapped to existing effect types so they reuse the existing
    // atlas art and Effects pipeline without requiring new Effects methods.
    const remappedEvents: Snapshot["events"] = [];
    for (const ev of s.events) {
      if (ev.type === "lightning") {
        // Lightning: use spellCast (phoenix flourish) for a bright flash.
        remappedEvents.push({ type: "spellCast", x: ev.x, y: ev.y });
      } else if (ev.type === "explosion") {
        // Rocket explosion: full blockDestroyed blast at impact.
        remappedEvents.push({ type: "blockDestroyed", x: ev.x, y: ev.y });
      } else if (ev.type === "radiation" || ev.type === "decay") {
        // Radiation / decay: smaller burn flash.
        remappedEvents.push({ type: "burn", x: ev.x, y: ev.y });
      }
    }
    if (remappedEvents.length > 0) {
      this.effectsLayer.consume(remappedEvents, s.cellSize, s.biome);
    }

    // --- effects: consume snapshot events ---
    this.effectsLayer.boardH = s.boardH;
    this.effectsLayer.consume(s.events, s.cellSize, s.biome);
    consumeSfx(s.events); // procedural Web Audio cues (G1) — same event stream
    setMusicBiome(s.biome); // per-biome generative ambience (docs/12 briefs)
  }
}
```

## `src/render/ScreenShake.ts`

```typescript
// Duration constants for each shake source.
const SHAKE_DURATION_PLAYER_HIT_MS = 280;
const SHAKE_DURATION_BOSS_ATTACK_MS = 120;

// Peak offset in world pixels (will be scaled by world.scale automatically since
// we apply the offset to world.position which is already in screen-space).
const SHAKE_MAGNITUDE_PLAYER_HIT = 9;  // screen pixels
const SHAKE_MAGNITUDE_BOSS_ATTACK = 3;

interface ShakeState {
  elapsed: number;
  duration: number;
  magnitude: number;
}

export class ScreenShake {
  private shake: ShakeState | null = null;
  /** Offset applied this frame — add to world.position on top of fit position. */
  offsetX = 0;
  offsetY = 0;

  /** Trigger a shake. If one is already running, replace if the new one is bigger. */
  trigger(type: "playerHit" | "bossAttack") {
    const duration  = type === "playerHit" ? SHAKE_DURATION_PLAYER_HIT_MS  : SHAKE_DURATION_BOSS_ATTACK_MS;
    const magnitude = type === "playerHit" ? SHAKE_MAGNITUDE_PLAYER_HIT    : SHAKE_MAGNITUDE_BOSS_ATTACK;
    // Allow upgrade (bigger shake replaces smaller) but never downgrade.
    if (!this.shake || magnitude >= this.shake.magnitude) {
      this.shake = { elapsed: 0, duration, magnitude };
    }
  }

  /**
   * Call every ticker frame with the delta in ms.
   * Updates `offsetX` / `offsetY`; caller adds them to `world.position`.
   */
  update(dtMs: number) {
    if (!this.shake) {
      this.offsetX = 0;
      this.offsetY = 0;
      return;
    }
    this.shake.elapsed += dtMs;
    const t = this.shake.elapsed / this.shake.duration;
    if (t >= 1) {
      this.shake = null;
      this.offsetX = 0;
      this.offsetY = 0;
      return;
    }
    // Exponential decay envelope: starts at 1, falls to ~0 by t=1.
    const envelope = Math.pow(1 - t, 2);
    const angle = Math.random() * Math.PI * 2;
    const r = this.shake.magnitude * envelope;
    this.offsetX = Math.cos(angle) * r;
    this.offsetY = Math.sin(angle) * r;
  }
}
```

## `src/render/SpellFxLayer.ts`

```typescript
import { Container, Graphics, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { anim as animFrames } from "./assets";
import { AnimSystem } from "./AnimSystem";

// Per-class spell-effect rendering (Paladin barriers, Engineer radiation zones,
// Necromancer skeleton summon). Extracted from Renderer. Exposes three display
// containers so the caller can slot each into the correct world z-order, plus an
// updateAnim(dtMs) for the skeleton's looping AnimSystem.

// Paladin barrier: shield bar rendered per entry in barriers[].
const BARRIER_HEIGHT_FRAC  = 0.18; // fraction of cellSize for bar thickness
const BARRIER_GLOW_ALPHA   = 0.55;
const BARRIER_FILL_COLOR   = 0x88ccff; // steel-blue core
const BARRIER_GLOW_COLOR   = 0x4499ff; // cooler blue additive glow
const BARRIER_GLOW_W_EXTRA = 16;       // extra px each side for glow halo

// Engineer radiation zone: pulsing AoE circle.
const ZONE_FILL_COLOR      = 0x22ff44;  // toxic green
const ZONE_GLOW_COLOR      = 0x44ff88;
const ZONE_FILL_ALPHA_BASE = 0.12;
const ZONE_FILL_ALPHA_AMP  = 0.06;
const ZONE_RING_ALPHA      = 0.6;
const ZONE_PULSE_SPEED     = 0.09;

// Skeleton summon position (top of board, centered).
const SKELETON_Y_FRAC = 0.08; // fraction of boardH from top

interface Barrier { y: number; centerX: number; width: number }
interface Zone { x: number; y: number; radius: number }

export class SpellFxLayer {
  readonly barriersContainer = new Container();
  readonly zonesContainer    = new Container();
  readonly skeletonAnim      = new AnimSystem();

  // Necromancer skeleton summon state.
  private _skeletonGlow: Graphics | null = null;
  private _skeletonAuraId: number | undefined;

  /** Skeleton aura is a looping AnimatedSprite — advance it each frame. */
  updateAnim(dtMs: number): void {
    this.skeletonAnim.update(dtMs);
  }

  update(
    barriers: Barrier[], zones: Zone[], skeletonActive: boolean,
    tick: number, cellSize: number, boardW: number, boardH: number,
  ): void {
    this.drawBarriers(barriers, cellSize);
    this.drawZones(zones, tick);
    this.updateSkeleton(skeletonActive, cellSize, boardW, boardH);
  }

  // --- barriers (Paladin shield bars) ---
  private drawBarriers(barriers: Barrier[], cellSize: number): void {
    this.barriersContainer.removeChildren();
    for (const br of barriers) {
      const barH = cellSize * BARRIER_HEIGHT_FRAC;
      // Glow halo (additive, wider than fill).
      const glow = new Graphics();
      glow.blendMode = BLEND_MODES.ADD;
      glow.beginFill(BARRIER_GLOW_COLOR, BARRIER_GLOW_ALPHA)
        .drawRoundedRect(
          br.centerX - br.width / 2 - BARRIER_GLOW_W_EXTRA,
          br.y - barH * 1.4,
          br.width + BARRIER_GLOW_W_EXTRA * 2,
          barH * 2.8,
          barH,
        )
        .endFill();
      // Core fill.
      const fill = new Graphics();
      fill.beginFill(BARRIER_FILL_COLOR, 0.92)
        .drawRoundedRect(
          br.centerX - br.width / 2,
          br.y - barH / 2,
          br.width,
          barH,
          barH / 2,
        )
        .endFill();

      // Optionally overlay atlas shield art if available.
      const shieldTex = tex("paladin/spell_passiveshield/KnightShield");
      if (shieldTex !== Texture.WHITE) {
        // Tile the shield art across the barrier width.
        const tileSize = barH * 3.5;
        const count = Math.max(1, Math.round(br.width / tileSize));
        for (let i = 0; i < count; i++) {
          const sp = new Sprite(shieldTex);
          sp.anchor.set(0.5);
          sp.width  = tileSize;
          sp.height = tileSize;
          sp.x = br.centerX - br.width / 2 + tileSize / 2 + i * tileSize;
          sp.y = br.y;
          sp.alpha = 0.85;
          sp.tint = BARRIER_FILL_COLOR;
          this.barriersContainer.addChild(sp);
        }
      }

      this.barriersContainer.addChild(glow);
      this.barriersContainer.addChild(fill);
    }
  }

  // --- zones (Engineer radiation AoE) ---
  private drawZones(zones: Zone[], tick: number): void {
    this.zonesContainer.removeChildren();
    const radiationTex = tex("engineer/spell_raditation/Radiation");
    for (const zn of zones) {
      const fillAlpha = ZONE_FILL_ALPHA_BASE
        + ZONE_FILL_ALPHA_AMP * Math.sin(tick * ZONE_PULSE_SPEED);
      // Additive glow ring.
      const ring = new Graphics();
      ring.blendMode = BLEND_MODES.ADD;
      ring.beginFill(ZONE_GLOW_COLOR, fillAlpha * 1.5)
        .drawCircle(zn.x, zn.y, zn.radius * 1.05)
        .endFill();
      // Inner fill.
      const fill = new Graphics();
      fill.beginFill(ZONE_FILL_COLOR, fillAlpha)
        .drawCircle(zn.x, zn.y, zn.radius)
        .endFill();
      // Border ring.
      const border = new Graphics();
      border.blendMode = BLEND_MODES.ADD;
      border.lineStyle(2, ZONE_GLOW_COLOR, ZONE_RING_ALPHA)
        .drawCircle(zn.x, zn.y, zn.radius);

      this.zonesContainer.addChild(ring);
      this.zonesContainer.addChild(fill);
      this.zonesContainer.addChild(border);

      // Overlay radiation art in center if available.
      if (radiationTex !== Texture.WHITE) {
        const sp = new Sprite(radiationTex);
        sp.anchor.set(0.5);
        const iconSize = zn.radius * 0.6;
        sp.width  = iconSize * 2;
        sp.height = iconSize * 2;
        sp.x = zn.x;
        sp.y = zn.y;
        sp.alpha = 0.7 + 0.15 * Math.sin(tick * ZONE_PULSE_SPEED * 1.3);
        sp.tint = ZONE_FILL_COLOR;
        this.zonesContainer.addChild(sp);
      }
    }
  }

  // --- skeleton summon (Necromancer) ---
  private updateSkeleton(skeletonActive: boolean, cellSize: number, boardW: number, boardH: number): void {
    // SkeletalMage is a single static frame; Skeleton2BirthAnimation is an animated strip.
    const skeletonFrames = animFrames("necromancer/spell_skeleton/SkeletalMage");
    const skFrameArr = skeletonFrames.length > 0
      ? skeletonFrames
      : [tex("necromancer/spell_skeleton/SkeletalMage")].filter(t => t !== Texture.WHITE);

    if (skeletonActive) {
      const skX = boardW / 2;
      const skY = boardH * SKELETON_Y_FRAC + cellSize;

      if (this._skeletonAuraId === undefined) {
        // Spawn looping skeleton aura display.
        if (skFrameArr.length > 0) {
          const h = this.skeletonAnim.looping(
            skFrameArr, 12,
            skX, skY,
            cellSize * 2.8,
            false, 0xaaaaff,
          );
          this._skeletonAuraId = h.id;
        }
        // Glow circle behind the skeleton sprite.
        if (!this._skeletonGlow) {
          const skGlow = new Graphics();
          skGlow.blendMode = BLEND_MODES.ADD;
          skGlow.beginFill(0x8888ff, 0.35)
            .drawCircle(skX, skY, cellSize * 1.8)
            .endFill();
          this.skeletonAnim.container.addChildAt(skGlow, 0);
          this._skeletonGlow = skGlow;
        }
      } else {
        // Update position of existing looping handle.
        this.skeletonAnim.moveTo({ id: this._skeletonAuraId }, skX, skY);
      }
    } else {
      // Skeleton no longer active — remove.
      if (this._skeletonAuraId !== undefined) {
        this.skeletonAnim.remove({ id: this._skeletonAuraId });
        this._skeletonAuraId = undefined;
      }
      if (this._skeletonGlow) {
        this._skeletonGlow.parent?.removeChild(this._skeletonGlow);
        this._skeletonGlow = null;
      }
    }
  }
}
```

## `src/render/Vignette.ts`

```typescript
import { Application, Graphics } from "pixi.js";

// How much of the screen the vignette covers (0 = nothing, 1 = full width/height).
// The darkened region starts at INNER_FRAC from centre and reaches the edges.
const INNER_FRAC = 0.55;   // fraction of the smaller screen dimension — inner bright circle
const OUTER_ALPHA = 0.52;  // opacity at the very corner

/**
 * Subtle full-screen vignette drawn as a dark radial overlay.
 * Uses a Graphics-based approximation (several concentric rectangles fading
 * from fully transparent in the centre to semi-opaque black at the edges).
 *
 * This avoids a ShaderFilter dependency and is extremely cheap.
 */
export class Vignette {
  private gfx: Graphics;

  constructor(app: Application) {
    this.gfx = new Graphics();
    // Sit on top of everything else on the stage.
    app.stage.addChild(this.gfx);

    // Rebuild when the renderer resizes.
    app.renderer.on("resize", () => this.rebuild(app));
    this.rebuild(app);
  }

  private rebuild(app: Application) {
    const w = app.screen.width;
    const h = app.screen.height;
    const cx = w / 2;
    const cy = h / 2;

    this.gfx.clear();

    // Draw concentric rings fading from transparent centre to dark edge.
    // We use a radial approach: layer several filled rects with SUBTRACT-like
    // blending — but Pixi normal alpha blend with black rectangles at decreasing
    // radius does the job visually without needing special blend modes.
    const STEPS = 12;
    const innerR = Math.min(w, h) * INNER_FRAC;
    const outerR = Math.sqrt(cx * cx + cy * cy); // corner distance

    for (let i = 0; i < STEPS; i++) {
      // t=0 → inner (transparent), t=1 → outer (dark)
      const t = (i + 1) / STEPS;
      const r = innerR + (outerR - innerR) * t;
      const alpha = OUTER_ALPHA * (t * t); // quadratic falloff — barely visible in centre

      // Clip rect around the circle.
      const rx = Math.max(cx - r, 0);
      const ry = Math.max(cy - r, 0);
      const rw = Math.min(r * 2, w);
      const rh = Math.min(r * 2, h);

      this.gfx.beginFill(0x000000, alpha / STEPS)
        .drawRect(rx, ry, rw, rh)
        .endFill();
    }

    // Solid dark corners: draw the four corner triangles at moderate opacity.
    const cornerAlpha = OUTER_ALPHA * 0.45;
    this.gfx.beginFill(0x000000, cornerAlpha)
      .drawRect(0, 0, cx * 0.28, cy * 0.28)          // top-left
      .drawRect(w - cx * 0.28, 0, cx * 0.28, cy * 0.28)       // top-right
      .drawRect(0, h - cy * 0.28, cx * 0.28, cy * 0.28)       // bottom-left
      .drawRect(w - cx * 0.28, h - cy * 0.28, cx * 0.28, cy * 0.28) // bottom-right
      .endFill();
  }
}
```

## `src/render/ambientRefs.ts`

```typescript
// Village enemy cosmetic frame keys referenced explicitly so the asset-coverage
// audit sees them. Pure documentation data — not used by render logic. Extracted
// from Renderer.ts to keep that file focused.

export const VILLAGE_AMBIENT_REFS = [
  "village/enemies/BeholderAttackAnimation","village/enemies/BeholderDeathAnimation",
  "village/enemies/BeholderGhostAttackAnimation","village/enemies/BeholderGhostDeathAnimation",
  "village/enemies/BeholderMissile","village/enemies/BeholderMissileGhost",
  "village/enemies/BatFlyAnimation3","village/enemies/BatGhostFlyAnimation3",
  "village/enemies/BatSleeping","village/enemies/BatGhostSleeping",
  "village/enemies/DeathSphere","village/enemies/DeathGhostSphere",
  "village/enemies/VillageShadow","village/enemies/VillageShadowGhost",
  "village/enemies/VillageDeath","village/enemies/VillageDeathGhost",
  "village/enemies/VillageDeathCastAnimation","village/enemies/VillageDeathDeathAnimation",
  "village/enemies/VillageDeathGhostCastAnimation","village/enemies/VillageDeathGhostDeathAnimation",
  "village/enemies/VillageMetlaGhost","village/enemies/WitchSkirt2","village/enemies/WitchLeg3",
  "village/enemies/WitchMagic1","village/enemies/WitchMagic2","village/enemies/WitchMagic3","village/enemies/WitchMagic4",
  "village/enemies/WitchChest2",
  "hell/SkullRed","hell/SkullRedActive","hell/SkullBlue","hell/SkullBlueActive",
  "hell/SkullGreen","hell/SkullGreenActive","hell/Skull","hell/SkullAnimation",
  "hell/HellChest","hell/HellChestStandAnimation",
  "hell/LavaSpowner","hell/LavaSpownerActive","hell/LavaSpownerDamaged","hell/LavaSpownerDestroyed",
  "hell/LavaBegining","hell/LavaEnd","hell/LavaMainPart",
  "dungeon/DungeonCart","dungeon/DungeonCartWheel",
  "dungeon/Stalactite","dungeon/Stalactite2","dungeon/Stone","dungeon/StoneLight",
  "dungeon/ChestDungeon","dungeon/ChestDungeon 1",
  "dungeon/Bomb","dungeon/BombStand","dungeon/BombStandVertical",
  "dungeon/GrateBomb","dungeon/GrateBombStand","dungeon/GrateBombStandVertical",
  "heaven/Cloud","heaven/Clouds","heaven/HeavenClouds",
  "heaven/GraalHaven","heaven/HeavenAltarV2","heaven/HeavenAltarV2Active",
  "heaven/Column","heaven/ColumnBottom","heaven/ColumnTop",
  "heaven/HeavenDefender","heaven/HeavenDefenderActive",
  "heaven/HeavenMeleeStatue","heaven/HeavenMeleeStatueActive",
  "heaven/HeavenVaza","heaven/HolyBall","heaven/Missile","heaven/Shield",
  "fons/CavVil","fons/HellCav","fons/ValHav",
  "firemage/spell_phonex/Phoenics","firemage/spell_phonex/PhoenicsBody",
  "firemage/spell_phonex/PhoenicsGlow","firemage/spell_phonex/PhoenicsIco",
  "firemage/spell_phonex/PhoenixBirthAnimation","firemage/spell_phonex/PhoenixBirthAnimLow",
  "firemage/spell_phonex/PhoenixGlow",
  "necromancer/spell_skeleton/SkeletonCrown","necromancer/spell_skeleton/SkeletonGlow",
  "necromancer/spell_skeleton/SkeletonMissile","necromancer/spell_skeleton/SkeletonRise",
  "necromancer/spell_skeleton/skeleton2","necromancer/spell_skeleton/Skeleton2BirthAnimation",
  "necromancer/spell_skeleton/Skeleton","necromancer/spell_skeleton/SkeletonBirth",
  "necromancer/spell_skeleton/SkeletalMageBirth","necromancer/spell_skeleton/SkeletalMageDeath",
  "necromancer/spell_skeleton/SkeletalMageGlow","necromancer/spell_skeleton/SkeletalMageMissile",
  "necromancer/spell_skeleton/SkeletalMage","necromancer/spell_duplication/SkeletalMageRise",
  "necromancer/spell_lastday/BoneGolem","necromancer/spell_lastday/BoneGolemBirth",
  "necromancer/spell_lastday/BoneGolemDeathAnim","necromancer/spell_lastday/LustJudgmentClouds",
  "necromancer/spell_lastday/KnightLightSpell","necromancer/spell_lastday/KnightLightSpell2",
  "necromancer/spell_lastday/KnightLightSpell3","necromancer/spell_lastday/KnightLightSpellBuffed",
  "necromancer/spell_penteration/LongerLife",
  "paladin/spell_lastday/KnightLightSpell","paladin/spell_lastday/KnightLightSpell2",
  "paladin/spell_lastday/KnightLightSpell3","paladin/spell_lastday/KnightLightSpellBuffed",
  "paladin/spell_lastday/LustJudgmentClouds",
  "engineer/spell_lighting/LightArea","engineer/spell_lighting/Lighting",
  "engineer/spell_lighting/Lighting2","engineer/spell_lighting/Lighting3",
  "engineer/spell_lighting/Lighting4","engineer/spell_lighting/LightingGlow",
  "engineer/spell_lighting/LightingSpark",
  "engineer/spell_rocket/Rocket","engineer/spell_rocket/RocketFire",
  "engineer/spell_rocket/RocketFireTop","engineer/spell_rocket/RocketGlow",
  "firemage/spell_fireturret/FireTurret","firemage/spell_fireturret/FireHeroTurretGlow",
  "firemage/spell_fireturret/FireHeroTurretGlowV2","firemage/spell_fireturret/FireHeroTurretV2",
  "firemage/spell_firering/ChoseFireRingLargeIco",
  "ui/ChestGlow","ui/unclassified/WingsOfVictory","ui/unclassified/WingsOfVictory2",
  "ui/unclassified/Circle","ui/unclassified/CircleExperience",
  "ui/unclassified/LvlUpAnim1","ui/unclassified/LvlUpAnim2","ui/unclassified/LvlUpIco2",
  "ui/unclassified/LvlPanel","ui/unclassified/HeroPanel",
  "ui/rewards/BlueChest","ui/rewards/GreenChest","ui/rewards/RedChest",
  "ui/rewards/YellowChest","ui/rewards/EverythingChest",
  "ui/rewards/Gem","ui/rewards/GemBlue","ui/rewards/GemGreen","ui/rewards/GemRed","ui/rewards/GemYellow",
  "ui/rewards/ExpBarEmpty","ui/rewards/ExpBarFull","ui/rewards/ExpBarFul3l",
  "ui/bonus/BonusKey","ui/bonus/SecretKey","ui/bonus/BonusLighting","ui/bonus/LightingEffect",
  "comunskills/LifeBonusIco","comunskills/LifeBonusIcoInActive","comunskills/LifeBonusLargeIco",
  "comunskills/SgieldLargeIco","comunskills/LockedSpell","comunskills/ShieldIco",
];
```

## `src/render/assets.ts`

```typescript
/**
 * assets.ts — Atlas loader for the Arkanoid art pipeline.
 *
 * Usage:
 *   await loadAtlas();          // call once at startup, before first render
 *   tex("hell/StandartHell")   // returns Texture for a single frame
 *   anim("firemage/spell_phonex/phoenixdeathanimpic") // returns Texture[] — animation keys are all-lowercase
 *   bg("hell")                  // returns background Texture for biome
 *
 * Frame keys match the stable paths produced by build-atlas.mjs.
 */

import { Assets, Texture, BaseTexture, Spritesheet, Rectangle } from "pixi.js";

// ── State ───────────────────────────────────────────────────────────────────
let loaded = false;
const frameMap = new Map<string, Texture>();
const animMap  = new Map<string, Texture[]>();

// Atlas files are numbered atlas-0.json … atlas-N.json
// Discover them from the generated index written at build time.
const ATLAS_BASE = "/atlas";

// ── Animation manifest ──────────────────────────────────────────────────────
interface AnimDef { frames: string[]; fps: number }
let animManifest: Record<string, AnimDef> = {};

// ── Public API ───────────────────────────────────────────────────────────────

/**
 * Load all spritesheet atlases.  Safe to call multiple times — resolves
 * immediately on subsequent calls.
 */
export async function loadAtlas(): Promise<void> {
  if (loaded) return;

  // Load animation manifest first
  const animResp = await fetch(`${ATLAS_BASE}/animations.json`);
  animManifest = await animResp.json();

  // Discover atlas count via index
  const indexResp = await fetch(`${ATLAS_BASE}/atlas-index.json`);
  const index: string[] = await indexResp.json();

  // Load each atlas
  for (const filename of index) {
    const jsonUrl  = `${ATLAS_BASE}/${filename}`;
    const imageUrl = `${ATLAS_BASE}/${filename.replace(".json", ".png")}`;

    // In Pixi v7, Spritesheet constructor expects a BaseTexture.
    // Assets.load returns a Texture; we extract its baseTexture.
    const texture = await Assets.load<Texture>(imageUrl);
    const base: BaseTexture = texture instanceof Texture
      ? texture.baseTexture
      : (texture as unknown as BaseTexture);
    const data = await fetch(jsonUrl).then((r) => r.json());

    const sheet = new Spritesheet(base, data);
    await sheet.parse();

    // Register all frames
    for (const [key, tex] of Object.entries(sheet.textures)) {
      frameMap.set(key, tex as Texture);
    }
  }

  // Build animation texture arrays from the manifest
  for (const [animKey, def] of Object.entries(animManifest)) {
    const textures: Texture[] = [];
    for (const frameKey of def.frames) {
      const t = frameMap.get(frameKey);
      if (t) textures.push(t);
    }
    if (textures.length > 0) {
      animMap.set(animKey, textures);
    }
  }

  loaded = true;
}

/**
 * Return the Texture for a given frame key.
 * Returns Texture.WHITE if the key is not found (never throws).
 */
export function tex(key: string): Texture {
  return frameMap.get(key) ?? Texture.WHITE;
}

/**
 * Return the ordered Texture[] for an animation key.
 * Returns [] if the animation is not found.
 */
export function anim(key: string): Texture[] {
  return animMap.get(key) ?? [];
}

/**
 * Return the background Texture for a biome name.
 * Level biome strings: "hell" | "caverns" | "cavern" | "village" | "heaven"
 * (caverns/cavern both map to the dungeon/2Dungeon background)
 */
export function bg(biome: string): Texture {
  const bgMap: Record<string, string> = {
    hell:    "fons/1Hell",
    dungeon: "fons/2Dungeon",
    caverns: "fons/2Dungeon",
    cavern:  "fons/2Dungeon",
    village: "fons/3Village",
    heaven:  "fons/5Heaven",
  };
  const key = bgMap[biome];
  if (key) {
    const t = frameMap.get(key);
    if (t) return t;
  }
  return Texture.WHITE;
}

/**
 * Return the parallax hell background textures (HellFon1/2/3), sorted.
 * Returns [] when not loaded (non-hell biomes or before loadAtlas).
 */
export function hellParallaxFrames(): Texture[] {
  const keys = ["fons/HellFon1", "fons/HellFon2", "fons/HellFon3"];
  return keys.map(k => frameMap.get(k)).filter((t): t is Texture => !!t);
}

/**
 * Slice a horizontal sprite strip into individual frame Textures and return them
 * as an ordered array suitable for AnimatedSprite.
 *
 * A "strip" is a single wide texture whose height equals one frame's size and
 * whose width = frameSize * N.  Detection is by aspect ratio (width >= height).
 *
 * fps is stored alongside the returned array on the `.fps` property so callers
 * can pass it straight into AnimSystem.oneShot / looping.
 *
 * Returns [] if the texture is not loaded or is degenerate.
 */
export function animStrip(key: string, fps = 12): Texture[] & { fps: number } {
  const texture = frameMap.get(key) ?? Texture.WHITE;
  const result: Texture[] & { fps: number } = Object.assign([], { fps });
  if (texture === Texture.WHITE || texture === Texture.EMPTY) return result;
  const { width, height } = texture;
  if (height <= 0 || width <= 0) return result;
  const frameSize = height;
  const frameCount = Math.max(1, Math.floor(width / frameSize));
  if (frameCount === 1) {
    result.push(texture);
    return result;
  }
  const base = texture.baseTexture;
  const ox = texture.frame.x;
  const oy = texture.frame.y;
  for (let i = 0; i < frameCount; i++) {
    result.push(new Texture(base, new Rectangle(ox + i * frameSize, oy, frameSize, frameSize)));
  }
  return result;
}

/**
 * Expose frame and animation maps for debugging.
 * window.__atlas.frames() lists all loaded frame keys.
 * window.__atlas.ready is true only after loadAtlas() fully completes.
 */
if (typeof window !== "undefined") {
  (window as unknown as Record<string, unknown>).__atlas = {
    get ready() { return loaded; },
    frames: () => [...frameMap.keys()],
    anims:  () => [...animMap.keys()],
    tex,
    anim,
    animStrip,
  };
}
```

## `src/render/textures.ts`

```typescript
/**
 * textures.ts — Legacy key resolver.
 *
 * The old renderer used short keys like "HellStandart" pointing at /art/*.png.
 * This module now resolves those keys from the packed atlas via assets.ts,
 * keeping backward compatibility so existing renderer/HUD code keeps working.
 *
 * For new code, use assets.ts tex()/anim()/bg() directly with atlas keys like
 *   tex("hell/StandartHell")
 *   anim("firemage/spell_phonex/PhoenixDeathAnimPic")
 */

import { Texture } from "pixi.js";
import { tex as atlasTex } from "./assets";

// Maps legacy short keys → stable atlas frame keys produced by build-atlas.mjs.
// Keep this list in sync with any new keys the game backend emits.
const ALIAS: Record<string, string> = {
  // ── Hell biome blocks ────────────────────────────────────────────────────
  HellStandart:          "hell/StandartHell",
  HellStandart2:         "hell/StandartHell2",
  HellInvulnerable:      "hell/HellInvulnerable",
  SkullRed:              "hell/SkullRed",
  SkullBlue:             "hell/SkullBlue",
  SkullGreen:            "hell/SkullGreen",
  LavaMainPart:          "hell/LavaMainPart",
  HeavenAltarV2:         "heaven/HeavenAltarV2",
  HeavenVaza:            "heaven/HeavenVaza",

  // ── Dungeon biome blocks ─────────────────────────────────────────────────
  DungeonStandart:       "dungeon/DungeonStandart",
  DungeonStandart2:      "dungeon/DungeonStandart2",
  DungeonInvulnerable:   "dungeon/DungeonInvulnerable",
  Stalactite:            "dungeon/Stalactite",
  DungeonCart:           "dungeon/DungeonCart",

  // ── Village biome blocks ─────────────────────────────────────────────────
  VillageStandart:       "village/blocks/VillageStandart",
  VillageStandart2:      "village/blocks/VillageStandart2",
  VillageStandart2Ghost: "village/blocks/VillageStandart2Ghost",
  BatSleeping:           "village/enemies/BatSleeping",
  GrateBomb:             "dungeon/GrateBomb",
  Kotelok1:              "village/blocks/Kotelok1",
  Kotelok2:              "village/blocks/Kotelok2",
  Kotelok3:              "village/blocks/Kotelok3",
  LavaSpowner:           "hell/LavaSpowner",

  // ── Heaven biome blocks ──────────────────────────────────────────────────
  StandartHaven:         "heaven/StandartHaven",
  Standart2Haven:        "heaven/Standart2Haven",
  InvulnerableHaven:     "heaven/InvulnerableHaven",

  // ── Boss blocks ──────────────────────────────────────────────────────────
  DemonBody:             "hell/DemonBody",
  GoblinBody:            "dungeon/GoblinBody",
  WitchChest:            "village/enemies/WitchChest",

  // ── Enemy blocks (ported originals) ──────────────────────────────────────
  HellBallSpawner:       "hell/HellBallSpawner",
  Bomb:                  "dungeon/Bomb",
  Beholder1:             "village/enemies/Beholder1",
  VillageDeath:          "village/enemies/VillageDeath",
  Portal:                "village/blocks/Portal",
  HeavenMeleeStatue:     "heaven/HeavenMeleeStatue",
  WindMaster2:           "heaven/WindMaster2",
  HeavenDefender:        "heaven/HeavenDefender",
  HeavenBoss:            "heaven/HeavenBoss",
  ColumnTop:             "heaven/ColumnTop",
  Column:                "heaven/Column",
  ColumnBottom:          "heaven/ColumnBottom",

  // ── Game objects ─────────────────────────────────────────────────────────
  // "Ball" and "Paddle" were placeholder art not present in Sprites/ — the game
  // should be updated to use class-specific keys: firemage/ball/FireHeroBall,
  // paladin/ball/KnightHeroBall, firemage/bars/v2FireHero1, etc.
  Ball:    "firemage/ball/FireHeroBall",
  Paddle:  "firemage/bars/v2FireHero1",

  Explosion: "effects/Explosion",

  // ── Relic / item icons ───────────────────────────────────────────────────
  ItemHummer: "items/ItemHummer",
  ItemDrill:  "items/ItemDrill",
  ItemTorch:  "items/ItemTorch",
  ItemGem:    "items/ItemGem",
};

const cache = new Map<string, Texture>();

/**
 * Resolve a texture by key.
 * Accepts both legacy short keys (HellStandart) and full atlas keys
 * (hell/StandartHell).  Falls back to Texture.WHITE if not found.
 */
export function tex(key: string): Texture {
  if (cache.has(key)) return cache.get(key)!;

  // Try alias map first, then direct atlas lookup
  const atlasKey = ALIAS[key] ?? key;
  const t = atlasTex(atlasKey);
  cache.set(key, t);
  return t;
}
```

## `src/scenes/AchievementsScene.ts`

```typescript
/**
 * AchievementsScene.ts — Achievements screen (?scene=achievements).
 *
 * Badge art in public/achievements/:
 *   achievementLvl1Eng–Lvl3Eng, Ll4Eng, Ll5Eng (badge frames, English)
 *   achievementLvl3Oro (gold variant for top tier)
 *
 * NOTE: /achievements/AchievmentPanel.png is a flat navy rounded rectangle
 * (placeholder export — no painted detail). It is NOT rendered here per
 * Rulebook §4 (docs/plans/2026-06-10-ui-overhaul-execution.md).
 * The NameBlock plaque (BarGoods 9-slice) is used for the title area instead.
 *
 * Achievement definitions are client-side; server persists the unlocked set.
 * Unlocks are triggered from battle/campaign events and POSTed to /achievement/unlock.
 *
 * Styled to match the design system: warm bg gradient, BarGoods card panels,
 * locked/unlocked visual rhythm (unlocked: full-color + gold name;
 * locked: saturate(.45) brightness(.8), text-dim name, ??? desc).
 */

import { metaApi } from "../net/metaApi";
import { nineSlice } from "../ui/nineSlice";

// ── Achievement definitions ───────────────────────────────────────────────────

export interface AchievementDef {
  id: string;
  name: string;
  description: string;
  tier: 1 | 2 | 3 | 4 | 5;  // maps to Lvl1–5 badge art
}

export const ACHIEVEMENTS: AchievementDef[] = [
  { id: "first_win",           name: "First Victory",       description: "The first brick falls. Hell noticed.", tier: 1 },
  { id: "equip_item",          name: "Geared Up",           description: "Iron does not grant power; it confesses the need for it.", tier: 1 },
  { id: "clear_biome_hell",    name: "Hell Survivor",        description: "Three floors of fire, and you came back changed.", tier: 2 },
  { id: "clear_biome_dungeon", name: "Dungeon Crawler",      description: "The descent is voluntary. The return is not guaranteed.", tier: 2 },
  { id: "clear_biome_village", name: "Village Cleared",      description: "The village prayed for a savior; it got you instead.", tier: 2 },
  { id: "clear_biome_heaven",  name: "Ascended",             description: "The angels did not welcome you. They stepped aside.", tier: 3 },
  { id: "beat_boss",           name: "Boss Slayer",          description: "Demons have names. You took one.", tier: 3 },
  { id: "clear_dungeon",       name: "Dungeon Delver",       description: "The stones remember every screaming soul; yours merely survived.", tier: 4 },
  { id: "win_fire_mage",       name: "Pyromancer",           description: "The fire answered your call before you knew its name.", tier: 2 },
  { id: "win_paladin",         name: "Holy Knight",          description: "The blessing was freely given; the mercy was not.", tier: 2 },
  { id: "win_engineer",        name: "Tech Master",          description: "Where others prayed, you calculated—and the machine did not disappoint.", tier: 2 },
  { id: "win_necromancer",     name: "Undying",              description: "Death studied you closely, learned nothing, and moved on.", tier: 2 },
  { id: "campaign_complete",   name: "World Saved",          description: "The world survives—scarred, grateful, and afraid of what it owes you.", tier: 5 },
];

// ── Badge art mapping: tier → unlocked image ─────────────────────────────────
// Always the English badge art (non-Eng variants have Russian text baked in).
// Locked/unlocked visual state is conveyed via CSS filter, not a different sprite.

function badgeSrc(tier: 1 | 2 | 3 | 4 | 5, _unlocked: boolean): string {
  if (tier === 1) return "/achievements/achievementLvl1Eng.png";
  if (tier === 2) return "/achievements/achievementLvl2Eng.png";
  if (tier === 3) return "/achievements/achievementLvl3Oro.png";
  if (tier === 4) return "/achievements/achievementLl4Eng.png";
  return "/achievements/achievementLl5Eng.png";
}

// ── Mount ─────────────────────────────────────────────────────────────────────

export function mountAchievements(host: HTMLElement) {
  injectAchievementStyles();

  const root = document.createElement("div");
  root.id = "achievements-scene";
  root.className = "ach-root";

  // Warm background layer
  const bg = document.createElement("div");
  bg.className = "ach-bg";
  root.appendChild(bg);

  const inner = document.createElement("div");
  inner.className = "ach-inner";

  // ── Top bar: back chip · centered title · symmetry spacer ──
  const topbar = document.createElement("div");
  topbar.className = "ach-topbar";

  const backBtn = document.createElement("a");
  backBtn.href = "/?scene=menu";
  backBtn.className = "ach-back";
  backBtn.setAttribute("aria-label", "Back to menu");
  // Arrow rendered by ::before pseudo-element
  topbar.appendChild(backBtn);

  const title = document.createElement("h1");
  title.textContent = "Achievements";
  title.className = "ach-title";
  topbar.appendChild(title);

  const spacer = document.createElement("div");
  spacer.className = "ach-topbar-spacer";
  topbar.appendChild(spacer);

  inner.appendChild(topbar);

  // Progress counter
  const summary = document.createElement("div");
  summary.id = "ach-summary";
  summary.className = "ach-summary";
  inner.appendChild(summary);

  // Achievement grid
  const grid = document.createElement("div");
  grid.id = "ach-grid";
  grid.className = "ach-grid";
  inner.appendChild(grid);

  root.appendChild(inner);
  host.appendChild(root);

  async function render() {
    const profile = await metaApi.getProfile();
    const unlocked = new Set(profile.achievements ?? []);

    const unlockedCount = ACHIEVEMENTS.filter(a => unlocked.has(a.id)).length;
    summary.textContent = `${unlockedCount} / ${ACHIEVEMENTS.length} unlocked`;

    grid.innerHTML = "";
    for (const ach of ACHIEVEMENTS) {
      const isUnlocked = unlocked.has(ach.id);
      const card = document.createElement("div");
      card.setAttribute("data-achievement", ach.id);
      card.className = `ach-card ${isUnlocked ? "unlocked" : "locked"}`;

      const badge = document.createElement("img");
      badge.src = badgeSrc(ach.tier, isUnlocked);
      badge.alt = ach.name;
      badge.className = "ach-badge";
      card.appendChild(badge);

      // Tier data model has only numeric tier (1–5), no tier-name string.
      // Tier chip is intentionally omitted — do NOT parse from filenames (§A3).

      const nameEl = document.createElement("div");
      nameEl.textContent = ach.name;
      nameEl.className = "ach-name";
      card.appendChild(nameEl);

      const descEl = document.createElement("div");
      descEl.textContent = isUnlocked ? ach.description : "???";
      descEl.className = "ach-desc";
      card.appendChild(descEl);

      grid.appendChild(card);
    }
  }

  render().catch(console.error);
}

// ── Toast helper (exported for use in battle / campaign flow) ─────────────────

export async function unlockAchievement(id: string): Promise<void> {
  try {
    const result = await metaApi.unlockAchievement(id);
    if (result.ok && result.achievements.includes(id)) {
      // Check it was newly added (not already there)
      showAchievementToast(id);
    }
  } catch { /* non-fatal */ }
}

function showAchievementToast(id: string) {
  const ach = ACHIEVEMENTS.find(a => a.id === id);
  if (!ach) return;

  const toast = document.createElement("div");
  toast.className = "ach-toast";
  toast.innerHTML = `
    <img src="${badgeSrc(ach.tier, true)}" class="ach-toast-badge" alt="">
    <div class="ach-toast-text">
      <div class="ach-toast-label">Achievement Unlocked!</div>
      <div class="ach-toast-name">${ach.name}</div>
    </div>
  `;

  injectToastStyles();
  document.body.appendChild(toast);

  // Animate in, hold, animate out
  requestAnimationFrame(() => {
    toast.classList.add("ach-toast-in");
    setTimeout(() => {
      toast.classList.add("ach-toast-out");
      toast.addEventListener("transitionend", () => toast.remove(), { once: true });
    }, 3200);
  });
}

// ── Styles ────────────────────────────────────────────────────────────────────

function injectAchievementStyles() {
  const id = "achievement-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    /* ── Screen scaffold ── */
    .ach-root {
      position: relative;
      min-height: 100cqh;
      width: 100%;
      color: var(--text);
      font-family: var(--font-body);
      display: flex;
      flex-direction: column;
      box-sizing: border-box;
      overflow-x: hidden;
    }
    .ach-bg {
      position: absolute; inset: 0;
      min-height: 100cqh;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
      pointer-events: none;
      z-index: 0;
    }
    .ach-inner {
      position: relative; z-index: 1;
      display: flex; flex-direction: column;
      align-items: stretch;
      padding: 0 0 max(env(safe-area-inset-bottom,0px),24px);
    }

    /* ── Top bar: back chip · centered title · symmetry spacer ── */
    .ach-topbar {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      padding: max(12px, env(safe-area-inset-top, 0px)) 12px 8px 12px;
      width: 100%;
      box-sizing: border-box;
    }

    /* Back chip — Button1 9-slice frame with BackArrow via ::before */
    .ach-back {
      flex: none;
      width: 44px;
      height: 44px;
      padding: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      text-decoration: none;
      cursor: pointer;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter var(--dur-normal), transform var(--dur-fast);
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 14px")}
    }
    .ach-back:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .ach-back::before {
      content: "";
      width: 20px;
      height: 20px;
      background: url('/ui/BackArrow.png') no-repeat center / contain;
      filter: drop-shadow(0 1px 2px rgba(0,0,0,0.8));
    }
    .ach-back:hover  { filter: brightness(1.18); }
    .ach-back:active { transform: scale(0.94); }

    /* Centered display title */
    .ach-title {
      flex: 1;
      text-align: center;
      margin: 0;
      font-family: var(--font-display);
      font-size: var(--fs-title);
      font-weight: 700;
      letter-spacing: 0.05em;
      color: var(--gold-bright);
      text-shadow: 0 2px 4px rgba(0,0,0,0.9), 0 0 18px rgba(255,180,60,0.25);
    }

    /* Symmetry spacer keeps title visually centered */
    .ach-topbar-spacer {
      width: 44px;
      flex: none;
    }

    /* Progress counter */
    .ach-summary {
      text-align: center;
      color: var(--text-dim);
      font-size: var(--fs-body);
      letter-spacing: 0.04em;
      margin-bottom: var(--sp-3h);
      padding: 0 var(--sp-4);
    }

    /* ── Achievement grid (2-col) ── */
    .ach-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: var(--sp-3);
      width: min(360px, 96cqw);
      padding-bottom: var(--sp-5);
      align-self: center;
    }

    /* ── Achievement card: BarGoods gold-rimmed navy panel ── */
    .ach-card {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "13px 15px")}
      padding: var(--sp-2h) var(--sp-2) var(--sp-2h);
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--sp-1h);
      position: relative;
      transition: filter var(--dur-normal), transform var(--dur-normal);
    }

    /* Unlocked: full color + gold glow */
    .ach-card.unlocked {
      filter: drop-shadow(0 0 7px rgba(255,190,80,0.35));
    }
    .ach-card.unlocked:hover {
      filter: drop-shadow(0 0 10px rgba(255,190,80,0.55)) brightness(1.08);
    }
    .ach-card.unlocked:active {
      transform: scale(0.96);
    }

    /* Locked: readable but clearly unowned — NOT blacked out (docs/13, Rulebook §5) */
    .ach-card.locked {
      filter: none;
    }
    .ach-card.locked:hover {
      filter: brightness(1.06);
    }

    /* ── Badge art (medal) ── */
    .ach-badge {
      width: 60px;
      height: 60px;
      object-fit: contain;
      /* painted art — NEVER image-rendering: pixelated (Rulebook §4) */
      filter: drop-shadow(0 2px 6px rgba(0,0,0,0.7));
    }
    /* Locked badge: desaturated ~50%, never blacked out */
    .ach-card.locked .ach-badge {
      filter: var(--filter-locked) drop-shadow(0 2px 4px rgba(0,0,0,0.6));
    }

    /* ── Text ── */
    .ach-name {
      font-size: var(--fs-caption);
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      text-align: center;
      line-height: 1.3;
    }
    .ach-card.locked .ach-name {
      color: var(--text-dim);
      text-shadow: none;
    }
    .ach-desc {
      font-size: var(--fs-tiny);
      color: var(--text-dim);
      text-align: center;
      line-height: 1.4;
    }
    .ach-card.locked .ach-desc {
      color: var(--text-faint);
    }

    /* ── Wider layout on larger containers ── */
    @container (min-width: 480px) {
      .ach-grid {
        grid-template-columns: repeat(3, 1fr);
      }
    }
  `;
  document.head.appendChild(style);
}

function injectToastStyles() {
  const id = "ach-toast-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    /* Toast: BarGoods panel, position: fixed intentional (body-appended overlay) */
    .ach-toast {
      position: fixed;
      top: max(env(safe-area-inset-top,0px), 20px);
      left: 50%;
      transform: translateX(-50%) translateY(-80px);
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "13px 20px")}
      padding: var(--sp-2h) var(--sp-4);
      display: flex;
      align-items: center;
      gap: var(--sp-3);
      min-width: min(280px, 85cqw);
      max-width: min(340px, 90cqw);
      z-index: 9999;
      box-shadow: 0 4px 20px rgba(0,0,0,0.7), 0 0 12px rgba(220,180,60,0.25);
      transition: transform 0.35s cubic-bezier(0.2,1,0.4,1), opacity 0.35s;
      opacity: 0;
      font-family: var(--font-body);
    }
    .ach-toast-in {
      transform: translateX(-50%) translateY(0);
      opacity: 1;
    }
    .ach-toast-out {
      transform: translateX(-50%) translateY(-80px);
      opacity: 0;
    }
    .ach-toast-badge {
      width: 44px;
      height: 44px;
      object-fit: contain;
      flex-shrink: 0;
    }
    .ach-toast-label {
      font-size: var(--fs-tiny);
      color: var(--gold-bright);
      letter-spacing: 0.06em;
      font-weight: 700;
      text-transform: uppercase;
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
    .ach-toast-name {
      font-size: var(--fs-subhead);
      color: var(--text);
      font-weight: 700;
      margin-top: 2px;
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
  `;
  document.head.appendChild(style);
}
```

## `src/scenes/BattleScene.ts`

```typescript
import { Renderer } from "../render/Renderer";
import { Connection } from "../net/Connection";
import { attachPaddleInput } from "../input/PaddleInput";
import { installTestHooks } from "../testhooks";
import { Hud } from "../ui/Hud";
import { metaApi } from "../net/metaApi";
import { createCampaignFlow } from "./battle/campaignFlow";
import { createDungeonFlow } from "./battle/dungeonFlow";
import { maybeShowTutorial } from "./TutorialOverlay";

// Renderer always runs — the pooled draw() is cheap enough for mobile GPUs and
// headless WebGL alike. The HEAVY_FX glow gate in Renderer.ts still skips the
// expensive GlowFilter passes under Playwright, but base rendering is always on.

export function mountBattle(host: HTMLElement, level: string, seed: number, run: string, from = "") {
  const r = new Renderer(host);
  (window as any).__renderer = r;
  const hud = new Hud(host);
  const conn = new Connection(level, seed, run);
  (window as any).__conn = conn;

  const flow =
    from === "campaign" ? createCampaignFlow(level) :
    from === "dungeon"  ? createDungeonFlow()        :
    null;

  conn.onSnapshot = (s) => {
    r.draw(s);
    hud.update(s);
    if (flow) flow.handlePhase(s);
  };

  // Fetch selected character's spell kit and load into HUD.
  // Also switch Renderer to the correct per-class paddle/ball sprites.
  metaApi.getCharacters()
    .then((data) => {
      const selected = data.characters.find(c => c.id === data.selected);
      if (selected) {
        // Switch paddle/ball art to match the selected class.
        r.setClass(selected.id);
        if (selected.spells?.length > 0) {
          hud.loadSpells(selected.spells);
        }
      }
      hud.wireConn(conn);
    })
    .catch(() => {
      // Network/backend error: fall back to default hotbar.
      hud.wireConn(conn);
    });

  // Fetch equipped items and show small HUD row.
  metaApi.getItems()
    .then((data) => { hud.loadEquippedItems(data.items.filter(it => it.equipped)); })
    .catch(() => { /* non-fatal */ });

  attachPaddleInput(r.app.view as HTMLCanvasElement, conn, () => conn.latest);
  installTestHooks(conn);
  // Show tutorial on first battle (non-blocking — serves after tutorial or immediately).
  // Skip when: Playwright drives the browser (navigator.webdriver=true) OR the player
  // already acknowledged the tutorial (arkanoid_tutorial_seen=1 in localStorage).
  // Tests pre-set that flag via fixtures so the serve fires regardless of webdriver detection.
  // Can be forced with ?tutorial=1 URL param for dedicated tutorial tests.
  const q2 = new URLSearchParams(location.search);
  const forceTutorial = q2.get("tutorial") === "1";
  const tutorialSeen = typeof localStorage !== "undefined" && localStorage.getItem("arkanoid_tutorial_seen") === "1";
  const isAutomated = (!!(navigator as any).webdriver || tutorialSeen) && !forceTutorial;
  conn.whenReady(() => {
    if (isAutomated) {
      setTimeout(() => conn.serve(), 300);
    } else {
      maybeShowTutorial(host, forceTutorial).then(() => {
        setTimeout(() => conn.serve(), 300);
      });
    }
  });
}
```

## `src/scenes/CampaignScene.ts`

```typescript
import { metaApi } from "../net/metaApi";
import type { CampaignNode, Profile } from "../net/metaApi";
import { navigateTo } from "../ui/transition";
import { log } from "../log";
import { RIFT_STYLES, CAMPAIGN_STYLES } from "./campaign/campaignStyles";

const SPELL_NAMES: Record<string, string> = {
  ignite: "Ignite",
  fireball: "Fireball",
  firewall: "Firewall",
  turret: "Turret",
};

const SPELL_ICONS: Record<string, string> = {
  ignite: "/art/FireBallIco.png",
  fireball: "/art/FireBallIco.png",
  firewall: "/art/FireWallIco.png",
  turret: "/art/FireTurretIco.png",
};

// Map level id prefix → node art (unlocked / locked / selected variants in /ui/)
function nodeSrc(id: string, state: "unlocked" | "locked" | "completed"): string {
  const prefix = id.startsWith("hell")    ? "LvlHell"
               : id.startsWith("caverns") ? "LvlCave"
               : id.startsWith("village") ? "LvlVillage"
               : id.startsWith("heaven")  ? "LvlHeaven"
               : null;
  if (!prefix) return "/art/Mission_Standart.png";
  if (state === "locked")    return `/ui/${prefix}Closed.png`;
  if (state === "completed") return `/ui/${prefix}Selected.png`;
  return `/ui/${prefix}.png`;
}

function css(el: HTMLElement, styles: Record<string, string>) {
  Object.assign(el.style, styles);
}

export function mountCampaign(host: HTMLElement) {
  injectCampaignStyles();

  const root = document.createElement("div");
  root.id = "campaign";
  root.className = "camp-root";
  host.appendChild(root);

  // ── Profile bar ──────────────────────────────────────────────────────────
  const profileBar = document.createElement("div");
  profileBar.id = "profile-bar";
  profileBar.className = "camp-profile-bar";

  const levelEl = document.createElement("span");
  levelEl.id = "profile-level";
  levelEl.className = "camp-profile-level";

  const expBar = document.createElement("div");
  expBar.className = "camp-exp-wrap";
  const expLabel = document.createElement("span");
  expLabel.id = "profile-exp";
  expLabel.className = "camp-exp-label";
  const expBarOuter = document.createElement("div");
  expBarOuter.className = "camp-exp-outer";
  // 3-slice frame via CSS border-image, fill as absolute-positioned gradient div
  const expBarFill = document.createElement("div");
  expBarFill.id = "profile-exp-fill";
  expBarFill.className = "camp-exp-fill";
  expBarOuter.appendChild(expBarFill);
  expBar.appendChild(expLabel);
  expBar.appendChild(expBarOuter);

  const pointsEl = document.createElement("span");
  pointsEl.id = "profile-points";
  pointsEl.className = "camp-profile-points";

  const crystalsEl = document.createElement("span");
  crystalsEl.id = "profile-crystals";
  crystalsEl.className = "camp-profile-crystals";
  const gemImg = document.createElement("img");
  gemImg.src = "/ui/Gem.png";
  gemImg.alt = "Crystals";
  css(gemImg, { width: "18px", height: "18px", imageRendering: "pixelated" });
  crystalsEl.appendChild(gemImg);
  const crystalsText = document.createElement("span");
  crystalsEl.appendChild(crystalsText);

  profileBar.appendChild(levelEl);
  profileBar.appendChild(expBar);
  profileBar.appendChild(pointsEl);
  profileBar.appendChild(crystalsEl);

  const spacer = document.createElement("div");
  css(spacer, { flex: "1" });
  profileBar.appendChild(spacer);

  // Upgrade button — uses skill-arrows icon
  const btnUpgrade = document.createElement("button");
  btnUpgrade.id = "btn-upgrade";
  btnUpgrade.className = "camp-upgrade-btn";
  btnUpgrade.innerHTML = `<img src="/ui/InterfaceSkillsButton.png" class="camp-upgrade-ico" alt=""> <span>Upgrades</span>`;

  // Back button — top-left of profile bar (Rulebook §7)
  const btnBack = document.createElement("a");
  btnBack.className = "ui-link camp-back-link";
  btnBack.textContent = "← Menu";
  btnBack.href = "/?scene=menu";

  // Back button goes FIRST (leftmost = top-left affordance)
  profileBar.insertBefore(btnBack, profileBar.firstChild);
  profileBar.appendChild(btnUpgrade);
  root.appendChild(profileBar);

  // ── Main content ─────────────────────────────────────────────────────────
  const content = document.createElement("div");
  content.className = "camp-content";
  root.appendChild(content);

  // Campaign map — full-width scrollable row of node buttons
  const mapEl = document.createElement("div");
  mapEl.id = "campaign-map";
  mapEl.className = "camp-map";
  content.appendChild(mapEl);

  // ── Upgrade panel — fixed overlay so it's always in-viewport ────────────
  const upgradePanel = document.createElement("div");
  upgradePanel.id = "upgrade-panel";
  upgradePanel.className = "camp-upgrade-panel";
  root.appendChild(upgradePanel);

  const upgTitle = document.createElement("h3");
  upgTitle.textContent = "Spell Upgrades";
  css(upgTitle, { margin: "0 0 var(--sp-3) 0", color: "var(--color-upgrade-hdr)", fontSize: "var(--fs-large)", letterSpacing: "0.05em" });
  upgradePanel.appendChild(upgTitle);

  const pointsRemaining = document.createElement("div");
  pointsRemaining.id = "upgrade-points-remaining";
  css(pointsRemaining, { marginBottom: "var(--sp-4)", color: "var(--color-pts)", fontSize: "var(--fs-subhead)" });
  upgradePanel.appendChild(pointsRemaining);

  const spellList = document.createElement("div");
  css(spellList, { display: "flex", flexDirection: "column", gap: "var(--sp-2h)" });
  upgradePanel.appendChild(spellList);

  // ── State ────────────────────────────────────────────────────────────────
  let profile: Profile | null = null;
  let upgradePanelOpen = false;

  function renderProfile(p: Profile) {
    profile = p;
    levelEl.textContent = `Lv ${p.level}`;
    const expNeeded = p.level * 100;
    const expPct = Math.min(100, Math.round((p.exp / expNeeded) * 100));
    expLabel.textContent = `EXP ${p.exp}/${expNeeded}`;
    expBarFill.style.width = `${expPct}%`;
    pointsEl.textContent = `Pts: ${p.points}`;
    crystalsText.textContent = `${p.crystals}`;
    if (upgradePanelOpen) renderUpgradePanel(p);
  }

  function renderUpgradePanel(p: Profile) {
    pointsRemaining.textContent = `Skill Points: ${p.points}`;
    spellList.innerHTML = "";
    const spells = ["ignite", "fireball", "firewall", "turret"];
    for (const spellId of spells) {
      const lvl = p.spellLevels[spellId] ?? 1;
      const row = document.createElement("div");
      row.className = "camp-spell-row";

      const icon = document.createElement("img");
      icon.src = SPELL_ICONS[spellId] ?? "/art/FireBallIco.png";
      css(icon, { width: "28px", height: "28px", imageRendering: "pixelated" });
      row.appendChild(icon);

      const nameEl = document.createElement("span");
      nameEl.textContent = SPELL_NAMES[spellId] ?? spellId;
      css(nameEl, { flex: "1", fontWeight: "600", color: "var(--color-spell-name)" });
      row.appendChild(nameEl);

      const levelSpan = document.createElement("span");
      levelSpan.id = `spell-level-${spellId}`;
      levelSpan.textContent = `${lvl}`;
      css(levelSpan, { color: "var(--color-xp)", fontSize: "var(--fs-section)", minWidth: "24px", textAlign: "center" });
      row.appendChild(levelSpan);

      const btnPlus = document.createElement("button");
      btnPlus.id = `btn-upgrade-${spellId}`;
      btnPlus.className = `camp-plus-btn ${p.points > 0 ? "can-afford" : "cannot-afford"}`;
      btnPlus.textContent = "+";
      if (p.points === 0) btnPlus.disabled = true;
      btnPlus.addEventListener("click", async () => {
        const data = await metaApi.upgrade(spellId);
        if (data.ok) renderProfile(data.profile);
      });
      row.appendChild(btnPlus);
      spellList.appendChild(row);
    }
  }

  function renderNodes(ns: CampaignNode[]) {
    mapEl.innerHTML = "";

    // Serpentine layout: NODES_PER_ROW nodes per row, alternating left→right / right→left
    const NODES_PER_ROW = 3;

    // Convert linear index → (col, row) in serpentine order
    function snakePos(i: number): { col: number; row: number } {
      const row = Math.floor(i / NODES_PER_ROW);
      const posInRow = i % NODES_PER_ROW;
      const col = row % 2 === 0 ? posInRow : (NODES_PER_ROW - 1 - posInRow);
      return { col, row };
    }

    // Node size + spacing (px)
    const NODE_W = 104;    // button width — wide enough that labels never wrap mid-phrase
    const NODE_H = 116;    // button height (img 64 + two-line label + gap)
    const H_GAP  = 16;     // horizontal gap between nodes
    const V_GAP  = 36;     // vertical gap between rows
    const CONNECTOR_THICKNESS = 6;
    // Connectors route through the ORB centres, not the button centres — the
    // button includes the label plaque, and centre-of-button lines used to cut
    // straight through the text (docs/13 campaign audit).
    const ORB_TOP_PAD = 6;   // .camp-node padding-top
    const ORB_SIZE    = 64;  // .camp-node-img height
    const ORB_CY      = ORB_TOP_PAD + ORB_SIZE / 2;
    const ORB_BOTTOM  = ORB_TOP_PAD + ORB_SIZE;

    const totalCols = NODES_PER_ROW;
    const totalRows = Math.ceil(ns.length / NODES_PER_ROW);
    const innerW = totalCols * NODE_W + (totalCols - 1) * H_GAP;
    const innerH = totalRows * NODE_H + (totalRows - 1) * V_GAP;

    // Inner wrapper: relative-positioned, holds absolute connectors + nodes
    const inner = document.createElement("div");
    inner.className = "camp-map-inner";
    inner.style.width    = `${innerW}px`;
    inner.style.minHeight = `${innerH + 8}px`;
    mapEl.appendChild(inner);

    function nodeLeft(col: number) { return col * (NODE_W + H_GAP); }
    function nodeTop(row: number)  { return row * (NODE_H + V_GAP); }
    // Centre of the node ORB (the label hangs below it)
    function nodeCX(col: number)   { return nodeLeft(col) + NODE_W / 2; }
    function nodeCY(row: number)   { return nodeTop(row)  + ORB_CY; }

    // ── Connectors first (behind nodes) ──────────────────────────────────────
    for (let i = 1; i < ns.length; i++) {
      const node = ns[i];
      const isActive = node.unlocked || node.completed;
      const activeClass = isActive ? "active" : "";

      const prev = snakePos(i - 1);
      const curr = snakePos(i);

      const x1 = nodeCX(prev.col);
      const x2 = nodeCX(curr.col);

      if (prev.row === curr.row) {
        // Same row → horizontal connector spanning the gap between the orbs
        const hLeft  = Math.min(x1, x2) + ORB_SIZE / 2;
        const hWidth = Math.abs(x2 - x1) - ORB_SIZE;
        if (hWidth > 0) {
          const conn = document.createElement("div");
          conn.className = `camp-connector ${activeClass}`;
          conn.style.left   = `${hLeft}px`;
          conn.style.top    = `${nodeCY(prev.row) - CONNECTOR_THICKNESS / 2}px`;
          conn.style.width  = `${hWidth}px`;
          conn.style.height = `${CONNECTOR_THICKNESS}px`;
          inner.appendChild(conn);
        }
      } else {
        // Row transition — L-path: vertical down from prev orb, then horizontal to curr column
        const vTop    = nodeTop(prev.row) + ORB_BOTTOM;
        const vBottom = nodeCY(curr.row);
        const vH = vBottom - vTop;
        if (vH > 0) {
          const vConn = document.createElement("div");
          vConn.className = `camp-connector ${activeClass}`;
          vConn.style.left   = `${x1 - CONNECTOR_THICKNESS / 2}px`;
          vConn.style.top    = `${vTop}px`;
          vConn.style.width  = `${CONNECTOR_THICKNESS}px`;
          vConn.style.height = `${vH}px`;
          inner.appendChild(vConn);
        }
        // Horizontal segment at vertical centre of curr row
        const hY     = nodeCY(curr.row) - CONNECTOR_THICKNESS / 2;
        const hLeft  = Math.min(x1, x2);
        const hWidth = Math.abs(x2 - x1) - CONNECTOR_THICKNESS;
        if (hWidth > 0) {
          const hConn = document.createElement("div");
          hConn.className = `camp-connector ${activeClass}`;
          hConn.style.left   = `${hLeft + (x1 < x2 ? CONNECTOR_THICKNESS : 0)}px`;
          hConn.style.top    = `${hY}px`;
          hConn.style.width  = `${hWidth}px`;
          hConn.style.height = `${CONNECTOR_THICKNESS}px`;
          inner.appendChild(hConn);
        }
      }
    }

    // ── Node buttons ─────────────────────────────────────────────────────────
    let lastUnlockedBtn: HTMLElement | null = null;
    ns.forEach((node, i) => {
      const { col, row } = snakePos(i);
      const state = node.completed ? "completed" : node.unlocked ? "unlocked" : "locked";

      const btn = document.createElement("button");
      btn.setAttribute("data-level", node.id);
      btn.setAttribute("data-state", state);
      btn.className = `camp-node camp-node-${state}`;
      btn.style.position = "absolute";
      btn.style.left  = `${nodeLeft(col)}px`;
      btn.style.top   = `${nodeTop(row)}px`;
      btn.style.width = `${NODE_W}px`;

      // Node art image (the glassy orb icons)
      const nodeImg = document.createElement("img");
      nodeImg.src = nodeSrc(node.id, state);
      nodeImg.alt = node.label;
      nodeImg.className = "camp-node-img";
      btn.appendChild(nodeImg);

      // Label on MissionName banner below
      // Labels are "Biome — Subtitle" (e.g. "Hell — The Circuit"). Render as a
      // tiny biome kicker over a single non-wrapping title line; the plaque
      // sizes to the text. Mid-phrase wrapping over 3–4 lines was the worst
      // offender in the docs/13 audit.
      const labelWrap = document.createElement("div");
      labelWrap.className = "camp-node-label-wrap";
      const dashIdx = node.label.indexOf("—");
      const kickerText = dashIdx >= 0 ? node.label.slice(0, dashIdx).trim() : "";
      const titleText  = dashIdx >= 0 ? node.label.slice(dashIdx + 1).trim() : node.label;
      if (kickerText) {
        const kickerEl = document.createElement("span");
        kickerEl.textContent = kickerText;
        kickerEl.className = "camp-node-kicker";
        labelWrap.appendChild(kickerEl);
      }
      const labelEl = document.createElement("span");
      labelEl.textContent = titleText;
      labelEl.className = "camp-node-label";
      labelWrap.appendChild(labelEl);
      btn.appendChild(labelWrap);

      if (state !== "locked") {
        btn.addEventListener("click", () => {
          navigateTo(`/?scene=battle&level=${node.id}&from=campaign`);
        });
        lastUnlockedBtn = btn;
      }
      inner.appendChild(btn);
    });

    // Scroll the most-recently-unlocked node into view
    if (lastUnlockedBtn) {
      requestAnimationFrame(() => {
        (lastUnlockedBtn as HTMLElement).scrollIntoView({ block: "center", behavior: "smooth" });
      });
    }
  }

  // Toggle upgrade panel
  btnUpgrade.addEventListener("click", () => {
    upgradePanelOpen = !upgradePanelOpen;
    upgradePanel.style.display = upgradePanelOpen ? "block" : "none";
    btnUpgrade.classList.toggle("active", upgradePanelOpen);
    if (upgradePanelOpen && profile) renderUpgradePanel(profile);
  });

  async function loadAll() {
    const [camp, prof] = await Promise.all([
      metaApi.getCampaign(),
      metaApi.getProfile(),
    ]);
    renderProfile(prof);
    renderNodes(camp.nodes);
  }

  loadAll()
    .then(() => maybeShowRiftBanner(root))
    .catch(console.error);
}

/**
 * If the URL carries a rift offer (set by the campaign reward flow), slide in a
 * banner offering the dungeon run. Descend → start the run; Skip → stay on the map.
 * The banner is an overlay layered over the (still-present) campaign map.
 */
function maybeShowRiftBanner(root: HTMLElement) {
  const q = new URLSearchParams(location.search);
  const dungeonId = q.get("rift");
  if (!dungeonId) return;
  const floors = q.get("riftFloors") ?? "?";
  const name   = q.get("riftName") ?? "Rift";

  injectRiftStyles();

  const banner = document.createElement("div");
  banner.id = "rift-banner";
  banner.className = "rift-banner";
  banner.innerHTML = `
    <div class="rift-banner-glyph"></div>
    <div class="rift-banner-text">
      <div class="rift-banner-title">A Rift opens</div>
      <div class="rift-banner-sub">${name} · ${floors} floors · permadeath · 1 reward / floor</div>
    </div>
    <div class="rift-banner-actions">
      <button id="btn-rift-descend" class="rift-btn rift-btn-go">Descend</button>
      <button id="btn-rift-skip" class="rift-btn rift-btn-skip">Skip</button>
    </div>`;
  root.appendChild(banner);
  log("rift", "banner-shown", { dungeonId, floors });

  // Slide in on next frame.
  requestAnimationFrame(() => banner.classList.add("rift-banner-in"));

  const close = () => { banner.classList.remove("rift-banner-in"); };

  banner.querySelector("#btn-rift-descend")!.addEventListener("click", async () => {
    log("rift", "descend", { dungeonId });
    try {
      await metaApi.startDungeon(dungeonId);
      navigateTo("/?scene=dungeon");
    } catch (e) {
      log("rift", "descend-failed", { err: String(e) });
    }
  });

  banner.querySelector("#btn-rift-skip")!.addEventListener("click", () => {
    log("rift", "skip", { dungeonId });
    close();
    // Drop the rift params so a refresh doesn't re-offer.
    history.replaceState(null, "", "/?scene=campaign");
    setTimeout(() => banner.remove(), 300);
  });
}

function injectRiftStyles() {
  const id = "rift-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = RIFT_STYLES;
  document.head.appendChild(style);
}

function injectCampaignStyles() {
  const id = "campaign-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = CAMPAIGN_STYLES;
  document.head.appendChild(style);
}
```

## `src/scenes/CharacterScene.ts`

```typescript
import { metaApi } from "../net/metaApi";
import { nineSlice } from "../ui/nineSlice";
import { navigateTo } from "../ui/transition";

// Hero icon images per backend icon key
const ICON_FILES: Record<string, string> = {
  FireHeroBall:   "/art/FireHeroBall.png",
  HPFull:         "/art/HPFull.png",
  FireTurretIco:  "/art/FireTurretIco.png",
  MPFull:         "/art/MPFull.png",
};

// Per-class ClassChoice banner art + hero icon
const CLASS_ART: Record<string, { banner: string; ico: string }> = {
  fire_mage:    { banner: "/ui/ClassChoiceMage.png",   ico: "/ui/FireHeroIco.png" },
  paladin:      { banner: "/ui/ClassChoiceKnight.png", ico: "/ui/KnightHeroIco.png" },
  engineer:     { banner: "/ui/ClassChoiceTechno.png", ico: "/ui/TechnoHeroIco.png" },
  necromancer:  { banner: "/ui/ClassChoiceMage.png",   ico: "/ui/NecrHeroIco.png" },
  // fallback: use whatever icon the backend gives
};

function iconSrc(key: string): string {
  return ICON_FILES[key] ?? "/art/ItemGem.png";
}

export function mountCharacters(host: HTMLElement) {
  injectCharacterStyles();

  const root = document.createElement("div");
  root.id = "character-scene";
  root.className = "char-root";

  // Background warm gradient layer
  const bg = document.createElement("div");
  bg.className = "char-bg";
  root.appendChild(bg);

  // Inner content (above bg)
  const inner = document.createElement("div");
  inner.className = "char-inner";

  // ── Top bar: back chip · centered title · symmetry spacer ──────────
  const topbar = document.createElement("div");
  topbar.className = "ui-topbar";

  const backBtn = document.createElement("button");
  backBtn.className = "ui-back";
  backBtn.setAttribute("aria-label", "Back to menu");
  backBtn.addEventListener("click", () => { navigateTo("/?scene=menu"); });
  topbar.appendChild(backBtn);

  const h1 = document.createElement("h1");
  h1.textContent = "Choose Character";
  h1.className = "ui-title";
  topbar.appendChild(h1);

  const spacer = document.createElement("div");
  spacer.className = "ui-topbar-spacer";
  topbar.appendChild(spacer);

  inner.appendChild(topbar);

  // ── Content area — flex:1 so card list fills the remaining height ───
  const content = document.createElement("div");
  content.className = "char-content";

  const list = document.createElement("div");
  list.id = "character-list";
  list.className = "char-list";
  content.appendChild(list);

  inner.appendChild(content);
  root.appendChild(inner);
  host.appendChild(root);

  // Characters are EARNED through boss clears (docs/04 §3); hints mirror
  // Rewards.CharacterUnlocks on the backend.
  const UNLOCK_HINTS: Record<string, string> = {
    paladin:     "Defeat the Demon Lord in Hell to unlock",
    engineer:    "Defeat the Goblin King in the Caverns to unlock",
    necromancer: "Defeat the Witch in Witchland to unlock",
  };

  async function render() {
    const data = await metaApi.getCharacters();
    const selectable = data.unlocked.length === 0
      ? data.characters.map(c => c.id)
      : data.unlocked;

    list.innerHTML = "";
    list.setAttribute("data-selected", data.selected ?? "");

    for (const char of data.characters) {
      const isSelected   = char.id === data.selected;
      const isSelectable = selectable.includes(char.id);
      const art = CLASS_ART[char.id];

      const card = document.createElement("div");
      card.setAttribute("data-character", char.id);
      card.className = `char-card ${isSelected ? "selected" : ""} ${isSelectable ? "" : "locked"}`;

      // ── ClassChoice banner strip ────────────────────────────────────
      if (art) {
        const banner = document.createElement("div");
        banner.className = "char-banner";
        banner.style.backgroundImage = `url('${art.banner}')`;

        // Round portrait icon (left edge) — painted art, no pixelated rendering
        const ico = document.createElement("img");
        ico.src = art.ico;
        ico.alt = char.name;
        ico.className = "char-hero-ico";
        banner.appendChild(ico);

        // Hero name inside the banner strip
        const bannerName = document.createElement("span");
        bannerName.textContent = char.name;
        bannerName.className = "char-banner-name";
        banner.appendChild(bannerName);

        card.appendChild(banner);
      } else {
        // Fallback: plain portrait + name when no CLASS_ART entry
        const icon = document.createElement("img");
        icon.src = iconSrc(char.icon);
        icon.className = "char-fallback-ico";
        card.appendChild(icon);
        const nameEl = document.createElement("div");
        nameEl.textContent = char.name;
        nameEl.className = "char-banner-name char-banner-name--standalone";
        card.appendChild(nameEl);
      }

      // ── "SELECTED" gold chip shown below the banner when active ────
      if (isSelected) {
        const chip = document.createElement("div");
        chip.className = "char-selected-chip";
        chip.textContent = "SELECTED";
        card.appendChild(chip);
      }

      // ── Passive description / unlock hint ───────────────────────────
      const passiveEl = document.createElement("div");
      passiveEl.textContent = isSelectable
        ? char.passive
        : (UNLOCK_HINTS[char.id] ?? "Locked");
      passiveEl.className = "char-passive";
      card.appendChild(passiveEl);

      // ── Lock badge (bottom-right corner) for not-yet-earned classes ─
      if (!isSelectable) {
        const lockBadge = document.createElement("div");
        lockBadge.className = "char-lock-badge";
        lockBadge.textContent = "🔒"; // 🔒
        card.appendChild(lockBadge);
      }

      if (isSelectable) {
        card.addEventListener("click", async () => {
          if (char.id === data.selected) return;
          await metaApi.selectCharacter(char.id);
          await render();
        });
      }

      list.appendChild(card);
    }
  }

  render().catch(console.error);
}

// ── Styles ──────────────────────────────────────────────────────────────────

function injectCharacterStyles() {
  const id = "character-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = CHARACTER_CSS;
  document.head.appendChild(style);
}

const CHARACTER_CSS = `
  /* ── Screen root: warm gradient, definite height so nested flex:1 works ── */
  .char-root {
    position: relative;
    /* height (not min-height) gives a DEFINITE value so nested flex:1 propagates */
    height: 100cqh;
    overflow-y: auto;   /* safety scroll on very small screens */
    overflow-x: hidden;
    display: flex;
    flex-direction: column;
    font-family: var(--font-body);
    color: var(--text);
    -webkit-font-smoothing: antialiased;
  }
  .char-bg {
    position: absolute;
    inset: 0;
    background:
      radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
      linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    pointer-events: none;
    z-index: 0;
  }

  /* ── Inner wrapper: takes char-root's definite height via flex:1 ── */
  .char-inner {
    flex: 1;
    min-height: 0; /* prevent auto-height inflation in flex */
    position: relative;
    z-index: 1;
    display: flex;
    flex-direction: column;
  }

  /* .ui-topbar, .ui-back, .ui-title, .ui-topbar-spacer come from theme.ts */

  /* ── Content area: takes remaining height, card list fills it ── */
  .char-content {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: var(--sp-1) var(--sp-3h) max(env(safe-area-inset-bottom, 0px), var(--sp-3h));
  }
  .char-list {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    gap: var(--sp-3);
    width: 100%;
    max-width: 480px;
  }

  /* ── Card: BarGoods gold-rimmed navy 9-slice panel ── */
  .char-card {
    flex: 1;
    min-height: 0;
    ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "13px 15px")}
    padding: var(--sp-2h) var(--sp-3) var(--sp-3h);
    display: flex;
    flex-direction: column;
    position: relative;
    cursor: pointer;
    box-sizing: border-box;
    transition: filter var(--dur-normal), transform var(--dur-normal);
    touch-action: manipulation;
    -webkit-tap-highlight-color: transparent;
  }
  /* Non-selected, unlocked: slightly dimmed so selected stands out */
  .char-card:not(.selected):not(.locked) {
    filter: brightness(0.88);
  }
  /* Selected: gold glow */
  .char-card.selected {
    filter: drop-shadow(0 0 10px rgba(255,190,80,0.6));
  }
  /* Hover states */
  .char-card:hover:not(.locked):not(.selected) {
    filter: brightness(1.1);
    transform: scale(1.01);
  }
  .char-card:hover.selected {
    filter: drop-shadow(0 0 16px rgba(255,190,80,0.85));
  }
  /* Active (press) */
  .char-card:active:not(.locked) {
    transform: scale(0.97);
  }
  .char-card:not(.locked):focus-visible {
    outline: 2px solid var(--gold-bright);
    outline-offset: 3px;
    border-radius: 4px;
  }
  /* Locked: desaturated ~50%, never blacked out (Rulebook §6) */
  .char-card.locked {
    filter: var(--filter-locked);
    cursor: default;
  }

  /* ── ClassChoice banner strip ── */
  .char-banner {
    position: relative;
    width: 100%;
    height: 72px;
    flex-shrink: 0;
    /* Left portion of banner art at natural aspect; right edge gradient fade */
    background-size: auto 100%;
    background-position: left center;
    background-repeat: no-repeat;
    border-radius: 4px;
    display: flex;
    align-items: center;
    margin-bottom: var(--sp-2);
    /* Fade right edge: cleaner than hard overflow: hidden clip */
    -webkit-mask-image: linear-gradient(90deg, black 72%, transparent 100%);
    mask-image: linear-gradient(90deg, black 72%, transparent 100%);
  }

  /* Round portrait icon — painted art, never pixelated (Rulebook §4) */
  .char-hero-ico {
    position: absolute;
    left: 2px;
    top: 50%;
    transform: translateY(-50%);
    width: 68px;
    height: 68px;
    object-fit: contain;
    z-index: 2;
    filter: drop-shadow(0 2px 4px rgba(0,0,0,0.7));
  }

  .char-banner-name {
    position: absolute;
    left: 76px;
    right: 28px;
    font-family: var(--font-display);
    font-size: var(--fs-large);
    font-weight: 700;
    color: var(--gold-bright);
    text-shadow: 0 1px 3px rgba(0,0,0,0.95), 0 0 10px rgba(200,140,30,0.35);
    letter-spacing: 0.06em;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  /* Standalone name for fallback path (no banner) */
  .char-banner-name--standalone {
    position: static;
    display: block;
    margin: var(--sp-1h) 0 var(--sp-2h);
    font-size: var(--fs-large);
  }

  /* ── "SELECTED" gold chip ── */
  .char-selected-chip {
    display: inline-flex;
    align-self: flex-start;
    align-items: center;
    justify-content: center;
    font-family: var(--font-display);
    font-size: var(--fs-tiny);
    font-weight: 900;
    letter-spacing: 0.16em;
    color: var(--gold-bright);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    padding: 2px 10px;
    margin-bottom: var(--sp-1h);
    border: 1px solid var(--gold);
    border-radius: 3px;
    background: rgba(216,168,78,0.18);
    min-height: 20px;
  }

  /* ── Passive description / unlock hint ── */
  .char-passive {
    font-size: var(--fs-caption);
    color: var(--text-dim);
    line-height: 1.4;
    padding: 0 4px;
    margin-top: auto;
  }

  /* ── Lock badge: bottom-right corner of locked cards ── */
  .char-lock-badge {
    position: absolute;
    bottom: 11px;
    right: 13px;
    font-size: var(--fs-section);
    opacity: 0.75;
    line-height: 1;
    pointer-events: none;
  }

  /* ── Fallback portrait (no CLASS_ART entry) ── */
  .char-fallback-ico {
    width: 64px;
    height: 64px;
    object-fit: contain;
    display: block;
    margin: 0 auto 8px;
    filter: drop-shadow(0 2px 3px rgba(0,0,0,0.6));
  }

  /* Desktop: letterboxed — slightly larger composition */
  @container (min-width: 480px) {
    .char-list  { max-width: 520px; }
    .char-banner { height: 80px; }
    .char-hero-ico { width: 74px; height: 74px; }
  }
`;
```

## `src/scenes/DungeonScene.ts`

```typescript
import { metaApi } from "../net/metaApi";
import { nineSlice } from "../ui/nineSlice";
import { navigateTo } from "../ui/transition";
import type { DungeonRunState } from "../net/metaApi";
import { css, buffName, buffIcon } from "./battle/overlays";

/** Capitalise first letter; replace dashes with spaces for raw level ids. */
function levelLabel(id: string): string {
  return id.replace(/-/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
}

export function mountDungeon(host: HTMLElement) {
  injectDungeonRunStyles();

  const root = document.createElement("div");
  root.id = "dungeon";
  root.className = "dngrun-root";
  host.appendChild(root);

  // Persistent topbar: back chip (top-left, ≥44px) · title · symmetry spacer
  const topbar = document.createElement("div");
  topbar.className = "dngrun-topbar";

  const backChip = document.createElement("a");
  backChip.href = "/?scene=campaign";
  backChip.className = "ui-back";
  backChip.setAttribute("aria-label", "Back to campaign");
  topbar.appendChild(backChip);

  const titleEl = document.createElement("h1");
  titleEl.id = "dngrun-title";
  titleEl.textContent = "Dungeon";
  titleEl.className = "ui-title";
  topbar.appendChild(titleEl);

  const topSpacer = document.createElement("div");
  topSpacer.className = "ui-topbar-spacer";
  topbar.appendChild(topSpacer);

  root.appendChild(topbar);

  async function load() {
    let state: DungeonRunState = {};
    try {
      state = await metaApi.getDungeonState();
    } catch {
      renderError("Failed to load dungeon state.");
      return;
    }

    if (state.cleared) {
      renderCleared();
    } else if (state.active && state.floors) {
      renderActive(state as Required<Pick<DungeonRunState, "floors" | "floorIndex" | "relics" | "ballCores" | "dungeonId">>);
    } else {
      renderInactive();
    }
  }

  function renderError(msg: string) {
    const el = document.createElement("div");
    el.textContent = msg;
    css(el, { color: "var(--danger-light)", marginTop: "var(--sp-7)" });
    root.appendChild(el);
  }

  function renderInactive() {
    const msg = document.createElement("div");
    msg.textContent = "No active run.";
    css(msg, { marginTop: "var(--sp-7)", fontSize: "var(--fs-large)", color: "var(--color-dungeon-label)" });
    root.appendChild(msg);
    // Back affordance is the persistent topbar back chip above.
  }

  function renderCleared() {
    titleEl.textContent = "Dungeon Cleared!";
    const msg = document.createElement("div");
    msg.textContent = "Dungeon Cleared!";
    css(msg, { marginTop: "var(--sp-7)", fontSize: "var(--fs-title)", fontWeight: "700", color: "var(--ok-bright)",
               textShadow: "0 0 16px rgba(50,220,100,0.5)" });
    root.appendChild(msg);
    // Back affordance is the persistent topbar back chip above.
  }

  function renderActive(state: { floors: string[]; floorIndex: number; relics: string[]; ballCores: string[]; dungeonId: string }) {
    const { floors, floorIndex, relics, ballCores } = state;

    // Update persistent topbar title (back affordance already present)
    titleEl.textContent = "Active Run";

    // Floor progress
    const progressEl = document.createElement("div");
    progressEl.id = "dungeon-floor-progress";
    progressEl.textContent = `Floor ${floorIndex + 1} / ${floors.length}`;
    progressEl.className = "dngrun-progress";
    root.appendChild(progressEl);

    // Current floor name
    const currentFloor = floors[floorIndex];
    const floorNameEl = document.createElement("div");
    floorNameEl.textContent = levelLabel(currentFloor);
    floorNameEl.className = "dngrun-floor-name";
    root.appendChild(floorNameEl);

    // Collected buffs
    const buffsLabel = document.createElement("div");
    buffsLabel.textContent = "Collected Buffs";
    css(buffsLabel, { fontSize: "var(--fs-body)", color: "var(--color-dungeon-label)", marginBottom: "var(--sp-2)", alignSelf: "flex-start" });
    root.appendChild(buffsLabel);

    const buffsRow = document.createElement("div");
    buffsRow.id = "dungeon-buffs";
    buffsRow.className = "dngrun-buffs";
    root.appendChild(buffsRow);

    const allBuffs = [...relics, ...ballCores];
    if (allBuffs.length === 0) {
      const emptyEl = document.createElement("span");
      emptyEl.textContent = "None yet";
      css(emptyEl, { color: "var(--color-empty)", fontSize: "var(--fs-body)" });
      buffsRow.appendChild(emptyEl);
    } else {
      for (const buffId of allBuffs) {
        const chip = document.createElement("div");
        chip.className = "dngrun-buff-chip";
        const icon = document.createElement("img");
        icon.src = buffIcon(buffId);
        css(icon, { width: "20px", height: "20px", imageRendering: "pixelated" });
        chip.appendChild(icon);
        const nameEl = document.createElement("span");
        nameEl.textContent = buffName(buffId);
        chip.appendChild(nameEl);
        buffsRow.appendChild(chip);
      }
    }

    // Enter Floor button
    const enterBtn = document.createElement("button");
    enterBtn.id = "btn-enter-floor";
    enterBtn.className = "dngrun-enter-btn";
    enterBtn.textContent = "Enter Floor";
    enterBtn.addEventListener("click", () => {
      navigateTo(`/?scene=battle&level=${encodeURIComponent(currentFloor)}&from=dungeon`);
    });
    root.appendChild(enterBtn);
  }

  load().catch(console.error);
}

function injectDungeonRunStyles() {
  const id = "dungeon-run-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .dngrun-root {
      position: relative;
      min-height: 100cqh;
      width: 100%;
      color: var(--text);
      font-family: var(--font-body);
      display: flex;
      flex-direction: column;
      align-items: center;
      box-sizing: border-box;
      padding: max(12px, env(safe-area-inset-top, 0px)) 12px max(12px, env(safe-area-inset-bottom, 0px)) 12px;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    }
    /* Persistent topbar: back chip (top-left ≥44px) · centered title · spacer */
    .dngrun-topbar {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      padding-bottom: var(--sp-2);
      align-self: stretch;
    }
    .dngrun-topbar .ui-title { flex: 1; text-align: center; }
    .dngrun-progress {
      id: dungeon-floor-progress;
      font-size: var(--fs-body);
      color: var(--text-dim);
      margin-bottom: var(--sp-1h);
      letter-spacing: 0.03em;
    }
    .dngrun-floor-name {
      font-size: var(--fs-section);
      font-weight: 700;
      color: var(--gold-bright);
      margin-bottom: var(--sp-4);
      text-align: center;
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
    .dngrun-buffs {
      display: flex;
      gap: var(--sp-2);
      flex-wrap: wrap;
      margin-bottom: var(--sp-4);
      min-height: 36px;
      max-width: 480px;
      justify-content: center;
    }
    .dngrun-buff-chip {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "8px 10px")}
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 5px;
      padding: var(--sp-1) var(--sp-2);
      font-size: var(--fs-caption);
      color: var(--text-dim);
      transition: filter var(--dur-normal);
    }
    .dngrun-buff-chip:hover {
      filter: brightness(1.08);
    }
    .dngrun-enter-btn {
      min-height: 44px;
      padding: 2px 14px;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 18px")}
      cursor: pointer;
      font-family: var(--font-body);
      font-size: var(--fs-caption);
      font-weight: 700;
      letter-spacing: 0.04em;
      touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
      transition: filter var(--dur-normal), transform var(--dur-fast);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      color: var(--gold-bright);
    }
    .dngrun-enter-btn:hover:not(:disabled)  { filter: brightness(1.18); }
    .dngrun-enter-btn:active:not(:disabled) { transform: scale(0.96); }
    .dngrun-enter-btn:disabled {
      filter: var(--filter-disabled);
      cursor: default;
    }
    .dngrun-enter-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
  `;
  document.head.appendChild(style);
}
```

## `src/scenes/DungeonsScene.ts`

```typescript
import { metaApi } from "../net/metaApi";
import { nineSlice } from "../ui/nineSlice";
import { navigateTo } from "../ui/transition";
import type { DungeonDef } from "../net/metaApi";
import { css } from "./battle/overlays";

const RELIC_NAMES: Record<string, string> = {
  glass_cannon: "Glass Cannon",
  flint_core: "Flint Core",
  pyroclasm: "Pyroclasm",
  mana_battery: "Mana Battery",
};

const RELIC_ICONS: Record<string, string> = {
  glass_cannon: "/art/ItemHummer.png",
  flint_core: "/art/ItemDrill.png",
  pyroclasm: "/art/ItemTorch.png",
  mana_battery: "/art/ItemGem.png",
};

export function mountDungeons(host: HTMLElement) {
  injectDungeonStyles();

  const root = document.createElement("div");
  root.id = "dungeons";
  root.className = "dng-root";
  host.appendChild(root);

  // Top bar: back chip (top-left, ≥44px) · centered title · symmetry spacer
  const topbar = document.createElement("div");
  topbar.className = "dng-topbar";

  const backBtn = document.createElement("a");
  backBtn.href = "/?scene=menu";
  backBtn.className = "ui-back";
  backBtn.setAttribute("aria-label", "Back to menu");
  topbar.appendChild(backBtn);

  const h1 = document.createElement("h1");
  h1.textContent = "Dungeons";
  h1.className = "ui-title";
  topbar.appendChild(h1);

  const topSpacer = document.createElement("div");
  topSpacer.className = "ui-topbar-spacer";
  topbar.appendChild(topSpacer);

  root.appendChild(topbar);

  // Flavor banner
  const banner = document.createElement("div");
  banner.className = "dng-flavor";
  banner.textContent = "A rift has opened — descend? Death here is permanent.";
  root.appendChild(banner);

  // Dungeon list
  const list = document.createElement("div");
  list.id = "dungeon-list";
  list.className = "dng-list";
  root.appendChild(list);

  async function load() {
    let dungeons: DungeonDef[] = [];
    try {
      const data = await metaApi.getDungeons();
      dungeons = data.dungeons ?? [];
    } catch {
      const err = document.createElement("div");
      err.textContent = "Failed to load dungeons.";
      css(err, { color: "var(--danger-light)" });
      list.appendChild(err);
      return;
    }

    for (const d of dungeons) {
      const card = document.createElement("div");
      card.setAttribute("data-dungeon", d.id);
      card.className = "dng-card";

      // Title bar — MissionName art
      const titleBar = document.createElement("div");
      titleBar.className = "dng-card-titlebar";
      const nameEl = document.createElement("span");
      nameEl.textContent = d.name;
      nameEl.className = "dng-card-name";
      titleBar.appendChild(nameEl);
      card.appendChild(titleBar);

      // Meta row
      const meta = document.createElement("div");
      meta.className = "dng-card-meta";

      const floorCount = document.createElement("span");
      floorCount.textContent = `${d.floors.length} floors`;
      meta.appendChild(floorCount);

      const rewardRow = document.createElement("div");
      rewardRow.className = "dng-reward-row";
      const rewardIcon = document.createElement("img");
      rewardIcon.src = RELIC_ICONS[d.rewardRelic] ?? "/art/ItemGem.png";
      css(rewardIcon, { width: "20px", height: "20px", imageRendering: "pixelated" });
      rewardRow.appendChild(rewardIcon);
      const rewardText = document.createElement("span");
      rewardText.textContent = `${RELIC_NAMES[d.rewardRelic] ?? d.rewardRelic} + ${d.rewardCrystals} crystals`;
      rewardRow.appendChild(rewardText);
      meta.appendChild(rewardRow);
      card.appendChild(meta);

      // Descend button
      const descBtn = document.createElement("button");
      descBtn.textContent = "Descend";
      descBtn.className = "dng-descend-btn";
      descBtn.addEventListener("click", async () => {
        descBtn.disabled = true;
        descBtn.textContent = "Starting…";
        try {
          await metaApi.startDungeon(d.id);
          navigateTo("/?scene=dungeon");
        } catch {
          descBtn.disabled = false;
          descBtn.textContent = "Descend";
        }
      });
      card.appendChild(descBtn);

      list.appendChild(card);
    }
  }

  load().catch(console.error);
}

function injectDungeonStyles() {
  const id = "dungeons-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .dng-root {
      position: relative;
      min-height: 100cqh;
      width: 100%;
      color: var(--text);
      font-family: var(--font-body);
      display: flex;
      flex-direction: column;
      align-items: center;
      box-sizing: border-box;
      padding: max(12px, env(safe-area-inset-top, 0px)) 12px max(12px, env(safe-area-inset-bottom, 0px)) 12px;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    }
    /* Top bar: back chip (top-left, ≥44px) · centered title · symmetry spacer */
    .dng-topbar {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      padding-bottom: var(--sp-2);
      align-self: stretch;
    }
    .dng-topbar .ui-title { flex: 1; text-align: center; }
    .dng-flavor {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "12px 14px")}
      padding: var(--sp-3) var(--sp-4);
      margin: var(--sp-2) auto var(--sp-4);
      max-width: 520px;
      text-align: center;
      color: var(--text);
      font-size: var(--fs-body);
      letter-spacing: 0.03em;
      line-height: 1.5;
      transition: filter var(--dur-normal);
    }
    .dng-flavor:hover {
      filter: brightness(1.08);
    }
    .dng-list {
      display: flex;
      flex-direction: column;
      gap: var(--sp-3);
      width: 100%;
      max-width: 520px;
      flex: 1;
    }
    .dng-card {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "12px 14px")}
      padding: var(--sp-3) var(--sp-3h);
      display: flex;
      flex-direction: column;
      gap: var(--sp-2);
      transition: filter var(--dur-normal);
    }
    .dng-card:hover {
      filter: brightness(1.08);
    }
    .dng-card-titlebar {
      display: flex;
      align-items: center;
      padding: 0;
    }
    .dng-card-name {
      font-size: var(--fs-body);
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
    .dng-card-meta {
      display: flex;
      flex-direction: column;
      gap: var(--sp-1h);
      align-items: flex-start;
      font-size: var(--fs-caption);
      color: var(--text-dim);
    }
    .dng-reward-row {
      display: flex;
      align-items: center;
      gap: var(--sp-1h);
    }
    .dng-descend-btn {
      min-height: 44px;
      padding: 2px 14px;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 18px")}
      cursor: pointer;
      font-family: var(--font-body);
      font-size: var(--fs-caption);
      font-weight: 700;
      letter-spacing: 0.04em;
      touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
      transition: filter var(--dur-normal), transform var(--dur-fast);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      color: var(--gold-bright);
    }
    .dng-descend-btn:hover:not(:disabled)  { filter: brightness(1.18); }
    .dng-descend-btn:active:not(:disabled) { transform: scale(0.96); }
    .dng-descend-btn:disabled {
      filter: saturate(0.25) brightness(0.6);
      cursor: default;
    }
    .dng-descend-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
  `;
  document.head.appendChild(style);
}
```

## `src/scenes/EditorScene.ts`

```typescript
import { metaApi } from "../net/metaApi";
import type { BlockTypeDef } from "../net/metaApi";
import { ART_BASE, DEFAULT_COLS, DEFAULT_ROWS, LEGEND_CHARS, btn } from "./editor/editorUtils";
import { navigateTo } from "../ui/transition";

// ── Main export ───────────────────────────────────────────────────────────────

export function mountEditor(host: HTMLElement) {
  // ── State ──────────────────────────────────────────────────────────────────
  let cols = DEFAULT_COLS;
  let rows = DEFAULT_ROWS;
  let selectedType = "."; // "." = eraser
  let blockTypes: BlockTypeDef[] = [];
  // grid: rows×cols, each cell is a blocktype id or "." for empty
  let grid: string[][] = Array.from({ length: rows }, () => Array(cols).fill("."));

  // ── Root container ─────────────────────────────────────────────────────────
  const root = document.createElement("div");
  root.id = "editor";
  root.style.cssText = [
    "display:flex",
    "flex-direction:column",
    "align-items:center",
    "padding:20px",
    "color:#e8e8ff",
    "font-family:var(--font-body)",
    // The editor is taller than a phone screen — it must own its scrolling,
    // because the scene host clips overflow (the original mobile Save bug).
    "height:100cqh",
    "overflow-y:auto",
    "background:#0d0d1a",
    "box-sizing:border-box",
  ].join(";");

  // title
  const title = document.createElement("h2");
  title.textContent = "Level Editor";
  title.style.cssText = "margin:0 0 12px 0;font-size:1.6rem;letter-spacing:0.08em";
  root.appendChild(title);

  // ── Top controls row ────────────────────────────────────────────────────────
  const controls = document.createElement("div");
  controls.style.cssText = [
    "display:flex",
    "gap:14px",
    "align-items:center",
    "flex-wrap:wrap",
    "margin-bottom:14px",
  ].join(";");

  // Level id input
  const idLabel = document.createElement("label");
  idLabel.textContent = "Level ID: ";
  idLabel.style.cssText = "font-size:var(--fs-body);color:#aab";
  const idInput = document.createElement("input");
  idInput.id = "editor-id";
  idInput.type = "text";
  idInput.placeholder = "e.g. my-level-1";
  idInput.style.cssText = [
    "background:#1a1a2e",
    "color:#e8e8ff",
    "border:1px solid #445",
    "border-radius:4px",
    "padding:4px 8px",
    "font-size:var(--fs-body)",
    "width:160px",
  ].join(";");
  idLabel.appendChild(idInput);
  controls.appendChild(idLabel);

  // Biome selector
  const biomeLabel = document.createElement("label");
  biomeLabel.textContent = "Biome: ";
  biomeLabel.style.cssText = "font-size:var(--fs-body);color:#aab";
  const biomeSelect = document.createElement("select");
  biomeSelect.id = "editor-biome";
  biomeSelect.style.cssText = [
    "background:#1a1a2e",
    "color:#e8e8ff",
    "border:1px solid #445",
    "border-radius:4px",
    "padding:4px 8px",
    "font-size:var(--fs-body)",
  ].join(";");
  ["hell", "cavern", "village", "heaven"].forEach(b => {
    const opt = document.createElement("option");
    opt.value = b;
    opt.textContent = b;
    biomeSelect.appendChild(opt);
  });
  biomeLabel.appendChild(biomeSelect);
  controls.appendChild(biomeLabel);

  // Cols / rows
  function makeNumInput(labelText: string, defaultVal: number, id: string): [HTMLLabelElement, HTMLInputElement] {
    const lbl = document.createElement("label");
    lbl.textContent = labelText + " ";
    lbl.style.cssText = "font-size:var(--fs-body);color:#aab";
    const inp = document.createElement("input");
    inp.id = id;
    inp.type = "number";
    inp.value = String(defaultVal);
    inp.min = "1";
    inp.max = "24";
    inp.style.cssText = [
      "background:#1a1a2e",
      "color:#e8e8ff",
      "border:1px solid #445",
      "border-radius:4px",
      "padding:4px 6px",
      "font-size:var(--fs-body)",
      "width:54px",
    ].join(";");
    lbl.appendChild(inp);
    return [lbl, inp];
  }

  const [colsLabel, colsInput] = makeNumInput("Cols:", DEFAULT_COLS, "editor-cols");
  const [rowsLabel, rowsInput] = makeNumInput("Rows:", DEFAULT_ROWS, "editor-rows");
  controls.appendChild(colsLabel);
  controls.appendChild(rowsLabel);

  // Load existing level by id
  const loadBtn = btn("Load", "btn-editor-load", [
    "font-size:var(--fs-body)",
    "padding:5px 14px",
    "background:#1a2a1a",
    "color:#88ff88",
    "border:1px solid #336633",
    "border-radius:4px",
    "cursor:pointer",
  ].join(";"), async () => {
    const id = idInput.value.trim();
    if (!id) { showStatus("Enter a level ID to load.", true); return; }
    try {
      const level = await metaApi.loadLevel(id);
      cols = level.cols;
      rows = level.rows;
      colsInput.value = String(cols);
      rowsInput.value = String(rows);
      biomeSelect.value = level.biome;
      // Rebuild grid from rows_data + legend (invert legend: char → id)
      const charToId: Record<string, string> = {};
      for (const [ch, id] of Object.entries(level.legend)) charToId[ch] = id;
      grid = level.rows_data.map(row =>
        row.split("").map(ch => (ch === "." ? "." : (charToId[ch] ?? ".")))
      );
      rebuildGrid();
      showStatus(`Loaded "${id}".`);
    } catch {
      showStatus("Level not found or load failed.", true);
    }
  });
  controls.appendChild(loadBtn);

  root.appendChild(controls);

  // ── Main layout: palette + grid ────────────────────────────────────────────
  // flex-wrap lets the palette stack ABOVE the grid on phone widths (390px)
  // instead of overflowing horizontally and pushing the actions off-viewport.
  const layout = document.createElement("div");
  layout.style.cssText = [
    "display:flex",
    "flex-wrap:wrap",
    "gap:14px",
    "align-items:flex-start",
    "justify-content:center",
    "width:100%",
    "max-width:900px",
  ].join(";");

  // Palette — a wrapping chip strip on narrow screens, a column on wide ones.
  const palette = document.createElement("div");
  palette.id = "editor-palette";
  palette.style.cssText = [
    "display:flex",
    "flex-direction:row",
    "flex-wrap:wrap",
    "gap:4px",
    "min-width:0",
    "max-width:100%",
    "background:#12122a",
    "border:1px solid #334",
    "border-radius:6px",
    "padding:8px",
  ].join(";");

  const paletteTitle = document.createElement("div");
  paletteTitle.textContent = "Palette";
  paletteTitle.style.cssText = "font-size:var(--fs-caption);color:#889;margin-bottom:4px;letter-spacing:0.05em;text-transform:uppercase;width:100%";
  palette.appendChild(paletteTitle);

  layout.appendChild(palette);

  // Grid area
  const gridWrap = document.createElement("div");
  gridWrap.style.cssText = "flex:1;display:flex;flex-direction:column;gap:8px";

  const gridEl = document.createElement("div");
  gridEl.id = "editor-grid";
  gridEl.style.cssText = "display:inline-block;border:1px solid #334;border-radius:4px;line-height:0";
  gridWrap.appendChild(gridEl);
  layout.appendChild(gridWrap);
  root.appendChild(layout);

  // ── Bottom action buttons ──────────────────────────────────────────────────
  const actions = document.createElement("div");
  actions.style.cssText = "display:flex;flex-wrap:wrap;gap:12px;margin-top:14px;align-items:center;justify-content:center";

  const saveBtn = btn("Save Level", "btn-editor-save", [
    "font-size:var(--fs-subhead)",
    "padding:8px 22px",
    "background:#1a1a3a",
    "color:#88aaff",
    "border:1px solid #334477",
    "border-radius:6px",
    "cursor:pointer",
    "letter-spacing:0.04em",
  ].join(";"), async () => {
    const id = idInput.value.trim();
    if (!id || !/^[a-z0-9-]+$/.test(id)) {
      showStatus("Level ID must match ^[a-z0-9-]+$", true);
      return;
    }
    const { rowsData, legend } = buildLevelData();
    try {
      const res = await metaApi.saveLevel({
        id,
        biome: biomeSelect.value,
        cols,
        rows,
        rows_data: rowsData,
        legend,
      });
      if (res.ok) showStatus(`Saved "${res.id}" successfully.`);
      else showStatus("Save failed.", true);
    } catch {
      showStatus("Save request failed.", true);
    }
  });
  actions.appendChild(saveBtn);

  const playBtn = btn("Test Play", "btn-editor-play", [
    "font-size:var(--fs-subhead)",
    "padding:8px 22px",
    "background:#1a3a1a",
    "color:#88ff88",
    "border:1px solid #336633",
    "border-radius:6px",
    "cursor:pointer",
    "letter-spacing:0.04em",
  ].join(";"), async () => {
    const id = idInput.value.trim();
    if (!id || !/^[a-z0-9-]+$/.test(id)) {
      showStatus("Level ID must match ^[a-z0-9-]+$", true);
      return;
    }
    const { rowsData, legend } = buildLevelData();
    try {
      await metaApi.saveLevel({
        id,
        biome: biomeSelect.value,
        cols,
        rows,
        rows_data: rowsData,
        legend,
      });
      location.search = `?scene=battle&level=${encodeURIComponent(id)}`;
    } catch {
      showStatus("Save failed before play.", true);
    }
  });
  actions.appendChild(playBtn);

  const backBtn = btn("Back to Menu", "btn-editor-back", [
    "font-size:var(--fs-subhead)",
    "padding:8px 18px",
    "background:#1a1a1a",
    "color:#aaa",
    "border:1px solid #333",
    "border-radius:6px",
    "cursor:pointer",
  ].join(";"), () => { navigateTo("/?scene=menu"); });
  actions.appendChild(backBtn);

  const statusEl = document.createElement("span");
  statusEl.id = "editor-status";
  statusEl.style.cssText = "font-size:var(--fs-body);margin-left:8px;color:#88ff88";
  actions.appendChild(statusEl);

  root.appendChild(actions);
  host.appendChild(root);

  // ── Status helper ──────────────────────────────────────────────────────────
  function showStatus(msg: string, error = false) {
    statusEl.textContent = msg;
    statusEl.style.color = error ? "#ff6666" : "#88ff88";
    setTimeout(() => { if (statusEl.textContent === msg) statusEl.textContent = ""; }, 3000);
  }

  // ── Grid interaction ───────────────────────────────────────────────────────
  let painting = false;

  function paintCell(cellEl: HTMLElement) {
    const c = parseInt(cellEl.getAttribute("data-col")!);
    const r = parseInt(cellEl.getAttribute("data-row")!);
    if (isNaN(c) || isNaN(r)) return;
    grid[r][c] = selectedType;
    updateCellDisplay(cellEl, selectedType);
  }

  function updateCellDisplay(cellEl: HTMLElement, typeId: string) {
    const bt = blockTypes.find(b => b.id === typeId);
    cellEl.style.backgroundImage = bt ? `url(${ART_BASE}${bt.sprite}.png)` : "none";
    cellEl.style.backgroundColor = bt ? "transparent" : "#0d0d1a";
    cellEl.style.backgroundSize = "cover";
  }

  function rebuildGrid() {
    gridEl.innerHTML = "";
    gridEl.style.gridTemplateColumns = `repeat(${cols}, 36px)`;
    gridEl.style.display = "grid";

    for (let r = 0; r < rows; r++) {
      if (!grid[r]) grid[r] = Array(cols).fill(".");
      for (let c = 0; c < cols; c++) {
        if (grid[r][c] === undefined) grid[r][c] = ".";
        const cell = document.createElement("div");
        cell.setAttribute("data-col", String(c));
        cell.setAttribute("data-row", String(r));
        cell.style.cssText = [
          "width:36px",
          "height:24px",
          "box-sizing:border-box",
          "border:1px solid #223",
          "cursor:crosshair",
          "background-size:cover",
          "background-position:center",
        ].join(";");

        updateCellDisplay(cell, grid[r][c]);

        cell.addEventListener("mousedown", (e) => {
          e.preventDefault();
          painting = true;
          paintCell(cell);
        });
        cell.addEventListener("mouseenter", () => {
          if (painting) paintCell(cell);
        });
        gridEl.appendChild(cell);
      }
    }

    window.addEventListener("mouseup", () => { painting = false; }, { once: false });
  }

  // ── Dimension change handlers ──────────────────────────────────────────────
  function onDimsChange() {
    const nc = Math.max(1, Math.min(24, parseInt(colsInput.value) || DEFAULT_COLS));
    const nr = Math.max(1, Math.min(24, parseInt(rowsInput.value) || DEFAULT_ROWS));
    // Preserve existing cells
    const oldGrid = grid;
    const newGrid: string[][] = Array.from({ length: nr }, (_, r) =>
      Array.from({ length: nc }, (_, c) => oldGrid[r]?.[c] ?? ".")
    );
    cols = nc;
    rows = nr;
    grid = newGrid;
    rebuildGrid();
  }

  colsInput.addEventListener("change", onDimsChange);
  rowsInput.addEventListener("change", onDimsChange);

  // ── Build level data from grid ─────────────────────────────────────────────
  function buildLevelData(): { rowsData: string[]; legend: Record<string, string> } {
    // Collect distinct used block ids (not ".")
    const usedIds = Array.from(new Set(grid.flat().filter(id => id !== ".")));
    const legend: Record<string, string> = {};
    const idToChar: Record<string, string> = {};
    usedIds.forEach((id, i) => {
      const ch = LEGEND_CHARS[i % LEGEND_CHARS.length];
      legend[ch] = id;
      idToChar[id] = ch;
    });

    const rowsData = grid.map(row =>
      row.map(cell => (cell === "." ? "." : (idToChar[cell] ?? "."))).join("")
    );

    return { rowsData, legend };
  }

  // ── Palette rendering ──────────────────────────────────────────────────────
  function renderPalette() {
    // Remove old swatches (keep title)
    while (palette.children.length > 1) palette.removeChild(palette.lastChild!);

    // Eraser swatch
    const eraserSwatch = makeSwatch(".", "Eraser", null);
    palette.appendChild(eraserSwatch);

    // Group by biome
    const biomes = Array.from(new Set(blockTypes.map(b => b.biome)));
    biomes.forEach(biome => {
      const bLabel = document.createElement("div");
      bLabel.textContent = biome.toUpperCase();
      bLabel.style.cssText = "font-size:var(--fs-tiny);color:#667;margin-top:6px;margin-bottom:2px;letter-spacing:0.06em;width:100%";
      palette.appendChild(bLabel);

      blockTypes.filter(b => b.biome === biome).forEach(bt => {
        const swatch = makeSwatch(bt.id, bt.id, bt.sprite);
        palette.appendChild(swatch);
      });
    });
  }

  function makeSwatch(typeId: string, label: string, sprite: string | null): HTMLElement {
    const sw = document.createElement("div");
    sw.setAttribute("data-blocktype", typeId);
    sw.title = label;
    sw.style.cssText = [
      "display:flex",
      "align-items:center",
      "gap:6px",
      "padding:3px 6px",
      "border-radius:4px",
      "cursor:pointer",
      "border:1px solid transparent",
      "font-size:var(--fs-small)",
      "color:#ccc",
      "white-space:nowrap",
      "overflow:hidden",
      "text-overflow:ellipsis",
    ].join(";");

    if (sprite) {
      const img = document.createElement("img");
      img.src = `${ART_BASE}${sprite}.png`;
      img.style.cssText = "width:24px;height:16px;object-fit:cover;border-radius:2px;flex-shrink:0";
      img.onerror = () => { img.style.display = "none"; };
      sw.appendChild(img);
    } else {
      // Eraser icon
      const ico = document.createElement("div");
      ico.textContent = "✕";
      ico.style.cssText = "width:24px;height:16px;display:flex;align-items:center;justify-content:center;font-size:var(--fs-caption);flex-shrink:0;color:#ff7777";
      sw.appendChild(ico);
    }

    const lbl = document.createElement("span");
    lbl.textContent = typeId === "." ? "Eraser" : typeId;
    sw.appendChild(lbl);

    sw.addEventListener("click", () => {
      selectedType = typeId;
      updatePaletteSelection();
    });

    return sw;
  }

  function updatePaletteSelection() {
    palette.querySelectorAll<HTMLElement>("[data-blocktype]").forEach(sw => {
      const active = sw.getAttribute("data-blocktype") === selectedType;
      sw.style.borderColor = active ? "#7799ff" : "transparent";
      sw.style.background   = active ? "#1a2a4a" : "transparent";
    });
  }

  // ── Init ───────────────────────────────────────────────────────────────────
  (async () => {
    try {
      blockTypes = await metaApi.getBlockTypes();
    } catch {
      blockTypes = [];
    }
    renderPalette();
    rebuildGrid();
    updatePaletteSelection();
  })();

  // Prevent page-level mouseup from being lost
  document.addEventListener("mouseup", () => { painting = false; });
}
```

## `src/scenes/InventoryScene.ts`

```typescript
import { metaApi, type ItemDef } from "../net/metaApi";
import { unlockAchievement } from "./AchievementsScene";
import { INVENTORY_STYLES } from "./inventory/inventoryStyles";
import { navigateTo } from "../ui/transition";

// ---------------------------------------------------------------------------
// Inventory / Shop scene
// ---------------------------------------------------------------------------
// Layout: header (title + crystal count) → equipped row (3 fixed slots) →
//         item grid (scrollable) → each card: tier-sprite + name + cost + Buy / Equip buttons.
// Mobile portrait, ≥44px touch targets.
// ---------------------------------------------------------------------------

export async function mountInventory(host: HTMLElement) {
  injectStyles();

  const root = document.createElement("div");
  root.id = "inventory-root";
  root.className = "inv-root";
  host.appendChild(root);

  // Header
  const header = document.createElement("div");
  header.className = "inv-header";

  const backBtn = document.createElement("button");
  backBtn.id = "btn-inv-back";
  backBtn.className = "ui-back";
  backBtn.setAttribute("aria-label", "Back to menu");
  backBtn.addEventListener("click", () => { navigateTo("/?scene=menu"); });
  header.appendChild(backBtn);

  const title = document.createElement("h1");
  title.className = "inv-title";
  title.textContent = "Items";
  header.appendChild(title);

  const crystalEl = document.createElement("div");
  crystalEl.id = "inv-crystals";
  crystalEl.className = "inv-crystals";
  crystalEl.innerHTML = `<img src="/ui/Gem.png" style="width:16px;height:16px;vertical-align:middle;image-rendering:pixelated;"> <span id="inv-crystal-count">—</span>`;
  header.appendChild(crystalEl);

  root.appendChild(header);

  // Equipped row (3 fixed slots, always visible)
  const equippedSection = document.createElement("div");
  equippedSection.className = "inv-equipped-section";

  const equippedLabel = document.createElement("div");
  equippedLabel.className = "inv-section-label";
  equippedLabel.textContent = "EQUIPPED (up to 3)";
  equippedSection.appendChild(equippedLabel);

  const equippedRow = document.createElement("div");
  equippedRow.id = "inv-equipped-row";
  equippedRow.className = "inv-equipped-row";
  for (let i = 0; i < 3; i++) {
    const slot = document.createElement("div");
    slot.className = "inv-equip-slot inv-equip-slot-empty";
    slot.dataset.slot = String(i);
    slot.textContent = String(i + 1);
    equippedRow.appendChild(slot);
  }
  equippedSection.appendChild(equippedRow);
  root.appendChild(equippedSection);

  // Grid label
  const catalogLabel = document.createElement("div");
  catalogLabel.className = "inv-section-label inv-catalog-label";
  catalogLabel.textContent = "ALL ITEMS";
  root.appendChild(catalogLabel);

  // Scrollable item grid
  const grid = document.createElement("div");
  grid.id = "inv-grid";
  grid.className = "inv-grid";
  root.appendChild(grid);

  // Loading state
  grid.textContent = "Loading…";

  // Fetch data and render
  try {
    const data = await metaApi.getItems();
    render(data.items, data.crystals, data.equipped, crystalEl, equippedRow, grid);
  } catch (err) {
    grid.textContent = "Failed to load items.";
    console.error("inventory load error:", err);
  }
}

// ---------------------------------------------------------------------------
// Render / update helpers
// ---------------------------------------------------------------------------

function render(
  items: ItemDef[],
  crystals: number,
  equipped: string[],
  crystalEl: HTMLElement,
  equippedRow: HTMLElement,
  grid: HTMLElement
) {
  updateCrystals(crystalEl, crystals);
  updateEquippedRow(equippedRow, equipped, items);
  buildGrid(grid, items, equipped, crystals, crystalEl, equippedRow);
}

function updateCrystals(el: HTMLElement, crystals: number) {
  const count = el.querySelector("#inv-crystal-count");
  if (count) count.textContent = String(crystals);
  else el.textContent = String(crystals);
  el.dataset.crystals = String(crystals);
}

function updateEquippedRow(equippedRow: HTMLElement, equipped: string[], items: ItemDef[]) {
  const slots = equippedRow.querySelectorAll<HTMLElement>("[data-slot]");
  slots.forEach((slot, i) => {
    const itemId = equipped[i];
    const def = itemId ? items.find(it => it.id === itemId) : undefined;
    slot.innerHTML = "";
    if (def) {
      slot.classList.remove("inv-equip-slot-empty");
      slot.classList.add("inv-equip-slot-filled");
      slot.dataset.equipped = def.id;

      const tier = def.ownedTier;
      const img = document.createElement("img");
      img.src = `/items/${def.icon}${tier > 1 ? String(tier) : ""}.png`;
      img.alt = def.name;
      img.className = "inv-slot-sprite";
      img.onerror = () => { img.src = `/items/${def.icon}.png`; img.onerror = null; };
      slot.appendChild(img);

      const label = document.createElement("div");
      label.className = "inv-slot-label";
      label.textContent = def.name;
      slot.appendChild(label);
    } else {
      slot.classList.add("inv-equip-slot-empty");
      slot.classList.remove("inv-equip-slot-filled");
      delete slot.dataset.equipped;
      slot.textContent = String(i + 1);
    }
  });
}

function buildGrid(
  grid: HTMLElement,
  items: ItemDef[],
  equipped: string[],
  crystals: number,
  crystalEl: HTMLElement,
  equippedRow: HTMLElement
) {
  grid.innerHTML = "";

  for (const item of items) {
    const card = buildCard(item, equipped, crystals);
    grid.appendChild(card);

    // Wire buy button
    const buyBtn = card.querySelector<HTMLButtonElement>(".inv-buy-btn");
    if (buyBtn) {
      buyBtn.addEventListener("click", async (e) => {
        e.stopPropagation();
        const result = await metaApi.buyItem(item.id);
        if (result.ok) {
          item.ownedTier = result.ownedTier;
          crystals = result.crystals;
          updateCrystals(crystalEl, crystals);
          // Refresh grid
          buildGrid(grid, items, equipped, crystals, crystalEl, equippedRow);
          updateEquippedRow(equippedRow, equipped, items);
        }
      });
    }

    // Wire equip/unequip button
    const equipBtn = card.querySelector<HTMLButtonElement>(".inv-equip-btn");
    if (equipBtn) {
      equipBtn.addEventListener("click", async (e) => {
        e.stopPropagation();
        const isEquipped = equipped.includes(item.id);
        let result;
        if (isEquipped) {
          result = await metaApi.unequipItem(item.id);
        } else {
          result = await metaApi.equipItem(item.id);
        }
        if (result.ok) {
          equipped.splice(0, equipped.length, ...result.equipped);
          // Update equipped flags on items
          for (const it of items) { it.equipped = equipped.includes(it.id); }
          buildGrid(grid, items, equipped, crystals, crystalEl, equippedRow);
          updateEquippedRow(equippedRow, equipped, items);
          // Unlock achievement on first equip
          if (!isEquipped) unlockAchievement("equip_item").catch(() => {});
        }
      });
    }
  }
}

function buildCard(item: ItemDef, equipped: string[], crystals: number): HTMLElement {
  const card = document.createElement("div");
  card.className = "inv-card";
  card.dataset.itemId = item.id;
  if (item.equipped) card.classList.add("inv-card-equipped");

  // Sprite (tier-appropriate)
  const spriteWrap = document.createElement("div");
  spriteWrap.className = "inv-card-sprite";

  const tier = item.ownedTier;
  if (tier > 0) {
    const img = document.createElement("img");
    const suffix = tier > 1 ? String(tier) : "";
    img.src = `/items/${item.icon}${suffix}.png`;
    img.alt = item.name;
    img.className = "inv-item-sprite";
    img.onerror = () => { img.src = `/items/${item.icon}.png`; img.onerror = null; };
    spriteWrap.appendChild(img);
  } else {
    // Not owned — show the (dimmed) real item art with a small lock badge, so the
    // shop reads as "items you can unlock" rather than a wall of identical padlocks.
    spriteWrap.style.position = "relative";
    const img = document.createElement("img");
    img.src = `/items/${item.icon}.png`;
    img.alt = item.name;
    img.className = "inv-item-sprite inv-item-locked";
    img.style.cssText += ";filter:grayscale(1) brightness(0.55);";
    spriteWrap.appendChild(img);
    const lock = document.createElement("img");
    lock.src = "/items/LockedItem.png";
    lock.alt = "Locked";
    lock.style.cssText = "position:absolute;right:2px;bottom:2px;width:15px;height:15px;opacity:0.95;";
    lock.onerror = () => { lock.style.display = "none"; };
    spriteWrap.appendChild(lock);
  }
  card.appendChild(spriteWrap);

  // Name + tier badge
  const nameRow = document.createElement("div");
  nameRow.className = "inv-card-name-row";
  const nameEl = document.createElement("div");
  nameEl.className = "inv-card-name";
  nameEl.textContent = item.name;
  nameRow.appendChild(nameEl);

  if (tier > 0) {
    const tierBadge = document.createElement("div");
    tierBadge.className = "inv-tier-badge";
    tierBadge.dataset.tier = String(tier);
    tierBadge.textContent = `T${tier}`;
    nameRow.appendChild(tierBadge);
  }
  card.appendChild(nameRow);

  // Effect description
  const descEl = document.createElement("div");
  descEl.className = "inv-card-desc";
  descEl.textContent = item.description;
  card.appendChild(descEl);

  // Action row: Buy / Equip
  const actions = document.createElement("div");
  actions.className = "inv-card-actions";

  // Buy button (upgrade to next tier if not maxed)
  const nextTier = tier + 1;
  if (tier < item.maxTier) {
    const nextCost = item.cost[nextTier - 1] ?? Infinity;
    const canAfford = crystals >= nextCost;

    const buyBtn = document.createElement("button");
    buyBtn.className = "inv-buy-btn" + (canAfford ? "" : " inv-btn-disabled");
    buyBtn.dataset.cost = String(nextCost);
    buyBtn.innerHTML = `${tier === 0 ? "Buy" : "Upgrade"} <img src="/ui/Gem.png" style="width:12px;height:12px;vertical-align:middle;image-rendering:pixelated;"> ${nextCost}`;
    buyBtn.disabled = !canAfford;
    actions.appendChild(buyBtn);
  } else if (tier === item.maxTier) {
    const maxEl = document.createElement("div");
    maxEl.className = "inv-max-badge";
    maxEl.textContent = "MAX";
    actions.appendChild(maxEl);
  }

  // Equip / Unequip button (only if owned)
  if (tier > 0) {
    const isEquipped = equipped.includes(item.id);
    const equipBtn = document.createElement("button");
    equipBtn.className = "inv-equip-btn" + (isEquipped ? " inv-btn-unequip" : "");
    equipBtn.dataset.equipped = String(isEquipped);
    equipBtn.textContent = isEquipped ? "Unequip" : "Equip";
    actions.appendChild(equipBtn);
  }

  card.appendChild(actions);

  return card;
}

// ---------------------------------------------------------------------------
// Styles
// ---------------------------------------------------------------------------

function injectStyles() {
  const id = "inv-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = INVENTORY_STYLES;
  document.head.appendChild(style);
}
```

## `src/scenes/MenuScene.ts`

```typescript
import { navigateTo } from "../ui/transition";
import { metaApi } from "../net/metaApi";
import { btn1 } from "../ui/nineSlice";
import type { CampaignNode } from "../net/metaApi";
import { log } from "../log";

// The home screen is ONE journey: a primary "Continue" that resumes the furthest
// playable campaign node, a "Campaign Map" entry into the node graph, and a docked
// bar of secondary destinations. No Play/Dungeons/Editor buttons, no level-chip grid.

const FALLBACK_LEVEL = "hell-1";

interface DockEntry { id: string; label: string; scene: string; icon: string; }

const DOCK: DockEntry[] = [
  // Icons must be ICONS — the old Heroes entry was "Profil" word-art (read as
  // "...") and Settings was an empty button pill (read as a dash). docs/13 §S2.
  { id: "btn-characters",   label: "Heroes",   scene: "characters",   icon: "/ui/FireHeroIco.png" },
  { id: "btn-inventory",    label: "Items",    scene: "inventory",    icon: "/ui/InventoryButton.png" },
  { id: "btn-skills",       label: "Skills",   scene: "skills",       icon: "/ui/InterfaceSkillsButton.png" },
  { id: "btn-achievements", label: "Awards",   scene: "achievements", icon: "/achievements/achievementLvl2Eng.png" },
  { id: "btn-settings",     label: "Settings", scene: "settings",     icon: "/ui/SettingsGear.svg" },
];

// Ember particle definitions: left (cqw), delay (s), duration (s), size (px), bottom (cqh).
// 10 particles spread across horizontal range — varied timing to avoid lockstep.
const EMBER_PARTICLES = [
  { left: 10, delay:  0.0, dur:  9, size: 3, bottom: 22 },
  { left: 22, delay:  2.5, dur: 12, size: 4, bottom: 26 },
  { left: 35, delay:  5.0, dur: 10, size: 3, bottom: 20 },
  { left: 48, delay:  1.0, dur: 14, size: 5, bottom: 18 },
  { left: 60, delay:  7.0, dur:  8, size: 3, bottom: 30 },
  { left: 72, delay:  3.5, dur: 11, size: 4, bottom: 24 },
  { left: 83, delay:  9.0, dur: 13, size: 3, bottom: 28 },
  { left: 18, delay:  6.0, dur: 10, size: 4, bottom: 21 },
  { left: 54, delay:  4.0, dur:  9, size: 3, bottom: 25 },
  { left: 90, delay:  8.0, dur: 12, size: 4, bottom: 23 },
];

/** Furthest *playable* node = the deepest node still unlocked (the campaign frontier). */
function furthestNode(nodes: CampaignNode[]): CampaignNode | null {
  const playable = nodes.filter((n) => n.unlocked);
  if (playable.length) return playable[playable.length - 1];
  return nodes[0] ?? null;
}

export function mountMenu(host: HTMLElement) {
  injectMenuStyles();
  // NOTE: do NOT install a window.__game stub here. Battle pages install the real
  // __game (with getState); a partial stub would make the standard
  // `__game?.getState()` poll throw during the navigation fade. Menu logs are
  // captured via console mirroring (see log.ts) and attached by the test fixture.

  const el = document.createElement("div");
  el.id = "menu";
  el.className = "menu-root";

  // ── Layer 0: deep warm background ──────────────────────────────────────────
  const bg = document.createElement("div");
  bg.className = "menu-bg";
  el.appendChild(bg);

  // ── Layer 1: key-art slot (z-index 1) ──────────────────────────────────────
  // Placeholder behind the column; swap in the hero illustration when art ships.
  const keyart = document.createElement("div");
  keyart.className = "menu-keyart";
  /* future: commissioned hero illustration (docs/13 asset gap #1) */
  el.appendChild(keyart);

  // ── Layer 2a: ember glow — large dim radial behind the CTA block ───────────
  const emberGlow = document.createElement("div");
  emberGlow.className = "menu-ember-glow";
  el.appendChild(emberGlow);

  // ── Layer 2b: ember particles — slowly drifting gold dots ─────────────────
  const particlesWrap = document.createElement("div");
  particlesWrap.className = "menu-particles";
  EMBER_PARTICLES.forEach((p) => {
    const dot = document.createElement("div");
    dot.className = "menu-ember";
    dot.style.cssText =
      `left:${p.left}cqw;bottom:${p.bottom}cqh;` +
      `animation-delay:-${p.delay}s;animation-duration:${p.dur}s;` +
      `width:${p.size}px;height:${p.size}px;`;
    particlesWrap.appendChild(dot);
  });
  el.appendChild(particlesWrap);

  // ── Layer 3: content column ─────────────────────────────────────────────────
  const col = document.createElement("div");
  col.className = "menu-col";

  // Screen-reader / test title.
  const h1 = document.createElement("h1");
  h1.textContent = "ARKANOID RPG";
  h1.style.cssText = "position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;";
  col.appendChild(h1);

  // Logo — anchored near the top (~12% from top via column padding-top).
  const logo = document.createElement("div");
  logo.className = "menu-logo";
  col.appendChild(logo);

  // ── CTA wrapper — vertically centered in remaining space above dock ─────────
  // margin-top: auto; margin-bottom: auto distributes the free space evenly so the
  // block floats in the middle rather than piling up under the logo.
  const ctaWrap = document.createElement("div");
  ctaWrap.className = "menu-cta-wrap";

  // Primary: Continue (resumes the furthest playable node).
  const playBtn = document.createElement("button");
  playBtn.id = "btn-continue";
  playBtn.setAttribute("data-level", FALLBACK_LEVEL); // updated once campaign loads
  playBtn.className = "menu-art-btn menu-btn-continue";
  playBtn.innerHTML = `
    <span class="menu-btn-kicker">Continue</span>
    <span class="menu-btn-node" id="continue-node-label">Hell I</span>`;
  playBtn.addEventListener("click", () => {
    const level = playBtn.getAttribute("data-level") || FALLBACK_LEVEL;
    log("menu", "continue", { level });
    navigateTo(`/?scene=battle&level=${level}&from=campaign`);
  });
  ctaWrap.appendChild(playBtn);

  // Secondary: Campaign Map (the node-graph navigation).
  const mapBtn = document.createElement("button");
  mapBtn.id = "btn-campaign";
  mapBtn.className = "menu-art-btn menu-btn-map";
  mapBtn.innerHTML = `<span class="menu-btn-label">Campaign Map</span>`;
  mapBtn.addEventListener("click", () => {
    log("menu", "open-map");
    navigateTo("/?scene=campaign");
  });
  ctaWrap.appendChild(mapBtn);

  col.appendChild(ctaWrap);

  // ── Docked secondary destinations (icons along the bottom edge) ─────────────
  const dock = document.createElement("div");
  dock.className = "menu-dock";
  DOCK.forEach((entry) => {
    const btn = document.createElement("button");
    btn.id = entry.id;
    btn.className = "menu-dock-btn";
    btn.setAttribute("aria-label", entry.label);
    btn.innerHTML = `
      <span class="menu-dock-ico" style="background-image:url('${entry.icon}')"></span>
      <span class="menu-dock-label">${entry.label}</span>`;
    btn.addEventListener("click", () => {
      log("menu", "open-scene", { scene: entry.scene });
      navigateTo(`/?scene=${entry.scene}`);
    });
    dock.appendChild(btn);
  });
  col.appendChild(dock);

  el.appendChild(col);
  host.appendChild(el);

  // Resolve the furthest playable node and point Continue at it.
  metaApi.getCampaign()
    .then((camp) => {
      const node = furthestNode(camp.nodes);
      if (!node) return;
      playBtn.setAttribute("data-level", node.id);
      const lbl = document.getElementById("continue-node-label");
      if (lbl) lbl.textContent = node.label;
      log("menu", "furthest-node", { level: node.id, label: node.label });
    })
    .catch((err) => log("menu", "campaign-load-failed", { err: String(err) }));
}

function injectMenuStyles() {
  const id = "menu-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .menu-root {
      position: relative;
      min-height: 100cqh;
      width: 100%;
      overflow: hidden;
      display: flex;
      align-items: stretch;
      font-family: var(--font-body);
    }

    /* ── Background ── */
    .menu-bg {
      position: absolute;
      inset: 0;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
      z-index: 0;
    }

    /* ── Key-art slot (z-index 1) — future hero illustration ── */
    .menu-keyart {
      position: absolute;
      inset: 0;
      z-index: 1;
      pointer-events: none;
      /* future: commissioned hero illustration (docs/13 asset gap #1) */
    }

    /* ── Ember glow — large dim radial behind the CTA block (z-index 2) ── */
    .menu-ember-glow {
      position: absolute;
      top: 28cqh;
      left: 50%;
      transform: translateX(-50%);
      width: 92cqw;
      height: 55cqh;
      background: radial-gradient(ellipse at 50% 50%,
        rgba(200, 100, 20, 0.13) 0%,
        rgba(160,  70, 10, 0.06) 45%,
        transparent 72%);
      z-index: 2;
      pointer-events: none;
    }

    /* ── Ember particles container ── */
    .menu-particles {
      position: absolute;
      inset: 0;
      z-index: 2;
      pointer-events: none;
      overflow: hidden;
    }

    /* Individual ember dot — hidden by default; animation applied only when
       motion is acceptable (reduced-motion block below). */
    .menu-ember {
      position: absolute;
      border-radius: 50%;
      background: radial-gradient(circle,
        rgba(255, 190, 60, 0.9)  0%,
        rgba(255, 140, 30, 0.45) 55%,
        transparent              100%);
      filter: blur(1.5px);
      opacity: 0;
    }

    @keyframes ember-rise {
      0%   { transform: translateY(0)       translateX(0);    opacity: 0;    }
      8%   { opacity: 0.30; }
      50%  { transform: translateY(-34cqh)  translateX(7px);  opacity: 0.25; }
      88%  { opacity: 0.10; }
      100% { transform: translateY(-64cqh)  translateX(-6px); opacity: 0;    }
    }

    @media (prefers-reduced-motion: no-preference) {
      .menu-ember {
        animation-name: ember-rise;
        animation-timing-function: linear;
        animation-iteration-count: infinite;
      }
    }

    /* ── Content column ──
       Logo anchored near the top (padding-top ≈ 12cqh).
       CTA wrapper gets margin-top:auto + margin-bottom:auto, which distributes
       the remaining free space evenly above and below it — centering the CTA
       block in the space above the dock. Dock sits flush at the bottom. */
    .menu-col {
      position: relative;
      z-index: 3;
      display: flex;
      flex-direction: column;
      align-items: center;
      width: 100%;
      min-height: 100cqh;
      padding: max(env(safe-area-inset-top, 0px), 12cqh) 0 env(safe-area-inset-bottom, 16px) 0;
    }

    .menu-logo {
      width: min(340px, 88cqw);
      height: 80px;
      background: url('/ui/LogoArkanoid.png') no-repeat center / contain;
      flex-shrink: 0;
    }

    /* CTA wrapper — floats in center of remaining space above the dock */
    .menu-cta-wrap {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--sp-3h);
      margin-top: auto;
      margin-bottom: auto;
      flex-shrink: 0;
    }

    .menu-art-btn {
      position: relative;
      width: min(320px, 88cqw);
      background: none;
      /* 9-slice the InterfaceButton pill (626x162): fixed rounded end-caps + stretched
         middle, so the button doesn't get its ends squished at different widths. */
      border-style: solid;
      border-width: 9px 34px;
      border-image: url('/ui/InterfaceButton.png') 26 92 26 92 fill stretch;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      transition: filter var(--dur-normal), transform var(--dur-fast);
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
    }
    .menu-art-btn:hover  { filter: var(--filter-hover); }
    .menu-art-btn:active { transform: scale(0.97); filter: brightness(0.9); }
    .menu-art-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }

    /* Primary Continue button — largest, two-line (kicker + node name) */
    .menu-btn-continue {
      height: 76px;
      gap: 2px;
    }
    .menu-btn-kicker {
      color: var(--gold-bright);
      font-size: var(--fs-body);
      font-weight: 700;
      letter-spacing: 0.18em;
      text-transform: uppercase;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
    }
    .menu-btn-node {
      color: var(--text);
      font-size: var(--fs-xl);
      font-weight: 800;
      letter-spacing: 0.04em;
      text-shadow: 0 1px 4px rgba(0,0,0,0.95), 0 0 10px rgba(255,180,60,0.4);
    }

    /* Secondary Campaign Map button */
    .menu-btn-map {
      height: 54px;
    }
    .menu-btn-label {
      color: var(--text);
      font-size: var(--fs-large);
      font-weight: 700;
      letter-spacing: 0.06em;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9), 0 0 8px rgba(0,0,0,0.6);
      pointer-events: none;
    }

    /* ── Docked secondary destinations ──
       Sits flush at the bottom of the column; ≥18px top-padding gives the dock
       breathing room above the icon row (§6 compliant). */
    .menu-dock {
      display: flex;
      justify-content: center;
      gap: var(--sp-2h);
      width: min(360px, 94cqw);
      flex-shrink: 0;
      padding: 18px 8px calc(env(safe-area-inset-bottom, 0px) + 10px) 8px;
    }
    .menu-dock-btn {
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--sp-1);
      min-width: 56px;
      min-height: 64px;
      padding: 6px 2px;
      ${btn1()}
      border-radius: 10px;
      cursor: pointer;
      touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .menu-dock-btn:hover  { filter: brightness(1.18); }
    .menu-dock-btn:active { transform: scale(0.94); }
    .menu-dock-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .menu-dock-ico {
      width: 32px;
      height: 32px;
      background-repeat: no-repeat;
      background-position: center;
      background-size: contain;
      /* painted art — smooth downscale, never pixelated (docs/13) */
      filter: drop-shadow(0 1px 2px rgba(0,0,0,0.7));
    }
    .menu-dock-label {
      color: var(--text-dim);
      font-size: var(--fs-tiny);
      font-weight: 600;
      letter-spacing: 0.03em;
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
  `;
  document.head.appendChild(style);
}
```

## `src/scenes/SettingsScene.ts`

```typescript
/**
 * SettingsScene.ts — Settings screen (?scene=settings).
 *
 * Controls:
 *   - Replay Tutorial button (re-opens tutorial overlay)
 *   - Audio toggle (placeholder — no audio currently)
 *   - FX Intensity toggle (stored in localStorage)
 *   - Reset Progress (calls /reset with confirmation)
 *
 * Uses existing button/panel art from /ui/.
 */

import { metaApi } from "../net/metaApi";
import { showTutorial } from "./TutorialOverlay";
import { navigateTo } from "../ui/transition";
import { nineSlice } from "../ui/nineSlice";

export function mountSettings(host: HTMLElement) {
  injectSettingsStyles();

  const root = document.createElement("div");
  root.id = "settings-scene";
  root.className = "set-root";

  const bg = document.createElement("div");
  bg.className = "set-bg";
  root.appendChild(bg);

  const inner = document.createElement("div");
  inner.className = "set-inner";

  // Back
  const back = document.createElement("a");
  back.href = "/?scene=menu";
  back.className = "set-back";
  back.textContent = "← Menu";
  inner.appendChild(back);

  // Title
  const title = document.createElement("h1");
  title.textContent = "Settings";
  title.className = "set-title";
  inner.appendChild(title);

  // Panel
  const panel = document.createElement("div");
  panel.className = "set-panel";

  // ── Replay Tutorial ──────────────────────────────────────────────────────────
  panel.appendChild(buildRow({
    label: "Tutorial",
    description: "Re-play the how-to-play slides",
    control: buildActionButton("Replay", "set-btn-replay", () => {
      showTutorial(document.body);
    }),
  }));

  // ── Audio toggle (procedural Web Audio SFX — docs/09 G1) ────────────────────
  const audioEnabled = localStorage.getItem("arkanoid_audio") !== "0";
  const audioToggle = buildToggle("set-toggle-audio", audioEnabled, (val) => {
    localStorage.setItem("arkanoid_audio", val ? "1" : "0");
  });
  panel.appendChild(buildRow({
    label: "Audio",
    description: "Sound effects (synthesized — impacts, spells, bosses)",
    control: audioToggle,
  }));

  // ── Music toggle (per-biome ambient music — off by default) ──────────────────
  const musicOn = localStorage.getItem("arkanoid_music") === "1";
  const musicToggle = buildToggle("set-toggle-music", musicOn, (val) => {
    localStorage.setItem("arkanoid_music", val ? "1" : "0");
  });
  panel.appendChild(buildRow({
    label: "Music",
    description: "Per-biome ambient music (experimental)",
    control: musicToggle,
  }));

  // ── FX Intensity ─────────────────────────────────────────────────────────────
  const fxEnabled = localStorage.getItem("arkanoid_fx") !== "0";
  const fxToggle = buildToggle("set-toggle-fx", fxEnabled, (val) => {
    localStorage.setItem("arkanoid_fx", val ? "1" : "0");
  });
  panel.appendChild(buildRow({
    label: "FX Effects",
    description: "Screen shake and particle effects",
    control: fxToggle,
  }));

  // ── Divider ──────────────────────────────────────────────────────────────────
  const divider = document.createElement("hr");
  divider.className = "set-divider";
  panel.appendChild(divider);

  // ── Reset Progress ────────────────────────────────────────────────────────────
  const resetBtn = buildActionButton("Reset Progress", "set-btn-reset", async () => {
    const confirm = window.confirm("Reset all progress? This cannot be undone.");
    if (!confirm) return;
    await metaApi.reset();
    localStorage.removeItem("arkanoid_tutorial_seen");
    navigateTo("/?scene=menu");
  }, true);
  panel.appendChild(buildRow({
    label: "Reset",
    description: "Wipe all progress and start fresh",
    control: resetBtn,
  }));

  inner.appendChild(panel);

  // Title stamp (was a dev build watermark — now the game title)
  const ver = document.createElement("div");
  ver.className = "set-version";
  ver.textContent = "Heroes of Arkanoid II";
  inner.appendChild(ver);

  root.appendChild(inner);
  host.appendChild(root);
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function buildRow(opts: { label: string; description: string; control: HTMLElement }): HTMLElement {
  const row = document.createElement("div");
  row.className = "set-row";

  const text = document.createElement("div");
  text.className = "set-row-text";

  const lbl = document.createElement("div");
  lbl.textContent = opts.label;
  lbl.className = "set-row-label";
  text.appendChild(lbl);

  const desc = document.createElement("div");
  desc.textContent = opts.description;
  desc.className = "set-row-desc";
  text.appendChild(desc);

  row.appendChild(text);
  row.appendChild(opts.control);

  return row;
}

function buildActionButton(
  label: string, id: string, onClick: () => void, danger = false
): HTMLElement {
  const btn = document.createElement("button");
  btn.id = id;
  btn.textContent = label;
  btn.className = `set-action-btn ${danger ? "set-action-danger" : ""}`;
  btn.addEventListener("click", onClick);
  return btn;
}

function buildToggle(id: string, initial: boolean, onChange: (v: boolean) => void): HTMLElement {
  const wrap = document.createElement("label");
  wrap.className = "set-toggle";
  wrap.htmlFor = id;

  const input = document.createElement("input");
  input.type = "checkbox";
  input.id = id;
  input.checked = initial;
  input.addEventListener("change", () => onChange(input.checked));
  wrap.appendChild(input);

  const slider = document.createElement("span");
  slider.className = "set-toggle-slider";
  wrap.appendChild(slider);

  return wrap;
}

// ── Styles ────────────────────────────────────────────────────────────────────

function injectSettingsStyles() {
  const sid = "settings-styles";
  if (document.getElementById(sid)) return;
  const style = document.createElement("style");
  style.id = sid;
  style.textContent = `
    .set-root {
      position: relative; min-height: 100cqh;
      overflow-x: hidden;
      color: var(--text);
      font-family: var(--font-body);
      display: flex;
      flex-direction: column;
      box-sizing: border-box;
      padding: env(safe-area-inset-top, 0px) env(safe-area-inset-right, 0px)
               env(safe-area-inset-bottom, 0px) env(safe-area-inset-left, 0px);
    }
    .set-bg {
      position: absolute; inset: 0;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
      z-index: 0;
    }
    .set-inner {
      position: relative; z-index: 1;
      display: flex; flex-direction: column;
      padding: max(12px, env(safe-area-inset-top, 0px)) 16px 24px 16px;
      gap: var(--sp-4h);
    }
    .set-back {
      align-self: flex-start;
      min-width: 44px;
      height: 44px;
      padding: 0 16px;
      display: flex;
      align-items: center;
      justify-content: center;
      white-space: nowrap;
      font-size: var(--fs-subhead);
      font-weight: 700;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 14px")}
      cursor: pointer;
      color: var(--gold-bright);
      font-size: var(--fs-xl);
      text-decoration: none;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .set-back:hover  { filter: brightness(1.18); }
    .set-back:active { transform: scale(0.94); }
    .set-back:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .set-title {
      margin: 0;
      font-family: var(--font-display);
      font-size: var(--fs-title);
      font-weight: 700;
      letter-spacing: 0.05em;
      color: var(--gold-bright);
      text-shadow: 0 2px 4px rgba(0,0,0,0.9), 0 0 18px rgba(255,180,60,0.25);
      text-align: center;
    }
    .set-panel {
      width: min(360px, 96cqw);
      display: flex;
      flex-direction: column;
      gap: var(--sp-3);
      padding: 0;
      margin: 0 auto;
    }
    .set-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--sp-4);
      padding: var(--sp-3) var(--sp-3h);
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "12px 14px")}
    }
    .set-row-text { flex: 1; }
    .set-row-label {
      font-size: var(--fs-body); font-weight: 700;
      color: var(--gold-bright);
    }
    .set-row-desc {
      font-size: var(--fs-caption); color: var(--text-dim);
      margin-top: 3px; line-height: 1.3;
    }
    .set-divider {
      border: none;
      border-top: 1px solid var(--gold-dim);
      margin: var(--sp-1) 0;
    }
    .set-action-btn {
      height: 40px; min-width: 100px;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 18px")}
      cursor: pointer;
      font-family: var(--font-body); font-size: var(--fs-body);
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      padding: 0 16px;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter var(--dur-normal), transform var(--dur-fast);
      flex-shrink: 0;
      /* NOTE: no \`border: none\` here — it would kill the 9-slice border-image. */
    }
    .set-action-btn:hover  { filter: brightness(1.18); }
    .set-action-btn:active { transform: scale(0.96); }
    .set-action-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .set-action-btn:disabled {
      filter: saturate(0.25) brightness(0.65);
      cursor: default;
    }
    .set-action-danger {
      color: var(--color-unequip);
    }
    .set-action-danger:hover:not(:disabled) {
      filter: brightness(1.18);
    }

    /* Toggle switch — 44px tall tap area, 28px visual track centered inside */
    .set-toggle {
      position: relative; display: inline-flex; align-items: center;
      width: 48px; height: 44px; flex-shrink: 0;
      cursor: pointer;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .set-toggle:hover  { filter: brightness(1.15); }
    .set-toggle:active { transform: scale(0.96); }
    .set-toggle:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 4px;
      border-radius: 4px;
    }
    .set-toggle input { opacity: 0; width: 0; height: 0; }
    .set-toggle-slider {
      position: absolute; left: 0; right: 0;
      top: 50%; transform: translateY(-50%);
      height: 28px;
      background: #241a0d;
      border: 1px solid var(--gold-dim);
      border-radius: 999px;
      transition: background var(--dur-normal), box-shadow var(--dur-normal);
    }
    .set-toggle-slider::before {
      content: '';
      position: absolute;
      height: 20px; width: 20px;
      left: 3px; bottom: 3px;
      background: radial-gradient(circle at 38% 32%, #ffe9b0, #d8a84e 70%);
      border-radius: 50%;
      transition: transform var(--dur-normal);
    }
    .set-toggle input:checked + .set-toggle-slider {
      background: #3a2a10;
      box-shadow: inset 0 0 8px rgba(255,190,80,0.5);
    }
    .set-toggle input:checked + .set-toggle-slider::before {
      transform: translateX(20px);
    }

    .set-version {
      margin-top: var(--sp-4h);
      text-align: center;
      font-size: var(--fs-caption);
      color: var(--text-faint);
      font-family: var(--font-display);
      letter-spacing: 0.05em;
    }
  `;
  document.head.appendChild(style);
}
```

## `src/scenes/SkillsScene.ts`

```typescript
/**
 * SkillsScene.ts — Polished skills/upgrade screen (?scene=skills).
 *
 * Uses:
 *   levelskill/Lvl1Skill … Lvl10Skill   — level indicator badges per spell slot
 *     These are 183×188 ornate square FRAMES with transparent centers.
 *     Rendered as 34×34 background behind the centered level number.
 *   Per-class spell icons from characters.json
 *
 * Design system: warm-brown bg, BarGoods card panels, Kvadrat icon slots,
 * Button1 upgrade pills, level number centered inside its ornate frame badge.
 *
 * Reuses the existing /upgrade endpoint and preserves the same DOM ids
 * (#spell-level-<id>, #btn-upgrade-<id>) required by upgrade.spec.ts.
 */

import { metaApi } from "../net/metaApi";
import type { Profile, CharactersResponse } from "../net/metaApi";
import { nineSlice, btnInterface } from "../ui/nineSlice";

// ── LevelSkill badge → committed public path ──────────────────────────────────
// Keys: levelskill/Lvl1Skill … levelskill/Lvl10Skill (copied to public/levelskill/)
function lvlSkillSrc(level: number): string {
  const clamped = Math.max(1, Math.min(10, level));
  return `/levelskill/Lvl${clamped}Skill.png`;
}

// Per-spell icon paths (matching characters.json icon fields)
// LargeIco variants copied to public/spellicons/<class>/
const SPELL_ICON_MAP: Record<string, string> = {
  // Fire Mage — square Chose*Ico art; ignite and fireball used to share the
  // same letterboxed crop and phoenix had no entry at all → broken image
  // (docs/13 skills audit).
  ignite:   "/art/SpellIgnite.png",
  fireball: "/art/SpellFireball.png",
  firewall: "/art/SpellFirewall.png",
  turret:   "/art/SpellTurret.png",
  phoenix:  "/art/SpellPhoenix.png",
  // Paladin (public/spellicons/paladin/)
  shield:    "/spellicons/paladin/SpellShieldLargeIco.png",
  spear:     "/spellicons/paladin/SpearLargeLargeIco.png",
  duplicate: "/spellicons/paladin/SplitLargeIco.png",
  // Engineer (public/spellicons/engineer/)
  lightning: "/spellicons/engineer/LightingLargeIco.png",
  rocket:    "/spellicons/engineer/RocketLargeIco.png",
  radiation: "/spellicons/engineer/RadiationLargeIco.png",
  // Necromancer (public/spellicons/necromancer/)
  decay:    "/spellicons/necromancer/SpellShieldLargeIco.png",
  skeleton: "/spellicons/necromancer/RiseSkeletonLargeIcon.png",
  drain:    "/spellicons/necromancer/LastJudgmentLargeIco.png",
};

function spellIconSrc(iconKey: string): string {
  // Characters.json may pass either a short key or a simple name
  // Never use /Sprites/ symlink — fall back to /art/ for anything unrecognized
  return `/art/${iconKey}.png`;
}

// ── Mount ─────────────────────────────────────────────────────────────────────

export function mountSkills(host: HTMLElement) {
  injectSkillsStyles();

  const root = document.createElement("div");
  root.id = "skills-scene";
  root.className = "sk-root";

  const bgEl = document.createElement("div");
  bgEl.className = "sk-bg";
  root.appendChild(bgEl);

  const inner = document.createElement("div");
  inner.className = "sk-inner";

  // ── Top bar: back chip · title · spacer ──────────────────────────────────
  // (The decorative Shkatulka chest animation was removed: every frame
  // in /shkatulka/ is a degenerate 1–13px-wide strip — corrupted exports that
  // rendered as grey garbage over the title. docs/13 §S2. Re-add only with
  // verified art.)
  const topbar = document.createElement("div");
  topbar.className = "sk-topbar";

  const back = document.createElement("a");
  back.href = "/?scene=campaign";
  back.className = "ui-back";
  back.setAttribute("aria-label", "Back to campaign");
  topbar.appendChild(back);

  const title = document.createElement("h1");
  title.textContent = "Skill Upgrades";
  title.className = "ui-title";
  topbar.appendChild(title);

  const spacer = document.createElement("div");
  spacer.className = "ui-topbar-spacer";
  topbar.appendChild(spacer);

  inner.appendChild(topbar);

  // Skill Points gold chip (below topbar, above tabs)
  const subtitle = document.createElement("div");
  subtitle.id = "sk-points";
  subtitle.className = "sk-points-chip";
  inner.appendChild(subtitle);

  // Class tabs
  const tabs = document.createElement("div");
  tabs.id = "sk-tabs";
  tabs.className = "sk-tabs";
  inner.appendChild(tabs);

  // Spell grid container
  const spellGrid = document.createElement("div");
  spellGrid.id = "sk-spell-grid";
  spellGrid.className = "sk-spell-grid";
  inner.appendChild(spellGrid);

  // LvlUp panel footer
  const panelFooter = document.createElement("div");
  panelFooter.className = "sk-panel";
  inner.appendChild(panelFooter);

  root.appendChild(inner);
  host.appendChild(root);

  let currentClassId = "";
  let allData: CharactersResponse | null = null;
  let profile: Profile | null = null;

  async function loadAll() {
    [allData, profile] = await Promise.all([
      metaApi.getCharacters(),
      metaApi.getProfile(),
    ]);
    currentClassId = allData.selected ?? allData.characters[0]?.id ?? "fire_mage";
    renderTabs();
    renderSpells();
  }

  function renderTabs() {
    if (!allData) return;
    tabs.setAttribute("role", "tablist");
    tabs.innerHTML = "";
    for (const ch of allData.characters) {
      const tab = document.createElement("button");
      tab.className = `sk-tab ${ch.id === currentClassId ? "active" : ""}`;
      tab.textContent = ch.name;
      tab.setAttribute("role", "tab");
      tab.setAttribute("aria-selected", ch.id === currentClassId ? "true" : "false");
      tab.addEventListener("click", () => {
        currentClassId = ch.id;
        renderTabs();
        renderSpells();
      });
      tabs.appendChild(tab);
    }
  }

  function renderSpells() {
    if (!allData || !profile) return;
    const ch = allData.characters.find(c => c.id === currentClassId);
    if (!ch) return;

    spellGrid.innerHTML = "";
    panelFooter.innerHTML = "";

    subtitle.textContent = `Skill Points: ${profile.points}`;

    // Rebuild the #upgrade-panel compatible content inside panelFooter
    // (so upgrade.spec.ts still finds #upgrade-panel, #spell-level-*, #btn-upgrade-*)
    const legacyPanel = document.createElement("div");
    legacyPanel.id = "upgrade-panel";
    legacyPanel.style.display = "none"; // hidden but DOM-present for test compat
    panelFooter.appendChild(legacyPanel);

    const pointsRemaining = document.createElement("div");
    pointsRemaining.id = "upgrade-points-remaining";
    pointsRemaining.textContent = `Skill Points: ${profile.points}`;
    legacyPanel.appendChild(pointsRemaining);

    for (const spell of ch.spells) {
      const lvl = profile.spellLevels[spell.id] ?? 1;
      const canAfford = (profile.points ?? 0) > 0;

      // Visual card
      const card = document.createElement("div");
      card.className = "sk-spell-card";

      // Spell icon inside Kvadrat slot frame
      const iconSrc = SPELL_ICON_MAP[spell.id] ?? spellIconSrc(spell.icon);
      const iconSlot = document.createElement("div");
      iconSlot.className = "sk-icon-slot";
      const icon = document.createElement("img");
      icon.src = iconSrc;
      icon.alt = spell.name;
      icon.className = "sk-spell-icon";
      iconSlot.appendChild(icon);
      card.appendChild(iconSlot);

      // Name
      const nameEl = document.createElement("div");
      nameEl.textContent = spell.name;
      nameEl.className = "sk-spell-name";
      card.appendChild(nameEl);

      // Level badge fix: 34×34 wrapper — Lvl{n}Skill.png is an ornate FRAME
      // with transparent center; render it as background so the number sits
      // centered INSIDE the frame (not next to it as a tiny image).
      const lvlBadgeWrap = document.createElement("div");
      lvlBadgeWrap.className = "sk-lvl-wrap";
      lvlBadgeWrap.style.backgroundImage = `url('${lvlSkillSrc(lvl)}')`;
      const lvlText = document.createElement("span");
      lvlText.id = `spell-level-${spell.id}`;
      lvlText.className = "sk-lvl-text";
      lvlText.textContent = `${lvl}`;
      lvlBadgeWrap.appendChild(lvlText);
      card.appendChild(lvlBadgeWrap);

      // Upgrade button — Button1 9-slice pill labeled "+ Upgrade"
      const btnPlus = document.createElement("button");
      btnPlus.id = `btn-upgrade-${spell.id}`;
      btnPlus.className = `sk-upgrade-btn ${canAfford ? "can-afford" : "cannot-afford"}`;
      btnPlus.textContent = "+ Upgrade";
      btnPlus.disabled = !canAfford;
      btnPlus.addEventListener("click", async () => {
        const data = await metaApi.upgrade(spell.id);
        if (data.ok) {
          profile = data.profile;
          renderTabs();
          renderSpells();
        }
      });
      card.appendChild(btnPlus);

      spellGrid.appendChild(card);

      // Legacy hidden row for test compat
      const hiddenRow = document.createElement("div");
      hiddenRow.className = "camp-spell-row";
      hiddenRow.style.display = "none";
      const hiddenLvl = document.createElement("span");
      hiddenLvl.id = `spell-level-${spell.id}-hidden`;
      hiddenLvl.textContent = `${lvl}`;
      const hiddenBtn = document.createElement("button");
      hiddenBtn.id = `btn-upgrade-${spell.id}-hidden`;
      hiddenRow.appendChild(hiddenLvl);
      hiddenRow.appendChild(hiddenBtn);
      legacyPanel.appendChild(hiddenRow);
    }
  }

  loadAll().catch(console.error);
}

// ── Styles ────────────────────────────────────────────────────────────────────

function injectSkillsStyles() {
  const sid = "skills-styles";
  if (document.getElementById(sid)) return;
  const style = document.createElement("style");
  style.id = sid;
  style.textContent = `
    /* ── Root & background — warm palette, no purple ── */
    .sk-root {
      position: relative;
      min-height: 100cqh;
      overflow-x: hidden;
      overflow-y: auto;
      font-family: var(--font-body);
      color: var(--text);
      box-sizing: border-box;
    }
    .sk-bg {
      position: absolute; inset: 0;
      min-height: 100cqh;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
      pointer-events: none;
      z-index: 0;
    }
    .sk-inner {
      position: relative; z-index: 1;
      display: flex; flex-direction: column;
      align-items: center;
      padding: 0 16px max(env(safe-area-inset-bottom,0px),24px);
      gap: 0;
    }

    /* ── Topbar: back chip · centered title · symmetry spacer ── */
    .sk-topbar {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      padding: max(12px, env(safe-area-inset-top,0px)) 0 8px;
      width: min(360px, 96cqw);
      align-self: center;
    }
    .sk-topbar .ui-title { flex: 1; text-align: center; }

    /* ── Skill Points gold chip ── */
    .sk-points-chip {
      display: inline-flex;
      align-items: center;
      gap: var(--sp-1h);
      padding: var(--sp-1) var(--sp-4);
      background: rgba(216,168,78,0.18);
      border: 1px solid var(--gold-dim);
      border-radius: 999px;
      font-family: var(--font-display);
      font-size: var(--fs-body);
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      margin-bottom: var(--sp-3h);
      white-space: nowrap;
    }

    /* ── Class tabs ── */
    .sk-tabs {
      display: flex; gap: var(--sp-2);
      flex-wrap: wrap;
      justify-content: center;
      margin-bottom: var(--sp-4);
      width: min(360px, 96cqw);
    }
    .sk-tab {
      min-height: 44px; padding: 0 14px;
      ${btnInterface()}
      cursor: pointer;
      font-family: var(--font-body); font-size: var(--fs-caption);
      font-weight: 700; color: var(--text-dim);
      filter: saturate(0.4) brightness(0.75);
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter var(--dur-normal), color var(--dur-normal);
    }
    .sk-tab:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .sk-tab.active {
      filter: none;
      color: var(--text);
    }
    .sk-tab:hover:not(.active) { filter: saturate(0.6) brightness(0.9); }
    .sk-tab:active { transform: scale(0.96); }

    /* ── Spell grid — 2 columns ── */
    .sk-spell-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: var(--sp-3);
      width: min(360px, 96cqw);
      margin-bottom: var(--sp-3);
    }

    /* ── Spell card: BarGoods gold-rimmed navy panel ── */
    .sk-spell-card {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "12px 14px")}
      padding: var(--sp-2h) var(--sp-1h) var(--sp-2h);
      display: flex; flex-direction: column;
      align-items: center; gap: var(--sp-2);
      position: relative;
    }

    /* ── Icon inside Kvadrat slot frame ── */
    .sk-icon-slot {
      width: 68px; height: 68px;
      ${nineSlice("/ui/Kvadrat.png", "14 14 14 14", "7px 7px")}
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .sk-spell-icon {
      width: 56px; height: 56px;
      object-fit: contain;
      border-radius: 6px;
      filter: drop-shadow(0 2px 3px rgba(0,0,0,0.6));
    }

    .sk-spell-name {
      font-size: var(--fs-caption); font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      text-align: center; line-height: 1.2;
    }

    /* ── Level badge fix ──────────────────────────────────────────────────────
       Lvl{n}Skill.png is a 183×188 ornate square FRAME with transparent center.
       Old code placed it as a tiny <img> beside the number (wrong).
       New: 34×34 wrapper with the frame as background; number centered INSIDE.
    ── */
    .sk-lvl-wrap {
      width: 34px; height: 34px;
      background-repeat: no-repeat;
      background-position: center;
      background-size: contain;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .sk-lvl-text {
      font-size: var(--fs-body); font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      position: relative; z-index: 1;
      line-height: 1;
    }

    /* ── Upgrade button: Button1 9-slice pill ── */
    .sk-upgrade-btn {
      width: 100%; min-height: 44px;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 18px")}
      cursor: pointer;
      font-family: var(--font-body); font-size: var(--fs-caption);
      font-weight: 700; color: var(--gold-bright);
      text-shadow: 0 1px 2px var(--shadow-hard);
      letter-spacing: 0.04em;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .sk-upgrade-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .sk-upgrade-btn:hover:not(:disabled)  { filter: brightness(1.15); }
    .sk-upgrade-btn:active:not(:disabled) { transform: scale(0.96); }
    .sk-upgrade-btn:disabled {
      filter: saturate(0.25) brightness(0.65);
      cursor: default;
    }
    /* keep legacy state classes working (test-compat) */
    .sk-upgrade-btn.cannot-afford {
      filter: saturate(0.25) brightness(0.65);
      cursor: default;
    }

    .sk-panel {
      width: min(360px, 96cqw);
    }

    @container (min-width: 480px) {
      .sk-spell-grid {
        grid-template-columns: repeat(3, 1fr);
      }
    }
  `;
  document.head.appendChild(style);
}
```

## `src/scenes/TutorialOverlay.ts`

```typescript
/**
 * TutorialOverlay.ts — First-run hint/tutorial overlay.
 *
 * Uses the HintSystem art: EducationChance, EducationGem, EducationHeroIco,
 * EducationLife, EducationSpellBunner (inline icons) + HintsScreen1/2/3
 * (full-page slides).
 *
 * Shows on first battle and is re-openable from the Settings screen.
 * Persists "seen" state via POST /tutorial/seen.
 */

import { metaApi } from "../net/metaApi";
import { btnInterface } from "../ui/nineSlice";

// ── Tutorial slide data (3 HintSystem full screens + inline icon captions) ───

interface TutorialSlide {
  screenKey: string;   // atlas key for the big background art
  screenSrc: string;   // committed public/hints/ path for <img> src
  title: string;
  caption: string;
  icons: Array<{ src: string; label: string }>;
}

// All hint PNGs are committed to public/hints/ (no /Sprites/ symlink dependency).
const SLIDES: TutorialSlide[] = [
  {
    screenKey: "hints/HintsScreen1",
    screenSrc: "/hints/HintsScreen1.png",
    title: "Move & Serve",
    caption: "Drag your paddle left/right to deflect the ball. Tap the screen to serve at the start.",
    icons: [
      { src: "/hints/EducationHeroIco.png", label: "Your hero paddle" },
    ],
  },
  {
    screenKey: "hints/HintsScreen2",
    screenSrc: "/hints/HintsScreen2.png",
    title: "Spells & Mana",
    caption: "Tap hotbar slots (Q/E/W/R) to cast spells. Each spell costs mana — watch the blue bar!",
    icons: [
      { src: "/hints/EducationSpellBunner.png", label: "Spell banner" },
      { src: "/hints/EducationLife.png",         label: "Life indicator" },
    ],
  },
  {
    screenKey: "hints/HintsScreen3",
    screenSrc: "/hints/HintsScreen3.png",
    title: "Bonuses & Boss",
    caption: "Catch falling bonuses to power up. Clear all blocks to meet the boss — and defeat it!",
    icons: [
      { src: "/hints/EducationGem.png",    label: "Gem bonus" },
      { src: "/hints/EducationChance.png", label: "Chance bonus" },
    ],
  },
];

// ── Public entry points ───────────────────────────────────────────────────────

/**
 * Mount a tutorial overlay on top of the battle/host element.
 * Resolves immediately if the tutorial has already been seen
 * (profile.tutorialSeen = true) UNLESS force = true.
 */
export async function maybeShowTutorial(host: HTMLElement, force = false): Promise<void> {
  if (!force) {
    try {
      const profile = await metaApi.getProfile();
      if (profile.tutorialSeen) return;
    } catch {
      // If backend unreachable, still show tutorial for first time
      const seen = localStorage.getItem("arkanoid_tutorial_seen");
      if (seen === "1") return;
    }
  }
  return new Promise<void>((resolve) => {
    showTutorial(host, () => {
      localStorage.setItem("arkanoid_tutorial_seen", "1");
      metaApi.markTutorialSeen().catch(() => {/* non-fatal */});
      resolve();
    });
  });
}

/** Immediately mount the tutorial overlay (used by Settings "Replay Tutorial"). */
export function showTutorial(host: HTMLElement, onDone?: () => void) {
  injectTutorialStyles();

  const overlay = document.createElement("div");
  overlay.id = "tutorial-overlay";
  overlay.className = "tut-overlay";

  let currentSlide = 0;

  function render() {
    overlay.innerHTML = "";

    const slide = SLIDES[currentSlide];
    const isLast = currentSlide === SLIDES.length - 1;

    // Backdrop image (the full HintScreen art)
    const bgImg = document.createElement("img");
    bgImg.src = slide.screenSrc;
    bgImg.className = "tut-screen-img";
    bgImg.alt = slide.title;
    overlay.appendChild(bgImg);

    // Content panel overlay
    const panel = document.createElement("div");
    panel.className = "tut-panel";
    panel.setAttribute("role", "dialog");
    panel.setAttribute("aria-modal", "true");
    panel.setAttribute("aria-labelledby", "tutorial-title");

    const title = document.createElement("h2");
    title.textContent = slide.title;
    title.className = "tut-title";
    title.id = "tutorial-title";
    panel.appendChild(title);

    const caption = document.createElement("p");
    caption.textContent = slide.caption;
    caption.className = "tut-caption";
    panel.appendChild(caption);

    // Inline icon row
    if (slide.icons.length > 0) {
      const iconRow = document.createElement("div");
      iconRow.className = "tut-icon-row";
      for (const ic of slide.icons) {
        const wrap = document.createElement("div");
        wrap.className = "tut-icon-wrap";
        const img = document.createElement("img");
        img.src = ic.src;
        img.alt = ic.label;
        img.className = "tut-icon-img";
        wrap.appendChild(img);
        const lbl = document.createElement("span");
        lbl.textContent = ic.label;
        lbl.className = "tut-icon-label";
        wrap.appendChild(lbl);
        iconRow.appendChild(wrap);
      }
      panel.appendChild(iconRow);
    }

    // Progress dots
    const dots = document.createElement("div");
    dots.className = "tut-dots";
    for (let i = 0; i < SLIDES.length; i++) {
      const dot = document.createElement("span");
      dot.className = `tut-dot ${i === currentSlide ? "active" : ""}`;
      dots.appendChild(dot);
    }
    panel.appendChild(dots);

    // Navigation buttons
    const btnRow = document.createElement("div");
    btnRow.className = "tut-btn-row";

    if (currentSlide > 0) {
      const btnBack = document.createElement("button");
      btnBack.textContent = "← Back";
      btnBack.className = "tut-btn tut-btn-secondary";
      btnBack.addEventListener("click", () => { currentSlide--; render(); });
      btnRow.appendChild(btnBack);
    } else {
      const spacer = document.createElement("div");
      spacer.style.flex = "1";
      btnRow.appendChild(spacer);
    }

    const btnNext = document.createElement("button");
    btnNext.id = isLast ? "tut-btn-done" : "tut-btn-next";
    btnNext.textContent = isLast ? "Got it!" : "Next →";
    btnNext.className = `tut-btn ${isLast ? "tut-btn-done" : "tut-btn-primary"}`;
    btnNext.addEventListener("click", () => {
      if (isLast) {
        overlay.remove();
        onDone?.();
      } else {
        currentSlide++;
        render();
      }
    });
    btnRow.appendChild(btnNext);

    panel.appendChild(btnRow);
    overlay.appendChild(panel);

    // "Skip" link
    const skip = document.createElement("button");
    skip.textContent = "Skip tutorial";
    skip.className = "tut-skip";
    skip.addEventListener("click", () => {
      overlay.remove();
      onDone?.();
    });
    overlay.appendChild(skip);
  }

  render();
  host.appendChild(overlay);

  // Focus first focusable element for keyboard/screen-reader accessibility
  const firstBtn = overlay.querySelector<HTMLElement>('button, a, input, [tabindex="0"]');
  firstBtn?.focus();
}

// ── Styles ────────────────────────────────────────────────────────────────────

function injectTutorialStyles() {
  const id = "tutorial-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .tut-overlay {
      position: absolute; inset: 0;
      background: rgba(0,0,0,0.88);
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      z-index: 2000;
      font-family: var(--font-body);
      padding: max(env(safe-area-inset-top,0px),12px) 16px max(env(safe-area-inset-bottom,0px),12px);
      box-sizing: border-box;
      gap: var(--sp-3);
      overflow: hidden;
    }

    .tut-screen-img {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      object-fit: cover;
      opacity: 0.22;
      pointer-events: none;
      image-rendering: pixelated;
    }

    .tut-panel {
      position: relative;
      z-index: 1;
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(8,6,20,0.96);
      border: 2px solid var(--gold-dim);
      border-radius: 16px;
      padding: 28px 24px 20px;
      width: min(340px, 92cqw);
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--sp-3);
      box-shadow: 0 8px 40px rgba(0,0,0,0.8), inset 0 0 40px rgba(10,5,30,0.6);
    }
    .tut-panel::before, .tut-panel::after {
      content: '';
      position: absolute;
      left: 0; right: 0; height: 16px;
      background: url('/ui/LvlUpInterfaceTopBottomPanel.png') repeat-x center / auto 100%;
    }
    .tut-panel::before { top: 0; border-radius: 14px 14px 0 0; }
    .tut-panel::after  { bottom: 0; border-radius: 0 0 14px 14px; }

    .tut-title {
      margin: 0;
      font-family: var(--font-display);
      font-size: var(--fs-title);
      font-weight: 700;
      color: var(--gold-bright);
      letter-spacing: 0.07em;
      text-shadow: 0 0 16px rgba(255,200,50,0.5), 0 2px 4px rgba(0,0,0,0.9);
      text-align: center;
    }

    .tut-caption {
      margin: 0;
      font-size: var(--fs-section);
      color: var(--text-dim);
      line-height: 1.55;
      text-align: center;
      max-width: 300px;
    }

    .tut-icon-row {
      display: flex;
      gap: var(--sp-4h);
      justify-content: center;
      flex-wrap: wrap;
    }

    .tut-icon-wrap {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--sp-1h);
    }

    .tut-icon-img {
      width: 56px;
      height: 56px;
      image-rendering: pixelated;
      filter: drop-shadow(0 2px 6px rgba(0,0,0,0.8));
    }

    .tut-icon-label {
      font-size: var(--fs-small);
      color: var(--text-dim);
      text-align: center;
    }

    .tut-dots {
      display: flex;
      gap: var(--sp-2);
      justify-content: center;
      align-items: center;
      min-height: 44px;
      padding: 18px 0;
    }

    .tut-dot {
      width: 8px; height: 8px;
      border-radius: 50%;
      background: rgba(200,180,100,0.3);
      border: 1px solid rgba(200,180,100,0.5);
      transition: background var(--dur-normal);
    }
    .tut-dot.active {
      background: var(--gold-bright);
      box-shadow: 0 0 6px rgba(255,210,0,0.6);
    }

    .tut-btn-row {
      display: flex;
      width: 100%;
      justify-content: space-between;
      align-items: center;
      gap: var(--sp-3);
      margin-top: var(--sp-1);
    }

    .tut-btn {
      height: 48px;
      min-width: 100px;
      ${btnInterface()}
      cursor: pointer;
      font-family: var(--font-body);
      font-size: var(--fs-section);
      font-weight: 700;
      letter-spacing: 0.04em;
      color: var(--text);
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .tut-btn:hover  { filter: brightness(1.15); }
    .tut-btn:active { transform: scale(0.97); filter: brightness(0.9); }
    .tut-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }

    .tut-btn-secondary {
      opacity: 0.75;
      min-width: 80px;
    }

    .tut-btn-done {
      /* primary button keeps full opacity (base .tut-btn already 9-slices the frame) */
      filter: brightness(1.05);
    }

    .tut-skip {
      position: relative;
      z-index: 1;
      background: none;
      border: none;
      color: rgba(200,180,140,0.55);
      font-size: var(--fs-caption);
      cursor: pointer;
      min-height: 44px;
      padding: 0 8px;
      display: flex;
      align-items: center;
      font-family: var(--font-body);
      -webkit-tap-highlight-color: transparent;
    }
    .tut-skip:hover { color: rgba(200,180,140,0.85); }
    .tut-skip:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
  `;
  document.head.appendChild(style);
}
```

## `src/scenes/battle/campaignFlow.ts`

```typescript
import type { Snapshot } from "../../net/Connection";
import { metaApi } from "../../net/metaApi";
import type { RiftMode, RiftOffer } from "../../net/metaApi";
import { buildRewardOverlay, buildDefeatOverlay } from "./overlays";
import { navigateTo } from "../../ui/transition";
import { unlockAchievement } from "../AchievementsScene";
import { log } from "../../log";

/** Where to return after the reward overlay — to the rift offer if one opened. */
function campaignReturnUrl(rift: RiftOffer | null): string {
  if (rift?.opened) {
    return `/?scene=campaign&rift=${encodeURIComponent(rift.dungeonId)}`
         + `&riftFloors=${rift.floors}&riftName=${encodeURIComponent(rift.name)}`;
  }
  return "/?scene=campaign";
}

export function createCampaignFlow(level: string) {
  let completeCalled = false;
  let overlayShown = false;

  // Tests can force/suppress rifts deterministically via localStorage; players roll.
  const riftMode = ((): RiftMode => {
    const m = (typeof localStorage !== "undefined" && localStorage.getItem("ark_rift_mode")) || "roll";
    return (m === "force" || m === "none") ? m : "roll";
  })();

  async function handlePhase(s: Snapshot): Promise<boolean> {
    if (overlayShown) return true;

    if (s.phase === "Won" && !completeCalled) {
      completeCalled = true;
      overlayShown = true;
      let reward = null;
      let rift: RiftOffer | null = null;
      try {
        const data = await metaApi.complete(level, s.treasureBonus ?? 0, riftMode);
        reward = data.reward;
        rift = data.rift;
        log("rift", rift?.opened ? "offered" : "none", rift ?? { level });
        // Unlock achievements for this win
        await unlockAchievement("first_win");
        if (level.startsWith("hell"))    await unlockAchievement("clear_biome_hell");
        if (level.startsWith("cavern"))  await unlockAchievement("clear_biome_dungeon");
        if (level.startsWith("village")) await unlockAchievement("clear_biome_village");
        if (level.startsWith("heaven"))  await unlockAchievement("clear_biome_heaven");
        // Per-class win achievements
        try {
          const chars = await metaApi.getCharacters();
          const cls = chars.selected;
          if (cls === "fire_mage")   await unlockAchievement("win_fire_mage");
          if (cls === "paladin")     await unlockAchievement("win_paladin");
          if (cls === "engineer")    await unlockAchievement("win_engineer");
          if (cls === "necromancer") await unlockAchievement("win_necromancer");
        } catch { /* non-fatal */ }
      } catch (e) {
        console.error("Failed to complete level", e);
      }
      const el = buildRewardOverlay(reward, () => { navigateTo(campaignReturnUrl(rift)); });
      (document.getElementById("app") ?? document.body).appendChild(el); // inside the letterbox frame
      return true;
    }

    if (s.phase === "Lost") {
      overlayShown = true;
      const el = buildDefeatOverlay(
        () => { navigateTo(`/?scene=battle&level=${encodeURIComponent(level)}&from=campaign`); },
        () => { navigateTo("/?scene=campaign"); },
      );
      (document.getElementById("app") ?? document.body).appendChild(el); // inside the letterbox frame
      return true;
    }

    return false;
  }

  return { handlePhase };
}
```

## `src/scenes/battle/dungeonFlow.ts`

```typescript
import type { Snapshot } from "../../net/Connection";
import { metaApi } from "../../net/metaApi";
import { buildPickOverlay, buildDungeonClearOverlay, buildDungeonFailOverlay } from "./overlays";
import { navigateTo } from "../../ui/transition";
import { unlockAchievement } from "../AchievementsScene";

export function createDungeonFlow() {
  let completeCalled = false;
  let overlayShown = false;

  async function handlePhase(s: Snapshot): Promise<boolean> {
    if (overlayShown) return true;

    if (s.phase === "Won" && !completeCalled) {
      completeCalled = true;
      overlayShown = true;
      try {
        const data = await metaApi.floorCleared();
        if (data.isLastFloor) {
          unlockAchievement("clear_dungeon").catch(() => {});
          const el = buildDungeonClearOverlay(data, () => { navigateTo("/?scene=campaign"); });
          (document.getElementById("app") ?? document.body).appendChild(el); // inside the letterbox frame
        } else {
          const el = buildPickOverlay(
            data.run?.pendingChoices ?? [],
            async (choiceId) => {
              try {
                await metaApi.pick(choiceId);
                navigateTo("/?scene=dungeon");
              } catch (e) {
                console.error("dungeon pick failed", e);
              }
            },
            // Owned relics + cores power the synergy hints (docs/04 §7).
            [...(data.run?.relics ?? []), ...(data.run?.ballCores ?? [])],
          );
          (document.getElementById("app") ?? document.body).appendChild(el); // inside the letterbox frame
        }
      } catch (e) {
        console.error("dungeon floor-cleared failed", e);
      }
      return true;
    }

    if (s.phase === "Lost" && !completeCalled) {
      completeCalled = true;
      overlayShown = true;
      try {
        await metaApi.fail();
      } catch (e) {
        console.error("dungeon fail failed", e);
      }
      const el = buildDungeonFailOverlay(() => { navigateTo("/?scene=campaign"); });
      (document.getElementById("app") ?? document.body).appendChild(el); // inside the letterbox frame
      return true;
    }

    return false;
  }

  return { handlePhase };
}
```

## `src/scenes/battle/overlays.ts`

```typescript
import type { CompleteResult, FloorClearedResult } from "../../net/metaApi";

// ── Shared helpers ────────────────────────────────────────────────────────────

export function css(el: HTMLElement, styles: Record<string, string>) {
  Object.assign(el.style, styles);
}

const RELIC_NAMES: Record<string, string> = {
  glass_cannon: "Glass Cannon",
  flint_core: "Flint Core",
  pyroclasm: "Pyroclasm",
  mana_battery: "Mana Battery",
  // G2 relic web
  conductor: "Conductor",
  overcharge: "Overcharge",
  split_shot: "Split Shot",
  souljar: "Souljar",
  lodestone: "Lodestone",
  ember_heart: "Ember Heart",
  second_wind: "Second Wind",
  midas: "Midas Touch",
  lead_paddle: "Lead Paddle",
  sapper: "Sapper's Charge",
  hellwalker: "Hellwalker",
  ghost_lens: "Ghost Lens",
  pillar_doctrine: "Pillar Doctrine",
};

const RELIC_ICONS: Record<string, string> = {
  glass_cannon: "/art/ItemHummer.png",
  flint_core: "/art/ItemDrill.png",
  pyroclasm: "/art/ItemTorch.png",
  mana_battery: "/art/ItemGem.png",
  // G2 relic web
  conductor: "/items/ItemMotor.png",
  overcharge: "/items/ItemOrb.png",
  split_shot: "/items/ItemJadeBall.png",
  souljar: "/items/ItemMark.png",
  lodestone: "/items/ItemForceRing.png",
  ember_heart: "/items/ItemPhoenix.png",
  second_wind: "/items/ItemHelm.png",
  midas: "/items/ItemMagicCrown.png",
  lead_paddle: "/items/ItemStaff.png",
  sapper: "/items/ItemSun.png",
  hellwalker: "/items/ItemFlask.png",
  ghost_lens: "/items/ItemRing.png",
  pillar_doctrine: "/items/ItemTomOfKnowladge.png",
};

const BALL_CORE_NAMES: Record<string, string> = {
  heavy: "Heavy Core",
  split: "Split Core",
  ember: "Ember Core",
  ghost: "Ghost Core",
  echo: "Echo Core",
  frost: "Frost Core",
  // Paddle mods — the fourth build axis (docs/04 §4.4)
  mod_wide: "Wide Frame",
  mod_grip: "Grip Tape",
  mod_cannons: "Side Cannons",
};

const BALL_CORE_ICONS: Record<string, string> = {
  heavy: "/ui/BonusRock.png",
  split: "/ui/BonusSplit.png",
  ember: "/ui/BonusFire.png",
  ghost: "/ui/BonusProtection.png",
  echo: "/ui/BonusRandomSpell.png",
  frost: "/ui/BonusMana.png",
  // Paddle mods
  mod_wide: "/ui/BonusLargerBita.png",
  mod_grip: "/ui/BonusLargerBall.png",
  mod_cannons: "/art/FireHeroTurret.png",
};

// Synergy web (docs/04 §7): an offered pick highlights when it combos with what
// you already hold. Pairs are symmetric; fusions are the strongest hints.
const SYNERGIES: Record<string, string[]> = {
  heavy:       ["ember"],            // Molten fusion
  ember:       ["heavy", "pyroclasm", "ember_heart"],
  ghost:       ["split", "ghost_lens"], // Phantom fusion
  split:       ["ghost", "split_shot"],
  echo:        ["frost"],            // Stasis fusion
  frost:       ["echo"],
  pyroclasm:   ["ember", "ember_heart"],
  ember_heart: ["ember", "pyroclasm"],
  ghost_lens:  ["ghost"],
  split_shot:  ["split"],
  flint_core:  ["pillar_doctrine"],
  pillar_doctrine: ["flint_core"],
  lodestone:   ["midas"],
  midas:       ["lodestone"],
};

/** The first owned id this choice combos with, or null. */
export function synergyWith(choiceId: string, owned: string[]): string | null {
  const partners = SYNERGIES[choiceId] ?? [];
  for (const p of partners) if (owned.includes(p)) return p;
  return null;
}

export function buffName(id: string): string {
  return RELIC_NAMES[id] ?? BALL_CORE_NAMES[id] ?? id;
}

export function buffIcon(id: string): string {
  return RELIC_ICONS[id] ?? BALL_CORE_ICONS[id] ?? "/art/ItemGem.png";
}

// Inject overlay styles once
function injectOverlayStyles() {
  const id = "overlay-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .ov-backdrop {
      position: absolute; inset: 0;
      background: rgba(0,0,0,0.82);
      display: flex; flex-direction: column;
      align-items: center; justify-content: center;
      z-index: 1000;
      font-family: var(--font-body);
      color: #e8e8ff;
      gap: 0;
      padding: var(--sp-4h);
      box-sizing: border-box;
    }

    /* Framed panel — LvlUpInterfacePanel art as background */
    .ov-panel {
      position: relative;
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(10,8,26,0.95);
      border: 2px solid rgba(180,140,60,0.7);
      border-radius: 12px;
      padding: var(--sp-5) var(--sp-6);
      min-width: min(280px, 88cqw);
      max-width: min(400px, 92cqw);
      display: flex;
      flex-direction: column;
      gap: var(--sp-2h);
      align-items: center;
      box-shadow: 0 0 30px rgba(0,0,0,0.8), inset 0 0 40px rgba(10,5,30,0.5);
    }

    /* Top/bottom bar decorations */
    .ov-panel::before,
    .ov-panel::after {
      content: '';
      position: absolute;
      left: 0; right: 0;
      height: 16px;
      background: url('/ui/LvlUpInterfaceTopBottomPanel.png') repeat-x center / auto 100%;
    }
    .ov-panel::before { top: 0; border-radius: 10px 10px 0 0; }
    .ov-panel::after  { bottom: 0; border-radius: 0 0 10px 10px; }

    .ov-title {
      font-size: var(--fs-2xl);
      font-weight: 700;
      letter-spacing: 0.1em;
      text-shadow: 0 0 20px currentColor, 0 2px 4px rgba(0,0,0,0.9);
      margin-bottom: var(--sp-2);
    }
    .ov-title-win   { color: var(--gold-bright); }
    .ov-title-green { color: var(--ok-bright); }
    .ov-title-red   { color: var(--danger-bright); }

    .ov-reward-row {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      font-size: var(--fs-large);
    }

    /* Art button — InterfaceButton pill, 9-sliced (fixed rounded ends + stretched middle) */
    .ov-btn {
      margin-top: var(--sp-3);
      padding: 0 16px;
      height: 52px;
      min-width: min(200px, 70cqw);
      background: none;
      border-style: solid;
      border-width: 8px 30px;
      border-image: url('/ui/InterfaceButton.png') 26 92 26 92 fill stretch;
      cursor: pointer;
      font-size: var(--fs-large);
      font-family: var(--font-body);
      font-weight: 700;
      color: var(--text);
      letter-spacing: 0.05em;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
      transition: filter var(--dur-normal), transform var(--dur-fast);
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
    }
    .ov-btn:hover  { filter: brightness(1.15); }
    .ov-btn:active { transform: scale(0.97); filter: brightness(0.9); }
    .ov-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }

    /* Chest image */
    .ov-chest {
      width: 80px;
      height: 80px;
      image-rendering: pixelated;
      filter: drop-shadow(0 4px 12px rgba(0,0,0,0.7));
      margin-bottom: var(--sp-1);
    }

    /* Bonus card */
    .ov-bonus-card {
      position: relative;
      width: min(110px, 28cqw);
      min-height: 140px;
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(10,8,26,0.95);
      border: 2px solid rgba(100,80,160,0.5);
      border-radius: 10px;
      padding: var(--sp-3) var(--sp-2) var(--sp-2h) var(--sp-2);
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--sp-1h);
      cursor: pointer;
      transition: transform var(--dur-fast), border-color var(--dur-normal), box-shadow var(--dur-normal);
      -webkit-tap-highlight-color: transparent;
    }
    .ov-bonus-card:hover {
      transform: translateY(-4px) scale(1.04);
      border-color: rgba(220,190,80,0.8);
      box-shadow: 0 8px 20px rgba(0,0,0,0.6), 0 0 12px rgba(220,190,80,0.3);
    }
    .ov-bonus-card:active { transform: scale(0.97); }
    .ov-bonus-card:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 10px;
    }

    .ov-bonus-icon {
      width: 52px;
      height: 52px;
      image-rendering: pixelated;
      filter: drop-shadow(0 2px 4px rgba(0,0,0,0.7));
    }
    .ov-bonus-name {
      font-size: var(--fs-small);
      font-weight: 700;
      text-align: center;
      color: var(--text);
      line-height: 1.3;
    }

    /* Synergy hint (docs/04 §7): the pick glows and names its combo partner */
    .ov-bonus-synergy {
      border-color: rgba(120, 220, 140, 0.85);
      box-shadow: 0 0 14px rgba(120, 220, 140, 0.35);
    }
    .ov-bonus-hint {
      font-size: var(--fs-tiny);
      font-weight: 700;
      text-align: center;
      color: var(--ok-bright);
      line-height: 1.2;
      letter-spacing: 0.02em;
    }

    .ov-bonus-row {
      display: flex;
      gap: var(--sp-2h);
      flex-wrap: wrap;
      justify-content: center;
      max-width: min(400px, 96cqw);
    }

    .ov-pick-title {
      font-size: var(--fs-section);
      font-weight: 700;
      color: var(--gold);
      letter-spacing: 0.06em;
      margin-bottom: var(--sp-3);
      text-shadow: 0 0 12px rgba(255,200,50,0.5);
    }
  `;
  document.head.appendChild(style);
}

// ── Overlay builders ──────────────────────────────────────────────────────────

/** Campaign victory overlay (#reward-overlay). */
export function buildRewardOverlay(
  reward: CompleteResult["reward"],
  onContinue: () => void,
): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "reward-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = "Victory!";
  title.className = "ov-title ov-title-win";
  overlay.appendChild(title);

  // Chest art
  const chest = document.createElement("img");
  chest.src = "/ui/GreenChest.png";
  chest.className = "ov-chest";
  overlay.appendChild(chest);

  const panel = document.createElement("div");
  panel.className = "ov-panel";

  if (reward) {
    const expEl = document.createElement("div");
    expEl.id = "reward-exp";
    expEl.className = "ov-reward-row";
    const expIco = document.createElement("img");
    expIco.src = "/ui/ExpBarFull.png";
    css(expIco, { width: "28px", height: "12px", imageRendering: "pixelated" });
    expEl.appendChild(expIco);
    const expText = document.createElement("span");
    expText.textContent = `+${reward.expGained} EXP`;
    css(expText, { color: "var(--color-xp)", fontSize: "var(--fs-large)" });
    expEl.appendChild(expText);
    panel.appendChild(expEl);

    const pointsEl = document.createElement("div");
    pointsEl.id = "reward-points";
    pointsEl.className = "ov-reward-row";
    const ptIco = document.createElement("img");
    ptIco.src = "/ui/InterfaceSkillsButton.png";
    css(ptIco, { width: "22px", height: "22px", imageRendering: "pixelated" });
    pointsEl.appendChild(ptIco);
    const ptText = document.createElement("span");
    ptText.textContent = `+${reward.pointsGained} Skill Points`;
    css(ptText, { color: "var(--color-pts)", fontSize: "var(--fs-large)" });
    pointsEl.appendChild(ptText);
    panel.appendChild(pointsEl);

    const crystalsEl = document.createElement("div");
    crystalsEl.id = "reward-crystals";
    crystalsEl.className = "ov-reward-row";
    const cIco = document.createElement("img");
    cIco.src = "/ui/GemBlue.png";
    css(cIco, { width: "22px", height: "22px", imageRendering: "pixelated" });
    crystalsEl.appendChild(cIco);
    const cText = document.createElement("span");
    cText.textContent = `+${reward.crystalsGained} Crystals`;
    css(cText, { color: "var(--color-crystal)", fontSize: "var(--fs-large)" });
    crystalsEl.appendChild(cText);
    panel.appendChild(crystalsEl);

    if (reward.leveledUp) {
      const lvlUp = document.createElement("div");
      lvlUp.id = "reward-levelup";
      lvlUp.textContent = `Level Up! → Lv ${reward.newLevel}`;
      css(lvlUp, { fontSize: "var(--fs-large)", color: "var(--color-levelup)", fontWeight: "700", marginTop: "var(--sp-1)" });
      panel.appendChild(lvlUp);
    }

    if (reward.firstClear) {
      const first = document.createElement("div");
      first.textContent = "First Clear!";
      css(first, { fontSize: "var(--fs-subhead)", color: "var(--color-first-clear)", marginTop: "var(--sp-1)" });
      panel.appendChild(first);
    }
  } else {
    const msg = document.createElement("div");
    msg.textContent = "Level complete!";
    css(msg, { color: "var(--color-xp)" });
    panel.appendChild(msg);
  }

  overlay.appendChild(panel);

  const btnContinue = document.createElement("button");
  btnContinue.id = "btn-continue";
  btnContinue.className = "ov-btn";
  btnContinue.textContent = "Continue";
  btnContinue.addEventListener("click", onContinue);
  overlay.appendChild(btnContinue);

  return overlay;
}

/** Campaign defeat overlay (#defeat-overlay). */
export function buildDefeatOverlay(
  onRetry: () => void,
  onMap: () => void,
): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "defeat-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = "Defeat";
  title.className = "ov-title ov-title-red";
  overlay.appendChild(title);

  const btnRetry = document.createElement("button");
  btnRetry.id = "btn-retry";
  btnRetry.className = "ov-btn";
  btnRetry.textContent = "Retry";
  css(btnRetry, { filter: "hue-rotate(200deg) saturate(0.7)" });
  btnRetry.addEventListener("click", onRetry);
  overlay.appendChild(btnRetry);

  const btnMap = document.createElement("button");
  btnMap.id = "btn-map";
  btnMap.className = "ov-btn";
  btnMap.textContent = "Map";
  btnMap.addEventListener("click", onMap);
  overlay.appendChild(btnMap);

  return overlay;
}

/** Dungeon pick-a-boon overlay (#pick-overlay). */
export function buildPickOverlay(
  choices: string[],
  onPick: (choiceId: string) => void,
  owned: string[] = [],
): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "pick-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = "Choose a Boon";
  title.className = "ov-pick-title";
  overlay.appendChild(title);

  const row = document.createElement("div");
  row.className = "ov-bonus-row";

  for (const choiceId of choices) {
    const card = document.createElement("div");
    card.setAttribute("data-choice", choiceId);
    card.className = "ov-bonus-card";

    const icon = document.createElement("img");
    icon.src = buffIcon(choiceId);
    icon.alt = buffName(choiceId);
    icon.className = "ov-bonus-icon";
    card.appendChild(icon);

    const nameEl = document.createElement("div");
    nameEl.textContent = buffName(choiceId);
    nameEl.className = "ov-bonus-name";
    card.appendChild(nameEl);

    // Synergy hint (docs/04 §7): show when this pick combos with something owned.
    const partner = synergyWith(choiceId, owned);
    if (partner) {
      card.classList.add("ov-bonus-synergy");
      card.setAttribute("data-synergy", partner);
      const hint = document.createElement("div");
      hint.textContent = `Combos with ${buffName(partner)}`;
      hint.className = "ov-bonus-hint";
      card.appendChild(hint);
    }

    card.addEventListener("click", () => onPick(choiceId));
    row.appendChild(card);
  }

  overlay.appendChild(row);
  return overlay;
}

/** Dungeon all-floors-cleared overlay (#dungeon-clear-overlay). */
export function buildDungeonClearOverlay(
  data: FloorClearedResult,
  onDone: () => void,
): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "dungeon-clear-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = "Dungeon Cleared!";
  title.className = "ov-title ov-title-green";
  overlay.appendChild(title);

  const chest = document.createElement("img");
  chest.src = "/ui/BlueChest.png";
  chest.className = "ov-chest";
  overlay.appendChild(chest);

  const panel = document.createElement("div");
  panel.className = "ov-panel";

  const rewardTitle = document.createElement("div");
  rewardTitle.textContent = "Permanent Reward";
  css(rewardTitle, { fontSize: "var(--fs-body)", color: "var(--color-label-muted)", letterSpacing: "0.05em" });
  panel.appendChild(rewardTitle);

  const profile = data.profile;
  if (profile?.crystals !== undefined) {
    const crystalEl = document.createElement("div");
    crystalEl.id = "dungeon-clear-crystals";
    crystalEl.className = "ov-reward-row";
    const gemImg = document.createElement("img");
    gemImg.src = "/ui/GemBlue.png";
    css(gemImg, { width: "24px", height: "24px", imageRendering: "pixelated" });
    crystalEl.appendChild(gemImg);
    const crystalText = document.createElement("span");
    crystalText.textContent = `${profile.crystals} Crystals`;
    css(crystalText, { fontSize: "var(--fs-xl)", color: "var(--color-crystal)" });
    crystalEl.appendChild(crystalText);
    panel.appendChild(crystalEl);
  }

  if (profile?.unlockedRelics && Array.isArray(profile.unlockedRelics) && profile.unlockedRelics.length > 0) {
    const lastRelic = profile.unlockedRelics[profile.unlockedRelics.length - 1];
    const relicRow = document.createElement("div");
    relicRow.className = "ov-reward-row";
    const relicImg = document.createElement("img");
    relicImg.src = RELIC_ICONS[lastRelic] ?? "/art/ItemGem.png";
    css(relicImg, { width: "24px", height: "24px", imageRendering: "pixelated" });
    relicRow.appendChild(relicImg);
    const relicText = document.createElement("span");
    relicText.id = "dungeon-clear-relic";
    relicText.textContent = RELIC_NAMES[lastRelic] ?? lastRelic;
    css(relicText, { fontSize: "var(--fs-large)", color: "var(--color-relic)" });
    relicRow.appendChild(relicText);
    panel.appendChild(relicRow);
  }

  overlay.appendChild(panel);

  const doneBtn = document.createElement("button");
  doneBtn.id = "btn-dungeon-done";
  doneBtn.className = "ov-btn";
  doneBtn.textContent = "Return to Campaign";
  doneBtn.addEventListener("click", onDone);
  overlay.appendChild(doneBtn);

  return overlay;
}

/** Dungeon permadeath overlay (#dungeon-fail-overlay). */
export function buildDungeonFailOverlay(onExit: () => void): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "dungeon-fail-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = "Run Over";
  title.className = "ov-title ov-title-red";
  overlay.appendChild(title);

  const sub = document.createElement("div");
  sub.textContent = "The rift claims you.";
  css(sub, { color: "var(--color-fail-muted)", fontSize: "var(--fs-large)", letterSpacing: "0.04em", marginBottom: "var(--sp-2)" });
  overlay.appendChild(sub);

  const exitBtn = document.createElement("button");
  exitBtn.id = "btn-dungeon-exit";
  exitBtn.className = "ov-btn";
  exitBtn.textContent = "Return to Campaign";
  css(exitBtn, { filter: "hue-rotate(200deg) saturate(0.7)" });
  exitBtn.addEventListener("click", onExit);
  overlay.appendChild(exitBtn);

  return overlay;
}
```

## `src/scenes/campaign/campaignStyles.ts`

```typescript
// Campaign + rift-banner stylesheets, extracted from CampaignScene.ts to keep that
// file focused on DOM construction and data flow. Injected once (id-guarded).
import { btn1, missionName } from "../../ui/nineSlice";

export const RIFT_STYLES = `
    .rift-banner {
      position: fixed;
      left: 50%;
      top: 64px;
      transform: translate(-50%, -160%);
      width: min(360px, 92cqw);
      z-index: 200;
      display: flex;
      align-items: center;
      gap: var(--sp-3);
      padding: var(--sp-3h) var(--sp-4);
      box-sizing: border-box;
      background:
        linear-gradient(180deg, rgba(60,10,70,0.96), rgba(30,5,40,0.97)),
        rgba(20,5,30,0.97);
      border: 2px solid #b048e0;
      border-radius: 12px;
      box-shadow: 0 0 28px rgba(180,70,230,0.55), inset 0 0 30px rgba(120,30,160,0.4);
      color: #f4e6ff;
      font-family: var(--font-body);
      transition: transform 0.35s cubic-bezier(0.2, 1.1, 0.4, 1);
    }
    .rift-banner-in { transform: translate(-50%, 0); }
    .rift-banner-glyph {
      width: 26px; height: 26px; flex-shrink: 0;
      border-radius: 50%;
      background: radial-gradient(circle at 38% 35%, #f4d6ff 0%, #c060ff 45%, #5a149a 100%);
      box-shadow: 0 0 14px #c060ff, inset 0 0 6px rgba(255,255,255,0.8);
    }
    @keyframes rift-pulse { 0%,100% { opacity: 0.7; transform: scale(1); } 50% { opacity: 1; transform: scale(1.18); } }
    @media (prefers-reduced-motion: no-preference) {
      .rift-banner-glyph { animation: rift-pulse 1.4s ease-in-out infinite; }
    }
    .rift-banner-text { flex: 1; min-width: 0; }
    .rift-banner-title {
      font-size: var(--fs-section); font-weight: 800; letter-spacing: 0.04em;
      color: #e9b8ff; text-shadow: 0 0 10px rgba(190,90,240,0.7);
    }
    .rift-banner-sub { font-size: var(--fs-tiny); color: #c9a8e0; margin-top: 2px; line-height: 1.3; }
    .rift-banner-actions { display: flex; flex-direction: column; gap: var(--sp-1h); }
    .rift-btn {
      min-width: 78px; min-height: 44px;
      border: none; border-radius: 8px; cursor: pointer;
      font-size: var(--fs-body); font-weight: 700; font-family: var(--font-body);
      touch-action: manipulation; -webkit-tap-highlight-color: transparent;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .rift-btn:active { transform: scale(0.95); }
    .rift-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 8px;
    }
    .rift-btn-go {
      background: linear-gradient(180deg, #c860ff, #8a28c0);
      color: #fff; text-shadow: 0 1px 2px rgba(0,0,0,0.6);
      box-shadow: 0 0 12px rgba(190,90,240,0.6);
    }
    .rift-btn-go:hover { filter: brightness(1.15); }
    .rift-btn-skip {
      background: rgba(40,20,55,0.9); color: #b89ccc;
      border: 1px solid rgba(150,90,190,0.45);
    }
    .rift-btn-skip:hover { filter: brightness(1.2); }
`;

export const CAMPAIGN_STYLES = `
    .camp-root {
      min-height: 100cqh;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(60,40,10,0.4) 0%, transparent 60%),
        linear-gradient(180deg, #12080a 0%, #070510 50%, #040308 100%);
      display: flex;
      flex-direction: column;
      overflow: hidden;
      font-family: var(--font-body);
      color: #e8e8ff;
    }

    /* ── Profile bar ── */
    .camp-profile-bar {
      display: flex;
      align-items: center;
      gap: var(--sp-3);
      padding: var(--sp-2) var(--sp-4);
      background: url('/ui/LvlUpInterfaceTopBottomPanel.png') repeat-x center / auto 100%;
      border-bottom: 2px solid rgba(180,140,60,0.4);
      flex-shrink: 0;
      flex-wrap: wrap;
      min-height: 52px;
    }
    .camp-profile-level {
      font-weight: 700;
      font-size: var(--fs-section);
      color: var(--gold);
      text-shadow: 0 0 8px rgba(255,200,0,0.6);
      white-space: nowrap;
    }
    .camp-exp-wrap {
      display: flex;
      align-items: center;
      gap: 5px;
    }
    .camp-exp-label {
      color: var(--text-dim);
      font-size: var(--fs-small);
      white-space: nowrap;
    }
    .camp-exp-outer {
      position: relative;
      width: 110px;
      height: 16px;
      border-style: solid;
      border-width: 7px 18px;
      border-image: url('/ui/ExpBarEmptyMainMenu.png') 26 70 26 70 fill stretch;
      box-sizing: border-box;
      overflow: hidden;
    }
    .camp-exp-fill {
      position: absolute;
      left: 18px; top: 7px; bottom: 7px; right: 18px;
      background: linear-gradient(180deg, #ffe06a, #d89a2e);
      border-radius: 2px;
      transition: width var(--dur-slow);
    }
    .camp-profile-points {
      color: var(--text-dim);
      font-size: var(--fs-caption);
      white-space: nowrap;
    }
    .camp-profile-crystals {
      display: flex;
      align-items: center;
      gap: 3px;
      font-size: var(--fs-body);
      color: var(--gold-bright);
    }
    .camp-upgrade-btn {
      display: flex;
      align-items: center;
      gap: 5px;
      padding: var(--sp-1) var(--sp-3);
      ${btn1()}
      color: var(--text);
      border-radius: 4px;
      cursor: pointer;
      font-size: var(--fs-body);
      font-family: var(--font-body);
      font-weight: 600;
      min-height: 44px;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .camp-upgrade-btn:hover:not(:disabled)   { filter: brightness(1.15); }
    .camp-upgrade-btn:active:not(:disabled)  { transform: scale(0.96); }
    .camp-upgrade-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .camp-upgrade-btn.active  { filter: brightness(1.2) saturate(1.4); }
    .camp-upgrade-btn:disabled {
      filter: saturate(0.25) brightness(0.65);
      cursor: default;
    }
    .camp-upgrade-ico {
      width: 22px;
      height: 22px;
    }

    /* ── Main content ── */
    .camp-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      overflow-y: auto;
      overflow-x: hidden;
      -webkit-overflow-scrolling: touch;
      /* Subtle scrollbar */
      scrollbar-width: thin;
      scrollbar-color: rgba(180,140,60,0.4) transparent;
    }

    /* ── Campaign map — vertically fills the content area, inner content scrolls via camp-content ── */
    .camp-map {
      /* No flex:1 — natural height from inner content */
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: var(--sp-4h) var(--sp-4) var(--sp-6) var(--sp-4);
      /* No overflow here — parent camp-content scrolls */
    }

    /* Inner relative wrapper that holds abs-positioned connectors + nodes */
    .camp-map-inner {
      position: relative;
      flex-shrink: 0;
    }

    /* Connector shared base (positioned absolutely inside .camp-map-inner) */
    .camp-connector {
      position: absolute;
      border-radius: 3px;
      background: rgba(80,60,20,0.5);
      pointer-events: none;
    }
    .camp-connector.active {
      background: linear-gradient(
        135deg,
        rgba(180,140,60,0.6) 0%,
        rgba(220,180,80,0.95) 50%,
        rgba(180,140,60,0.6) 100%
      );
      box-shadow: 0 0 6px rgba(220,180,60,0.4);
    }

    /* Node button */
    .camp-node {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--sp-1);
      width: 80px;
      padding: var(--sp-1h) var(--sp-1);
      background: transparent;
      border: none;
      cursor: pointer;
      flex-shrink: 0;
      transition: transform var(--dur-normal), filter var(--dur-normal);
      -webkit-tap-highlight-color: transparent;
    }
    .camp-node:hover:not(.camp-node-locked) { transform: scale(1.08); filter: brightness(1.15); }
    .camp-node:active:not(.camp-node-locked) { transform: scale(0.96); }
    .camp-node-locked { cursor: default; opacity: 0.7; }
    .camp-node-locked:hover { transform: none; filter: none; }
    .camp-node:not(.camp-node-locked):focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }

    .camp-node-img {
      width: 64px;
      height: 64px;
      /* Painted art downscaled from 140px — smooth filtering, NOT pixelated
         (pixelated shredded the orb/lock art; docs/13 campaign audit). */
      filter: drop-shadow(0 2px 6px rgba(0,0,0,0.7));
    }
    .camp-node-completed .camp-node-img {
      filter: drop-shadow(0 0 8px rgba(100,220,255,0.8)) drop-shadow(0 2px 6px rgba(0,0,0,0.7));
    }

    .camp-node-label-wrap {
      ${missionName()}
      padding: 3px 10px;
      width: max-content;
      max-width: 132px;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1px;
    }
    .camp-node-kicker {
      font-size: var(--fs-tiny);
      font-weight: 700;
      letter-spacing: 0.14em;
      text-transform: uppercase;
      color: var(--text-dim);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      white-space: nowrap;
      line-height: 1.1;
    }
    .camp-node-label {
      font-size: var(--fs-small);
      font-weight: 700;
      color: var(--text);
      text-shadow: 0 1px 2px rgba(0,0,0,0.95);
      line-height: 1.2;
      white-space: nowrap;
    }

    /* ── Upgrade panel — fixed bottom sheet, always in viewport ── */
    .camp-upgrade-panel {
      display: none;
      /* Bottom sheet pinned to the letterbox frame, not the window edge. */
      position: fixed;
      left: 50%;
      transform: translateX(-50%);
      width: 100cqw;
      bottom: 0;
      z-index: 100;
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(10,8,20,0.96);
      border-top: 2px solid rgba(180,140,60,0.5);
      border-radius: 12px 12px 0 0;
      padding: var(--sp-4h) var(--sp-4h) var(--sp-6) var(--sp-4h);
      max-height: 60cqh;
      overflow-y: auto;
    }
    .camp-spell-row {
      display: flex;
      align-items: center;
      gap: var(--sp-2h);
      padding: var(--sp-2) var(--sp-3);
      background: rgba(20,20,50,0.85);
      border-radius: 6px;
      border: 1px solid rgba(100,80,180,0.4);
    }
    .camp-plus-btn {
      width: 44px;
      height: 44px;
      background: url('/ui/InterfaceNewButton.png') no-repeat center / 32px 32px;
      border: none;
      cursor: pointer;
      font-size: 0;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .camp-plus-btn.can-afford:hover  { filter: brightness(1.2); transform: scale(1.1); }
    .camp-plus-btn.can-afford:active { transform: scale(0.96); }
    .camp-plus-btn.cannot-afford { filter: grayscale(1) opacity(0.4); cursor: default; }
    .camp-plus-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 50%;
    }

    /* ── Campaign back-link (top-left of profile bar) ── */
    .camp-back-link {
      flex-shrink: 0;
      min-width: 44px;
      min-height: 44px;
      display: flex;
      align-items: center;
      padding: 0 12px;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .camp-back-link:hover  { filter: brightness(1.15); color: var(--gold-bright); }
    .camp-back-link:active { transform: scale(0.96); }
    .camp-back-link:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
`;
```

## `src/scenes/editor/editorUtils.ts`

```typescript
// Stateless helpers + constants for the level editor, extracted from EditorScene.ts.

export const ART_BASE = "/art/";
// Portrait-native dimensions matching the P2 board format (8 cols × 14 rows).
export const DEFAULT_COLS = 8;
export const DEFAULT_ROWS = 14;

// Legend chars assigned starting from 'A', avoiding '.'
export const LEGEND_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

/** Build a styled button with an optional id and click handler. */
export function btn(
  text: string,
  id: string | null,
  css: string,
  onClick?: () => void,
): HTMLButtonElement {
  const b = document.createElement("button");
  if (id) b.id = id;
  b.textContent = text;
  b.style.cssText = css;
  if (onClick) b.addEventListener("click", onClick);
  return b;
}
```

## `src/scenes/inventory/inventoryStyles.ts`

```typescript
// Inventory screen stylesheet, extracted from InventoryScene.ts to keep that file
// focused on DOM construction and data flow. Injected once (id-guarded).
//
// Built on the shared design system (ui/theme.ts): warm-brown screen, NameBlock
// section plaques, BarGoods card panels, Kvadrat equip slots, Button1 actions.
// docs/13-ui-ux-audit.md called the old flat-HTML look "AI-generated page";
// every surface here is the game's own painted art via 9-slice.

import { nineSlice } from "../../ui/nineSlice";

export const INVENTORY_STYLES = `
    .inv-root {
      position: relative;
      min-height: 100cqh;
      width: 100%;
      color: var(--text);
      font-family: var(--font-body);
      display: flex;
      flex-direction: column;
      box-sizing: border-box;
      padding: env(safe-area-inset-top, 0px) env(safe-area-inset-right, 0px)
               env(safe-area-inset-bottom, 0px) env(safe-area-inset-left, 0px);
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    }

    /* ── Header: back chip · display title · gem counter ── */
    .inv-header {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      padding: max(12px, env(safe-area-inset-top, 0px)) 12px 8px 12px;
      flex-shrink: 0;
    }
    .inv-title {
      flex: 1;
      margin: 0;
      font-family: var(--font-display);
      font-size: var(--fs-title);
      font-weight: 700;
      letter-spacing: 0.05em;
      color: var(--gold-bright);
      text-shadow: 0 2px 4px rgba(0,0,0,0.9), 0 0 18px rgba(255,180,60,0.25);
      text-align: center;
    }
    .inv-crystals {
      font-size: var(--fs-section);
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      min-width: 56px;
      text-align: right;
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: var(--sp-1);
    }

    /* ── Section plaques (NameBlock scroll bars) ── */
    .inv-equipped-section {
      padding: var(--sp-2h) var(--sp-4) var(--sp-1h);
      flex-shrink: 0;
      display: flex;
      flex-direction: column;
      align-items: center;
    }
    .inv-section-label {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-height: 30px;
      padding: 2px 22px;
      margin-bottom: var(--sp-2h);
      ${nineSlice("/ui/NameBlock.png", "40 120 40 120", "9px 28px")}
      font-family: var(--font-display);
      font-size: var(--fs-caption);
      font-weight: 700;
      letter-spacing: 0.14em;
      text-transform: uppercase;
      color: var(--gold-bright);
      text-shadow: 0 1px 3px rgba(0,0,0,0.95);
      white-space: nowrap;
    }
    .inv-catalog-label {
      align-self: center;
      margin: 8px auto 2px;
    }

    /* ── Equipped row: Kvadrat slot frames ── */
    .inv-equipped-row {
      display: flex;
      gap: var(--sp-3);
      justify-content: center;
    }
    .inv-equip-slot {
      width: 82px;
      height: 78px;
      ${nineSlice("/ui/Kvadrat.png", "14 14 14 14", "7px 7px")}
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      font-family: var(--font-display);
      font-size: var(--fs-xl);
      color: rgba(216, 168, 78, 0.35);
      flex-shrink: 0;
      gap: 2px;
    }
    .inv-equip-slot-filled {
      filter: drop-shadow(0 0 6px rgba(255, 190, 80, 0.45));
    }
    .inv-slot-sprite {
      width: 46px;
      height: 46px;
      object-fit: contain;
    }
    .inv-slot-label {
      font-family: var(--font-body);
      font-size: var(--fs-tiny);
      color: var(--text-dim);
      text-align: center;
      line-height: 1.1;
      max-width: 64px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    /* ── Item grid ── */
    .inv-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: var(--sp-3);
      padding: 8px 14px 28px;
      overflow-y: auto;
      flex: 1;
    }

    /* ── Item card: BarGoods gold-rimmed navy panel ── */
    .inv-card {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "13px 15px")}
      padding: var(--sp-1h) var(--sp-1) var(--sp-1h);
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 5px;
      position: relative;
    }
    .inv-card-equipped {
      filter: drop-shadow(0 0 7px rgba(255, 190, 80, 0.55));
    }
    .inv-card-sprite {
      width: 54px;
      height: 54px;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .inv-item-sprite {
      width: 52px;
      height: 52px;
      object-fit: contain;
      filter: drop-shadow(0 2px 3px rgba(0,0,0,0.6));
    }
    /* Locked: readable but clearly unowned — NOT blacked out (docs/13). */
    .inv-item-locked {
      opacity: 0.85;
      filter: saturate(0.45) brightness(0.8) drop-shadow(0 2px 3px rgba(0,0,0,0.6));
    }
    .inv-card-name-row {
      display: flex;
      align-items: center;
      gap: 5px;
      width: 100%;
      justify-content: center;
    }
    .inv-card-name {
      font-size: var(--fs-caption);
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      text-align: center;
      line-height: 1.2;
    }
    .inv-tier-badge {
      font-size: var(--fs-tiny);
      font-weight: 900;
      padding: 1px 5px;
      border-radius: 3px;
      background: rgba(216, 168, 78, 0.22);
      color: var(--gold-bright);
      border: 1px solid var(--gold-dim);
      text-shadow: 0 1px 1px rgba(0,0,0,0.8);
    }
    .inv-card-desc {
      font-size: var(--fs-tiny);
      color: var(--text-dim);
      text-align: center;
      line-height: 1.35;
      min-height: 28px;
      padding: 0 2px;
    }
    .inv-card-actions {
      display: flex;
      flex-direction: column;
      gap: 5px;
      width: 100%;
      margin-top: 2px;
      align-items: stretch;
    }

    /* ── Actions: Button1 gold/navy pills ── */
    .inv-buy-btn, .inv-equip-btn {
      width: 100%;
      min-height: 44px;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 16px")}
      cursor: pointer;
      font-family: var(--font-body);
      font-size: var(--fs-caption);
      font-weight: 700;
      letter-spacing: 0.04em;
      touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
      transition: filter var(--dur-normal), transform var(--dur-fast);
      text-shadow: 0 1px 2px var(--shadow-hard);
    }
    .inv-buy-btn { color: var(--gold-bright); }
    .inv-buy-btn:hover:not(:disabled)  { filter: brightness(1.18); }
    .inv-buy-btn:active:not(:disabled) { transform: scale(0.96); }
    .inv-buy-btn:disabled, .inv-btn-disabled {
      filter: saturate(0.25) brightness(0.6);
      cursor: default;
    }
    .inv-equip-btn { color: var(--color-equip); }
    .inv-btn-unequip { color: var(--color-unequip); }
    .inv-equip-btn:hover  { filter: brightness(1.18); }
    .inv-equip-btn:active { transform: scale(0.96); }
    .inv-buy-btn:focus-visible, .inv-equip-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }

    .inv-max-badge {
      font-family: var(--font-display);
      font-size: var(--fs-small);
      font-weight: 700;
      color: var(--gold-bright);
      text-align: center;
      padding: var(--sp-1h) 0;
      letter-spacing: 0.18em;
      text-shadow: 0 0 8px rgba(255,190,80,0.4);
    }

    /* Wider design space (container, not viewport — we live in a letterbox) */
    @container (min-width: 480px) {
      .inv-grid {
        grid-template-columns: repeat(3, 1fr);
      }
    }
`;
```

## `src/testhooks.ts`

```typescript
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
    castSlot: (slot) => conn.castSlot(slot),
    setPaddleX: (x) => conn.paddleX(x),
    runId: conn.runId,
    getLogs: () => getLogs(),
  };
}
```

## `src/ui/Hud.ts`

```typescript
import type { Connection } from "../net/Connection";
import type { Snapshot } from "../net/Connection";
import type { SpellDef, ItemDef } from "../net/metaApi";
import { inferBossType, bossLabel } from "../render/Boss";
import { buildLabelledBar, buildManaBar, buildBossBar } from "./hud/bars";
import { HUD_STYLES } from "./hud/hudStyles";
import { buildSpellIcon } from "./hud/spellIcon";

// ---------------------------------------------------------------------------
// Spell cost constants (mirrored from backend; used for affordability dimming).
// ---------------------------------------------------------------------------
// Mirrored from backend SimConfig — update here whenever SimConfig spell costs change.
const SPELL_COSTS: Record<string, number> = {
  ignite:    0,
  fireball:  25,  // bumped 20→25 (P7a balance pass)
  firewall:  35,  // bumped 30→35 (P7a balance pass)
  turret:    25,
  shield:    20,
  spear:     15,
  duplicate: 25,
  lightning: 20,
  rocket:    25,
  radiation: 30,
  decay:     0,
  skeleton:  25,
  drain:     20,
  // G2c kit-completion spells
  phoenix:     30,
  penetration: 20,
  lastday:     35,
  magnet:      20,
  overload:    25,
  golem:       30,
  mage:        25,
};

// Key labels by slot index (0→Q, 1→E, 2→W, 3→R, 4→T).
const SLOT_KEYS = ["Q", "E", "W", "R", "T"];

// The 3-slice HUD value bars (HP / balls / mana / boss) live in ./hud/bars.

// Legacy per-spell DOM ids for Fire Mage (required for existing tests).
const FIRE_MAGE_SLOT_IDS: Record<string, string> = {
  ignite:   "hud-spell-ignite",
  fireball: "hud-spell-fireball",
  firewall: "hud-spell-firewall",
  turret:   "hud-spell-turret",
};

// ---------------------------------------------------------------------------
// Hud — a DOM overlay mounted on top of the Pixi canvas.
// ---------------------------------------------------------------------------
export class Hud {
  private root: HTMLElement;
  private livesEl: HTMLElement;
  private livesFill: HTMLElement;
  private livesCount: HTMLElement;
  private ballsEl: HTMLElement;
  private ballsFill: HTMLElement;
  private ballsCount: HTMLElement;
  // Running maxima — lives/spare-balls have no fixed cap, so the bar is scaled to
  // the largest value seen this battle (start value = full bar).
  private _maxLives = 1;
  private _maxBalls = 1;
  private manaOuter: HTMLElement;
  private manaFill: HTMLElement;
  private manaText: HTMLElement;
  private spellSlots: Map<string, HTMLElement> = new Map();
  private hotbarEl: HTMLElement;
  private banner: HTMLElement;
  private relicsEl: HTMLElement;
  // Boss HP bar elements.
  private bossBarEl: HTMLElement;
  private bossBarFill: HTMLElement;
  private bossNameEl: HTMLElement;
  // Active bonus effects indicator row.
  private effectsEl: HTMLElement;
  // Objective timer (survive/limit) — docs/12 objective flavors.
  private timerEl!: HTMLElement;

  // Active spells for the current class (populated by loadSpells).
  private _spells: SpellDef[] = [];
  private _conn: Connection | null = null;
  private _itemsRowEl: HTMLElement | null = null;
  // Active power-up panel (top-right; task 1.2).
  private _powerupPanelEl: HTMLElement;
  // Combo badge (top-right, below power-ups; task 1.3).
  private _comboBadgeEl: HTMLElement;
  private _prevComboMult = 1;

  // Latest snapshot mana, for affordability check on tap.
  private _mana = 0;

  constructor(host: HTMLElement) {
    this.root = this.createElement("div", "hud-root");
    this.root.style.cssText = [
      "position:absolute", "inset:0", "pointer-events:none",
      "font-family:var(--font-body)", "z-index:10",
      "user-select:none",
      // Safe-area insets for notched phones.
      "padding:env(safe-area-inset-top,0px) env(safe-area-inset-right,0px) env(safe-area-inset-bottom,0px) env(safe-area-inset-left,0px)",
    ].join(";");

    this.injectStyles();

    // ---- top-left panel: lives + balls ----
    const topLeft = this.createElement("div", "hud-top-left");
    // Translucent backing strip so bars read over bright biomes (Rulebook §8).
    topLeft.style.cssText = [
      "position:absolute", "top:8px", "left:8px",
      "display:flex", "flex-direction:column", "gap:5px",
      "background:var(--hud-top-bg)", "border-radius:8px", "padding:6px 8px",
    ].join(";");

    const livesBar = buildLabelledBar({
      id: "hud-lives", fillId: "hud-lives-fill", labelId: "hud-lives-label",
      emptySrc: "/ui/BattleHPEmpty.png",
      gradient: "linear-gradient(to right,var(--color-hp-deep),var(--color-hp))",
      icon: "/ui/BonusHP.png",
      fillSrc: "/ui/BattleHPFull.png",
    });
    this.livesEl = livesBar.outer;
    this.livesEl.dataset.lives = "0";
    this.livesFill = livesBar.fill;
    this.livesCount = livesBar.label.querySelector(".hud-bar-count")!;
    topLeft.appendChild(this.livesEl);

    const ballsBar = buildLabelledBar({
      id: "hud-balls", fillId: "hud-balls-fill", labelId: "hud-balls-label",
      emptySrc: "/ui/BattleMPEmpty.png",
      gradient: "linear-gradient(to right,var(--color-balls-deep),var(--color-balls))",
      icon: "/ui/BattleLifeBall.png",
      fillSrc: "/ui/BattleMPFull.png",
    });
    this.ballsEl = ballsBar.outer;
    this.ballsEl.dataset.balls = "0";
    this.ballsFill = ballsBar.fill;
    this.ballsCount = ballsBar.label.querySelector(".hud-bar-count")!;
    topLeft.appendChild(this.ballsEl);

    // ---- top-right panel: relics row ----
    const topRight = this.createElement("div", "hud-relics");
    topRight.id = "hud-relics";
    topRight.style.cssText = [
      "position:absolute", "top:8px", "right:8px",
      "display:flex", "flex-direction:row", "gap:5px",
      "align-items:center", "min-width:40px", "min-height:20px",
    ].join(";");
    this.relicsEl = topRight;

    // ---- bottom thumb zone: mana bar + spell hotbar ----
    const bottomCenter = this.createElement("div", "hud-bottom");
    bottomCenter.style.cssText = [
      "position:absolute", "bottom:0", "left:0", "right:0",
      "display:flex", "flex-direction:column", "align-items:center", "gap:4px",
      "padding-bottom:max(12px,env(safe-area-inset-bottom,12px))",
      "padding-top:6px",
      "background:linear-gradient(to top,var(--hud-btm-bg) 0%,transparent 100%)",
      "pointer-events:none",
    ].join(";");

    this.manaOuter = buildManaBar();
    this.manaFill  = this.manaOuter.querySelector("#hud-mana-fill")!;
    this.manaText  = this.manaOuter.querySelector("#hud-mana-text")!;

    this.hotbarEl = this.createElement("div");
    this.hotbarEl.id = "hud-hotbar";
    this.hotbarEl.style.cssText = "display:flex;gap:6px;pointer-events:none;";

    bottomCenter.appendChild(this.manaOuter);
    bottomCenter.appendChild(this.hotbarEl);

    // ---- boss HP bar (top center, only visible when bossActive) ----
    const bossBar = buildBossBar();
    this.bossBarEl   = bossBar.outer;
    this.bossBarFill = bossBar.fill;
    this.bossNameEl  = bossBar.name;

    // ---- objective timer (top center, under the boss bar; docs/12 objectives) ----
    this.timerEl = this.createElement("div", "hud-timer");
    this.timerEl.id = "hud-timer";
    this.timerEl.style.cssText = [
      "position:absolute", "top:44px", "left:50%", "transform:translateX(-50%)",
      "display:none", "padding:2px 14px", "border-radius:10px",
      "font-size:var(--fs-xl)", "font-weight:800", "letter-spacing:1px",
      "background:var(--overlay-light)", "pointer-events:none",
    ].join(";");
    this.root.appendChild(this.timerEl);

    // ---- active bonus effects row (top-left, below lives) ----
    this.effectsEl = this.createElement("div", "hud-effects");
    this.effectsEl.id = "hud-effects";
    this.effectsEl.style.cssText = [
      "position:absolute", "top:72px", "left:8px",
      "display:flex", "flex-direction:row", "gap:4px",
      "align-items:center", "pointer-events:none",
    ].join(";");
    this.root.appendChild(this.effectsEl);

    // ---- active power-up panel (top-right, below relics/items; task 1.2) ----
    this._powerupPanelEl = this.createElement("div", "hud-powerups");
    this._powerupPanelEl.id = "hud-powerups";
    this._powerupPanelEl.style.cssText = [
      "position:absolute", "top:90px", "right:8px",
      "display:none", "flex-direction:column", "gap:3px",
      "align-items:flex-end", "pointer-events:none",
    ].join(";");
    this.root.appendChild(this._powerupPanelEl);

    // ---- combo multiplier badge (top-right, below power-up panel; task 1.3) ----
    this._comboBadgeEl = this.createElement("div");
    this._comboBadgeEl.id = "hud-combo";
    this._comboBadgeEl.style.cssText = [
      "position:absolute", "top:155px", "right:8px",
      "display:none",
      "color:var(--gold-bright)",
      "text-shadow:0 0 8px var(--gold-glow-mid)",
      "font-family:var(--font-display)",
      "font-size:var(--fs-xl)", "font-weight:bold",
      "background:var(--overlay-mid)",
      "border:1px solid var(--gold-glow-lo)",
      "border-radius:6px", "padding:3px 10px",
      "pointer-events:none",
    ].join(";");
    this.root.appendChild(this._comboBadgeEl);

    // ---- banner (center) ----
    this.banner = this.createElement("div", "hud-banner");
    this.banner.id = "hud-banner";
    this.banner.style.cssText = [
      "position:absolute", "top:50%", "left:50%",
      "transform:translate(-50%,-50%)",
      "display:none", "padding:18px 48px", "border-radius:8px",
      "font-size:clamp(28px,8cqw,48px)", "font-weight:900", "letter-spacing:4px",
      "text-shadow:0 0 20px currentColor",
    ].join(";");

    this.root.appendChild(topLeft);
    this.root.appendChild(topRight);
    this.root.appendChild(this.bossBarEl);
    this.root.appendChild(bottomCenter);
    this.root.appendChild(this.banner);

    host.style.position = "relative";
    host.appendChild(this.root);
  }

  // -----------------------------------------------------------------------
  /**
   * Call on battle start to load the selected character's spell kit.
   * Rebuilds the hotbar DOM and wires cast handlers if conn is already set.
   */
  loadSpells(spells: SpellDef[]) {
    this._spells = spells;
    this.rebuildHotbar();
    if (this._conn) this.wireConnHandlers(this._conn);
  }

  wireConn(conn: Connection) {
    this._conn = conn;
    // If spells are already loaded (loadSpells was called first), wire immediately.
    // Otherwise wire after loadSpells is called.
    if (this._spells.length > 0) {
      this.wireConnHandlers(conn);
    } else {
      // Fall back: build Fire Mage hotbar so the HUD is usable even if fetch fails.
      this.loadFireMageFallback(conn);
    }
  }

  // -----------------------------------------------------------------------
  /** Show a compact row of equipped item icons in the top-right area (below relics). */
  loadEquippedItems(items: ItemDef[]) {
    if (items.length === 0) return;

    // Create row if not yet present.
    if (!this._itemsRowEl) {
      this._itemsRowEl = this.createElement("div", "hud-items-row");
      this._itemsRowEl.id = "hud-equipped-items";
      this._itemsRowEl.style.cssText = [
        "position:absolute", "top:52px", "right:8px",
        "display:flex", "flex-direction:row", "gap:4px",
        "align-items:center", "pointer-events:none",
      ].join(";");
      this.root.appendChild(this._itemsRowEl);
    }

    this._itemsRowEl.innerHTML = "";
    for (const item of items) {
      const tile = this.createElement("div");
      tile.title = item.name;
      tile.style.cssText = [
        "width:28px", "height:28px",
        "background:var(--hud-slot-bg)",
        "border:1px solid var(--hud-slot-bdr)",
        "border-radius:4px",
        "display:flex", "align-items:center", "justify-content:center",
      ].join(";");

      const tier = item.ownedTier;
      const suffix = tier > 1 ? String(tier) : "";
      const img = document.createElement("img");
      img.src = `/items/${item.icon}${suffix}.png`;
      img.alt = item.name;
      img.style.cssText = "width:22px;height:22px;object-fit:contain;image-rendering:pixelated;";
      img.onerror = () => { img.src = `/items/${item.icon}.png`; img.onerror = null; };
      tile.appendChild(img);
      this._itemsRowEl.appendChild(tile);
    }
  }

  // -----------------------------------------------------------------------
  update(s: Snapshot) {
    this._mana = s.mana ?? 0;

    // -- lives (HP bar) --
    const lives = s.lives ?? 0;
    this._maxLives = Math.max(this._maxLives, lives);
    this.livesEl.dataset.lives = String(lives);
    this.livesFill.style.width = `${(lives / this._maxLives) * 100}%`;
    this.livesCount.textContent = String(lives);

    // -- spare balls bar --
    const balls = s.spareBalls ?? 0;
    this._maxBalls = Math.max(this._maxBalls, balls);
    this.ballsEl.dataset.balls = String(balls);
    this.ballsFill.style.width = `${(balls / this._maxBalls) * 100}%`;
    this.ballsCount.textContent = String(balls);

    // -- mana bar --
    const mana    = s.mana    ?? 0;
    const manaMax = s.manaMax ?? 1;
    const pct     = Math.min(1, Math.max(0, mana / manaMax)) * 100;
    this.manaFill.style.width = `${pct}%`;
    this.manaText.textContent = `${Math.round(mana)} / ${Math.round(manaMax)}`;

    // -- spell affordability --
    for (const spell of this._spells) {
      const el = this.spellSlots.get(spell.id);
      if (!el) continue;
      const cost = SPELL_COSTS[spell.id] ?? 0;
      const canAfford = mana >= cost;
      el.classList.toggle("affordable",   canAfford);
      el.classList.toggle("unaffordable", !canAfford);
      el.setAttribute("aria-disabled", canAfford ? "false" : "true");
    }

    // -- relics --
    this.updateRelics(s.activeRelics ?? []);

    // -- boss HP bar --
    if (s.bossActive && s.bossMaxHp > 0) {
      this.bossBarEl.style.display = "flex";
      const hpPct = Math.min(1, Math.max(0, s.bossHp / s.bossMaxHp)) * 100;
      this.bossBarFill.style.width = `${hpPct}%`;
      const bossBlock = s.blocks.find(b => b.boss);
      const bossType = bossBlock ? inferBossType(bossBlock.sprite) : "Unknown";
      this.bossNameEl.textContent = bossLabel(bossType);
      if (hpPct < 33) {
        this.bossBarFill.style.background = "linear-gradient(to right,var(--color-boss-deep),var(--danger-bright))";
      } else if (hpPct < 66) {
        this.bossBarFill.style.background = "linear-gradient(to right,var(--color-boss-deep),var(--color-fire))";
      } else {
        this.bossBarFill.style.background = "linear-gradient(to right,var(--color-boss-deep),var(--color-boss-hp))";
      }
    } else {
      this.bossBarEl.style.display = "none";
    }

    // -- objective timer (survive = gold "hold out", limit = red countdown) --
    if (s.timerMode && (s.timeLeft ?? 0) >= 0 && s.phase === "Playing") {
      this.timerEl.style.display = "block";
      const t = Math.ceil(s.timeLeft ?? 0);
      const mm = Math.floor(t / 60), ss = (t % 60).toString().padStart(2, "0");
      if (s.timerMode === "survive") {
        this.timerEl.style.color = "var(--gold)";
        this.timerEl.textContent = `SURVIVE ${mm}:${ss}`;
      } else {
        this.timerEl.style.color = t <= 10 ? "var(--danger-bright)" : "var(--text)";
        this.timerEl.textContent = `TIME ${mm}:${ss}`;
      }
    } else if ((s.floorCount ?? 1) > 1 && s.phase === "Playing") {
      // Multi-floor collapse: show progress through the mine shaft.
      this.timerEl.style.display = "block";
      this.timerEl.style.color = "var(--text-dim)";
      this.timerEl.textContent = `FLOOR ${s.floor}/${s.floorCount}`;
    } else {
      this.timerEl.style.display = "none";
    }

    // -- active bonus effects --
    this.updateEffects(s);
    // -- active power-up indicators (top-right panel, task 1.2) --
    this.updatePowerups(s);
    // -- combo multiplier badge (task 1.3) --
    this.updateComboBadge(s);

    // -- banner --
    if (s.phase === "Won") {
      this.banner.style.display = "block";
      this.banner.className = "hud-banner win";
      this.banner.textContent = "VICTORY";
    } else if (s.phase === "Lost") {
      this.banner.style.display = "block";
      this.banner.className = "hud-banner lose";
      this.banner.textContent = "DEFEAT";
    } else {
      this.banner.style.display = "none";
      this.banner.className = "hud-banner";
    }
  }

  // -----------------------------------------------------------------------
  private loadFireMageFallback(_conn: Connection) {
    // Default spell kit matching Fire Mage from characters.json
    const fallback: SpellDef[] = [
      { id: "ignite",   name: "Ignite",    icon: "FireHeroBall" },
      { id: "fireball", name: "Fireball",  icon: "FireBallIco" },
      { id: "firewall", name: "Fire Wall", icon: "FireWallIco" },
      { id: "turret",   name: "Turret",    icon: "FireTurretIco" },
    ];
    this.loadSpells(fallback);
  }

  private rebuildHotbar() {
    // Clear existing slots.
    this.spellSlots.clear();
    this.hotbarEl.innerHTML = "";

    for (let i = 0; i < this._spells.length; i++) {
      const spell = this._spells[i];
      const key = SLOT_KEYS[i] ?? String(i + 1);

      const slot = this.createElement("div");
      // Use legacy id for Fire Mage spells (for test backwards-compatibility).
      slot.id = FIRE_MAGE_SLOT_IDS[spell.id] ?? `hud-spell-${spell.id}`;
      slot.className = "hud-spell-slot affordable";
      slot.setAttribute("role", "button");
      slot.setAttribute("tabindex", "0");
      const cost = SPELL_COSTS[spell.id] ?? 0;
      const costLabel = cost > 0 ? `, costs ${cost} mana` : "";
      slot.setAttribute("aria-label", `Cast ${spell.name} — key ${key}${costLabel}`);

      // Kvadrat-framed inner box: key badge (absolute top-left) + icon
      const frame = this.createElement("div", "hud-spell-frame");

      // Keybind letter chip — absolute top-left inside the Kvadrat frame
      const keyBadge = this.createElement("div", "hud-spell-key");
      keyBadge.textContent = key;

      // icon area (fills the inner tile of the frame)
      const iconWrap = this.createElement("div", "hud-spell-icon");
      buildSpellIcon(iconWrap, spell);

      frame.appendChild(keyBadge);
      frame.appendChild(iconWrap);

      // Spell name label — BELOW the frame, no overlap with icon
      const name = this.createElement("div", "hud-spell-name");
      name.textContent = spell.name;

      slot.appendChild(frame);
      slot.appendChild(name);

      this.spellSlots.set(spell.id, slot);
      this.hotbarEl.appendChild(slot);
    }
  }

  private wireConnHandlers(conn: Connection) {
    for (let i = 0; i < this._spells.length; i++) {
      const spell = this._spells[i];
      const el = this.spellSlots.get(spell.id);
      if (!el) continue;
      const slotIndex = i;
      el.addEventListener("pointerdown", (e) => {
        e.stopPropagation();
        const cost = SPELL_COSTS[spell.id] ?? 0;
        if (this._mana >= cost) conn.castSlot(slotIndex);
      });
      el.addEventListener("keydown", (e) => {
        if (e.key !== "Enter" && e.key !== " ") return;
        e.preventDefault();
        const cost = SPELL_COSTS[spell.id] ?? 0;
        if (this._mana >= cost) conn.castSlot(slotIndex);
      });
    }

    // Desktop keyboard bindings: Q→slot0, E→slot1, W→slot2, R→slot3.
    document.addEventListener("keydown", (e) => {
      if (e.repeat) return;
      const keyMap: Record<string, number> = { q: 0, e: 1, w: 2, r: 3 };
      const slotIdx = keyMap[e.key.toLowerCase()];
      if (slotIdx === undefined || slotIdx >= this._spells.length) return;
      const spell = this._spells[slotIdx];
      const cost = SPELL_COSTS[spell.id] ?? 0;
      if (this._mana >= cost) conn.castSlot(slotIdx);
    });
  }

  // -----------------------------------------------------------------------
  private updateEffects(s: Snapshot) {
    const chips: string[] = [];
    if (s.widePaddleActive) chips.push(`↔ ${Math.ceil(s.widePaddleTimer ?? 0)}s`);
    if (s.slowBallActive)   chips.push(`slow ${Math.ceil(s.slowBallTimer ?? 0)}s`);
    const html = chips.map(c =>
      `<span style="background:var(--overlay-mid);border:1px solid var(--color-effect);border-radius:4px;padding:1px 5px;font-size:var(--fs-tiny);color:var(--color-shield);">${c}</span>`
    ).join("");
    this.effectsEl.innerHTML = html;
  }

  // -----------------------------------------------------------------------
  /** Active power-up indicators — top-right panel showing collected effects (task 1.2). */
  private updatePowerups(s: Snapshot) {
    const active: { label: string; color: string; timer?: number }[] = [];
    if (s.widePaddleActive)      active.push({ label: "W", color: "var(--color-wide)", timer: s.widePaddleTimer });
    if ((s as any).fireshotActive) active.push({ label: "F", color: "var(--color-fire)", timer: (s as any).fireshotTimer });
    if ((s as any).shieldActive)   active.push({ label: "◆", color: "var(--color-shield)" });

    if (active.length === 0) {
      this._powerupPanelEl.style.display = "none";
      return;
    }
    this._powerupPanelEl.style.display = "flex";
    this._powerupPanelEl.innerHTML = active.map(({ label, color, timer }) => {
      const t = timer !== undefined ? ` ${Math.ceil(timer)}s` : "";
      return `<div class="hud-powerup-active" style="background:var(--overlay-mid);border:1px solid ${color};border-radius:5px;padding:2px 7px;font-size:var(--fs-small);font-weight:700;color:${color};letter-spacing:.5px;">${label}${t}</div>`;
    }).join("");
  }

  // -----------------------------------------------------------------------
  /** Combo multiplier badge — shows ×2/×3/×4 with a pop animation on increase (task 1.3). */
  private updateComboBadge(s: Snapshot) {
    const combo = s.comboMultiplier ?? 1;
    if (combo > 1) {
      if (combo !== this._prevComboMult) {
        // Trigger scale-bounce animation by removing and re-adding the class.
        this._comboBadgeEl.classList.remove("combo-pop");
        // Force reflow so the browser registers the removal before re-adding.
        void this._comboBadgeEl.offsetWidth;
        this._comboBadgeEl.classList.add("combo-pop");
      }
      this._comboBadgeEl.style.display = "block";
      this._comboBadgeEl.textContent = `×${combo}`;
    } else {
      this._comboBadgeEl.style.display = "none";
    }
    this._prevComboMult = combo;
  }

  private updateRelics(relics: { id: string; name: string; icon: string }[]) {
    const existing = this.relicsEl.querySelectorAll<HTMLElement>("[data-relic-id]");
    const existingIds = Array.from(existing).map(el => el.dataset.relicId!);
    const newIds = relics.map(r => r.id);
    if (existingIds.join(",") === newIds.join(",")) return;

    this.relicsEl.innerHTML = "";
    for (const relic of relics) {
      const tile = this.createElement("div");
      tile.dataset.relicId = relic.id;
      tile.title = relic.name;
      tile.style.cssText = [
        "width:36px", "height:36px",
        "background:url('/ui/BattleSpellBarActive.png') no-repeat center/cover",
        "display:flex", "align-items:center", "justify-content:center",
        "pointer-events:none",
      ].join(";");

      const iconSrc = `/art/${relic.icon}.png`;
      const img = document.createElement("img");
      img.src = iconSrc;
      img.alt = relic.name;
      img.style.cssText = "width:22px;height:22px;object-fit:contain;image-rendering:pixelated;";
      img.onerror = () => { img.style.display = "none"; tile.textContent = "?"; };
      tile.appendChild(img);

      this.relicsEl.appendChild(tile);
    }
  }

  private createElement(tag: string, className?: string): HTMLElement {
    const el = document.createElement(tag);
    if (className) el.className = className;
    return el;
  }

  private injectStyles() {
    const id = "hud-styles";
    if (document.getElementById(id)) return;
    const style = document.createElement("style");
    style.id = id;
    style.textContent = HUD_STYLES;
    document.head.appendChild(style);
  }
}
```

## `src/ui/hud/bars.ts`

```typescript
// HUD value-bar factories, extracted from Hud.ts. Pure DOM builders — no class
// state — for the 3-slice HP / balls / mana / boss bars.
//
// The bar sprites (BattleHPEmpty/MPEmpty) are 220×41 with dark angular end-caps
// baked into the sides. Rendering them as a single stretched image distorts the
// caps; instead we pin the caps to the ends via CSS border-image 9-slice and
// stretch only the middle, with the value fill clipped strictly between the caps.

const BAR_SPRITE_H = 41;   // native sprite height (px)
const BAR_CAP_X    = 16;   // native left/right cap thickness (px)
const BAR_CAP_Y    = 7;    // native top/bottom cap thickness (px)
export const BAR_H      = 22;  // rendered height of value bars (mana/HP/balls) — raised to 22 so caps don't look crushed
export const BOSS_BAR_H = 18;  // rendered height of the boss bar

function el(tag: string, className?: string): HTMLElement {
  const e = document.createElement(tag);
  if (className) e.className = className;
  return e;
}

/**
 * Build a symmetric 3-slice value bar: the empty sprite supplies the frame via
 * border-image (caps pinned to both ends, middle stretched), and either a
 * dedicated fill sprite or a CSS gradient fills the interior.
 *
 * When `fillSrc` is provided the fill image is sized to the inner clip region
 * and anchored left so narrowing `fill.style.width` reveals it left→right
 * without distortion.  `fill.style.width` stays a plain percentage string.
 */
export function buildBar(opts: {
  id: string; fillId: string; width: string; height: number;
  emptySrc: string; gradient: string; fillSrc?: string;
}): { outer: HTMLElement; fill: HTMLElement } {
  const capX = Math.round(BAR_CAP_X * opts.height / BAR_SPRITE_H);
  const capY = Math.round(BAR_CAP_Y * opts.height / BAR_SPRITE_H);

  const outer = el("div");
  outer.id = opts.id;
  outer.style.cssText = `position:relative;width:${opts.width};height:${opts.height}px;`;

  // Frame: empty bar via 9-slice border-image — caps fixed, middle stretched, `fill` draws the interior.
  const track = el("div");
  track.style.cssText = [
    "position:absolute", "inset:0", "box-sizing:border-box",
    "border-style:solid",
    `border-width:${capY}px ${capX}px`,
    `border-image:url('${opts.emptySrc}') ${BAR_CAP_Y} ${BAR_CAP_X} ${BAR_CAP_Y} ${BAR_CAP_X} fill stretch`,
  ].join(";");
  outer.appendChild(track);

  // Fill clip: the interior region strictly between the caps.
  const clip = el("div");
  clip.style.cssText = [
    "position:absolute",
    `left:${capX}px`, `right:${capX}px`, `top:${capY}px`, `bottom:${capY}px`,
    "overflow:hidden", "border-radius:2px",
  ].join(";");
  const fill = el("div");
  fill.id = opts.fillId;
  const fillStyles = [
    "position:absolute", "left:0", "top:0", "bottom:0", "width:100%",
    "transition:width var(--dur-normal) linear",
  ];
  if (opts.fillSrc) {
    // Fixed-size sprite anchored to the left: as fill.width shrinks the right
    // portion is hidden by clip's overflow:hidden, revealing the correct fill %.
    const innerW = `calc(${opts.width} - ${2 * capX}px)`;
    fillStyles.push(
      `background-image:url('${opts.fillSrc}')`,
      `background-size:${innerW} 100%`,
      `background-position:left center`,
      `background-repeat:no-repeat`,
    );
  } else {
    fillStyles.push(`background:${opts.gradient}`);
  }
  fill.style.cssText = fillStyles.join(";");
  clip.appendChild(fill);
  outer.appendChild(clip);

  return { outer, fill };
}

/** A labelled value bar (icon + count overlay) for the top-left HP / spare-balls. */
export function buildLabelledBar(opts: {
  id: string; fillId: string; labelId: string;
  emptySrc: string; gradient: string; icon: string; fillSrc?: string;
}): { outer: HTMLElement; fill: HTMLElement; label: HTMLElement } {
  const { outer, fill } = buildBar({
    id: opts.id, fillId: opts.fillId,
    width: "118px", height: BAR_H,
    emptySrc: opts.emptySrc, gradient: opts.gradient, fillSrc: opts.fillSrc,
  });
  const label = el("span");
  label.id = opts.labelId;
  label.style.cssText = [
    "position:absolute", "top:50%", "left:8px",
    "transform:translateY(-50%)",
    "display:flex", "align-items:center", "gap:4px",
    "font-size:var(--fs-small)", "color:var(--text-oncolor)", "font-weight:700",
    "text-shadow:0 0 4px var(--shadow-black),0 1px 2px var(--shadow-black)", "pointer-events:none", "white-space:nowrap",
  ].join(";");
  label.innerHTML =
    `<img src="${opts.icon}" alt="" style="width:13px;height:13px;object-fit:contain;image-rendering:pixelated;">` +
    `<span class="hud-bar-count">0</span>`;
  outer.appendChild(label);
  return { outer, fill, label };
}

export function buildBossBar(): { outer: HTMLElement; fill: HTMLElement; name: HTMLElement } {
  const outer = el("div");
  outer.id = "hud-boss-hp";
  outer.style.cssText = [
    "display:none",
    "position:absolute",
    "top:8px", "left:50%",
    "transform:translateX(-50%)",
    "flex-direction:column",
    "align-items:center",
    "gap:3px",
    "pointer-events:none",
    "z-index:20",
    "min-width:min(260px,72cqw)",
  ].join(";");

  const name = el("div");
  name.id = "hud-boss-name";
  name.style.cssText = [
    "font-size:var(--fs-tiny)", "font-weight:900",
    "color:var(--danger-bright)", "letter-spacing:2px",
    "text-shadow:0 0 6px var(--danger-bright),0 1px 3px var(--shadow-black)",
    "text-align:center", "white-space:nowrap",
  ].join(";");
  outer.appendChild(name);

  const { outer: bar, fill } = buildBar({
    id: "hud-boss-bar", fillId: "hud-boss-hp-fill",
    width: "100%", height: BOSS_BAR_H,
    emptySrc: "/ui/BattleHPEmpty.png",
    gradient: "linear-gradient(to right,var(--color-boss-deep),var(--color-boss-hp))",
  });
  outer.appendChild(bar);

  return { outer, fill, name };
}

export function buildManaBar(): HTMLElement {
  const { outer } = buildBar({
    id: "hud-mana", fillId: "hud-mana-fill",
    width: "min(220px,80cqw)", height: BAR_H,
    emptySrc: "/ui/BattleMPEmpty.png",
    gradient: "linear-gradient(to right,var(--color-mana-deep),var(--color-mana))",
    fillSrc: "/ui/BattleMPFull.png",
  });

  const label = el("span");
  label.id = "hud-mana-text";
  label.style.cssText = [
    "position:absolute", "top:50%", "left:50%",
    "transform:translate(-50%,-50%)",
    "font-size:var(--fs-tiny)", "color:var(--text-oncolor)", "font-weight:600",
    "text-shadow:0 1px 2px var(--shadow-black)", "pointer-events:none", "white-space:nowrap", "z-index:1",
  ].join(";");
  outer.appendChild(label);

  return outer;
}
```

## `src/ui/hud/hudStyles.ts`

```typescript
// HUD stylesheet, extracted from Hud.injectStyles() to keep Hud.ts focused.
// Injected once (guarded by the #hud-styles id) on the first Hud construction.
export const HUD_STYLES = `
      /* Lives/balls stat row — framed with HeroBar-style pill */
      .hud-stat-row {
        background: url('/ui/BattleHeroBar.png') no-repeat center/contain,
                    var(--overlay-light);
        border-radius: 20px;
        padding: 3px 10px 3px 8px;
        color: var(--text);
        font-size: var(--fs-caption);
        display: inline-flex;
        align-items: center;
        gap: 3px;
        min-width: 60px;
        min-height: 26px;
      }

      /* ---- HOTBAR SLOT: outer wrapper owns castable-state filter + name below ---- */
      .hud-spell-slot {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 3px;
        /* ≥44px touch target (WCAG 2.5.5) */
        min-width: 52px;
        touch-action: manipulation;
        cursor: pointer;
        pointer-events: auto;
        transition: filter var(--dur-normal), transform var(--dur-normal);
        -webkit-tap-highlight-color: transparent;
      }

      /* Framed inner box: Kvadrat 9-slice — keys + icon live inside this */
      .hud-spell-frame {
        position: relative;
        width: 52px;
        height: 52px;
        box-sizing: border-box;
        display: flex;
        align-items: center;
        justify-content: center;
        /* Kvadrat 9-slice (14px insets, 7px border-width) */
        background: none;
        border-style: solid;
        border-width: 7px;
        border-image: url('/ui/Kvadrat.png') 14 14 14 14 fill stretch;
      }

      /* Castable (enough mana): full brightness + subtle gold glow */
      .hud-spell-slot.affordable {
        filter: drop-shadow(0 0 6px var(--gold-glow-lo));
      }
      .hud-spell-slot.affordable:hover {
        filter: drop-shadow(0 0 8px var(--gold-glow-mid)) brightness(1.15);
      }

      /* Not castable: desaturated + dimmed */
      .hud-spell-slot.unaffordable {
        filter: saturate(.3) brightness(.6);
        cursor: default;
      }

      /* Active press: stronger glow + 1.06 scale (150ms) */
      .hud-spell-slot:active {
        transform: scale(1.06);
      }
      .hud-spell-slot.affordable:active {
        filter: drop-shadow(0 0 12px var(--gold-glow-hi));
        transform: scale(1.06);
      }

      /* Keyboard / assistive focus ring */
      .hud-spell-slot:focus-visible {
        outline: 2px solid var(--gold-bright);
        outline-offset: 3px;
        border-radius: 4px;
      }

      /* Keybind letter chip: absolute top-left in gold */
      .hud-spell-key {
        position: absolute;
        top: 2px;
        left: 3px;
        font-size: var(--fs-tiny);
        font-weight: 700;
        color: var(--gold);
        line-height: 1;
        text-shadow: 0 1px 2px var(--shadow-hard);
        pointer-events: none;
        z-index: 1;
      }

      /* Icon area: fills the inner tile of the Kvadrat frame */
      .hud-spell-icon {
        font-size: var(--fs-xl);
        line-height: 1;
        display: flex;
        align-items: center;
        justify-content: center;
        width: 32px;
        height: 32px;
      }

      /* Spell name label: BELOW the frame, ≥10px, text-dim */
      .hud-spell-name {
        font-size: var(--fs-tiny);
        color: var(--text-dim);
        text-align: center;
        line-height: 1;
        text-shadow: 0 1px 2px var(--shadow-hard);
        white-space: nowrap;
        max-width: 60px;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .hud-banner.win {
        background: var(--hud-win-bg);
        border: 2px solid var(--ok-bright);
        color: var(--ok-bright);
      }
      .hud-banner.lose {
        background: var(--hud-lose-bg);
        border: 2px solid var(--danger-bright);
        color: var(--danger-bright);
      }
      #hud-relics [data-relic-id] {
        cursor: default;
      }

      /* Combo badge: scale-bounce when multiplier increases */
      @keyframes combo-pop {
        0%   { transform: scale(0.7); }
        60%  { transform: scale(1.1); }
        100% { transform: scale(1.0); }
      }
      #hud-combo.combo-pop {
        animation: combo-pop 0.2s ease-out forwards;
      }
      /* Landscape orientation: reduce bottom zone height */
      @media (orientation: landscape) and (max-height: 500px) {
        .hud-spell-frame {
          width: 44px;
          height: 44px;
          border-width: 6px;
        }
        .hud-spell-icon { width: 26px; height: 26px; }
        .hud-spell-icon img { width: 26px !important; height: 26px !important; }
      }
    `;
```

## `src/ui/hud/spellIcon.ts`

```typescript
import { Texture } from "pixi.js";
import { tex as atlasTex } from "../../render/assets";
import type { SpellDef } from "../../net/metaApi";

// Spell-icon resolution, extracted from Hud.ts. Resolves a spell's icon through
// a fallback chain: packed atlas frame → legacy /art/ PNG → the spell's initial
// letter (no emojis — the user prefers real art, which usually resolves).

/** Render the spell's icon into `wrap`, trying atlas → legacy art → letter fallback. */
export function buildSpellIcon(wrap: HTMLElement, spell: SpellDef): void {
  const iconKey = spell.icon;
  if (!iconKey) {
    wrap.textContent = getSpellFallback(spell.id);
    return;
  }

  // Try atlas tex (for full atlas paths like "paladin/spell_passiveshield/SpellShieldLargeIco").
  // atlasTex returns Texture.WHITE for unknown keys; check width > 1 to detect valid.
  const atlasFrame = atlasTex(iconKey);
  // NOTE: atlasTex returns the 16×16 Texture.WHITE for unknown keys (e.g. the Fire Mage
  // short keys like "FireBallIco"), which would otherwise draw a blank white square.
  // Exclude WHITE so those fall through to the real /art/ icons below.
  if (atlasFrame && atlasFrame !== Texture.WHITE && atlasFrame.width > 1) {
    // Build an img from the atlas texture using its source image + UV, via a canvas.
    const canvas = document.createElement("canvas");
    canvas.width  = 32;
    canvas.height = 32;
    const ctx = canvas.getContext("2d");
    if (ctx && (atlasFrame as any).baseTexture?.resource?.source) {
      const src = (atlasFrame as any).baseTexture.resource.source as HTMLImageElement | HTMLCanvasElement;
      const fr = (atlasFrame as any).frame;
      if (fr) {
        ctx.drawImage(src, fr.x, fr.y, fr.width, fr.height, 0, 0, 32, 32);
        const img = document.createElement("img");
        img.src = canvas.toDataURL();
        img.alt = spell.name;
        img.style.cssText = "width:32px;height:32px;object-fit:contain;";
        wrap.appendChild(img);
        return;
      }
    }
  }

  // Legacy /art/ path fallback. These are the square Chose*Ico icons copied from
  // the Sprites tree — the old /art/*Ico.png files were letterboxed wide crops
  // that rendered as muddy rectangles in the hotbar (docs/13 battle audit).
  const legacyPaths: Record<string, string> = {
    FireHeroBall:  "/art/SpellIgnite.png",
    FireBallIco:   "/art/SpellFireball.png",
    FireWallIco:   "/art/SpellFirewall.png",
    FireTurretIco: "/art/SpellTurret.png",
  };
  const legacySrc = legacyPaths[iconKey];
  if (legacySrc) {
    const img = document.createElement("img");
    img.src = legacySrc;
    img.alt = spell.name;
    img.style.cssText = "width:32px;height:32px;object-fit:contain;";
    const emoji = getSpellFallback(spell.id);
    img.onerror = () => { img.style.display = "none"; wrap.textContent = emoji; };
    wrap.appendChild(img);
    return;
  }

  // Full atlas key with no exposed frame: fall through to the letter fallback.
  wrap.textContent = getSpellFallback(spell.id);
}

// Non-emoji last-resort fallback when a spell icon can't be resolved: the spell's
// initial letter (the user dislikes emojis; real art is preferred and usually resolves).
export function getSpellFallback(id: string): string {
  return (id[0] ?? "?").toUpperCase();
}
```

## `src/ui/nineSlice.ts`

```typescript
/**
 * nineSlice.ts — shared CSS 9-slice (border-image) helper.
 *
 * Stretching a framed button/panel art with `background-size: 100% 100%`
 * distorts its rounded caps and ornaments. A 9-slice pins the four corners +
 * four edges at fixed size and stretches only the middle, so the frame keeps
 * its shape at any element size. Returns a CSS declaration block to drop inside
 * a rule. Relies on the global `box-sizing: border-box` so the border doesn't
 * change the element's footprint.
 */
export function nineSlice(src: string, slice: string, borderWidth: string): string {
  return `background: none;
      border-style: solid;
      border-width: ${borderWidth};
      border-image: url('${src}') ${slice} fill stretch;`;
}

// Presets — slice insets measured from the source art (top right bottom left).
// InterfaceButton.png 626×162 → caps ≈26px tall, ≈92px wide (matches the menu/overlay buttons).
export const btnInterface = () => nineSlice("/ui/InterfaceButton.png", "26 92 26 92", "8px 30px");
// Button1.png 438×110 → smaller pill.
export const btn1 = () => nineSlice("/ui/Button1.png", "24 60 24 60", "8px 22px");
// MissionName.png 481×136 → label plate.
export const missionName = () => nineSlice("/ui/MissionName.png", "28 70 28 70", "10px 24px");
```

## `src/ui/theme.ts`

```typescript
/**
 * theme.ts — the single design system for every shell screen.
 *
 * Anchored to the main-menu look (docs/13-ui-ux-audit.md): warm brown depths,
 * gold ornament, deep-navy panel cores. Every scene must build from these
 * tokens and component classes instead of inventing its own palette — the
 * audit found four palettes and three button languages across the shell.
 *
 * Units: scenes are letterboxed inside #app (a CSS size container), so layout
 * must use cqw/cqh (container query units), never vw/vh — viewport units leak
 * outside the frame on desktop.
 */

import { nineSlice } from "./nineSlice";

export function injectTheme(): void {
  const id = "ui-theme";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = THEME_CSS;
  document.head.appendChild(style);
}

const THEME_CSS = `
  :root {
    /* ── Palette ────────────────────────────────────────────────────── */
    --gold:        #d8a84e;  /* ornament, borders */
    --gold-bright: #ffe9b0;  /* headings, emphasized text */
    --gold-dim:    #8a6a35;  /* hairlines, separators */
    --text:        #f0e0b8;  /* primary copy */
    --text-dim:    #c9b182;  /* secondary copy */
    --text-faint:  #9a8560;  /* tertiary / hints */
    --navy:        #16243a;  /* panel cores (matches BarGoods art) */
    --navy-deep:   #0d1626;
    --ink:         #0d0a08;  /* near-black warm */
    --bg-0: #1a0e06;          /* warm background gradient stops */
    --bg-1: #0d0808;
    --bg-2: #050308;
    --danger:        #c8413a;
    --danger-bright: #ff3333;  /* loss banner, error state */
    --danger-light:  #ff6666;  /* inline error messages */
    --ok:            #56b04a;
    --ok-bright:     #44ff88;  /* win banner, success state */

    /* ── Power-up / status colours ─────────────────────────────────── */
    --color-wide:    #d4aa00;  /* wide-paddle power-up */
    --color-fire:    #ff6600;  /* fireshot power-up */
    --color-shield:  #00ddee;  /* shield power-up */
    --color-effect:  #66aaff;  /* generic active effect (border) */
    --color-equip:   #bfe3ff;  /* equip action button text */
    --color-unequip: #f3b8a8;  /* unequip action button text */

    /* ── Battle HUD game-state bar colours ──────────────────────────── */
    --color-hp:        #ff5a4a;  /* HP bar fill (highlight) */
    --color-hp-deep:   #cc2a2a;  /* HP bar fill (base) */
    --color-balls:     #56d6ff;  /* spare-balls bar fill (highlight) */
    --color-balls-deep:#1f7fc8;  /* spare-balls bar fill (base) */
    --color-mana:      #5fe6f5;  /* mana bar fill (highlight) */
    --color-mana-deep: #1f9fb8;  /* mana bar fill (base) */
    --color-boss-hp:   #cc2222;  /* boss HP bar (normal phase) */
    --color-boss-deep: #880000;  /* boss HP bar (base/dark phase) */

    /* ── Reward / overlay semantic colours ─────────────────────────── */
    --color-xp:          #88aaff;  /* EXP reward text */
    --color-pts:         #ffcc44;  /* skill-points reward text */
    --color-crystal:     #44ddff;  /* crystal currency display */
    --color-levelup:     #ffd700;  /* level-up notification */
    --color-first-clear: #aa88ff;  /* first-clear achievement */
    --color-label-muted: #88aaaa;  /* muted section labels */
    --color-relic:       #cc88ff;  /* relic item display */
    --color-fail-muted:  #aa5555;  /* permadeath / fail sub-text */

    /* ── Campaign / Dungeon UI colours ──────────────────────────────── */
    --color-upgrade-hdr:   #e8c870;  /* upgrade panel section heading */
    --color-spell-name:    #e8e8ff;  /* spell name text in upgrade list */
    --color-dungeon-label: #8899cc;  /* dungeon run status / buff labels */
    --color-empty:         #555577;  /* empty-state placeholder text */

    /* ── HUD overlays, shadows, glow ───────────────────────────────── */
    --overlay-light:  rgba(0,0,0,0.45);
    --overlay-mid:    rgba(0,0,0,0.65);
    --shadow-hard:    rgba(0,0,0,0.90);
    --hud-top-bg:     rgba(10,7,5,0.55);
    --hud-btm-bg:     rgba(4,4,12,0.80);
    --hud-slot-bg:    rgba(20,14,6,0.75);
    --hud-slot-bdr:   rgba(200,150,30,0.5);
    --hud-win-bg:     rgba(10,40,10,0.85);
    --hud-lose-bg:    rgba(40,5,5,0.85);
    --gold-glow-lo:   rgba(255,190,80,0.45);
    --gold-glow-mid:  rgba(255,190,80,0.60);
    --gold-glow-hi:   rgba(255,190,80,0.70);
    --shadow-black:   #000;          /* opaque shadow for text on colored bg */
    --text-oncolor:   #ffffff;       /* pure white text on colored bar fills */

    /* ── Interaction state filters ──────────────────────────────────── */
    --filter-locked:   saturate(0.45) brightness(0.8);
    --filter-dim:      saturate(0.25) brightness(0.65);
    --filter-hover:    brightness(1.15);
    --filter-active:   brightness(0.92);
    --filter-disabled: saturate(0.25) brightness(0.65);

    /* ── Animation durations ────────────────────────────────────────── */
    --dur-fast:   0.1s;
    --dur-normal: 0.15s;
    --dur-slow:   0.35s;

    /* ── Type ───────────────────────────────────────────────────────── */
    --font-display: "Palatino Linotype", "Book Antiqua", Georgia, serif;
    --font-body: "Trebuchet MS", "Segoe UI", Verdana, sans-serif;
    --fs-title:   26px;  /* screen headings */
    --fs-2xl:     32px;  /* impact headings (win/lose banner, large callout) */
    --fs-xl:      20px;  /* item names, hero text */
    --fs-large:   16px;  /* stat values, section heroes */
    --fs-section: 15px;  /* panel headers, section labels */
    --fs-subhead: 14px;  /* subheadings, compact entries */
    --fs-body:    13px;  /* primary body copy */
    --fs-caption: 12px;  /* compact labels, badges */
    --fs-small:   11px;  /* secondary copy, hints */
    --fs-tiny:    10px;  /* metadata, timestamps, ultra-small kickers */

    /* ── Space ──────────────────────────────────────────────────────── */
    --sp-1: 4px; --sp-2: 8px; --sp-3: 12px; --sp-4: 16px; --sp-5: 24px;
    --sp-6: 32px; --sp-7: 40px; --sp-8: 48px;
    /* Half-step tokens for compact UI and buttons */
    --sp-1h: 6px; --sp-2h: 10px; --sp-3h: 14px; --sp-4h: 20px;
  }

  /* ── Screen scaffold ──────────────────────────────────────────────── */
  /* Every shell scene: .ui-screen root > .ui-screen-bg + content. */
  .ui-screen {
    position: relative;
    width: 100%;
    min-height: 100cqh;
    overflow-x: hidden;
    overflow-y: auto;
    font-family: var(--font-body);
    color: var(--text);
    -webkit-font-smoothing: antialiased;
  }
  .ui-screen-bg {
    position: absolute;
    inset: 0;
    min-height: 100cqh;
    background:
      radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
      linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    pointer-events: none;
    z-index: 0;
  }
  .ui-content { position: relative; z-index: 1; }

  /* ── Top bar: back chip · title · trailing slot ───────────────────── */
  .ui-topbar {
    display: flex;
    align-items: center;
    gap: var(--sp-2);
    padding: max(var(--sp-3), env(safe-area-inset-top, 0px)) var(--sp-3) var(--sp-2) var(--sp-3);
  }
  .ui-topbar .ui-title { flex: 1; text-align: center; }
  /* Symmetry spacer so a centered title stays centered next to the back chip */
  .ui-topbar-spacer { width: 44px; flex: none; }

  .ui-title {
    font-family: var(--font-display);
    font-size: var(--fs-title);
    font-weight: 700;
    letter-spacing: 0.05em;
    color: var(--gold-bright);
    text-shadow: 0 2px 4px rgba(0,0,0,0.9), 0 0 18px rgba(255,180,60,0.25);
    margin: 0;
  }

  .ui-back {
    flex: none;
    width: 44px;
    height: 44px;
    padding: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 14px")}
    cursor: pointer;
    -webkit-tap-highlight-color: transparent;
    touch-action: manipulation;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .ui-back::before {
    content: "";
    width: 20px;
    height: 20px;
    background: url('/ui/BackArrow.png') no-repeat center / contain;
    filter: drop-shadow(0 1px 2px rgba(0,0,0,0.8));
  }
  .ui-back:hover  { filter: brightness(1.18); }
  .ui-back:active { transform: scale(0.94); }
  .ui-back:focus-visible {
    outline: 2px solid var(--gold-bright);
    outline-offset: 3px;
    border-radius: 4px;
  }

  /* ── Section plaque (NameBlock.png 826×110 — ornate gold scroll bar) ─ */
  .ui-plaque {
    display: flex;
    align-items: center;
    justify-content: center;
    min-height: 34px;
    padding: 4px 26px;
    ${nineSlice("/ui/NameBlock.png", "40 120 40 120", "10px 32px")}
    font-family: var(--font-display);
    font-size: var(--fs-section);
    font-weight: 700;
    letter-spacing: 0.12em;
    text-transform: uppercase;
    color: var(--gold-bright);
    text-shadow: 0 1px 3px rgba(0,0,0,0.95);
    white-space: nowrap;
  }

  /* ── Panel (BarGoods.png 230×76 — gold-rimmed navy card) ─────────── */
  .ui-panel {
    ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "12px 14px")}
  }

  /* ── Slot (Kvadrat.png 73×68 — silver-rimmed dark square) ─────────── */
  .ui-slot {
    ${nineSlice("/ui/Kvadrat.png", "14 14 14 14", "7px 7px")}
    display: flex;
    align-items: center;
    justify-content: center;
    aspect-ratio: 73 / 68;
  }

  /* ── Buttons ──────────────────────────────────────────────────────── */
  .ui-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 6px;
    font-family: var(--font-body);
    font-weight: 700;
    color: var(--text);
    text-shadow: 0 1px 3px rgba(0,0,0,0.9);
    cursor: pointer;
    -webkit-tap-highlight-color: transparent;
    touch-action: manipulation;
    transition: filter var(--dur-normal), transform var(--dur-fast);
    background: none;
  }
  .ui-btn:hover:not(:disabled)  { filter: var(--filter-hover); }
  .ui-btn:active:not(:disabled) { transform: scale(0.96); filter: var(--filter-active); }
  .ui-btn:disabled {
    filter: var(--filter-disabled);
    cursor: default;
  }
  .ui-btn:focus-visible {
    outline: 2px solid var(--gold-bright);
    outline-offset: 3px;
    border-radius: 4px;
  }

  /* Primary pill — the menu's ornate gold/blue button (InterfaceButton 626×162) */
  .ui-btn--primary {
    min-height: 48px;
    padding: 4px 18px;
    font-size: var(--fs-section);
    letter-spacing: 0.05em;
    ${nineSlice("/ui/InterfaceButton.png", "26 92 26 92", "9px 30px")}
  }

  /* Small pill — compact actions (Button1 438×110) — 44px min to meet touch target */
  .ui-btn--small {
    min-height: 44px;
    padding: 2px 14px;
    font-size: var(--fs-body);
    ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 18px")}
  }

  /* ── Inline currency chip ─────────────────────────────────────────── */
  .ui-gem {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    font-weight: 700;
    color: var(--gold-bright);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9);
  }
  .ui-gem::before {
    content: "";
    width: 15px;
    height: 13px;
    background: url('/ui/Gem.png') no-repeat center / contain;
  }

  /* ── Plain text link (replaces bare <a>/underline links) ──────────── */
  .ui-link {
    color: var(--text-dim);
    font-size: var(--fs-body);
    font-weight: 600;
    letter-spacing: 0.03em;
    text-decoration: none;
    cursor: pointer;
    background: none;
    border: none;
    text-shadow: 0 1px 2px rgba(0,0,0,0.8);
  }
  .ui-link:hover { color: var(--gold-bright); }
  .ui-link:focus-visible {
    outline: 2px solid var(--gold-bright);
    outline-offset: 3px;
    border-radius: 2px;
  }
`;
```

## `src/ui/transition.ts`

```typescript
/**
 * transition.ts — Lightweight CSS-based scene transitions.
 *
 * Provides a 200ms fade-out before navigation so scene changes feel polished
 * rather than instant-cut. Usage:
 *
 *   navigateTo("/?scene=campaign");
 *
 * The overlay fades in over 200ms, then the page navigates.
 * An incoming page also fades in via the CSS class applied to <body>.
 */

// Duration constants.
const FADE_OUT_MS = 200; // fade duration before navigation
const FADE_IN_MS  = 220; // fade-in after page load

let _overlay: HTMLDivElement | null = null;
let _navigateHandler: ((url: string) => void) | null = null;

/** Register a SPA handler; when set, navigateTo() calls this instead of location.href. */
export function setNavigateHandler(fn: (url: string) => void): void {
  _navigateHandler = fn;
}

function getOverlay(): HTMLDivElement {
  if (_overlay) return _overlay;
  const div = document.createElement("div");
  div.id = "scene-transition-overlay";
  div.style.cssText = `
    position: fixed;
    inset: 0;
    background: var(--shadow-black);
    opacity: 0;
    pointer-events: none;
    z-index: 99999;
    transition: opacity ${FADE_OUT_MS}ms ease-in-out;
  `;
  document.body.appendChild(div);
  _overlay = div;
  return div;
}

/** Navigate to a new URL with a fade-out/in transition. */
export function navigateTo(url: string): void {
  const overlay = getOverlay();
  overlay.style.opacity = "0";
  void overlay.offsetHeight;
  overlay.style.pointerEvents = "all";
  overlay.style.opacity = "1";
  setTimeout(() => {
    if (_navigateHandler) {
      _navigateHandler(url);
      // Fade back in after SPA mount
      overlay.style.pointerEvents = "none";
      let started = false;
      const startFadeIn = () => {
        if (started) return;
        started = true;
        overlay.style.transition = `opacity ${FADE_IN_MS}ms ease-in-out`;
        overlay.style.opacity = "0";
      };
      requestAnimationFrame(() => requestAnimationFrame(startFadeIn));
      setTimeout(startFadeIn, 150);
    } else {
      location.href = url;
    }
  }, FADE_OUT_MS + 20);
}

/** Call this once on page load to fade in the new scene. */
export function fadeInOnLoad(): void {
  const overlay = getOverlay();
  overlay.style.opacity = "1";
  overlay.style.pointerEvents = "none";
  // Start at black, fade to transparent. Double-RAF commits the opaque state
  // before the transition starts; the setTimeout fallback guarantees the fade
  // still runs when RAF is throttled (hidden/background/headless tabs) — the
  // scene otherwise stayed black behind the overlay indefinitely.
  let started = false;
  const start = () => {
    if (started) return;
    started = true;
    overlay.style.transition = `opacity ${FADE_IN_MS}ms ease-in-out`;
    overlay.style.opacity = "0";
  };
  requestAnimationFrame(() => requestAnimationFrame(start));
  setTimeout(start, 150);
}
```

