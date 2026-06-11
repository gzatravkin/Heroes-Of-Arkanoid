// Campaign + rift-banner stylesheets, extracted from CampaignScene.ts to keep that
// file focused on DOM construction and data flow. Injected once (id-guarded).
import { btn1, missionName } from "../../ui/nineSlice";

export const RIFT_STYLES = `
    .rift-banner {
      position: fixed;
      left: 50%;
      top: 64px;
      transform: translate(-50%, -160%);
      width: min(360px, 92cqw);
      z-index: 200;
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 14px 16px;
      box-sizing: border-box;
      background:
        linear-gradient(180deg, rgba(60,10,70,0.96), rgba(30,5,40,0.97)),
        rgba(20,5,30,0.97);
      border: 2px solid #b048e0;
      border-radius: 12px;
      box-shadow: 0 0 28px rgba(180,70,230,0.55), inset 0 0 30px rgba(120,30,160,0.4);
      color: #f4e6ff;
      font-family: var(--font-body);
      transition: transform 0.35s cubic-bezier(0.2, 1.1, 0.4, 1);
    }
    .rift-banner-in { transform: translate(-50%, 0); }
    .rift-banner-glyph {
      width: 26px; height: 26px; flex-shrink: 0;
      border-radius: 50%;
      background: radial-gradient(circle at 38% 35%, #f4d6ff 0%, #c060ff 45%, #5a149a 100%);
      box-shadow: 0 0 14px #c060ff, inset 0 0 6px rgba(255,255,255,0.8);
    }
    @keyframes rift-pulse { 0%,100% { opacity: 0.7; transform: scale(1); } 50% { opacity: 1; transform: scale(1.18); } }
    @media (prefers-reduced-motion: no-preference) {
      .rift-banner-glyph { animation: rift-pulse 1.4s ease-in-out infinite; }
    }
    .rift-banner-text { flex: 1; min-width: 0; }
    .rift-banner-title {
      font-size: var(--fs-section); font-weight: 800; letter-spacing: 0.04em;
      color: #e9b8ff; text-shadow: 0 0 10px rgba(190,90,240,0.7);
    }
    .rift-banner-sub { font-size: var(--fs-tiny); color: #c9a8e0; margin-top: 2px; line-height: 1.3; }
    .rift-banner-actions { display: flex; flex-direction: column; gap: 6px; }
    .rift-btn {
      min-width: 78px; min-height: 44px;
      border: none; border-radius: 8px; cursor: pointer;
      font-size: var(--fs-body); font-weight: 700; font-family: var(--font-body);
      touch-action: manipulation; -webkit-tap-highlight-color: transparent;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .rift-btn:active { transform: scale(0.95); }
    .rift-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 8px;
    }
    .rift-btn-go {
      background: linear-gradient(180deg, #c860ff, #8a28c0);
      color: #fff; text-shadow: 0 1px 2px rgba(0,0,0,0.6);
      box-shadow: 0 0 12px rgba(190,90,240,0.6);
    }
    .rift-btn-go:hover { filter: brightness(1.15); }
    .rift-btn-skip {
      background: rgba(40,20,55,0.9); color: #b89ccc;
      border: 1px solid rgba(150,90,190,0.45);
    }
    .rift-btn-skip:hover { filter: brightness(1.2); }
`;

