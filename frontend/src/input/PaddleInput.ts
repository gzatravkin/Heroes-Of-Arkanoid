import type { Connection, Snapshot } from "../net/Connection";

export function attachPaddleInput(canvas: HTMLCanvasElement, conn: Connection, getSnap: () => Snapshot | null) {
  canvas.addEventListener("pointermove", (e) => {
    const s = getSnap(); if (!s) return;
    const rect = canvas.getBoundingClientRect();
    const scale = Math.min(rect.width / s.boardW, rect.height / s.boardH) * 0.95;
    const offX = (rect.width - s.boardW * scale) / 2;
    const simX = (e.clientX - rect.left - offX) / scale;
    conn.paddleX(simX);
  });
  window.addEventListener("keydown", (e) => {
    if (e.code === "Space") conn.serve();
    if (e.key === "q" || e.key === "Q") conn.castIgnite();
    if (e.key === "e" || e.key === "E") conn.castFireball();
    if (e.key === "w" || e.key === "W") conn.castFireWall();
    if (e.key === "r" || e.key === "R") conn.castTurret();
  });
}
