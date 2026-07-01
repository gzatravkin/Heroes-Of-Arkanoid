import type { Connection, Snapshot } from "../net/Connection";

// Pointer-based paddle input: works for both mouse (desktop) and touch (mobile).
// RELATIVE mode: the paddle moves by the same delta as the finger — you can start
// a drag from anywhere on the screen without the paddle jumping to your finger.
// Tap (no drag) on the canvas while in Serving phase to serve.
export function attachPaddleInput(canvas: HTMLCanvasElement, conn: Connection, getSnap: () => Snapshot | null): () => void {
  let dragging    = false;
  let lastClientX = 0;
  let simX        = 0;   // tracked paddle position in sim-space

  function scale(): number {
    const s = getSnap();
    if (!s) return 1;
    const rect = canvas.getBoundingClientRect();
    return Math.min(rect.width / s.boardW, rect.height / s.boardH) * 0.95;
  }

  const onDown = (e: PointerEvent) => {
    dragging    = true;
    lastClientX = e.clientX;
    // Anchor to the current server-confirmed paddle position so the first
    // move delta is relative to where the paddle actually is.
    const s = getSnap();
    simX = s?.paddleX ?? simX;
    canvas.setPointerCapture(e.pointerId);
  };

  const onMove = (e: PointerEvent) => {
    if (!dragging) return;
    const sc = scale();
    const dx = (e.clientX - lastClientX) / sc;
    lastClientX = e.clientX;
    simX += dx;
    conn.paddleX(simX);
  };

  const onUp = (_e: PointerEvent) => {
    if (dragging) {
      const s = getSnap();
      if (s?.phase === "Serving") conn.serve();
    }
    dragging = false;
  };

  const onKey = (e: KeyboardEvent) => {
    if (e.code === "Space") conn.serve();
    const k = e.key.toLowerCase();
    if (k === "q") conn.castSlot(0);
    else if (k === "e") conn.castSlot(1);
    else if (k === "w") conn.castSlot(2);
    else if (k === "r") conn.castSlot(3);
    else if (k === "t") conn.castSlot(4);
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