export const CAMPAIGN_STYLES = `
    .camp-root {
      min-height: 100cqh;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(60,40,10,0.4) 0%, transparent 60%),
        linear-gradient(180deg, #12080a 0%, #070510 50%, #040308 100%);
      display: flex;
      flex-direction: column;
      overflow: hidden;
      font-family: var(--font-body);
      color: #e8e8ff;
    }

    /* ── Profile bar ── */
    .camp-profile-bar {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 8px 16px;
      background: url('/ui/LvlUpInterfaceTopBottomPanel.png') repeat-x center / auto 100%;
      border-bottom: 2px solid rgba(180,140,60,0.4);
      flex-shrink: 0;
      flex-wrap: wrap;
      min-height: 52px;
    }
    .camp-profile-level {
      font-weight: 700;
      font-size: var(--fs-section);
      color: var(--gold);
      text-shadow: 0 0 8px rgba(255,200,0,0.6);
      white-space: nowrap;
    }
    .camp-exp-wrap {
      display: flex;
      align-items: center;
      gap: 5px;
    }
    .camp-exp-label {
      color: var(--text-dim);
      font-size: var(--fs-small);
      white-space: nowrap;
    }
    .camp-exp-outer {
      position: relative;
      width: 110px;
      height: 16px;
      border-style: solid;
      border-width: 7px 18px;
      border-image: url('/ui/ExpBarEmptyMainMenu.png') 26 70 26 70 fill stretch;
      box-sizing: border-box;
      overflow: hidden;
    }
    .camp-exp-fill {
      position: absolute;
      left: 18px; top: 7px; bottom: 7px; right: 18px;
      background: linear-gradient(180deg, #ffe06a, #d89a2e);
      border-radius: 2px;
      transition: width 0.3s;
    }
    .camp-profile-points {
      color: var(--text-dim);
      font-size: var(--fs-caption);
      white-space: nowrap;
    }
    .camp-profile-crystals {
      display: flex;
      align-items: center;
      gap: 3px;
      font-size: var(--fs-body);
      color: var(--gold-bright);
    }
    .camp-upgrade-btn {
      display: flex;
      align-items: center;
      gap: 5px;
      padding: 4px 12px;
      ${btn1()}
      color: var(--text);
      border-radius: 4px;
      cursor: pointer;
      font-size: var(--fs-body);
      font-family: var(--font-body);
      font-weight: 600;
      min-height: 44px;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .camp-upgrade-btn:hover:not(:disabled)   { filter: brightness(1.15); }
    .camp-upgrade-btn:active:not(:disabled)  { transform: scale(0.96); }
    .camp-upgrade-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .camp-upgrade-btn.active  { filter: brightness(1.2) saturate(1.4); }
    .camp-upgrade-btn:disabled {
      filter: saturate(0.25) brightness(0.65);
      cursor: default;
    }
    .camp-upgrade-ico {
      width: 22px;
      height: 22px;
    }

    /* ── Main content ── */
    .camp-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      overflow-y: auto;
      overflow-x: hidden;
      -webkit-overflow-scrolling: touch;
      /* Subtle scrollbar */
      scrollbar-width: thin;
      scrollbar-color: rgba(180,140,60,0.4) transparent;
    }

    /* ── Campaign map — vertically fills the content area, inner content scrolls via camp-content ── */
    .camp-map {
      /* No flex:1 — natural height from inner content */
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 20px 16px 32px 16px;
      /* No overflow here — parent camp-content scrolls */
    }

    /* Inner relative wrapper that holds abs-positioned connectors + nodes */
    .camp-map-inner {
      position: relative;
      flex-shrink: 0;
    }

    /* Connector shared base (positioned absolutely inside .camp-map-inner) */
    .camp-connector {
      position: absolute;
      border-radius: 3px;
      background: rgba(80,60,20,0.5);
      pointer-events: none;
    }
    .camp-connector.active {
      background: linear-gradient(
        135deg,
        rgba(180,140,60,0.6) 0%,
        rgba(220,180,80,0.95) 50%,
        rgba(180,140,60,0.6) 100%
      );
      box-shadow: 0 0 6px rgba(220,180,60,0.4);
    }

    /* Node button */
    .camp-node {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      width: 80px;
      padding: 6px 4px;
      background: transparent;
      border: none;
      cursor: pointer;
      flex-shrink: 0;
      transition: transform var(--dur-normal), filter var(--dur-normal);
      -webkit-tap-highlight-color: transparent;
    }
    .camp-node:hover:not(.camp-node-locked) { transform: scale(1.08); filter: brightness(1.15); }
    .camp-node:active:not(.camp-node-locked) { transform: scale(0.96); }
    .camp-node-locked { cursor: default; opacity: 0.7; }
    .camp-node-locked:hover { transform: none; filter: none; }
    .camp-node:not(.camp-node-locked):focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }

    .camp-node-img {
      width: 64px;
      height: 64px;
      /* Painted art downscaled from 140px — smooth filtering, NOT pixelated
         (pixelated shredded the orb/lock art; docs/13 campaign audit). */
      filter: drop-shadow(0 2px 6px rgba(0,0,0,0.7));
    }
    .camp-node-completed .camp-node-img {
      filter: drop-shadow(0 0 8px rgba(100,220,255,0.8)) drop-shadow(0 2px 6px rgba(0,0,0,0.7));
    }

    .camp-node-label-wrap {
      ${missionName()}
      padding: 3px 10px;
      width: max-content;
      max-width: 132px;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1px;
    }
    .camp-node-kicker {
      font-size: var(--fs-micro);
      font-weight: 700;
      letter-spacing: 0.14em;
      text-transform: uppercase;
      color: #b9a06a;
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      white-space: nowrap;
      line-height: 1.1;
    }
    .camp-node-label {
      font-size: var(--fs-small);
      font-weight: 700;
      color: #f5e6bf;
      text-shadow: 0 1px 2px rgba(0,0,0,0.95);
      line-height: 1.2;
      white-space: nowrap;
    }

    /* ── Upgrade panel — fixed bottom sheet, always in viewport ── */
    .camp-upgrade-panel {
      display: none;
      /* Bottom sheet pinned to the letterbox frame, not the window edge. */
      position: fixed;
      left: 50%;
      transform: translateX(-50%);
      width: 100cqw;
      bottom: 0;
      z-index: 100;
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(10,8,20,0.96);
      border-top: 2px solid rgba(180,140,60,0.5);
      border-radius: 12px 12px 0 0;
      padding: 20px 20px 32px 20px;
      max-height: 60cqh;
      overflow-y: auto;
    }
    .camp-spell-row {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 8px 12px;
      background: rgba(20,20,50,0.85);
      border-radius: 6px;
      border: 1px solid rgba(100,80,180,0.4);
    }
    .camp-plus-btn {
      width: 44px;
      height: 44px;
      background: url('/ui/InterfaceNewButton.png') no-repeat center / 32px 32px;
      border: none;
      cursor: pointer;
      font-size: 0;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .camp-plus-btn.can-afford:hover  { filter: brightness(1.2); transform: scale(1.1); }
    .camp-plus-btn.can-afford:active { transform: scale(0.96); }
    .camp-plus-btn.cannot-afford { filter: grayscale(1) opacity(0.4); cursor: default; }
    .camp-plus-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 50%;
    }

    /* ── Campaign back-link (top-left of profile bar) ── */
    .camp-back-link {
      flex-shrink: 0;
      min-width: 44px;
      min-height: 44px;
      display: flex;
      align-items: center;
      padding: 0 12px;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .camp-back-link:hover  { filter: brightness(1.15); color: var(--gold-bright); }
    .camp-back-link:active { transform: scale(0.96); }
    .camp-back-link:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
`;
