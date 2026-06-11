/**
 * SettingsScene.ts — Settings screen (?scene=settings).
 *
 * Controls:
 *   - Replay Tutorial button (re-opens tutorial overlay)
 *   - Audio toggle (placeholder — no audio currently)
 *   - FX Intensity toggle (stored in localStorage)
 *   - Reset Progress (calls /reset with confirmation)
 *
 * Uses existing button/panel art from /ui/.
 */

import { metaApi } from "../net/metaApi";
import { showTutorial } from "./TutorialOverlay";
import { navigateTo } from "../ui/transition";
import { nineSlice } from "../ui/nineSlice";

export function mountSettings(host: HTMLElement) {
  injectSettingsStyles();

  const root = document.createElement("div");
  root.id = "settings-scene";
  root.className = "set-root";

  const bg = document.createElement("div");
  bg.className = "set-bg";
  root.appendChild(bg);

  const inner = document.createElement("div");
  inner.className = "set-inner";

  // Back
  const back = document.createElement("a");
  back.href = "/?scene=menu";
  back.className = "set-back";
  back.textContent = "← Menu";
  inner.appendChild(back);

  // Title
  const title = document.createElement("h1");
  title.textContent = "Settings";
  title.className = "set-title";
  inner.appendChild(title);

  // Panel
  const panel = document.createElement("div");
  panel.className = "set-panel";

  // ── Replay Tutorial ──────────────────────────────────────────────────────────
  panel.appendChild(buildRow({
    label: "Tutorial",
    description: "Re-play the how-to-play slides",
    control: buildActionButton("Replay", "set-btn-replay", () => {
      showTutorial(document.body);
    }),
  }));

  // ── Audio toggle (procedural Web Audio SFX — docs/09 G1) ────────────────────
  const audioEnabled = localStorage.getItem("arkanoid_audio") !== "0";
  const audioToggle = buildToggle("set-toggle-audio", audioEnabled, (val) => {
    localStorage.setItem("arkanoid_audio", val ? "1" : "0");
  });
  panel.appendChild(buildRow({
    label: "Audio",
    description: "Sound effects (synthesized — impacts, spells, bosses)",
    control: audioToggle,
  }));

  // ── Music toggle (per-biome ambient music — off by default) ──────────────────
  const musicOn = localStorage.getItem("arkanoid_music") === "1";
  const musicToggle = buildToggle("set-toggle-music", musicOn, (val) => {
    localStorage.setItem("arkanoid_music", val ? "1" : "0");
  });
  panel.appendChild(buildRow({
    label: "Music",
    description: "Per-biome ambient music (experimental)",
    control: musicToggle,
  }));

  // ── FX Intensity ─────────────────────────────────────────────────────────────
  const fxEnabled = localStorage.getItem("arkanoid_fx") !== "0";
  const fxToggle = buildToggle("set-toggle-fx", fxEnabled, (val) => {
    localStorage.setItem("arkanoid_fx", val ? "1" : "0");
  });
  panel.appendChild(buildRow({
    label: "FX Effects",
    description: "Screen shake and particle effects",
    control: fxToggle,
  }));

  // ── Divider ──────────────────────────────────────────────────────────────────
  const divider = document.createElement("hr");
  divider.className = "set-divider";
  panel.appendChild(divider);

  // ── Reset Progress ────────────────────────────────────────────────────────────
  const resetBtn = buildActionButton("Reset Progress", "set-btn-reset", async () => {
    const confirm = window.confirm("Reset all progress? This cannot be undone.");
    if (!confirm) return;
    await metaApi.reset();
    localStorage.removeItem("arkanoid_tutorial_seen");
    navigateTo("/?scene=menu");
  }, true);
  panel.appendChild(buildRow({
    label: "Reset",
    description: "Wipe all progress and start fresh",
    control: resetBtn,
  }));

  inner.appendChild(panel);

  // Title stamp (was a dev build watermark — now the game title)
  const ver = document.createElement("div");
  ver.className = "set-version";
  ver.textContent = "Heroes of Arkanoid II";
  inner.appendChild(ver);

  root.appendChild(inner);
  host.appendChild(root);
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function buildRow(opts: { label: string; description: string; control: HTMLElement }): HTMLElement {
  const row = document.createElement("div");
  row.className = "set-row";

  const text = document.createElement("div");
  text.className = "set-row-text";

  const lbl = document.createElement("div");
  lbl.textContent = opts.label;
  lbl.className = "set-row-label";
  text.appendChild(lbl);

  const desc = document.createElement("div");
  desc.textContent = opts.description;
  desc.className = "set-row-desc";
  text.appendChild(desc);

  row.appendChild(text);
  row.appendChild(opts.control);

  return row;
}

function buildActionButton(
  label: string, id: string, onClick: () => void, danger = false
): HTMLElement {
  const btn = document.createElement("button");
  btn.id = id;
  btn.textContent = label;
  btn.className = `set-action-btn ${danger ? "set-action-danger" : ""}`;
  btn.addEventListener("click", onClick);
  return btn;
}

function buildToggle(id: string, initial: boolean, onChange: (v: boolean) => void): HTMLElement {
  const wrap = document.createElement("label");
  wrap.className = "set-toggle";
  wrap.htmlFor = id;

  const input = document.createElement("input");
  input.type = "checkbox";
  input.id = id;
  input.checked = initial;
  input.addEventListener("change", () => onChange(input.checked));
  wrap.appendChild(input);

  const slider = document.createElement("span");
  slider.className = "set-toggle-slider";
  wrap.appendChild(slider);

  return wrap;
}

// ── Styles ────────────────────────────────────────────────────────────────────

function injectSettingsStyles() {
  const sid = "settings-styles";
  if (document.getElementById(sid)) return;
  const style = document.createElement("style");
  style.id = sid;
  style.textContent = `
    .set-root {
      position: relative; min-height: 100cqh;
      overflow-x: hidden;
      color: var(--text);
      font-family: var(--font-body);
      display: flex;
      flex-direction: column;
      box-sizing: border-box;
      padding: env(safe-area-inset-top, 0px) env(safe-area-inset-right, 0px)
               env(safe-area-inset-bottom, 0px) env(safe-area-inset-left, 0px);
    }
    .set-bg {
      position: absolute; inset: 0;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
      z-index: 0;
    }
    .set-inner {
      position: relative; z-index: 1;
      display: flex; flex-direction: column;
      padding: max(12px, env(safe-area-inset-top, 0px)) 16px 24px 16px;
      gap: 20px;
    }
    .set-back {
      align-self: flex-start;
      min-width: 44px;
      height: 44px;
      padding: 0 16px;
      display: flex;
      align-items: center;
      justify-content: center;
      white-space: nowrap;
      font-size: 14px;
      font-weight: 700;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 14px")}
      cursor: pointer;
      color: var(--gold-bright);
      font-size: 20px;
      text-decoration: none;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .set-back:hover  { filter: brightness(1.18); }
    .set-back:active { transform: scale(0.94); }
    .set-back:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .set-title {
      margin: 0;
      font-family: var(--font-display);
      font-size: 26px;
      font-weight: 700;
      letter-spacing: 0.05em;
      color: var(--gold-bright);
      text-shadow: 0 2px 4px rgba(0,0,0,0.9), 0 0 18px rgba(255,180,60,0.25);
      text-align: center;
    }
    .set-panel {
      width: min(360px, 96cqw);
      display: flex;
      flex-direction: column;
      gap: 12px;
      padding: 0;
      margin: 0 auto;
    }
    .set-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      padding: 12px 14px;
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "12px 14px")}
    }
    .set-row-text { flex: 1; }
    .set-row-label {
      font-size: 13px; font-weight: 700;
      color: var(--gold-bright);
    }
    .set-row-desc {
      font-size: 12px; color: var(--text-dim);
      margin-top: 3px; line-height: 1.3;
    }
    .set-divider {
      border: none;
      border-top: 1px solid var(--gold-dim);
      margin: 4px 0;
    }
    .set-action-btn {
      height: 40px; min-width: 100px;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 18px")}
      cursor: pointer;
      font-family: var(--font-body); font-size: 13px;
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      padding: 0 16px;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter var(--dur-normal), transform var(--dur-fast);
      flex-shrink: 0;
      /* NOTE: no \`border: none\` here — it would kill the 9-slice border-image. */
    }
    .set-action-btn:hover  { filter: brightness(1.18); }
    .set-action-btn:active { transform: scale(0.96); }
    .set-action-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .set-action-btn:disabled {
      filter: saturate(0.25) brightness(0.65);
      cursor: default;
    }
    .set-action-danger {
      color: #f3b8a8;
    }
    .set-action-danger:hover:not(:disabled) {
      filter: brightness(1.18);
    }

    /* Toggle switch */
    .set-toggle {
      position: relative; display: inline-block;
      width: 48px; height: 28px; flex-shrink: 0;
      cursor: pointer;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .set-toggle:hover  { filter: brightness(1.15); }
    .set-toggle:active { transform: scale(0.96); }
    .set-toggle:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 4px;
      border-radius: 4px;
    }
    .set-toggle input { opacity: 0; width: 0; height: 0; }
    .set-toggle-slider {
      position: absolute; inset: 0;
      background: #241a0d;
      border: 1px solid var(--gold-dim);
      border-radius: 999px;
      transition: background var(--dur-normal), box-shadow var(--dur-normal);
    }
    .set-toggle-slider::before {
      content: '';
      position: absolute;
      height: 20px; width: 20px;
      left: 3px; bottom: 3px;
      background: radial-gradient(circle at 38% 32%, #ffe9b0, #d8a84e 70%);
      border-radius: 50%;
      transition: transform var(--dur-normal);
    }
    .set-toggle input:checked + .set-toggle-slider {
      background: #3a2a10;
      box-shadow: inset 0 0 8px rgba(255,190,80,0.5);
    }
    .set-toggle input:checked + .set-toggle-slider::before {
      transform: translateX(20px);
    }

    .set-version {
      margin-top: 20px;
      text-align: center;
      font-size: 12px;
      color: var(--text-faint);
      font-family: var(--font-display);
      letter-spacing: 0.05em;
    }
  `;
  document.head.appendChild(style);
}
