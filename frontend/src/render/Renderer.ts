import { Application, Container, Graphics, Sprite } from "pixi.js";
import type { Snapshot } from "../net/Connection";
import { tex } from "./textures";

export class Renderer {
  app: Application;
  private world = new Container();
  private blocks = new Container();
  private fx = new Container();
  private paddle = new Graphics();
  private balls = new Container();

  constructor(host: HTMLElement) {
    this.app = new Application({ resizeTo: host, background: "#0b0b12", antialias: true });
    host.appendChild(this.app.view as HTMLCanvasElement);
    this.world.addChild(this.blocks, this.balls, this.paddle, this.fx);
    this.app.stage.addChild(this.world);
  }

  private fit(s: Snapshot) {
    const scale = Math.min(this.app.screen.width / s.boardW, this.app.screen.height / s.boardH) * 0.95;
    this.world.scale.set(scale);
    this.world.position.set(
      (this.app.screen.width - s.boardW * scale) / 2,
      (this.app.screen.height - s.boardH * scale) / 2
    );
  }

  draw(s: Snapshot) {
    this.fit(s);
    // blocks
    this.blocks.removeChildren();
    for (const b of s.blocks) {
      const sp = new Sprite(tex(b.sprite));
      sp.anchor.set(0.5);
      sp.width = s.cellSize; sp.height = s.cellSize;
      sp.position.set(b.x, b.y);
      sp.alpha = 0.4 + 0.6 * (b.hp / b.maxHp);
      this.blocks.addChild(sp);
    }
    // paddle
    this.paddle.clear();
    this.paddle.beginFill(0x7fd1ff).drawRect(
      s.paddleX - s.paddleW / 2, (s.boardH + s.cellSize) - s.paddleH / 2, s.paddleW, s.paddleH
    ).endFill();
    // balls
    this.balls.removeChildren();
    for (const ball of s.balls) {
      const g = new Graphics();
      g.beginFill(ball.ignited ? 0xff7a2a : 0xffffff).drawCircle(ball.x, ball.y, s.cellSize * 0.25).endFill();
      this.balls.addChild(g);
    }
  }
}
