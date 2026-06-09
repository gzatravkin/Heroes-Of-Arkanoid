// HUD stylesheet, extracted from Hud.injectStyles() to keep Hud.ts focused.
// Injected once (guarded by the #hud-styles id) on the first Hud construction.
export const HUD_STYLES = `
      /* Lives/balls stat row — framed with HeroBar-style pill */
      .hud-stat-row {
        background: url('/ui/BattleHeroBar.png') no-repeat center/contain,
                    rgba(0,0,0,0.45);
        border-radius: 20px;
        padding: 3px 10px 3px 8px;
        color: #eee;
        font-size: 12px;
        display: inline-flex;
        align-items: center;
        gap: 3px;
        min-width: 60px;
        min-height: 26px;
      }

      .hud-spell-slot {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 2px;
        /* Use SpellBar art as the slot frame background */
        background: url('/ui/BattleSpellBar.png') no-repeat center/100% 100%;
        border: none;
        border-radius: 6px;
        padding: 4px 6px 6px 6px;
        /* ≥44px touch target (WCAG 2.5.5) */
        min-width: 52px;
        min-height: 72px;
        touch-action: manipulation;
        cursor: pointer;
        pointer-events: auto;
        transition: opacity 0.15s, filter 0.15s;
        -webkit-tap-highlight-color: transparent;
      }
      .hud-spell-slot.affordable {
        opacity: 1;
        filter: none;
      }
      .hud-spell-slot.affordable:hover {
        filter: brightness(1.2);
      }
      .hud-spell-slot.unaffordable {
        opacity: 0.4;
        filter: grayscale(0.6);
        cursor: default;
      }
      .hud-spell-slot:active {
        transform: scale(0.93);
      }
      .hud-spell-key {
        font-size: 10px;
        font-weight: 700;
        color: #ffcc66;
        line-height: 1;
      }
      .hud-spell-icon {
        font-size: 20px;
        line-height: 1;
        display: flex;
        align-items: center;
        justify-content: center;
        width: 28px;
        height: 28px;
      }
      .hud-spell-name {
        font-size: 8px;
        color: #e0c880;
        text-align: center;
        line-height: 1;
        text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      }
      .hud-banner.win {
        background: rgba(10,40,10,0.85);
        border: 2px solid #44ff88;
        color: #44ff88;
      }
      .hud-banner.lose {
        background: rgba(40,5,5,0.85);
        border: 2px solid #ff3333;
        color: #ff3333;
      }
      #hud-relics [data-relic-id] {
        cursor: default;
      }
      /* Landscape orientation: reduce bottom zone height */
      @media (orientation: landscape) and (max-height: 500px) {
        .hud-spell-slot {
          min-width: 44px;
          min-height: 56px;
          padding: 3px 5px 4px 5px;
        }
        .hud-spell-icon { width: 22px; height: 22px; }
        .hud-spell-icon img { width: 22px !important; height: 22px !important; }
      }
    `;
