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
import { btnInterface } from "../ui/nineSlice";

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
      position: relative; min-height: 100vh;
      overflow-x: hidden; font-family: sans-serif;
    }
    .set-bg {
      position: fixed; inset: 0;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(20,30,60,0.55) 0%, transparent 60%),
        linear-gradient(180deg, #080c18 0%, #06080f 50%, #040308 100%);
      z-index: 0;
    }
    .set-inner {
      position: relative; z-index: 1;
      display: flex; flex-direction: column;
      align-items: center;
      padding: max(env(safe-area-inset-top,0px),16px) 16px max(env(safe-area-inset-bottom,0px),24px);
      gap: 0;
    }
    .set-back {
      align-self: flex-start;
      color: #b8a070; font-size: 13px;
      text-decoration: none; padding: 8px 4px;
    }
    .set-title {
      margin: 4px 0 20px 0;
      font-size: 1.9rem; font-weight: 800;
      color: #e8d8b0;
      letter-spacing: 0.08em;
      text-shadow: 0 0 20px rgba(200,180,100,0.3), 0 2px 5px rgba(0,0,0,0.9);
    }
    .set-panel {
      width: min(360px, 96vw);
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(10,8,22,0.95);
      border: 1px solid rgba(150,120,60,0.45);
      border-radius: 14px;
      padding: 8px 0;
      box-shadow: 0 4px 24px rgba(0,0,0,0.6);
      overflow: hidden;
    }
    .set-row {
      display: flex; align-items: center;
      justify-content: space-between;
      padding: 14px 20px;
      border-bottom: 1px solid rgba(100,80,160,0.18);
      gap: 16px;
      min-height: 60px;
    }
    .set-row:last-child { border-bottom: none; }
    .set-row-text { flex: 1; }
    .set-row-label {
      font-size: 15px; font-weight: 700;
      color: #e8d8b0;
    }
    .set-row-desc {
      font-size: 11px; color: #7788aa;
      margin-top: 3px; line-height: 1.4;
    }
    .set-divider {
      border: none;
      border-top: 1px solid rgba(180,140,60,0.25);
      margin: 4px 20px;
    }
    .set-action-btn {
      height: 40px; min-width: 100px;
      ${btnInterface()}
      cursor: pointer;
      font-family: sans-serif; font-size: 13px;
      font-weight: 700; color: #f0e0b8;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
      padding: 0 16px;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter 0.15s, transform 0.1s;
      flex-shrink: 0;
    }
    .set-action-btn:hover  { filter: brightness(1.15); }
    .set-action-btn:active { transform: scale(0.97); filter: brightness(0.9); }
    .set-action-danger {
      filter: hue-rotate(-20deg) saturate(1.3);
    }
    .set-action-danger:hover {
      filter: hue-rotate(-20deg) saturate(1.3) brightness(1.15);
    }

    /* Toggle switch */
    .set-toggle {
      position: relative; display: inline-block;
      width: 48px; height: 28px; flex-shrink: 0;
      cursor: pointer;
    }
    .set-toggle input { opacity: 0; width: 0; height: 0; }
    .set-toggle-slider {
      position: absolute; inset: 0;
      background: rgba(40,30,70,0.9);
      border: 1px solid rgba(100,80,160,0.5);
      border-radius: 28px;
      transition: background 0.2s;
    }
    .set-toggle-slider::before {
      content: '';
      position: absolute;
      height: 20px; width: 20px;
      left: 3px; bottom: 3px;
      background: rgba(160,140,200,0.7);
      border-radius: 50%;
      transition: transform 0.2s, background 0.2s;
    }
    .set-toggle input:checked + .set-toggle-slider {
      background: rgba(60,40,120,0.9);
      border-color: rgba(180,140,220,0.7);
    }
    .set-toggle input:checked + .set-toggle-slider::before {
      transform: translateX(20px);
      background: #cc99ff;
    }

    .set-version {
      margin-top: 32px;
      font-size: 11px; color: rgba(120,100,80,0.5);
      letter-spacing: 0.06em;
    }
  `;
  document.head.appendChild(style);
}
