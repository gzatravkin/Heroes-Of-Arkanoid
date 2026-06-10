/**
 * transition.ts — Lightweight CSS-based scene transitions.
 *
 * Provides a 200ms fade-out before navigation so scene changes feel polished
 * rather than instant-cut. Usage:
 *
 *   navigateTo("/?scene=campaign");
 *
 * The overlay fades in over 200ms, then the page navigates.
 * An incoming page also fades in via the CSS class applied to <body>.
 */

// Duration constants.
const FADE_OUT_MS = 200; // fade duration before navigation
const FADE_IN_MS  = 220; // fade-in after page load

let _overlay: HTMLDivElement | null = null;

function getOverlay(): HTMLDivElement {
  if (_overlay) return _overlay;
  const div = document.createElement("div");
  div.id = "scene-transition-overlay";
  div.style.cssText = `
    position: fixed;
    inset: 0;
    background: #000;
    opacity: 0;
    pointer-events: none;
    z-index: 99999;
    transition: opacity ${FADE_OUT_MS}ms ease-in-out;
  `;
  document.body.appendChild(div);
  _overlay = div;
  return div;
}

/** Navigate to a new URL with a fade-out transition. */
export function navigateTo(url: string): void {
  const overlay = getOverlay();
  // Force reflow so the transition triggers from opacity=0.
  overlay.style.opacity = "0";
  void overlay.offsetHeight;
  overlay.style.pointerEvents = "all";
  overlay.style.opacity = "1";
  setTimeout(() => {
    location.href = url;
  }, FADE_OUT_MS + 20); // small buffer after fade completes
}

/** Call this once on page load to fade in the new scene. */
export function fadeInOnLoad(): void {
  const overlay = getOverlay();
  overlay.style.opacity = "1";
  overlay.style.pointerEvents = "none";
  // Start at black, fade to transparent. Double-RAF commits the opaque state
  // before the transition starts; the setTimeout fallback guarantees the fade
  // still runs when RAF is throttled (hidden/background/headless tabs) — the
  // scene otherwise stayed black behind the overlay indefinitely.
  let started = false;
  const start = () => {
    if (started) return;
    started = true;
    overlay.style.transition = `opacity ${FADE_IN_MS}ms ease-in-out`;
    overlay.style.opacity = "0";
  };
  requestAnimationFrame(() => requestAnimationFrame(start));
  setTimeout(start, 150);
}
