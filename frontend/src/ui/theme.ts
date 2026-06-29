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
    --danger:        #c8413a;
    --danger-bright: #ff3333;  /* loss banner, error state */
    --danger-light:  #ff6666;  /* inline error messages */
    --ok:            #56b04a;
    --ok-bright:     #44ff88;  /* win banner, success state */

    /* ── Power-up / status colours ─────────────────────────────────── */
    --color-wide:    #d4aa00;  /* wide-paddle power-up */
    --color-fire:    #ff6600;  /* fireshot power-up */
    --color-shield:  #00ddee;  /* shield power-up */
    --color-effect:  #66aaff;  /* generic active effect (border) */
    --color-equip:   #bfe3ff;  /* equip action button text */
    --color-unequip: #f3b8a8;  /* unequip action button text */

    /* ── Battle HUD game-state bar colours ──────────────────────────── */
    --color-hp:        #ff5a4a;  /* HP bar fill (highlight) */
    --color-hp-deep:   #cc2a2a;  /* HP bar fill (base) */
    --color-balls:     #56d6ff;  /* spare-balls bar fill (highlight) */
    --color-balls-deep:#1f7fc8;  /* spare-balls bar fill (base) */
    --color-mana:      #5fe6f5;  /* mana bar fill (highlight) */
    --color-mana-deep: #1f9fb8;  /* mana bar fill (base) */
    --color-boss-hp:   #cc2222;  /* boss HP bar (normal phase) */
    --color-boss-deep: #880000;  /* boss HP bar (base/dark phase) */

    /* ── Reward / overlay semantic colours ─────────────────────────── */
    --color-xp:          #88aaff;  /* EXP reward text */
    --color-pts:         #ffcc44;  /* skill-points reward text */
    --color-crystal:     #44ddff;  /* crystal currency display */
    --color-levelup:     #ffd700;  /* level-up notification */
    --color-first-clear: #aa88ff;  /* first-clear achievement */
    --color-label-muted: #88aaaa;  /* muted section labels */
    --color-relic:       #cc88ff;  /* relic item display */
    --color-fail-muted:  #aa5555;  /* permadeath / fail sub-text */

    /* ── Campaign / Dungeon UI colours ──────────────────────────────── */
    --color-upgrade-hdr:   #e8c870;  /* upgrade panel section heading */
    --color-spell-name:    #e8e8ff;  /* spell name text in upgrade list */
    --color-dungeon-label: #8899cc;  /* dungeon run status / buff labels */
    --color-empty:         #555577;  /* empty-state placeholder text */

    /* ── HUD overlays, shadows, glow ───────────────────────────────── */
    --overlay-light:  rgba(0,0,0,0.45);
    --overlay-mid:    rgba(0,0,0,0.65);
    --shadow-hard:    rgba(0,0,0,0.90);
    --hud-top-bg:     rgba(10,7,5,0.55);
    --hud-btm-bg:     rgba(4,4,12,0.80);
    --hud-slot-bg:    rgba(20,14,6,0.75);
    --hud-slot-bdr:   rgba(200,150,30,0.5);
    --hud-win-bg:     rgba(10,40,10,0.85);
    --hud-lose-bg:    rgba(40,5,5,0.85);
    --gold-glow-lo:   rgba(255,190,80,0.45);
    --gold-glow-mid:  rgba(255,190,80,0.60);
    --gold-glow-hi:   rgba(255,190,80,0.70);
    --shadow-black:   #000;          /* opaque shadow for text on colored bg */
    --text-oncolor:   #ffffff;       /* pure white text on colored bar fills */

    /* ── Interaction state filters ──────────────────────────────────── */
    --filter-locked:   saturate(0.45) brightness(0.8);
    --filter-dim:      saturate(0.25) brightness(0.65);
    --filter-hover:    brightness(1.15);
    --filter-active:   brightness(0.92);
    --filter-disabled: saturate(0.25) brightness(0.65);

    /* ── Animation durations ────────────────────────────────────────── */
    --dur-fast:   0.1s;
    --dur-normal: 0.15s;
    --dur-slow:   0.35s;

    /* ── Type ───────────────────────────────────────────────────────── */
    --font-display: "Palatino Linotype", "Book Antiqua", Georgia, serif;
    --font-body: "Trebuchet MS", "Segoe UI", Verdana, sans-serif;
    --fs-title:   26px;  /* screen headings */
    --fs-2xl:     32px;  /* impact headings (win/lose banner, large callout) */
    --fs-xl:      20px;  /* item names, hero text */
    --fs-large:   16px;  /* stat values, section heroes */
    --fs-section: 15px;  /* panel headers, section labels */
    --fs-subhead: 14px;  /* subheadings, compact entries */
    --fs-body:    13px;  /* primary body copy */
    --fs-caption: 12px;  /* compact labels, badges */
    --fs-small:   11px;  /* secondary copy, hints */
    --fs-tiny:    10px;  /* metadata, timestamps, ultra-small kickers */

    /* ── Space ──────────────────────────────────────────────────────── */
    --sp-1: 4px; --sp-2: 8px; --sp-3: 12px; --sp-4: 16px; --sp-5: 24px;
    --sp-6: 32px; --sp-7: 40px; --sp-8: 48px;
    /* Half-step tokens for compact UI and buttons */
    --sp-1h: 6px; --sp-2h: 10px; --sp-3h: 14px; --sp-4h: 20px;
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
    /* Shared header bar — gold/brown, matching the menu-list scenes' .topbar (UI consistency pass). */
    background: linear-gradient(180deg, rgba(46,34,16,0.96), rgba(24,18,10,0.92));
    border-bottom: 2px solid rgba(180,140,60,0.45);
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
    /* Centered title is the shared-header standard (also covers scenes with a local topbar class). */
    flex: 1; text-align: center;
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
    transition: filter var(--dur-normal), transform var(--dur-fast);
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
  .ui-back:focus-visible {
    outline: 2px solid var(--gold-bright);
    outline-offset: 3px;
    border-radius: 4px;
  }

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
    transition: filter var(--dur-normal), transform var(--dur-fast);
    background: none;
  }
  .ui-btn:hover:not(:disabled)  { filter: var(--filter-hover); }
  .ui-btn:active:not(:disabled) { transform: scale(0.96); filter: var(--filter-active); }
  .ui-btn:disabled {
    filter: var(--filter-disabled);
    cursor: default;
  }
  .ui-btn:focus-visible {
    outline: 2px solid var(--gold-bright);
    outline-offset: 3px;
    border-radius: 4px;
  }

  /* Primary pill — the menu's ornate gold/blue button (InterfaceButton 626×162) */
  .ui-btn--primary {
    min-height: 48px;
    padding: 4px 18px;
    font-size: var(--fs-section);
    letter-spacing: 0.05em;
    ${nineSlice("/ui/InterfaceButton.png", "26 92 26 92", "9px 30px")}
  }

  /* Small pill — compact actions (Button1 438×110) — 44px min to meet touch target */
  .ui-btn--small {
    min-height: 44px;
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

  /* ── Currency: THE single representation of the 3 player coins ─────────
     Sparks ✦ (items + modules), Souls ◆ (spells + heroes), Insight ◇ (mastery).
     Every screen shows them through .ui-coins / .ui-coin — never bespoke glyphs. */
  .ui-coins {
    display: flex; align-items: center; gap: var(--sp-2);
    flex-wrap: wrap;
  }
  .ui-coin {
    display: inline-flex; align-items: center; gap: 5px;
    font-weight: 800; font-size: var(--fs-body);
    padding: 4px 11px; border-radius: 999px; white-space: nowrap;
    text-shadow: 0 1px 2px rgba(0,0,0,0.9);
  }
  .ui-coin::before { font-size: 1.05em; line-height: 1; }
  .ui-coin-sparks  { color: #ffd56a; background: rgba(190,150,50,0.20); border: 1px solid rgba(255,210,100,0.35); }
  .ui-coin-souls   { color: #6cc0ff; background: rgba(50,110,190,0.20); border: 1px solid rgba(110,180,255,0.35); }
  .ui-coin-insight { color: #e8a64c; background: rgba(150,95,35,0.20);  border: 1px solid rgba(220,150,70,0.35); }
  .ui-coin-sparks::before  { content: "✦"; }
  .ui-coin-souls::before   { content: "◆"; }
  .ui-coin-insight::before { content: "◇"; }

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
  .ui-link:focus-visible {
    outline: 2px solid var(--gold-bright);
    outline-offset: 3px;
    border-radius: 2px;
  }
`;
