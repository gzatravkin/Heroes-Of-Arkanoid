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
