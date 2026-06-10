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
