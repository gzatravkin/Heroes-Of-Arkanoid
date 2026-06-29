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
    // Q/E/W/R/T → hotbar slots 0–4. Use the class-agnostic castSlot (like the tap
    // hotbar) so the keys cast the CURRENT class's spells — the old castIgnite/etc.
    // were hardcoded to Fire-Mage spell ids, so keyboard play was broken for the
    // other three classes (their keys tried to cast spells they don't have).
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
