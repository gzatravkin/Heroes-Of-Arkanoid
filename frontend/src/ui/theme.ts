/**
 * theme.ts — the single design system for every shell screen.
 *
 * Anchored to the main-menu look (docs/13-ui-ux-audit.md): warm brown depths,
 * gold ornament, deep-navy panel cores. Every scene must build from these
 * tokens and component classes instead of inventing its own palette — the
 * audit found four palettes and three button languages across the shell.
 *
 * Units: scenes are letterboxed inside #app (a CSS size container), so layout
 * must use cqw/cqh (container query units), never vw/vh — viewport units leak
 * outside the frame on desktop.
 */

import { nineSlice } from "./nineSlice";

export function injectTheme(): void {
  const id = "ui-theme";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = THEME_CSS;
  document.head.appendChild(style);
}

const THEME_CSS = `
  :root {
    /* ── Palette ────────────────────────────────────────────────────── */
    --gold:        #d8a84e;  /* ornament, borders */
    --gold-bright: #ffe9b0;  /* headings, emphasized text */
    --gold-dim:    #8a6a35;  /* hairlines, separators */
    --text:        #f0e0b8;  /* primary copy */
    --text-dim:    #c9b182;  /* secondary copy */
    --text-faint:  #9a8560;  /* tertiary / hints */
    --navy:        #16243a;  /* panel cores (matches BarGoods art) */
    --navy-deep:   #0d1626;
    --ink:         #0d0a08;  /* near-black warm */
    --bg-0: #1a0e06;          /* warm background gradient stops */
    --bg-1: #0d0808;
    --bg-2: #050308;
    --danger: #c8413a;
    --ok:     #56b04a;

    /* ── Type ───────────────────────────────────────────────────────── */
    --font-display: "Palatino Linotype", "Book Antiqua", Georgia, serif;
    --font-body: "Trebuchet MS", "Segoe UI", Verdana, sans-serif;
    --fs-title: 26px;
    --fs-section: 15px;
    --fs-body: 13px;
    --fs-small: 11px;

    /* ── Space ──────────────────────────────────────────────────────── */
    --sp-1: 4px; --sp-2: 8px; --sp-3: 12px; --sp-4: 16px; --sp-5: 24px;
  }

  /* ── Screen scaffold ──────────────────────────────────────────────── */
  /* Every shell scene: .ui-screen root > .ui-screen-bg + content. */
  .ui-screen {
    position: relative;
    width: 100%;
    min-height: 100cqh;
    overflow-x: hidden;
    overflow-y: auto;
    font-family: var(--font-body);
    color: var(--text);
    -webkit-font-smoothing: antialiased;
  }
  .ui-screen-bg {
    position: absolute;
    inset: 0;
    min-height: 100cqh;
    background:
      radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
      linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    pointer-events: none;
    z-index: 0;
  }
  .ui-content { position: relative; z-index: 1; }

  /* ── Top bar: back chip · title · trailing slot ───────────────────── */
  .ui-topbar {
    display: flex;
    align-items: center;
    gap: var(--sp-2);
    padding: max(var(--sp-3), env(safe-area-inset-top, 0px)) var(--sp-3) var(--sp-2) var(--sp-3);
  }
  .ui-topbar .ui-title { flex: 1; text-align: center; }
  /* Symmetry spacer so a centered title stays centered next to the back chip */
  .ui-topbar-spacer { width: 44px; flex: none; }

  .ui-title {
    font-family: var(--font-display);
    font-size: var(--fs-title);
    font-weight: 700;
    letter-spacing: 0.05em;
    color: var(--gold-bright);
    text-shadow: 0 2px 4px rgba(0,0,0,0.9), 0 0 18px rgba(255,180,60,0.25);
    margin: 0;
  }

  .ui-back {
    flex: none;
    width: 44px;
    height: 44px;
    padding: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 14px")}
    cursor: pointer;
    -webkit-tap-highlight-color: transparent;
    touch-action: manipulation;
    transition: filter 0.15s, transform 0.1s;
  }
  .ui-back::before {
    content: "";
    width: 20px;
    height: 20px;
    background: url('/ui/BackArrow.png') no-repeat center / contain;
    filter: drop-shadow(0 1px 2px rgba(0,0,0,0.8));
  }
  .ui-back:hover  { filter: brightness(1.18); }
  .ui-back:active { transform: scale(0.94); }

  /* ── Section plaque (NameBlock.png 826×110 — ornate gold scroll bar) ─ */
  .ui-plaque {
    display: flex;
    align-items: center;
    justify-content: center;
    min-height: 34px;
    padding: 4px 26px;
    ${nineSlice("/ui/NameBlock.png", "40 120 40 120", "10px 32px")}
    font-family: var(--font-display);
    font-size: var(--fs-section);
    font-weight: 700;
    letter-spacing: 0.12em;
    text-transform: uppercase;
    color: var(--gold-bright);
    text-shadow: 0 1px 3px rgba(0,0,0,0.95);
    white-space: nowrap;
  }

  /* ── Panel (BarGoods.png 230×76 — gold-rimmed navy card) ─────────── */
  .ui-panel {
    ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "12px 14px")}
  }

  /* ── Slot (Kvadrat.png 73×68 — silver-rimmed dark square) ─────────── */
  .ui-slot {
    ${nineSlice("/ui/Kvadrat.png", "14 14 14 14", "7px 7px")}
    display: flex;
    align-items: center;
    justify-content: center;
    aspect-ratio: 73 / 68;
  }

  /* ── Buttons ──────────────────────────────────────────────────────── */
  .ui-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 6px;
    font-family: var(--font-body);
    font-weight: 700;
    color: var(--text);
    text-shadow: 0 1px 3px rgba(0,0,0,0.9);
    cursor: pointer;
    -webkit-tap-highlight-color: transparent;
    touch-action: manipulation;
    transition: filter 0.15s, transform 0.1s;
    background: none;
  }
  .ui-btn:hover:not(:disabled)  { filter: brightness(1.15); }
  .ui-btn:active:not(:disabled) { transform: scale(0.96); filter: brightness(0.92); }
  .ui-btn:disabled {
    filter: saturate(0.25) brightness(0.65);
    cursor: default;
  }

  /* Primary pill — the menu's ornate gold/blue button (InterfaceButton 626×162) */
  .ui-btn--primary {
    min-height: 48px;
    padding: 4px 18px;
    font-size: 15px;
    letter-spacing: 0.05em;
    ${nineSlice("/ui/InterfaceButton.png", "26 92 26 92", "9px 30px")}
  }

  /* Small pill — compact actions (Button1 438×110) */
  .ui-btn--small {
    min-height: 38px;
    padding: 2px 14px;
    font-size: var(--fs-body);
    ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 18px")}
  }

  /* ── Inline currency chip ─────────────────────────────────────────── */
  .ui-gem {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    font-weight: 700;
    color: var(--gold-bright);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9);
  }
  .ui-gem::before {
    content: "";
    width: 15px;
    height: 13px;
    background: url('/ui/Gem.png') no-repeat center / contain;
  }

  /* ── Plain text link (replaces bare <a>/underline links) ──────────── */
  .ui-link {
    color: var(--text-dim);
    font-size: var(--fs-body);
    font-weight: 600;
    letter-spacing: 0.03em;
    text-decoration: none;
    cursor: pointer;
    background: none;
    border: none;
    text-shadow: 0 1px 2px rgba(0,0,0,0.8);
  }
  .ui-link:hover { color: var(--gold-bright); }
`;
