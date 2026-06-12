import type { Connection, Snapshot } from "../net/Connection";

// Pointer-based paddle input: works for both mouse (desktop) and touch (mobile).
// Drag anywhere on the canvas to move the paddle.
// Tap on the canvas while in Serving phase to serve.
export function attachPaddleInput(canvas: HTMLCanvasElement, conn: Connection, getSnap: () => Snapshot | null): () => void {
  // Translate a canvas clientX into sim-space X using the same fit scale as Renderer.
  function toSimX(clientX: number): number | null {
    const s = getSnap();
    if (!s) return null;
    const rect = canvas.getBoundingClientRect();
    const scale = Math.min(rect.width / s.boardW, rect.height / s.boardH) * 0.95;
    const offX  = (rect.width - s.boardW * scale) / 2;
    return (clientX - rect.left - offX) / scale;
  }

  const onMove = (e: PointerEvent) => { const x = toSimX(e.clientX); if (x !== null) conn.paddleX(x); };
  const onDown = (e: PointerEvent) => { const x = toSimX(e.clientX); if (x !== null) conn.paddleX(x); };
  const onUp   = (_e: PointerEvent) => { const s = getSnap(); if (s?.phase === "Serving") conn.serve(); };
  const onKey  = (e: KeyboardEvent) => {
    if (e.code === "Space") conn.serve();
    if (e.key === "q" || e.key === "Q") conn.castIgnite();
    if (e.key === "e" || e.key === "E") conn.castFireball();
    if (e.key === "w" || e.key === "W") conn.castFireWall();
    if (e.key === "r" || e.key === "R") conn.castTurret();
    if (e.key === "t" || e.key === "T") conn.castSlot(4); // 5th kit slot (G2c)
  };

  canvas.addEventListener("pointermove", onMove);
  canvas.addEventListener("pointerdown", onDown);
  canvas.addEventListener("pointerup",   onUp);
  window.addEventListener("keydown",     onKey);

  return () => {
    canvas.removeEventListener("pointermove", onMove);
    canvas.removeEventListener("pointerdown", onDown);
    canvas.removeEventListener("pointerup",   onUp);
    window.removeEventListener("keydown",     onKey);
  };
}
