// Inventory screen stylesheet, extracted from InventoryScene.ts to keep that file
// focused on DOM construction and data flow. Injected once (id-guarded).
//
// Built on the shared design system (ui/theme.ts): warm-brown screen, NameBlock
// section plaques, BarGoods card panels, Kvadrat equip slots, Button1 actions.
// docs/13-ui-ux-audit.md called the old flat-HTML look "AI-generated page";
// every surface here is the game's own painted art via 9-slice.

import { nineSlice } from "../../ui/nineSlice";

export const INVENTORY_STYLES = `
    .inv-root {
      position: relative;
      min-height: 100cqh;
      width: 100%;
      color: var(--text);
      font-family: var(--font-body);
      display: flex;
      flex-direction: column;
      box-sizing: border-box;
      padding: env(safe-area-inset-top, 0px) env(safe-area-inset-right, 0px)
               env(safe-area-inset-bottom, 0px) env(safe-area-inset-left, 0px);
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    }

    /* ── Header: back chip · display title · gem counter ── */
    .inv-header {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      padding: max(12px, env(safe-area-inset-top, 0px)) 12px 8px 12px;
      flex-shrink: 0;
    }
    .inv-title {
      flex: 1;
      margin: 0;
      font-family: var(--font-display);
      font-size: var(--fs-title);
      font-weight: 700;
      letter-spacing: 0.05em;
      color: var(--gold-bright);
      text-shadow: 0 2px 4px rgba(0,0,0,0.9), 0 0 18px rgba(255,180,60,0.25);
      text-align: center;
    }
    .inv-crystals {
      font-size: var(--fs-section);
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      min-width: 56px;
      text-align: right;
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: var(--sp-1);
    }

    /* ── Section plaques (NameBlock scroll bars) ── */
    .inv-equipped-section {
      padding: var(--sp-2h) var(--sp-4) var(--sp-1h);
      flex-shrink: 0;
      display: flex;
      flex-direction: column;
      align-items: center;
    }
    .inv-section-label {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-height: 30px;
      padding: 2px 22px;
      margin-bottom: var(--sp-2h);
      ${nineSlice("/ui/NameBlock.png", "40 120 40 120", "9px 28px")}
      font-family: var(--font-display);
      font-size: var(--fs-caption);
      font-weight: 700;
      letter-spacing: 0.14em;
      text-transform: uppercase;
      color: var(--gold-bright);
      text-shadow: 0 1px 3px rgba(0,0,0,0.95);
      white-space: nowrap;
    }
    .inv-catalog-label {
      align-self: center;
      margin: 8px auto 2px;
    }

    /* ── Equipped row: Kvadrat slot frames ── */
    .inv-equipped-row {
      display: flex;
      gap: var(--sp-3);
      justify-content: center;
    }
    .inv-equip-slot {
      width: 82px;
      height: 78px;
      ${nineSlice("/ui/Kvadrat.png", "14 14 14 14", "7px 7px")}
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      font-family: var(--font-display);
      font-size: var(--fs-xl);
      color: rgba(216, 168, 78, 0.35);
      flex-shrink: 0;
      gap: 2px;
    }
    .inv-equip-slot-filled {
      filter: drop-shadow(0 0 6px rgba(255, 190, 80, 0.45));
    }
    .inv-slot-sprite {
      width: 46px;
      height: 46px;
      object-fit: contain;
    }
    .inv-slot-label {
      font-family: var(--font-body);
      font-size: var(--fs-tiny);
      color: var(--text-dim);
      text-align: center;
      line-height: 1.1;
      max-width: 64px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    /* ── Item grid ── */
    .inv-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: var(--sp-3);
      padding: 8px 14px 28px;
      overflow-y: auto;
      flex: 1;
    }

    /* ── Item card: BarGoods gold-rimmed navy panel ── */
    .inv-card {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "13px 15px")}
      padding: var(--sp-1h) var(--sp-1) var(--sp-1h);
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 5px;
      position: relative;
    }
    .inv-card-equipped {
      filter: drop-shadow(0 0 7px rgba(255, 190, 80, 0.55));
    }
    .inv-card-sprite {
      width: 54px;
      height: 54px;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .inv-item-sprite {
      width: 52px;
      height: 52px;
      object-fit: contain;
      filter: drop-shadow(0 2px 3px rgba(0,0,0,0.6));
    }
    /* Locked: readable but clearly unowned — NOT blacked out (docs/13). */
    .inv-item-locked {
      opacity: 0.85;
      filter: saturate(0.45) brightness(0.8) drop-shadow(0 2px 3px rgba(0,0,0,0.6));
    }
    .inv-card-name-row {
      display: flex;
      align-items: center;
      gap: 5px;
      width: 100%;
      justify-content: center;
    }
    .inv-card-name {
      font-size: var(--fs-caption);
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      text-align: center;
      line-height: 1.2;
    }
    .inv-tier-badge {
      font-size: var(--fs-tiny);
      font-weight: 900;
      padding: 1px 5px;
      border-radius: 3px;
      background: rgba(216, 168, 78, 0.22);
      color: var(--gold-bright);
      border: 1px solid var(--gold-dim);
      text-shadow: 0 1px 1px rgba(0,0,0,0.8);
    }
    .inv-card-desc {
      font-size: var(--fs-tiny);
      color: var(--text-dim);
      text-align: center;
      line-height: 1.35;
      min-height: 28px;
      padding: 0 2px;
    }
    .inv-card-actions {
      display: flex;
      flex-direction: column;
      gap: 5px;
      width: 100%;
      margin-top: 2px;
      align-items: stretch;
    }

    /* ── Actions: Button1 gold/navy pills ── */
    .inv-buy-btn, .inv-equip-btn {
      width: 100%;
      min-height: 44px;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 16px")}
      cursor: pointer;
      font-family: var(--font-body);
      font-size: var(--fs-caption);
      font-weight: 700;
      letter-spacing: 0.04em;
      touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
      transition: filter var(--dur-normal), transform var(--dur-fast);
      text-shadow: 0 1px 2px var(--shadow-hard);
    }
    .inv-buy-btn { color: var(--gold-bright); }
    .inv-buy-btn:hover:not(:disabled)  { filter: brightness(1.18); }
    .inv-buy-btn:active:not(:disabled) { transform: scale(0.96); }
    .inv-buy-btn:disabled, .inv-btn-disabled {
      filter: saturate(0.25) brightness(0.6);
      cursor: default;
    }
    .inv-equip-btn { color: var(--color-equip); }
    .inv-btn-unequip { color: var(--color-unequip); }
    .inv-equip-btn:hover  { filter: brightness(1.18); }
    .inv-equip-btn:active { transform: scale(0.96); }
    .inv-buy-btn:focus-visible, .inv-equip-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }

    .inv-max-badge {
      font-family: var(--font-display);
      font-size: var(--fs-small);
      font-weight: 700;
      color: var(--gold-bright);
      text-align: center;
      padding: var(--sp-1h) 0;
      letter-spacing: 0.18em;
      text-shadow: 0 0 8px rgba(255,190,80,0.4);
    }

    /* Wider design space (container, not viewport — we live in a letterbox) */
    @container (min-width: 480px) {
      .inv-grid {
        grid-template-columns: repeat(3, 1fr);
      }
    }
`;
