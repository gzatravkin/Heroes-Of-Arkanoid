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
