// HUD stylesheet, extracted from Hud.injectStyles() to keep Hud.ts focused.
// Injected once (guarded by the #hud-styles id) on the first Hud construction.
export const HUD_STYLES = `
      /* Lives/balls stat row — framed with HeroBar-style pill */
      .hud-stat-row {
        background: url('/ui/BattleHeroBar.png') no-repeat center/contain,
                    rgba(0,0,0,0.45);
        border-radius: 20px;
        padding: 3px 10px 3px 8px;
        color: var(--text);
        font-size: var(--fs-caption);
        display: inline-flex;
        align-items: center;
        gap: 3px;
        min-width: 60px;
        min-height: 26px;
      }

      /* ---- HOTBAR SLOT: outer wrapper owns castable-state filter + name below ---- */
      .hud-spell-slot {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 3px;
        /* ≥44px touch target (WCAG 2.5.5) */
        min-width: 52px;
        touch-action: manipulation;
        cursor: pointer;
        pointer-events: auto;
        transition: filter var(--dur-normal), transform var(--dur-normal);
        -webkit-tap-highlight-color: transparent;
      }

      /* Framed inner box: Kvadrat 9-slice — keys + icon live inside this */
      .hud-spell-frame {
        position: relative;
        width: 52px;
        height: 52px;
        box-sizing: border-box;
        display: flex;
        align-items: center;
        justify-content: center;
        /* Kvadrat 9-slice (14px insets, 7px border-width) */
        background: none;
        border-style: solid;
        border-width: 7px;
        border-image: url('/ui/Kvadrat.png') 14 14 14 14 fill stretch;
      }

      /* Castable (enough mana): full brightness + subtle gold glow */
      .hud-spell-slot.affordable {
        filter: drop-shadow(0 0 6px rgba(255,190,80,.45));
      }
      .hud-spell-slot.affordable:hover {
        filter: drop-shadow(0 0 8px rgba(255,190,80,.6)) brightness(1.15);
      }

      /* Not castable: desaturated + dimmed */
      .hud-spell-slot.unaffordable {
        filter: saturate(.3) brightness(.6);
        cursor: default;
      }

      /* Active press: stronger glow + 1.06 scale (150ms) */
      .hud-spell-slot:active {
        transform: scale(1.06);
      }
      .hud-spell-slot.affordable:active {
        filter: drop-shadow(0 0 12px rgba(255,190,80,.7));
        transform: scale(1.06);
      }

      /* Keyboard / assistive focus ring */
      .hud-spell-slot:focus-visible {
        outline: 2px solid var(--gold-bright);
        outline-offset: 3px;
        border-radius: 4px;
      }

      /* Keybind letter chip: absolute top-left in gold */
      .hud-spell-key {
        position: absolute;
        top: 2px;
        left: 3px;
        font-size: var(--fs-tiny);
        font-weight: 700;
        color: var(--gold);
        line-height: 1;
        text-shadow: 0 1px 2px rgba(0,0,0,0.9);
        pointer-events: none;
        z-index: 1;
      }

      /* Icon area: fills the inner tile of the Kvadrat frame */
      .hud-spell-icon {
        font-size: var(--fs-xl);
        line-height: 1;
        display: flex;
        align-items: center;
        justify-content: center;
        width: 32px;
        height: 32px;
      }

      /* Spell name label: BELOW the frame, ≥10px, text-dim */
      .hud-spell-name {
        font-size: var(--fs-tiny);
        color: var(--text-dim);
        text-align: center;
        line-height: 1;
        text-shadow: 0 1px 2px rgba(0,0,0,0.9);
        white-space: nowrap;
        max-width: 60px;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .hud-banner.win {
        background: rgba(10,40,10,0.85);
        border: 2px solid var(--ok-bright);
        color: var(--ok-bright);
      }
      .hud-banner.lose {
        background: rgba(40,5,5,0.85);
        border: 2px solid var(--danger-bright);
        color: var(--danger-bright);
      }
      #hud-relics [data-relic-id] {
        cursor: default;
      }

      /* Combo badge: scale-bounce when multiplier increases */
      @keyframes combo-pop {
        0%   { transform: scale(0.7); }
        60%  { transform: scale(1.1); }
        100% { transform: scale(1.0); }
      }
      #hud-combo.combo-pop {
        animation: combo-pop 0.2s ease-out forwards;
      }
      /* Landscape orientation: reduce bottom zone height */
      @media (orientation: landscape) and (max-height: 500px) {
        .hud-spell-frame {
          width: 44px;
          height: 44px;
          border-width: 6px;
        }
        .hud-spell-icon { width: 26px; height: 26px; }
        .hud-spell-icon img { width: 26px !important; height: 26px !important; }
      }
    `;
