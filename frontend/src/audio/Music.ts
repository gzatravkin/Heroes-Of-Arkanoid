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
