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
    this.pulse(magnitude, duration);
  }

  /** Trigger an arbitrary shake (e.g. small combo-scaled punches). Never downgrades a bigger active shake. */
  pulse(magnitude: number, duration: number) {
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
