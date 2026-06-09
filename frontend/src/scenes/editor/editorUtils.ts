// Stateless helpers + constants for the level editor, extracted from EditorScene.ts.

export const ART_BASE = "/art/";
// Portrait-native dimensions matching the P2 board format (8 cols × 14 rows).
export const DEFAULT_COLS = 8;
export const DEFAULT_ROWS = 14;

// Legend chars assigned starting from 'A', avoiding '.'
export const LEGEND_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

/** Build a styled button with an optional id and click handler. */
export function btn(
  text: string,
  id: string | null,
  css: string,
  onClick?: () => void,
): HTMLButtonElement {
  const b = document.createElement("button");
  if (id) b.id = id;
  b.textContent = text;
  b.style.cssText = css;
  if (onClick) b.addEventListener("click", onClick);
  return b;
}
