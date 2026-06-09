// Inventory screen stylesheet, extracted from InventoryScene.ts to keep that file
// focused on DOM construction and data flow. Injected once (id-guarded).

export const INVENTORY_STYLES = `
    .inv-root {
      min-height: 100vh;
      width: 100%;
      background: linear-gradient(180deg, #1a0e06 0%, #0d0808 40%, #050308 100%);
      color: #f0e0b8;
      font-family: sans-serif;
      display: flex;
      flex-direction: column;
      box-sizing: border-box;
      padding: env(safe-area-inset-top, 0px) env(safe-area-inset-right, 0px)
               env(safe-area-inset-bottom, 0px) env(safe-area-inset-left, 0px);
    }

    /* ── Header ── */
    .inv-header {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 16px 8px 16px;
      background: rgba(0,0,0,0.55);
      border-bottom: 1px solid rgba(160,120,50,0.3);
      flex-shrink: 0;
    }
    .inv-back-btn {
      background: none;
      border: 1px solid rgba(160,120,50,0.5);
      color: #c8b888;
      border-radius: 6px;
      padding: 8px 12px;
      font-size: 13px;
      cursor: pointer;
      min-height: 44px;
      touch-action: manipulation;
    }
    .inv-back-btn:active { filter: brightness(0.8); }
    .inv-title {
      flex: 1;
      margin: 0;
      font-size: 20px;
      font-weight: 900;
      letter-spacing: 0.08em;
      color: #ffd88a;
      text-shadow: 0 0 12px rgba(255,180,50,0.5);
      text-align: center;
    }
    .inv-crystals {
      font-size: 15px;
      font-weight: 700;
      color: #88ddff;
      min-width: 70px;
      text-align: right;
    }

    /* ── Equipped row ── */
    .inv-equipped-section {
      padding: 10px 16px 6px;
      flex-shrink: 0;
    }
    .inv-section-label {
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.14em;
      color: rgba(200,180,100,0.7);
      margin-bottom: 6px;
    }
    .inv-catalog-label {
      padding: 6px 16px 2px;
    }
    .inv-equipped-row {
      display: flex;
      gap: 10px;
      justify-content: center;
    }
    .inv-equip-slot {
      width: 80px;
      height: 80px;
      border-radius: 8px;
      border: 2px solid rgba(160,120,50,0.4);
      background: rgba(20,15,5,0.7);
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      font-size: 22px;
      color: rgba(200,180,100,0.3);
      flex-shrink: 0;
      gap: 2px;
    }
    .inv-equip-slot-filled {
      border-color: rgba(200,160,60,0.8);
      background: rgba(40,30,10,0.8);
    }
    .inv-slot-sprite {
      width: 44px;
      height: 44px;
      object-fit: contain;
      image-rendering: pixelated;
    }
    .inv-slot-label {
      font-size: 8px;
      color: #c8b888;
      text-align: center;
      line-height: 1.1;
      max-width: 72px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    /* ── Item grid ── */
    .inv-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 10px;
      padding: 8px 16px 24px;
      overflow-y: auto;
      flex: 1;
    }

    /* ── Item card ── */
    .inv-card {
      background: rgba(20,14,6,0.85);
      border: 1px solid rgba(120,90,40,0.45);
      border-radius: 8px;
      padding: 10px 8px 8px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 5px;
      position: relative;
    }
    .inv-card-equipped {
      border-color: rgba(200,160,60,0.8);
      background: rgba(40,28,8,0.9);
      box-shadow: 0 0 8px rgba(200,150,30,0.35);
    }
    .inv-card-sprite {
      width: 48px;
      height: 48px;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .inv-item-sprite {
      width: 44px;
      height: 44px;
      object-fit: contain;
      image-rendering: pixelated;
    }
    .inv-item-locked {
      opacity: 0.35;
      filter: grayscale(0.7);
    }
    .inv-card-name-row {
      display: flex;
      align-items: center;
      gap: 4px;
      width: 100%;
      justify-content: center;
    }
    .inv-card-name {
      font-size: 11px;
      font-weight: 700;
      color: #f0e0b8;
      text-align: center;
      line-height: 1.2;
    }
    .inv-tier-badge {
      font-size: 9px;
      font-weight: 900;
      padding: 1px 4px;
      border-radius: 3px;
      background: rgba(200,150,30,0.3);
      color: #ffd060;
      border: 1px solid rgba(200,150,30,0.5);
    }
    .inv-card-desc {
      font-size: 9px;
      color: rgba(200,180,120,0.7);
      text-align: center;
      line-height: 1.3;
      min-height: 24px;
    }
    .inv-card-actions {
      display: flex;
      flex-direction: column;
      gap: 4px;
      width: 100%;
      margin-top: 2px;
    }
    .inv-buy-btn, .inv-equip-btn {
      width: 100%;
      min-height: 36px;
      border-radius: 6px;
      border: none;
      cursor: pointer;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.04em;
      touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
    }
    .inv-buy-btn {
      background: linear-gradient(135deg, #664400, #996600);
      color: #ffe090;
      border: 1px solid rgba(200,140,30,0.6);
    }
    .inv-buy-btn:hover:not(:disabled) { filter: brightness(1.15); }
    .inv-buy-btn:active:not(:disabled) { transform: scale(0.96); }
    .inv-buy-btn:disabled, .inv-btn-disabled {
      opacity: 0.4;
      cursor: default;
    }
    .inv-equip-btn {
      background: linear-gradient(135deg, #1a3a1a, #2a5a2a);
      color: #88dd88;
      border: 1px solid rgba(80,180,80,0.4);
    }
    .inv-btn-unequip {
      background: linear-gradient(135deg, #3a1a1a, #5a2a2a);
      color: #dd8888;
      border: 1px solid rgba(180,80,80,0.4);
    }
    .inv-equip-btn:hover { filter: brightness(1.15); }
    .inv-equip-btn:active { transform: scale(0.96); }
    .inv-max-badge {
      font-size: 10px;
      font-weight: 900;
      color: #ffd060;
      text-align: center;
      padding: 4px 0;
      letter-spacing: 0.1em;
    }

    @media (min-width: 480px) {
      .inv-grid {
        grid-template-columns: repeat(3, 1fr);
      }
    }
`;
