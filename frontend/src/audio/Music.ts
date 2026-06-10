// Music.ts — procedural per-biome ambience (docs/12 music briefs, docs/09 G1).
//
// Like Sfx.ts, everything is SYNTHESIZED — no assets. Each biome gets a
// continuous layer plus a sparse pattern layer, matching the docs/12 briefs:
//   hell    — low drone, metallic impacts
//   caverns — percussive echoing knocks, rumbles
//   village — sparse whispery detuned chimes
//   heaven  — choral pads, bell impacts
// Honors the Settings "Audio" toggle (localStorage arkanoid_audio).

const MUSIC_VOLUME       = 0.06;
const SCHEDULER_MS       = 400;   // pattern scheduler cadence
const LOOKAHEAD_S        = 0.9;   // schedule events up to this far ahead
const FADE_S             = 0.6;   // biome crossfade time

let _ctx: AudioContext | null = null;
let _master: GainNode | null = null;
let _biome = "";
let _timer: number | null = null;
let _nextEventAt = 0;
let _step = 0;
let _drones: { osc: OscillatorNode; gain: GainNode }[] = [];

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
    _master.gain.value = MUSIC_VOLUME;
    _master.connect(_ctx.destination);
  } catch { return null; }
  return _ctx;
}

// ── Synth helpers ─────────────────────────────────────────────────────────────

function note(c: AudioContext, out: GainNode, type: OscillatorType,
  freq: number, at: number, durS: number, vol: number, attackS = 0.01) {
  const osc = c.createOscillator();
  osc.type = type;
  osc.frequency.value = freq;
  const g = c.createGain();
  g.gain.setValueAtTime(0.0001, at);
  g.gain.exponentialRampToValueAtTime(vol, at + attackS);
  g.gain.exponentialRampToValueAtTime(0.0001, at + durS);
  osc.connect(g).connect(out);
  osc.start(at);
  osc.stop(at + durS + 0.05);
}

/** Continuous drone layer: started once per biome, faded on change. */
function startDrone(c: AudioContext, out: GainNode, parts: Array<{ type: OscillatorType; freq: number; vol: number }>) {
  for (const p of parts) {
    const osc = c.createOscillator();
    osc.type = p.type;
    osc.frequency.value = p.freq;
    const g = c.createGain();
    g.gain.setValueAtTime(0.0001, c.currentTime);
    g.gain.exponentialRampToValueAtTime(p.vol, c.currentTime + FADE_S);
    osc.connect(g).connect(out);
    osc.start();
    _drones.push({ osc, gain: g });
  }
}

function stopDrones(c: AudioContext) {
  for (const d of _drones) {
    try {
      d.gain.gain.exponentialRampToValueAtTime(0.0001, c.currentTime + FADE_S);
      d.osc.stop(c.currentTime + FADE_S + 0.1);
    } catch { /* already stopped */ }
  }
  _drones = [];
}

// ── Per-biome patterns (docs/12 briefs) ───────────────────────────────────────

// Witchland chime scale: A minor pentatonic, mid register.
const VILLAGE_SCALE = [220, 261.6, 293.7, 329.6, 392];
// Heaven pad chords: Am low / F low — slow alternation.
const HEAVEN_CHORDS = [[110, 164.8, 220], [87.3, 130.8, 174.6]];

function scheduleStep(c: AudioContext, out: GainNode, biome: string, at: number, step: number) {
  switch (biome) {
    case "hell": // metallic impact every 4th step (~3.2s), slightly varied
      if (step % 4 === 0) note(c, out, "square", 1100 + (step % 8) * 60, at, 0.5, 0.25);
      break;
    case "cavern":
    case "caverns": // knock pattern: deep thumps with an offbeat echo
      if (step % 2 === 0) note(c, out, "sine", 70, at, 0.35, 0.9, 0.005);
      if (step % 4 === 3) note(c, out, "sine", 95, at, 0.25, 0.45, 0.005);
      break;
    case "village": { // sparse detuned chime pairs
      if (step % 3 !== 0) break;
      const f = VILLAGE_SCALE[(step * 7) % VILLAGE_SCALE.length];
      note(c, out, "sine", f, at, 1.6, 0.30, 0.02);
      note(c, out, "sine", f * 1.006, at, 1.6, 0.22, 0.02); // ±cents shimmer
      break;
    }
    case "heaven": { // choral pad swells alternating chords + occasional bell
      if (step % 6 === 0) {
        const chord = HEAVEN_CHORDS[(step / 6) % 2];
        for (const f of chord) note(c, out, "triangle", f, at, 4.5, 0.18, 1.2);
      }
      if (step % 8 === 5) {
        note(c, out, "sine", 880, at, 1.4, 0.20, 0.01);
        note(c, out, "sine", 2200, at, 0.9, 0.08, 0.01); // bell partial
      }
      break;
    }
  }
}

const STEP_S = 0.8; // pattern grid

// ── Public API ────────────────────────────────────────────────────────────────

/** Idempotently switch the ambience to the given biome (call per snapshot). */
export function setMusicBiome(biome: string): void {
  if (!biome || biome === _biome) return;
  const c = ctx();
  if (!c || !_master) return;
  _biome = biome;
  if (c.state === "suspended") { c.resume().catch(() => {}); }

  // Continuous layer per biome.
  stopDrones(c);
  const base = biome.split("-")[0];
  if (base === "hell")
    startDrone(c, _master, [
      { type: "sawtooth", freq: 55,   vol: 0.16 },
      { type: "sawtooth", freq: 55.6, vol: 0.12 }, // beat-frequency unease
    ]);
  else if (base === "cavern" || base === "caverns")
    startDrone(c, _master, [{ type: "sine", freq: 49, vol: 0.14 }]);
  else if (base === "village")
    startDrone(c, _master, [{ type: "triangle", freq: 110, vol: 0.06 }]);
  else if (base === "heaven")
    startDrone(c, _master, [
      { type: "triangle", freq: 220, vol: 0.05 },
      { type: "triangle", freq: 330, vol: 0.04 },
    ]);

  // Pattern scheduler (one global timer).
  if (_timer === null) {
    _nextEventAt = c.currentTime + 0.1;
    _timer = window.setInterval(() => {
      const cc = _ctx;
      if (!cc || !_master) return;
      if (!enabled()) { _master.gain.value = 0; return; }
      _master.gain.value = MUSIC_VOLUME;
      while (_nextEventAt < cc.currentTime + LOOKAHEAD_S) {
        scheduleStep(cc, _master, _biome.split("-")[0], _nextEventAt, _step);
        _nextEventAt += STEP_S;
        _step++;
      }
    }, SCHEDULER_MS);
  }
}

/** Stop all ambience (scene teardown). */
export function stopMusic(): void {
  if (_timer !== null) { clearInterval(_timer); _timer = null; }
  if (_ctx) stopDrones(_ctx);
  _biome = "";
}
